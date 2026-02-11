# StayWell Available Upsells — wiring (review-only)

## Zakres
- Dokument opisuje **obecny stan** integracji dla upselli „available to buy” w StayWell.
- Bez checkoutu/zakupu: tylko pobranie i wyświetlenie listy.

---

## a) Endpoint + metoda BackendApi

### StayWell `BackendApi`
- Metoda: `GetAvailableUpsellsByReservationTokenAsync`
- Signature:
  - `Task<AvailableUpsellsResponseDto?> GetAvailableUpsellsByReservationTokenAsync(string token, string? locale = null)`
- Bazowy path wywołania:
  - `db/reservations/{token}/upsells/available`
- Locale/culture query string:
  - gdy `locale` przekazane: dokładany jest query string `?locale={escapedLocale}`
  - metoda **nie** wysyła `culture`, tylko `locale`

Dla porównania (już działające purchased):
- `GetPurchasedUpsellsByReservationTokenAsync(string token)` -> `GET db/reservations/{token}/upsells/purchased`

### Azure Functions (API)
- Available:
  - `GET db/reservations/{reservationTokenGuid}/upsells/available`
  - Funkcja akceptuje GUID i czyta query params: najpierw `locale`, fallback `culture`, dalej fallback do `reservation.Client.Language`, a finalnie `pl`.
- Purchased:
  - `GET db/reservations/{reservationToken}/upsells/purchased`
  - Funkcja także wymaga tokena w formacie GUID.

### Zgodność route’ów (StayWell vs API)
- StayWell wywołuje:
  - `db/reservations/{token}/upsells/available`
  - `db/reservations/{token}/upsells/purchased`
- Azure udostępnia dokładnie te same ścieżki (różni się tylko nazwa placeholdera w atrybucie trasy: `reservationTokenGuid` vs `reservationToken`, co nie zmienia URL).

---

## b) DTO (kontrakty)

### Available
- `BackendApi.GetAvailableUpsellsByReservationTokenAsync(...)` zwraca:
  - `AvailableUpsellsResponseDto?`
- `AvailableUpsellsResponseDto` pola:
  - `Guid ReservationGuid`
  - `ReservationPricingContext Context`
  - `List<UpsellTileDto> Available`
- Wniosek: endpoint available zwraca **obiekt** z listą `Available`, a nie gołe `List<UpsellTileDto>`.

### Purchased (weryfikacja)
- `BackendApi.GetPurchasedUpsellsByReservationTokenAsync(...)` zwraca:
  - `UpsellPurchasedSummaryDto?`
- `UpsellPurchasedSummaryDto` pola:
  - `Guid ReservationGuid`
  - `List<UpsellOrderLineRecord> PurchasedUpsellsWithVouchers`
- To jest już użyte na stronie `MyVouchersPage`.

---

## c) Komponenty RCL (listowanie)

### `SharedFrontend/Components/Shared/UpsellComponents/UpsellList.razor`
Parametry wejściowe:
- wymagane funkcjonalnie do listowania:
  - `IReadOnlyList<UpsellTileDto> AvailableUpsellsList`
  - `ReservationPricingContext ReservationContext`
  - `UpsellTextConfig Texts` (UI copy)
- opcjonalne:
  - `EventCallback<List<SelectedUpsellDto>> OnSelectionChanged`
  - `string Theme = "rentoom-theme"`

`UpsellList` renderuje wewnętrznie `UpsellTile` per element listy.

### `SharedFrontend/Components/Shared/UpsellComponents/UpsellTile.razor`
Parametry:
- `UpsellTileDto Tile`
- `ReservationPricingContext Context`
- `int Quantity = 1`
- `bool IsSelected`
- `EventCallback<bool> OnToggled`
- `UpsellTextConfig Texts`
- `CascadingParameter string Theme`

Dla samego „listowania” w StayWell preferowany jest poziom `UpsellList` (nie pojedyncze ręczne składanie `UpsellTile`).

---

## d) Routing nowej strony + nawigacja z HomePage

### Konwencja routingu w StayWell
- Stosowana konwencja: `/reservation/{token}/XxxPage` (czasem `Token` wielką literą, ale w praktyce działa case-insensitive binding).
- Folder dla stron upsellowych już istnieje:
  - `StayWell/Pages/Upsells/`
- Już istnieją route’y:
  - `@page "/reservation/{Token}/VouchersPage"` (AvailableUpsellsPage)
  - `@page "/reservation/{Token}/MyVouchersPage"` (PurchasedUpsellsPage)

### Dla nowej strony „Vouchery”
- Proponowany route z promptu: `/reservation/{Token}/Vouchers`.
- W obecnym projekcie nazewnictwo najczęściej kończy się na `...Page`; jeśli ma być spójnie z resztą, można też użyć `/reservation/{Token}/VouchersPage`.
- Aktualnie `HomePage.razor` ma już przycisk nawigujący do:
  - `/reservation/{Token}/VouchersPage` (etykieta: `Vouchery`)
  - `/reservation/{Token}/MyVouchersPage` (etykieta: `Twoje Vouchery`)
- Technicznie nowy link dodaje się analogicznie jak istniejące przyciski:
  - `<Button OnClick="@(() => Navigation.NavigateTo($"/reservation/{Token}/..."))" ... />`

---

## e) Jak budujemy `ReservationPricingContext` ze stanu StayWell

Źródło danych:
- `ReservationState.CurrentReservation?.Reservation`

Mapowanie pól:
1. `StartDate`
   - z `Reservation.ReservationDetails.getDateFrom()` -> `DateOnly.FromDateTime(...)`
2. `EndDate`
   - z `Reservation.ReservationDetails.getDateTo()` -> `DateOnly.FromDateTime(...)`
3. `Adults`
   - z `Reservation.Items.FirstOrDefault()?.numberOfAdults ?? 0`
4. `Children`
   - z `Reservation.Items.FirstOrDefault()?.numberOfSmallChildren`
   - parse `int.TryParse(..., out var children) ? children : 0`
5. `Currency`
   - preferowane źródło: `Reservation.ReservationDetails.currency`
   - fallback: `Reservation.Client?.Currency`
   - fallback końcowy: `"PLN"`

Pola pochodne (już liczone przez `ReservationPricingContext`):
- `Nights = max(0, EndDate - StartDate)`
- `TotalGuests = Adults + Children`

Uwaga:
- W Azure Function available `Currency` jest obecnie ustawiane na stałe `"PLN"`.
- W UI StayWell można i tak budować lokalny `ReservationPricingContext` ze stanu rezerwacji, żeby tile liczyły ceny i opisy noclegi/goście spójnie dla widoku.

---

## f) Założenia do kolejnych kroków
- Caching response available upsells po tokenie: **10 minut** (założenie implementacyjne na kolejny etap).
- Zakres funkcjonalny: **tylko wyświetlanie** upselli dostępnych do kupienia.
- Brak koszyka/zakupu/płatności w tym etapie.
- Używamy istniejących komponentów RCL (`UpsellList`/`UpsellTile`) zamiast tworzenia nowych komponentów listujących.
