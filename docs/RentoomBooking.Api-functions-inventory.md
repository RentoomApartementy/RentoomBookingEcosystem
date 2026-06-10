# RentoomBooking.Api - inwentaryzacja funkcji

Zakres dokumentu:
- Obejmuje aktywne funkcje Azure Functions znalezione w projekcie `Api`.
- Obejmuje funkcje HTTP i funkcje `TimerTrigger`.
- Nie obejmuje zakomentowanych stubów, m.in. `CreateWebsiteReservation`, `GetAmenitiesFilter`, starego `GetAllApartmentObjectsFunction`.

Konwencje:
- `Route` oznacza szablon route z atrybutu funkcji.
- Przy funkcjach HTTP podaję metodę i poziom autoryzacji tam, gdzie ma to znaczenie.
- `Input payload` opisuje body, route params i query params faktycznie używane przez kod.
- `Errors returned` dotyczy odpowiedzi zwracanych jawnie przez kod; w kilku prostszych funkcjach nieobsłużone wyjątki kończą się domyślnym błędem platformy Azure Functions.

## Chat

### StaywellChatStream
- Endpoint: `POST`, auth `Anonymous`, route `staywell/chat/stream`
- Input payload: JSON `ChatRequestDto`; wymagane body; opcjonalny nagłówek `x-correlation-id`.
- Logic description: waliduje body, uruchamia streaming odpowiedzi przez `IStaywellChatService`, zwraca SSE z eventami `chunk`, `done`, a po błędzie w trakcie streamu wysyła event `error`.
- Output response: `200` `text/event-stream`; końcowy event `done` zawiera `isDone` i `conversationId`.
- Errors returned:
  - `400` brak body, pusty payload, niepoprawny JSON, błąd walidacji.
  - `403` dostęp zabroniony przez warstwę chat.
  - `404` nie znaleziono zasobu/kontekstu rozmowy.
  - `429` rate limit, z nagłówkiem `Retry-After`.
  - `500` błąd wewnętrzny; jeśli stream już trwa, błąd idzie jako event `error`, a nie nowy status HTTP.

## Cookies

### GetCookieNotice
- Endpoint: `GET`, auth `Anonymous`, route `db/cookies/{appCode}/notice`
- Input payload: `appCode` w ścieżce; opcjonalny query param `culture|lang|language|locale`.
- Logic description: pobiera aktywną konfigurację notice cookie dla aplikacji i lokalizacji.
- Output response: `200` JSON z aktywnym notice.
- Errors returned:
  - `404` brak aktywnego notice.
  - `500` błąd wewnętrzny.

### SaveCookieConsent
- Endpoint: `POST`, auth `Anonymous`, route `db/cookies/{appCode}/consents`
- Input payload: `appCode` w ścieżce; body `SaveCookieConsentRequest`; dodatkowo funkcja zbiera metadane z nagłówków `X-Azure-ClientIP`, `X-Forwarded-For`, `User-Agent`, `Referer`.
- Logic description: waliduje body, buduje metadane requestu, zapisuje zgodę przez `CookieConsentService`, sprawdza zgodność payloadu z aktywnym notice.
- Output response: `200` JSON z wynikiem zapisu zgody.
- Errors returned:
  - `400` puste body, niepoprawny JSON, niepoprawny payload, niespójność z aktywnym notice.
  - `500` błąd wewnętrzny.

## Bitrix

### BulkUpdateDealStayTimes
- Endpoint: `POST`, auth `Function`, route `bitrix/deals/update-stay-times`
- Input payload: body `{ "dealIds": [int, ...] }`.
- Logic description: dla każdego `dealId` pobiera surowe pola check-in/check-out z Bitrix, podmienia godziny odpowiednio na `15:00` i `11:00`, aktualizuje deal i buduje wynik per rekord.
- Output response: `200` JSON z `requestedCount`, `updatedCount`, `failedCount`, `results[]`.
- Errors returned:
  - `400` niepoprawny JSON lub brak poprawnych `dealIds`.
  - `200` z błędami per element w `results[]` jeśli pojedyncza aktualizacja się nie uda.

### CreateBitrixContact
- Endpoint: `POST`, auth `Anonymous`, route `bitrix/contact`
- Input payload: body `CreateContactRequest`; wymagane `FirstName`, `LastName`, `Email`.
- Logic description: deserializuje żądanie, waliduje wymagane pola i tworzy kontakt w Bitrix.
- Output response: `200` JSON z `ContactId` i komunikatem.
- Errors returned:
  - `400` niepoprawny JSON lub brak wymaganych pól.
  - `502` błąd podczas wywołania Bitrix.

### GetBitrixContact
- Endpoint: `GET`, auth `Anonymous`, route `bitrix/contact/{id}`
- Input payload: `id` w ścieżce.
- Logic description: pobiera definicje pól klienta i szczegóły kontaktu z Bitrix.
- Output response: `200` JSON `BitrixResponseObject`.
- Errors returned:
  - `404` dowolny wyjątek zwracany jako komunikat tekstowy.

### GetDealFunction
- Endpoint: `GET|POST`, auth `Anonymous`, route `bitrix/deals/{id}`
- Input payload: `id` w ścieżce.
- Logic description: pobiera szczegóły deala, definicje pól, identyfikuje `CONTACT_ID`, dociąga dane klienta i zwraca złożony model `BitrixDealForm`.
- Output response: `200` JSON `BitrixDealForm`.
- Errors returned:
  - `404` dowolny wyjątek zwracany jako komunikat tekstowy.

### AddDeal
- Endpoint: `GET`, auth `Anonymous`, route `bitrix/deals/add`
- Input payload: brak body; funkcja używa konfiguracji pipeline i hardcodowanych danych testowych deala.
- Logic description: pobiera pipeline i stage z Bitrix, tworzy przykładowy deal i zwraca identyfikator wraz z listą stage/pipeline.
- Output response: `200` JSON z `dealId`, `stages`, `pipelines`.
- Errors returned:
  - `404` dowolny wyjątek zwracany jako komunikat tekstowy.

### GetDealEmailActivities
- Endpoint: `GET`, auth `Anonymous`, route `bitrix/deals/{id}/email-activities`
- Input payload: `id` w ścieżce.
- Logic description: pobiera listę aktywności mailowych powiązanych z deale'm.
- Output response: `200` JSON z listą aktywności.
- Errors returned:
  - `404` dowolny wyjątek zwracany jako komunikat tekstowy.

## Booking.com mail processing

### BackfillIncomingBookingEmailFromIdoAddDateRangeFunction
- Endpoint: `POST`, auth `Function`, route `mail/incoming/backfill/ido/add-date-range`
- Input payload: `startDate` i `endDate` w query lub body; akceptowane formaty dat m.in. `yyyy-MM-dd HH:mm`, `yyyy-MM-dd`, ISO.
- Logic description: wylicza zakres dat, pobiera rezerwacje z IdoBooking po `addDate`, dla każdej buduje enriched import maila Booking.com, a przy błędzie buduje fallback synthetic payload; następnie przetwarza każdy mail przez pipeline Booking.com.
- Output response: `200` JSON z zakresem dat, licznikami i `results[]` per rezerwacja.
- Errors returned:
  - `400` niepoprawne daty lub `endDate < startDate`.
  - `500` błąd globalny funkcji.
  - Błędy pojedynczych rekordów są raportowane w `results[]`, a nie osobnym statusem HTTP.

### BackfillIncomingBookingEmailFromIdoAddDateRangeCron
- Trigger: `TimerTrigger`, schedule `%CRON_SYNC_DAILY_RESERVATIONS%`
- Input payload: brak payloadu HTTP; używany jest domyślny zakres `Warsaw now - 30 min` do `Warsaw now + 30 min`.
- Logic description: uruchamia ten sam pipeline backfill co wersja HTTP, ale tylko w trybie harmonogramu i loguje statystyki.
- Output response: brak odpowiedzi HTTP; efekt widoczny w logach.
- Errors returned:
  - Błędy globalne są rzucane dalej do runtime Azure Functions.

### BackfillIncomingBookingEmailFunction
- Endpoint: `POST`, auth `Function`, route `mail/incoming/backfill`
- Input payload: body `BookingComBackfillRequest` albo surowa lista `int[]` z `reservationIds`.
- Logic description: dla każdej rezerwacji próbuje zbudować enriched import, fallbackuje do synthetic maila przy błędzie, przetwarza mail i zwraca zbiorcze podsumowanie.
- Output response: `200` JSON z licznikami i `results[]`.
- Errors returned:
  - `400` brak poprawnych `reservationIds`.
  - `200` z błędami per element w `results[]`.

### IncomingBookingEmailFunction
- Endpoint: `POST`, auth `Function`, route `mail/incoming`
- Input payload: body `BookingComIncomingEmail`.
- Logic description: zapisuje surowy payload, waliduje JSON, przekazuje mail do `IBookingComIncomingEmailProcessor`, a przy awarii dopisuje kroki do log store.
- Output response: `200` lub `500` JSON z `status`, `logId`, `reservationId`, `reservationGuid`, `emailConfirmed`, `messageId`, `message`.
- Errors returned:
  - `400` niepoprawny JSON; dodatkowo tworzony jest wpis logu z krokiem `payload_invalid`.
  - `500` wynik procesu oznaczony jako `Failed` albo nieobsłużony wyjątek.

## RentoomApp / arrival / QR / TTLock by reservation

### GetArrivalInstructionStepsForApartment
- Endpoint: `GET`, auth `Anonymous`, route `apartment/arrivalinstructions/{apartmentId:int}`
- Input payload: `apartmentId` w ścieżce; opcjonalny query param `language|lang|locale|culture`.
- Logic description: pobiera kroki instrukcji przyjazdu dla apartamentu i języka.
- Output response: `200` JSON z listą kroków.
- Errors returned:
  - `400` brak lub niepoprawny `apartmentId`.
  - `404` brak instrukcji dla apartamentu.
  - `500` błąd wewnętrzny.

### GetAvailableUpsellServicesForApartmentItemId
- Endpoint: `GET`, auth `Anonymous`, route `apartment/availableupsells/{apartmentItemId:int}/{locale}`
- Input payload: `apartmentItemId` i `locale` w ścieżce.
- Logic description: zwraca katalog upselli dla apartamentu i języka; komentarz w kodzie wskazuje, że endpoint jest przewidziany do wycofania.
- Output response: `200` JSON z listą upselli.
- Errors returned:
  - `400` brak lub niepoprawny `apartmentItemId`.
  - `404` brak upselli dla apartamentu.
  - `500` błąd wewnętrzny.

### GetQrMaintFormUrl
- Endpoint: `GET`, auth `Anonymous`, route `qrmaint/form-url/{apartmentItemId:int}`
- Input payload: `apartmentItemId` w ścieżce.
- Logic description: pobiera URL formularza QrMaint dla apartamentu.
- Output response: `200` JSON `{ "url": "..." }`.
- Errors returned:
  - `400` brak lub niepoprawny `apartmentItemId`.
  - `404` brak URL.
  - `500` błąd wewnętrzny.

### GetLockCode
- Endpoint: `GET`, auth `Anonymous`, route `lockcode/{apartmentItemId:int}`
- Input payload: `apartmentItemId` w ścieżce.
- Logic description: pobiera ustawienia apartamentu z QrMaint i zwraca `TTLockId` jako `lockCode`.
- Output response: `200` JSON `{ "lockCode": "..." }`.
- Errors returned:
  - `400` brak lub niepoprawny `apartmentItemId`.
  - `404` brak kodu zamka.
  - `500` błąd wewnętrzny.

### PingLockByReservationId
- Endpoint: `GET`, auth `Anonymous`, route `PingLockByReservationId/{reservationToken}`
- Input payload: `reservationToken` w ścieżce.
- Logic description: wyszukuje rezerwację po tokenie, pobiera `TTLockId` apartamentu i jeśli to numeryczny lock TTLock, odczytuje stan baterii; gdy `lockCode` nie jest TTLock ID, nadal zwraca wynik z komunikatem.
- Output response: `200` JSON z `lockCode`, `batteryLevel`, `status`, `timestamp`.
- Errors returned:
  - `400` brak lub pusty token.
  - `404` brak rezerwacji albo brak lock code.
  - `500` błąd wewnętrzny.

### OpenLockByReservationId
- Endpoint: `GET`, auth `Anonymous`, route `OpenLockByReservationId/{reservationToken}`
- Input payload: `reservationToken` w ścieżce.
- Logic description: znajduje rezerwację, pobiera TTLock ID i wysyła komendę `unlock`.
- Output response: `200` JSON `{ success, lockCode, action }`.
- Errors returned:
  - `400` brak tokenu albo TTLock zwróci błąd akcji.
  - `404` brak rezerwacji lub poprawnego TTLock ID.
  - `500` błąd wewnętrzny.

### CloseLockByReservationId
- Endpoint: `GET`, auth `Anonymous`, route `CloseLockByReservationId/{reservationToken}`
- Input payload: `reservationToken` w ścieżce.
- Logic description: znajduje rezerwację, pobiera TTLock ID i wysyła komendę `lock`.
- Output response: `200` JSON `{ success, lockCode, action }`.
- Errors returned:
  - `400` brak tokenu albo TTLock zwróci błąd akcji.
  - `404` brak rezerwacji lub poprawnego TTLock ID.
  - `500` błąd wewnętrzny.

### GetApartmentItemCodes
- Endpoint: `GET`, auth `Anonymous`, route `reservation/{reservationToken}/apartmentcodes`
- Input payload: `reservationToken` w ścieżce; musi dać się sparsować do GUID.
- Logic description: normalizuje token rezerwacji, wyszukuje rezerwację w DB, pobiera komplet kodów/ustawień apartamentu z QrMaint.
- Output response: `200` JSON z ustawieniami apartamentu.
- Errors returned:
  - `400` pusty token albo token niebędący GUID.
  - `404` brak rezerwacji lub brak kodów.
  - `500` błąd wewnętrzny.

### GenerateTTLockPasscode
- Endpoint: `POST`, auth `Anonymous`, route `reservation/{reservationToken}/passcode/generate`
- Input payload: `reservationToken` w ścieżce; body `{ "startDate": "...", "endDate": "...", "passcodeName": "..." }`.
- Logic description: waliduje żądanie, znajduje rezerwację i TTLock ID, sprawdza czy dla tej rezerwacji i dokładnie tej godziny startu istnieje już passcode, a jeśli nie to generuje nowy passcode w TTLock i zapisuje go w PostgreSQL.
- Output response: `200` JSON z `keyboardPwd`, `keyboardPwdId`, `generatedAt`, `startDate`, `endDate`.
- Errors returned:
  - `400` brak tokenu, brak body lub brak `PasscodeName`.
  - `404` brak rezerwacji albo brak poprawnego TTLock ID.
  - `502` TTLock nie wygenerował passcode.
  - `500` błąd wewnętrzny.

### GetTTLockPasscodes
- Endpoint: `GET`, auth `Anonymous`, route `reservation/{reservationToken}/passcode/history`
- Input payload: `reservationToken` w ścieżce.
- Logic description: pobiera historię wygenerowanych passcode z DB dla tokenu rezerwacji.
- Output response: `200` JSON z listą passcode; pusta lista też jest zwracana jako `200`.
- Errors returned:
  - `400` brak tokenu.
  - `500` błąd wewnętrzny.

## Tpay

### TpayCreateTransaction
- Endpoint: `POST`, auth `Anonymous`, route `tpay/create`
- Input payload: body `TpayCreatePaymentRequest`; dla `Reservation` wymagany `OrderId`, dla `Upsell` wymagany `UpsellOrder` lub istniejący `OrderId`.
- Logic description: deserializuje żądanie, buduje `PaymentIntentRequest`, uzupełnia dane kupującego dla upsella, waliduje warunki zależne od `FlowType`, zakłada sesję płatności przez `IPaymentOrchestrator`.
- Output response: `200` JSON `TpayCreatePaymentResponse` z `TransactionId`, `TransactionPaymentUrl`, `PaymentSessionGuid`.
- Errors returned:
  - `400` puste body, niepoprawny JSON, niepoprawny request, brak wymaganego `OrderId` / `UpsellOrder`.
  - Nieobsłużone wyjątki kończą się domyślnym błędem platformy.

### TpayNotification
- Endpoint: `POST`, auth `Anonymous`, route `tpay/notification`
- Input payload: raw payload w formacie formularza Tpay; opcjonalny nagłówek `X-JWS-Signature`.
- Logic description: loguje payload, parsuje pola settlement notification, przy statusie `true` przekazuje transakcję do `HandleTpayWebhookAsync`; dla statusów innych niż success tylko potwierdza odbiór. W obecnej implementacji walidacja JWS/MD5 jest wyłączona hardcodem.
- Output response: zawsze `200` `text/plain`; treść to `TRUE` albo diagnostyczny tekst `TRUE/FALSE`.
- Errors returned:
  - Funkcja jawnie nie używa statusów 4xx/5xx do walidacji webhooka; nawet błędy walidacji biznesowej zwracają `200` z komunikatem tekstowym.

## Direct TTLock endpoints

### TTLockUnlock
- Endpoint: `POST`, auth `Anonymous`, route `locks/{lockId}/unlock`
- Input payload: `lockId` w ścieżce.
- Logic description: bezpośrednio deleguje unlock do `TTLockService`.
- Output response: `200` JSON odpowiedzi TTLock przy `IsSuccess=true`.
- Errors returned:
  - `400` TTLock zwróci `IsSuccess=false`.
  - Nieobsłużone wyjątki kończą się domyślnym błędem platformy.

### TTLockLock
- Endpoint: `POST`, auth `Anonymous`, route `locks/{lockId}/lock`
- Input payload: `lockId` w ścieżce.
- Logic description: bezpośrednio deleguje lock do `TTLockService`.
- Output response: `200` JSON odpowiedzi TTLock przy `IsSuccess=true`.
- Errors returned:
  - `400` TTLock zwróci `IsSuccess=false`.
  - Nieobsłużone wyjątki kończą się domyślnym błędem platformy.

### TTLockGetState
- Endpoint: `GET`, auth `Anonymous`, route `locks/{lockId}/state`
- Input payload: `lockId` w ścieżce.
- Logic description: pobiera stan zamka z `TTLockService`.
- Output response: `200` JSON przy `IsSuccess=true`.
- Errors returned:
  - `400` TTLock zwróci `IsSuccess=false`.
  - Nieobsłużone wyjątki kończą się domyślnym błędem platformy.

### TTLockGetBattery
- Endpoint: `GET`, auth `Anonymous`, route `locks/{lockId}/battery`
- Input payload: `lockId` w ścieżce.
- Logic description: pobiera poziom baterii z `TTLockService`.
- Output response: `200` JSON przy `IsSuccess=true`.
- Errors returned:
  - `400` TTLock zwróci `IsSuccess=false`.
  - Nieobsłużone wyjątki kończą się domyślnym błędem platformy.

## Terms / customer workflow

### GetTermsForDisplay
- Endpoint: `GET`, auth `Anonymous`, route `db/terms/get-available`
- Input payload: opcjonalny query param `language|lang|locale|culture`.
- Logic description: pobiera aktywne źródła regulaminów/zgód do wyświetlenia dla języka.
- Output response: `200` JSON z listą `termsSources`.
- Errors returned:
  - `500` błąd wewnętrzny.

### GetAgreedTermsByReservation
- Endpoint: `GET`, auth `Anonymous`, route `db/reservations/{reservationToken}/agreed-terms`
- Input payload: `reservationToken` w ścieżce jako GUID; opcjonalny query param języka.
- Logic description: pobiera uzgodnione zgody/regulaminy dla konkretnej rezerwacji i języka.
- Output response: `200` JSON z listą uzgodnionych termów.
- Errors returned:
  - `400` `reservationToken` nie jest GUID.
  - `404` brak zapisanych uzgodnionych termów.
  - `500` błąd wewnętrzny.

### UpdateAgreedTerm
- Endpoint: `PATCH`, auth `Anonymous`, route `db/reservations/{reservationToken}/agreed-terms`
- Input payload: `reservationToken` w ścieżce jako GUID; body `UpdateAgreedTermRequest` z `termsSourceId` i `isAccepted`.
- Logic description: aktualizuje pojedynczy wpis uzgodnionego termu dla rezerwacji.
- Output response: `204 No Content`.
- Errors returned:
  - `400` niepoprawny GUID, niepoprawny JSON, brak lub niepoprawny `termsSourceId`.
  - `404` wpis nie istnieje dla podanej rezerwacji i `termsSourceId`.
  - `500` błąd wewnętrzny.

### SaveCustomerTerms
- Endpoint: `POST`, auth `Anonymous`, route `db/reservations/{reservationTokenGuid}/agreed-terms`
- Input payload: `reservationTokenGuid` w ścieżce jako GUID; body `Dictionary<int,bool>` mapujące `termsSourceId -> accepted`.
- Logic description: zapisuje komplet wyborów klienta dla rezerwacji przez `IReservationWorkflowService`.
- Output response: `204 No Content`.
- Errors returned:
  - `400` niepoprawny GUID, puste body, niepoprawny JSON, brak zaznaczeń.
  - `500` błąd wewnętrzny.

## Upsell

### CreateReservationUpsellOrderWithPaymentRedirectLink
- Endpoint: `POST`, auth `Anonymous`, route `reservations/{reservationToken}/upsells/orders`
- Input payload: `reservationToken` w ścieżce; body `UpsellOrderRequest` z `SelectedUpsells`, datami pobytu, liczbą gości i URL-ami sukces/błąd.
- Logic description: znajduje rezerwację i apartament, pobiera katalog upselli, liczy wartości pozycji i całego zamówienia, zapisuje order z liniami, buduje URL-e Tpay, zakłada sesję płatności i zwraca redirect do operatora.
- Output response: `200` JSON `PayUpsellOrderResponse` z `UpsellOrderGuid`, `PaymentStatus`, `RedirectUrl`, `PaymentSessionGuid`, `ProviderTransactionId`, `Provider`.
- Errors returned:
  - `400` brak tokenu, niepoprawny payload, brak `SelectedUpsells`, token nie daje GUID, brak pozycji rezerwacji.
  - `404` rezerwacja nie istnieje.
  - `500` błąd wewnętrzny; odpowiedź zawiera także `ex.Message`.

### GetUpsellOrderStatus
- Endpoint: `GET`, auth `Anonymous`, route `upsells/orders/{upsellOrderGuid}/status`
- Input payload: `upsellOrderGuid` w ścieżce jako GUID.
- Logic description: odczytuje order upsellowy ze store.
- Output response: `200` JSON pełnego order record.
- Errors returned:
  - `400` niepoprawny GUID.
  - `404` order nie istnieje.
  - `500` błąd wewnętrzny.

### GetReservationPurchasedUpsellsByToken
- Endpoint: `GET`, auth `Anonymous`, route `db/reservations/{reservationToken}/upsells/purchased`
- Input payload: `reservationToken` w ścieżce jako GUID.
- Logic description: buduje podsumowanie kupionych upselli dla rezerwacji.
- Output response: `200` JSON z podsumowaniem zakupów.
- Errors returned:
  - `400` brak tokenu albo token nie jest GUID.
  - `500` błąd wewnętrzny.

### GetReservationAvailableUpsells
- Endpoint: `GET`, auth `Anonymous`, route `db/reservations/{reservationTokenGuid}/upsells/available`
- Input payload: `reservationTokenGuid` w ścieżce jako GUID; opcjonalny query `locale|culture`.
- Logic description: znajduje rezerwację, pobiera katalog upselli dla apartamentu, odfiltrowuje już kupione linie `Paid` z wyjątkiem modeli `OneTime`, buduje odpowiedź z listą dostępnych upselli i kontekstem cenowym rezerwacji.
- Output response: `200` JSON `AvailableUpsellsResponseDto`.
- Errors returned:
  - `400` token nie jest GUID.
  - `404` brak rezerwacji.
  - `500` błąd wewnętrzny.

### ValidateUpsellVoucher
- Endpoint: `POST`, auth `Anonymous`, route `db/upsells/vouchers/validate`
- Input payload: body z dokładnie jednym z pól `codeShort` lub `qrToken` oraz `reservationToken` jako GUID.
- Logic description: waliduje voucher po krótkim kodzie albo QR tokenie, a potem sprawdza zgodność voucheru z rezerwacją.
- Output response: `200` JSON `RedeemResultDto`; odpowiedź może oznaczać niepowodzenie biznesowe mimo statusu `200`.
- Errors returned:
  - `400` niepoprawny JSON, brak dokładnie jednego z `codeShort|qrToken`, niepoprawny `reservationToken`.
  - `500` błąd wewnętrzny.

### RedeemUpsellVoucher
- Endpoint: `POST`, auth `Anonymous`, route `db/upsells/vouchers/redeem`
- Input payload: body z dokładnie jednym z pól `codeShort` lub `qrToken` oraz `reservationToken` jako GUID.
- Logic description: najpierw wykonuje walidację voucheru, potem próbę redeem, a na końcu jeszcze raz sprawdza zgodność z rezerwacją.
- Output response: `200` JSON `RedeemResultDto`.
- Errors returned:
  - `400` niepoprawny JSON, błędna kombinacja pól, niepoprawny `reservationToken`.
  - `500` błąd wewnętrzny.

## Reservations / IdoBooking / StayWell

### AddReservationToIdoSell
- Endpoint: `POST`, auth `Anonymous`, route `ido/reservations`
- Input payload: body `ReservationAddParams`; wymagane `reservations[]`.
- Logic description: waliduje body, wysyła listę rezerwacji do IdoSell, a po sukcesie pierwszej pozycji dociąga pełną rezerwację z IdoSell i zwraca model Rentoom.
- Output response: `200` JSON `RentoomReservationHashRecord` dla pierwszej dodanej rezerwacji.
- Errors returned:
  - `400` puste body lub brak elementów w `reservations[]`.
  - `500` gdy IdoSell zwróci błąd dla pierwszej rezerwacji albo wystąpi wyjątek globalny.

### ChangeReservationStatusInIdoSell
- Endpoint: `POST`, auth `Anonymous`, route `ido/reservations/statuschange`
- Input payload: body `List<EditReservationsStatusRequest>`.
- Logic description: zmienia statusy wskazanych rezerwacji w IdoSell.
- Output response: `200` JSON z wynikiem `ChangeReservationsStatusAsync`.
- Errors returned:
  - `400` puste body albo pusta lista.
  - `500` błąd wewnętrzny.

### EditReservationsInIdoSell
- Endpoint: `POST`, auth `Anonymous`, route `ido/reservations/edit`
- Input payload: body `ReservationEditParams`; wymagane `reservations[]`.
- Logic description: edytuje rezerwacje w IdoSell na podstawie listy zmian.
- Output response: `200` JSON z wynikiem `EditReservationsAsync`.
- Errors returned:
  - `400` puste body albo brak elementów w `reservations[]`.
  - `500` błąd wewnętrzny.

### EditReservationsInIdoSellByToken
- Endpoint: `POST`, auth `Anonymous`, route `reservation/{reservationToken}/edit`
- Input payload: `reservationToken` w ścieżce; body `ReservationEditParams`.
- Logic description: najpierw weryfikuje, że token rezerwacji istnieje w lokalnej DB i mapuje się do GUID, a dopiero potem wykonuje edycję w IdoSell.
- Output response: `200` JSON z wynikiem `EditReservationsAsync`.
- Errors returned:
  - `400` puste body, pusty token, token nie mapuje się do GUID, brak elementów w `reservations[]`.
  - `404` rezerwacja nie istnieje w DB.
  - `500` błąd wewnętrzny.

### GetReservationsByIdFromIdoBooking
- Endpoint: `GET`, auth `Anonymous`, route `ido/reservations/{reservationId:int?}/{save:bool?}`
- Input payload: `reservationId` w ścieżce; opcjonalne `save` w ścieżce.
- Logic description: pobiera rezerwację z IdoSell po ID; opcjonalnie zapisuje ją lokalnie gdy `save=true`.
- Output response: `200` JSON z pełnym wynikiem `FetchReservationByIDFromIdoSellAsync`.
- Errors returned:
  - `400` brak `reservationId`.
  - `404` rezerwacja nie została znaleziona w IdoSell.
  - `500` błąd wewnętrzny.

### GetReservationsByTokenFromDb
- Endpoint: `GET`, auth `Anonymous`, route `db/reservations/{reservationToken}`
- Input payload: `reservationToken` w ścieżce.
- Logic description: pilnuje istnienia rezerwacji w workflow/DB, dociąga rekord rezerwacji i dodatkowy rekord statusów/płatności, a jeśli data końca jest w przeszłości zwraca status `Gone`.
- Output response: `200` JSON `StayWellReservationLookupResponse` z `Reservation` i opcjonalnym `ReservationRecord`.
- Errors returned:
  - `400` brak tokenu.
  - `404` brak rezerwacji w bazie.
  - `410` rezerwacja wygasła (`ToDate < dziś UTC`).
  - `500` błąd wewnętrzny.

### SetReservationsDiscountInIdoSell
- Endpoint: `POST`, auth `Anonymous`, route `ido/reservations/setdiscount`
- Input payload: body `ReservationSetDiscountRequest` z `reservations[]` albo surowa lista `List<SetReservationDiscount>`.
- Logic description: waliduje payload, normalizuje dwa możliwe formaty wejścia i ustawia rabaty rezerwacji w IdoSell.
- Output response: `200` JSON z wynikiem jeśli `Errors == null`.
- Errors returned:
  - `400` puste body albo brak rezerwacji w payloadzie.
  - `500` wynik IdoSell zawiera `Errors` albo wystąpi wyjątek.

### SyncActiveReservationStatusesCron
- Trigger: `TimerTrigger`, schedule `%CRON_SYNC_ACTIVE_RESERVATION_STATUSES%`
- Input payload: brak payloadu HTTP.
- Logic description: pobiera aktywne rekordy rezerwacji z Ido ID, przetwarza je batchami po 50, dociąga rezerwacje z IdoSell i synchronizuje status rezerwacji oraz skutki uboczne zdefiniowane w `IReservationSyncService`; nie odpytuje Tpay o status płatności.
- Output response: brak odpowiedzi HTTP; wynik i liczniki są logowane.
- Errors returned:
  - Błędy pojedynczych rekordów są łapane i raportowane w logach.
  - Błędy globalne batcha/runtime są rzucane dalej do Azure Functions.

### SyncActiveReservationStatuses
- Endpoint: `POST`, auth `Function`, route `reservations/statuses/sync-active`
- Input payload: brak wymaganego body.
- Logic description: uruchamia pełną synchronizację aktywnych rezerwacji identycznie jak cron, ale zwraca podsumowanie HTTP; status płatności pochodzi wyłącznie z rekordu w DB.
- Output response: `200` JSON z `processedCount`, `succeededCount`, `failedCount`, `results[]`.
- Errors returned:
  - Błędy pojedynczych rekordów trafiają do `results[]`.
  - Nieobsłużony błąd globalny kończy się domyślnym błędem platformy.

### PreviewActiveReservationStatuses
- Endpoint: `POST`, auth `Function`, route `reservations/statuses/sync-active/preview`
- Input payload: brak wymaganego body.
- Logic description: wykonuje tę samą synchronizację w trybie `dryRun`, bez trwałego zapisu zmian; nie wykonuje żadnych lookupów statusu do Tpay.
- Output response: `200` JSON z `dryRun=true` oraz licznikami i `results[]`.
- Errors returned:
  - Błędy pojedynczych rekordów trafiają do `results[]`.
  - Nieobsłużony błąd globalny kończy się domyślnym błędem platformy.

### UpdateStayWellLinkExternalNote
- Endpoint: `POST`, auth `Function`, route `ido/reservations/external-note/staywell`
- Input payload: body `{ "reservationIds": [int, ...] }`.
- Logic description: dla każdej rezerwacji pobiera dane z IdoSell, znajduje token StayWell w lokalnej DB lub z `RentoomReservationId`, buduje link z konfiguracji i dopisuje go do `externalNote` rezerwacji, jeśli jeszcze tam nie istnieje.
- Output response: `200` JSON z `requestedCount`, `updatedCount`, `failedCount`, `results[]`.
- Errors returned:
  - `400` puste body albo brak poprawnych `reservationIds`.
  - `500` błąd globalny funkcji.
  - Błędy pojedynczych rezerwacji są raportowane w `results[]`.

## Apartments / amenities / filters

### GetAmenitiesForObjectTypes
- Endpoint: `GET`, auth `Anonymous`, route `amenities/getForObjects`
- Input payload: opcjonalny query param `objectTypesIds`, rozdzielany przecinkiem lub średnikiem; może zawierać ID enumów lub nazwy enumów.
- Logic description: parsuje listę typów obiektów i pobiera ich amenities z IdoSell; przy pustym query wysyła pustą listę typów do serwisu.
- Output response: `200` JSON `List<ObjectTypesAmenities>`.
- Errors returned:
  - `400` niepoprawna wartość w `objectTypesIds`.
  - `500` błąd wewnętrzny.

### GetAllFilters
- Endpoint: `GET`, auth `Anonymous`, route `filters/getAll`
- Input payload: brak.
- Logic description: pobiera komplet zapisanych filtrów wyszukiwania apartamentów.
- Output response: `200` JSON `List<SearchFilterDocument>`.
- Errors returned:
  - `500` błąd wewnętrzny.

### SeedAmenitiesFilters
- Endpoint: `GET`, auth `Anonymous`, route `postgres/amenitiesfilters/seed`
- Input payload: brak.
- Logic description: zapisuje/odświeża filtry amenities w storage.
- Output response: `200` tekst `Filters saved.`.
- Errors returned:
  - `500` błąd wewnętrzny.

### GetAllApartmentsFromIdoSellWithLocalizationInfoAsync
- Endpoint: `GET`, auth `Anonymous`, route `idb/apartments/getAll`
- Input payload: brak.
- Logic description: synchronizuje apartamenty i amenities z IdoSell, wyciąga unikalne regiony i zapisuje filtry regionów.
- Output response: `200` JSON z listą zsynchronizowanych apartamentów.
- Errors returned:
  - `500` błąd konfiguracji apartment service albo błąd wewnętrzny.

### GetAllApartmentsFromIdoSellWithLocalizationInfoAsyncCron
- Trigger: `TimerTrigger`, schedule `%CRON_SYNC_ALL_APARTMENTS_FROM_IDB%`
- Input payload: brak payloadu HTTP.
- Logic description: wykonuje tę samą synchronizację co endpoint HTTP i zapis region filters.
- Output response: brak odpowiedzi HTTP; statystyki trafiają do logów.
- Errors returned:
  - Błędy są rzucane dalej do runtime po zalogowaniu.

### SeedApartmentsToPostgres
- Endpoint: `GET`, auth `Anonymous`, route `postgres/apartments/seed`
- Input payload: brak.
- Logic description: uruchamia synchronizację apartamentów i zapis filtrów regionów, pełniąc rolę ręcznego triggera seedowania.
- Output response: `200` JSON z listą zsynchronizowanych apartamentów.
- Errors returned:
  - `500` błąd wewnętrzny.

### GetApartmentByIdAsync
- Endpoint: `GET`, auth `Anonymous`, route `db/apartments/{id}`
- Input payload: `id` w ścieżce.
- Logic description: pobiera apartament z lokalnego repozytorium.
- Output response: `200` JSON apartamentu.
- Errors returned:
  - `404` apartament nie istnieje w lokalnym repozytorium.
  - `500` błąd konfiguracji apartment service albo błąd wewnętrzny.

### ListApartments
- Endpoint: `GET`, auth `Anonymous`, route `db/apartments`
- Input payload: query `continuationToken`, `top`, opcjonalnie `city`.
- Logic description: zwraca stronicowaną listę apartamentów z lokalnej bazy; `top` jest ograniczony do `1..200`, domyślnie `50`; parametr `city` jest obecnie tylko logowany i nie wpływa na wynik.
- Output response: `200` JSON z wynikiem `GetApartmentsByPageAsync`.
- Errors returned:
  - Nieobsłużone wyjątki kończą się domyślnym błędem platformy.

### GetApartmentMedia
- Endpoint: `GET`, auth `Anonymous`, route `apartments/{objectId:int}/media`
- Input payload: `objectId` w ścieżce.
- Logic description: pobiera media obiektu bezpośrednio z IdoSell.
- Output response: `200` JSON `List<ObjectMedium>`.
- Errors returned:
  - `400` `objectId <= 0`.
  - `404` brak mediów.
  - `500` błąd wewnętrzny.

### GetApartmentAmenities
- Endpoint: `GET`, auth `Anonymous`, route `apartments/{objectId:int}/amenities`
- Input payload: `objectId` w ścieżce.
- Logic description: pobiera amenities obiektu z IdoSell.
- Output response: `200` JSON `List<ObjectAmenity>`.
- Errors returned:
  - `400` `objectId <= 0`.
  - `404` brak amenities.
  - `500` błąd wewnętrzny.

### GetApartmentDescriptions
- Endpoint: `GET`, auth `Anonymous`, route `apartments/{objectId:int}/descriptions`
- Input payload: `objectId` w ścieżce; opcjonalny query `language`.
- Logic description: pobiera opisy obiektu z IdoSell, filtrowane po języku jeśli przekazano.
- Output response: `200` JSON z listą opisów.
- Errors returned:
  - `400` `objectId <= 0`.
  - `404` brak opisów.
  - `500` błąd wewnętrzny.

## Clients

### GetClients
- Endpoint: `GET|POST`, auth `Anonymous`, route `clients/get`
- Input payload: opcjonalne body w jednym z dwóch formatów: `ClientGetRequestPayloadInternal` (`Params` + `Settings`) albo bezpośrednio `ClientGetParams`; body może być też puste.
- Logic description: próbuje rozpoznać format payloadu, waliduje JSON i wykonuje wyszukiwanie klientów z opcjonalnym pagingiem.
- Output response: `200` JSON z wynikiem `GetClientsAsync`.
- Errors returned:
  - `400` niepoprawny JSON.
  - `500` błąd wewnętrzny.

### GetClientById
- Endpoint: `GET`, auth `Anonymous`, route `clients/getbyId/{id:int?}`
- Input payload: `id` w ścieżce.
- Logic description: pobiera klienta po ID.
- Output response: `200` JSON z wynikiem `GetClientByIdAsync`.
- Errors returned:
  - `400` brak `id`.
  - `404` klient nie istnieje.
  - `500` błąd wewnętrzny.

### GetClientByEmail
- Endpoint: `GET`, auth `Anonymous`, route `clients/getbyEmail/{email}`
- Input payload: `email` w ścieżce.
- Logic description: pobiera klienta po adresie email.
- Output response: `200` JSON z wynikiem `GetClientByEmailAsync`.
- Errors returned:
  - `400` brak emaila.
  - `404` klient nie istnieje.
  - `500` błąd wewnętrzny.

### AddClient
- Endpoint: `POST`, auth `Anonymous`, route `clients/add`
- Input payload: body `ClientAddRequestClient`.
- Logic description: waliduje payload klienta i tworzy klienta przez `IClientService`.
- Output response: `200` JSON z wynikiem `AddClientAsync`.
- Errors returned:
  - `400` puste body, niepoprawny JSON, null payload.
  - `500` błąd wewnętrzny.

## Misc DB / forms

### GetDefinedAddons
- Endpoint: `GET`, auth `Anonymous`, route `db/definedaddons`
- Input payload: brak.
- Logic description: odczytuje z repozytorium listę zdefiniowanych addonów.
- Output response: `200` JSON z addonami.
- Errors returned:
  - `500` błąd wewnętrzny.

### GetLocks
- Endpoint: `GET`, auth `Anonymous`, route `idobooking/locks/{reservationId}/{itemId}`
- Input payload: `reservationId` i `itemId` w ścieżce, oba dodatnie integer.
- Logic description: pobiera locki dla kombinacji rezerwacja/item z serwisu IdoLocks.
- Output response: `200` JSON z lockami.
- Errors returned:
  - `400` brak lub niepoprawne `reservationId` / `itemId`.
  - `404` brak locków dla pary.
  - `500` błąd wewnętrzny.

### GetRegistrationCardByResToken
- Endpoint: `GET`, auth `Anonymous`, route `db/registrationcard/GetRegistrationCardByResToken/{resToken}`
- Input payload: `resToken` w ścieżce.
- Logic description: pobiera kartę meldunkową po tokenie rezerwacji.
- Output response: `200` JSON `RegistrationCardEntity`.
- Errors returned:
  - `400` brak tokenu.
  - `404` brak karty meldunkowej.
  - Nieobsłużone wyjątki kończą się domyślnym błędem platformy.

### SaveRegistrationCard
- Endpoint: `POST`, auth `Anonymous`, route `db/registrationcard/SaveRegistrationCard`
- Input payload: body `RegistrationCardEntity`; wymagany `ResToken`; imiona i nazwiska gości muszą przejść walidację `RegistrationCardGuestModel.IsValidName`.
- Logic description: waliduje payload i dane gości, a następnie zapisuje kartę meldunkową.
- Output response: `200` JSON zapisanej encji.
- Errors returned:
  - `400` niepoprawny payload, brak `ResToken`, niedozwolone znaki w imionach/nazwiskach gości.
  - Nieobsłużone wyjątki kończą się domyślnym błędem platformy.

### GetTermsByResToken
- Endpoint: `GET`, auth `Anonymous`, route `db/terms/GetTermsByResToken/{resToken}`
- Input payload: `resToken` w ścieżce.
- Logic description: pobiera zapisane terms po tokenie rezerwacji.
- Output response: `200` JSON `TermsEntity`.
- Errors returned:
  - `400` brak tokenu.
  - `404` brak terms.
  - Nieobsłużone wyjątki kończą się domyślnym błędem platformy.

### SaveTerms
- Endpoint: `POST`, auth `Anonymous`, route `db/terms/SaveTerms`
- Input payload: body `TermsEntity`; wymagany `ResToken`.
- Logic description: waliduje body i zapisuje terms w repozytorium.
- Output response: `200` JSON zapisanej encji.
- Errors returned:
  - `400` puste body, brak `ResToken`, niepoprawny payload.
  - `500` błąd wewnętrzny.

## Payments

### AddPayments
- Endpoint: `POST`, auth `Anonymous`, route `ido/payments`
- Input payload: body `PaymentAddParams`; wymagane `payments[]`.
- Logic description: dodaje płatności do IdoSell.
- Output response: `200` JSON wyniku `AddPaymentsAsync`.
- Errors returned:
  - `400` puste body albo brak płatności.
  - `500` błąd wewnętrzny.

### CancelPayments
- Endpoint: `POST`, auth `Anonymous`, route `ido/payments/cancel`
- Input payload: body `PaymentActionParams`; funkcja wyciąga z niego listę `payments[].id`.
- Logic description: anuluje wskazane płatności w IdoSell.
- Output response: `200` JSON wyniku `CancelPaymentsAsync`.
- Errors returned:
  - `400` brak choć jednego ID płatności.
  - `500` błąd wewnętrzny.

### ConfirmPayments
- Endpoint: `POST`, auth `Anonymous`, route `ido/payments/confirm`
- Input payload: body `PaymentActionParams`; funkcja wyciąga z niego listę `payments[].id`.
- Logic description: potwierdza wskazane płatności w IdoSell.
- Output response: `200` JSON wyniku `ConfirmPaymentsAsync`.
- Errors returned:
  - `400` brak choć jednego ID płatności.
  - `500` błąd wewnętrzny.

### EditPayments
- Endpoint: `POST`, auth `Anonymous`, route `ido/payments/edit`
- Input payload: body `PaymentEditParams`; wymagane `payments[]`.
- Logic description: edytuje płatności w IdoSell.
- Output response: `200` JSON wyniku `EditPaymentsAsync`.
- Errors returned:
  - `400` puste body albo brak płatności.
  - `500` błąd wewnętrzny.

### GetPaymentForms
- Endpoint: `GET`, auth `Anonymous`, route `ido/payments/forms`
- Input payload: brak.
- Logic description: pobiera słownik/formy płatności z IdoSell.
- Output response: `200` JSON wyniku `GetPaymentFormsAsync`.
- Errors returned:
  - `500` błąd wewnętrzny.

### GetPayments
- Endpoint: `POST`, auth `Anonymous`, route `ido/payments/search`
- Input payload: opcjonalne body `PaymentGetRequest`; przy pustym body wyszukiwanie działa z domyślnymi parametrami.
- Logic description: wyszukuje płatności w IdoSell z opcjonalnym `Params` i `Settings`.
- Output response: `200` JSON wyniku `GetPaymentsAsync`.
- Errors returned:
  - `500` błąd wewnętrzny.

## Offers / pricing / availability

### GetPricingOffers
- Endpoint: `GET`, auth `Anonymous`, route `ido/offers/pricing`
- Input payload: body `PricingOffersRequest` mimo że to `GET`.
- Logic description: czyta body, waliduje payload i pobiera oferty cenowe z IdoSell.
- Output response: `200` JSON `PricingOffersResponse`.
- Errors returned:
  - `400` niepoprawny albo pusty payload.
  - `500` błąd wewnętrzny.

### GetRentoomPricingOffers
- Endpoint: `GET`, auth `Anonymous`, route `offers/pricing`
- Input payload: body `RentoomQueryOffer` mimo że to `GET`.
- Logic description: uruchamia filtrowanie ofert po logice Rentoom (`getOfferWitFilter`).
- Output response: `200` JSON z ofertą/ofertami Rentoom.
- Errors returned:
  - `400` niepoprawny payload.
  - `404` brak apartamentów spełniających filtr.
  - `500` błąd wewnętrzny.

### GetAvailabilityAndPricesForDays
- Endpoint: `GET`, auth `Anonymous`, route `ido/offers/availability-and-prices-for-days`
- Input payload: body `OfferAvailabilityAndPricesParamsSearchInternal` mimo że to `GET`.
- Logic description: pobiera dostępność i ceny dla dni na podstawie przekazanych parametrów.
- Output response: `200` JSON `List<OfferAvailabilityObject>`.
- Errors returned:
  - `400` niepoprawny payload.
  - `500` błąd wewnętrzny.

### GetRestrictions
- Endpoint: `GET`, auth `Anonymous`, route `ido/restrictions`
- Input payload: body `GetRestrictionException` mimo że to `GET`.
- Logic description: pobiera wyjątki restrykcji z IdoSell.
- Output response: `200` JSON `List<RestrictionException>`.
- Errors returned:
  - `400` niepoprawny payload.
  - `500` błąd wewnętrzny.

### GetAvailabilityLocks
- Endpoint: `GET`, auth `Anonymous`, route `ido/availabilitylocks`
- Input payload: opcjonalne body `GetAvailabilityLocksRequestPayload`; przy pustym body tworzony jest pusty payload domyślny.
- Logic description: pobiera availability locks z IdoSell.
- Output response: `200` JSON `List<AvailabilityLock>`.
- Errors returned:
  - `500` błąd wewnętrzny.

### FindAvailableTerms
- Endpoint: `POST`, auth `Anonymous`, route `ido/offers/available-terms`
- Input payload: body `FindAvailableTermsRequest` z `ApartmentIds`, `StartDate`, `EndDate`, `Adults`, `Children`.
- Logic description: deleguje wyszukiwanie możliwych terminów do `IAvailabilityFinderService2`.
- Output response: `200` JSON z listą dostępnych terminów.
- Errors returned:
  - `400` niepoprawny payload.
  - `500` błąd wewnętrzny.

## QrMaint simple endpoint

### GetQrMaintWifiInfo
- Endpoint: `GET`, auth `Anonymous`, route `qrmaint/wifi/{apartmentItemId:int}`
- Input payload: `apartmentItemId` w ścieżce.
- Logic description: pobiera dane Wi-Fi apartamentu z QrMaint.
- Output response: `200` JSON z danymi Wi-Fi.
- Errors returned:
  - `400` niepoprawny `apartmentItemId`.
  - `404` brak danych Wi-Fi.
  - Nieobsłużone wyjątki kończą się domyślnym błędem platformy.

## Smoke test

### RentoomFirstTestAzureFunction
- Endpoint: `GET|POST`, auth `Anonymous`, route domyślna Azure Functions dla nazwy funkcji
- Input payload: brak wymagań.
- Logic description: prosty smoke test zwracający stały tekst.
- Output response: `200` tekst `Welcome to Azure Functions!`.
- Errors returned:
  - Brak jawnie obsługiwanych błędów w kodzie.
