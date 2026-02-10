# Voucher redemption review (stan aktualny)

> Zakres: **review-only** (bez zmian logiki), zmapowanie obecnego stanu voucherów i redeem.

## 1) Source of truth (DB + modele + status/limity)

### Tabela `upsell_vouchers`
Źródłem prawdy dla vouchera jest encja `UpsellVoucherEntity` mapowana na tabelę `upsell_vouchers`.

Pola (wg modelu/migracji):
- `upsell_voucher_guid` (`Guid`) – PK
- `upsell_order_line_guid` (`Guid`) – 1:1 do `upsell_order_lines` (UNIQUE + FK)
- `reservation_guid` (`Guid`)
- `qr_token` (`string`) – UNIQUE
- `code_short` (`string`) – UNIQUE
- `status` (`string`)
- `max_uses` (`int?`) – limit użyć (`null` = bez limitu)
- `used_count` (`int`) – licznik użyć
- `valid_from` / `valid_to` (`DateOnly`) – okno ważności
- `last_used_at_utc` (`DateTime?`)
- `row_version` (`byte[]?`, timestamp)
- `created_at` / `updated_at` (`DateTime`)

Dodatkowe indeksy: po `reservation_guid`, po `(status, valid_from, valid_to)`, oraz unikalne po `qr_token` i `code_short`.

### Status i limit uses
Statusy są zdefiniowane jako `UpsellVoucherStatuses`:
- `Active`
- `Expired`
- `Cancelled`
- `Completed`

Walidacja do redeem (w `UpsellVoucherRedeemService`):
- voucher musi mieć `Status == Active`
- data bieżąca musi być w zakresie `ValidFrom..ValidTo`
- jeśli `MaxUses` ma wartość, to `UsedCount < MaxUses`

Po udanym redeem wykonywany jest atomowy update:
- `UsedCount = UsedCount + 1`
- `LastUsedAtUtc = now`
- `UpdatedAt = now`

### Czy istnieje osobna tabela redemptions?
Na ten moment **nie widać** osobnej tabeli typu `upsell_voucher_redemptions` / historii redeems. Zużycia są trzymane agregacyjnie w `upsell_vouchers` (`used_count`, `last_used_at_utc`).

---

## 2) Serwisy: Query/GetByReservationAsync, GetByCode/Token, TryRedeem

### Query
W repo jest plik `UpsellVoucherQueryService`, ale obecnie cała implementacja (interfejs + klasa) jest zakomentowana i niepodpięta DI (`Api/Program.cs` ma rejestrację zakomentowaną).

Metody, które są tam opisane (ale nieaktywne):
- `GetByReservationAsync(Guid reservationGuid)`
- `GetByCodeShortAsync(string codeShort)`
- `GetByQrTokenAsync(string qrToken)`

### Redeem
Aktywny jest `IUpsellVoucherRedeemService` / `UpsellVoucherRedeemService` z metodami:
- `TryRedeemByCodeShortAsync(string codeShort)`
- `TryRedeemByQrTokenAsync(string qrToken)`

`TryRedeem*` obecnie:
- wyszukuje voucher + line (`FindByCodeShortAsync` / `FindByQrTokenAsync`)
- waliduje status/date/limit
- wykonuje atomowe zwiększenie użyć (EF `ExecuteUpdateAsync`)
- zwraca `RedeemResultDto`

Brak aktualnie function endpointu HTTP, który wystawia ten serwis na zewnątrz.

---

## 3) Endpointy functions: purchased + available

W `GetReservationUpsellsFunction` istnieją endpointy:
- `GET /api/db/reservations/{reservationToken}/upsells/purchased` (`GetReservationUpsellsByToken`)
- `GET /api/db/reservations/{reservationTokenGuid}/upsells/available` (`GetAvailable`)

Oba endpointy mają `AuthorizationLevel.Anonymous`.

---

## 4) DTO i pytanie o token/code z purchased endpointu

### Co zwraca purchased endpoint
`GetReservationUpsellsByToken` zwraca `UpsellPurchasedSummaryDto`, gdzie:
- `ReservationGuid`
- `PurchasedUpsellsWithVouchers: List<UpsellOrderLineRecord>`

`UpsellOrderLineRecord` ma pole:
- `Voucher: UpsellVoucherDto?`

A `UpsellVoucherDto` zawiera:
- `CodeShort`
- `QrToken` (nullable)
- `UsedCount`, `MaxUses`, `ValidFrom`, `ValidTo`, `Status`, itd.

Czyli: **tak**, purchased endpoint już niesie dane token/code (o ile `Voucher` jest obecny): co najmniej `CodeShort`, oraz modelowo także `QrToken`.

### Gdzie to jest używane w UI
W `SharedFrontend/Components/StayWell/UpsellComponents/PurchasedUpsellTile.razor` aktualnie wyświetlany jest tylko:
- `@Voucher.CodeShort`

Są TODO pod pokazanie QR/tokenu.

---

## 5) Proponowany kontrakt 2 nowych endpointów (Validate + Redeem)

Dopasowanie nazewnictwa do istniejących tras `db/reservations/.../upsells/...` oraz obecnego modelu (`code_short` / `qr_token`):

## A) Validate
`POST /api/db/upsells/vouchers/validate`

Request:
```json
{
  "codeShort": "ABC123",
  "qrToken": null
}
```
- dokładnie jedno z pól: `codeShort` albo `qrToken`

Response 200 (proponowane):
```json
{
  "success": true,
  "failureReason": null,
  "updatedUsedCount": 2,
  "maxUses": 5,
  "reservationGuid": "...",
  "partnerServiceId": 123,
  "titleSnapshot": "Breakfast"
}
```
- format zgodny z `RedeemResultDto`, ale operacja **nie zwiększa** `used_count`

Uwagi implementacyjne (na przyszłość):
- najlepiej wydzielić osobne `ValidateByCodeShortAsync` / `ValidateByQrTokenAsync` (bez `ExecuteUpdateAsync`), żeby nie mieszać semantyki z `TryRedeem*`.

## B) Redeem
`POST /api/db/upsells/vouchers/redeem`

Request:
```json
{
  "codeShort": "ABC123",
  "qrToken": null
}
```
- dokładnie jedno z pól: `codeShort` albo `qrToken`

Response 200:
- `RedeemResultDto` (obecny kontrakt serwisu), gdzie `updatedUsedCount` już po inkremencie.

Błędy:
- `400` – brak obu identyfikatorów lub podane oba jednocześnie
- `404` – opcjonalnie, jeśli nie chcemy always-200 z `Success=false`
- `200 + Success=false` – dla domenowych przyczyn (`NotFound`, `Expired`, `OutsideReservationWindow`, `LimitReached`, `Cancelled`) spójnie z aktualnym `FailureReason`

---

## 6) Miejsca do podpięcia (StayWell / SharedFrontend)

### StayWell
1. `StayWell/Services/BackendApi.cs`
   - dodać metody klienta HTTP:
     - `ValidateUpsellVoucherAsync(...)`
     - `RedeemUpsellVoucherAsync(...)`
2. nowa strona testowa (proponowane):
   - `StayWell/Pages/Upsells/VoucherRedeemTestPage.razor`
   - prosty formularz: `codeShort/qrToken`, przyciski Validate/Redeem, podgląd `RedeemResultDto`

### SharedFrontend
1. `SharedFrontend/Components/StayWell/UpsellComponents/PurchasedUpsellTile.razor`
   - użyć `Voucher.QrToken` do wygenerowania QR (lub linku payload)
   - zachować `Voucher.CodeShort` jako fallback human-readable
2. opcjonalnie `PurchasedUpsellList.razor`
   - dodać event/hook na akcję „Zrealizuj” z tile (jeśli UX ma to wspierać)

---

## 7) Ryzyka i TODO

### Ryzyka
1. **Anonymous endpoints**
   - obecne endpointy upsellowe są `AuthorizationLevel.Anonymous`; analogiczne wystawienie Redeem grozi nadużyciami (bruteforce po `code_short`, nadużycia partnerów).
2. **Brak historii redemptions**
   - bez osobnej tabeli eventów trudniej audytować kto/kiedy wykonał redeem i z jakiego kanału.
3. **Race conditions / idempotencja klienta**
   - backend ma atomowy update, ale klient może wysłać retry; trzeba jasno opisać semantykę odpowiedzi i UX.

### TODO (przyszłość)
1. Wprowadzić **partner auth** (np. klucz partnera/JWT, scope per partner/service).
2. Dodać rate limiting + monitoring na endpointy voucherowe.
3. Rozważyć tabelę `upsell_voucher_redemptions` (audit trail).
4. Rozważyć oddzielne DTO dla Validate (bez „updated” w nazwach pól) vs Redeem.
