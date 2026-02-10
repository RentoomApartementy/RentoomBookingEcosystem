# Staywell Upsell API review (current state)

## Scope
This review documents the current Azure Functions API structure and how it relates to Staywell reservation and upsell use cases, based on the `Api`, `SharedClasses`, and `StayWell` projects.

---

## 1) Function app structure and startup/DI pattern

### Function app project and folders
The Azure Functions project is `Api/` (`RentoomBooking.Api.csproj`) and currently uses a .NET isolated worker startup (`Program.cs`), not `FunctionsStartup`.

Current high-level layout:

- `Api/Program.cs` – isolated worker host + DI registrations.
- `Api/Upsell/` – upsell-specific function endpoints.
- `Api/ReservationFunctions/` – reservation-related function area (currently one function file is commented out).
- `Api/Integrations/TpayFunctions/` – payment creation + webhook endpoint.
- `Api/Integrations/RentoomApp/` – partner upsell catalog + QR maintenance URL endpoint.
- other domain/API files at `Api/` root (`GetReservationsFunction`, `AddReservationFunction`, etc.).

### Startup pattern
The app uses:

- `FunctionsApplication.CreateBuilder(args)`
- `builder.ConfigureFunctionsWebApplication()`
- service registrations via `builder.Services.AddScoped(...)` / `AddDbContextFactory(...)`
- `builder.Build().Run()`

No `FunctionsStartup` class is present.

### DI registrations relevant to reservation/upsell
`Program.cs` registers:

- Data/DB:
  - `AddDbContextFactory<PostgresBookingDbContext>`
  - `AddDbContextFactory<RappPartnersDBContext>`
  - `PostgresBookingDatabase`
- Reservation workflow:
  - `IReservationStore` → `ReservationStore`
  - `IReservationWorkflowService` → `ReservationWorkflowService`
- Upsell workflow:
  - `IUpsellCatalogService` → `UpsellCatalogService`
  - `IUpsellOrderStore` → `UpsellOrderStore`
  - `IUpsellOrderWorkflowService` → `UpsellOrderWorkflowService`
  - `IUpsellPurchasedSummaryService` → `UpsellPurchasedSummaryService`
  - voucher services (`IUpsellVoucherProvisioningService`, `IUpsellVoucherCodeGenerator`, `IUpsellVoucherRedeemService`)
- Payments:
  - both payment flow handlers are registered (`ReservationPaymentFlowHandler` and `UpsellPaymentFlowHandler`) behind `IPaymentFlowHandler`
  - `IPaymentOrchestrator`, `ITpayGateway`, `ITpayClient`, `ITpayNotificationValidator`

### Auth + routing conventions observed

- Functions are mostly `AuthorizationLevel.Anonymous`.
- Routes are explicitly set per function method with `[HttpTrigger(..., Route = "...")]`.
- Existing route families are mixed by domain:
  - `db/...` for PostgreSQL-backed reads/writes
  - `ido/...` for IdoSell integration
  - `upsell/...` for partner upsell catalog
  - `tpay/...` for payment/webhook
- `host.json` does not override route prefix; Azure Functions default `/api` prefix applies.

---

## 2) Existing reservation token mapping and reservation context

## What exists now

### Reservation token storage/lookup
- Reservation token (`resToken`) is persisted in PostgreSQL (`Reservations` table) and used as lookup key.
- `PostgresBookingDatabase.GetRentoomReservationByResTokenAsync(string resToken, ...)` loads the DB row by `ResToken`, then deserializes `Payload` to `RentoomReservation`.
- `GetReservationsFunction` exposes this as `GET db/reservations/{reservationToken}`.

### Reservation payload/context fields returned
`RentoomReservation` contains:

- `Id` (IdoSell reservation ID)
- `ResToken`
- `Reservation` object (IdoSell model)

Within `Reservation` / `ReservationDetails` / `ReservationItem`, the context used by Staywell is present:

- stay dates: `Reservation.ReservationDetails.dateFrom/dateTo` (+ helpers `getDateFrom()`, `getDateTo()`)
- apartment/object context per item: `Reservation.Items[*].objectId`, `itemId`, `objectItemId`
- guest counts per item: `numberOfAdults`, `numberOfSmallChildren`
- client guests list via `Reservation.Client.Guests`

`GetReservationsFunction` also applies an expiration rule: if `toDate < UtcToday`, endpoint returns HTTP `410 Gone`.

### reservationTokenGuid -> reservationGuid mapping for upsell
Important current behavior:

- `GetReservationUpsellsByToken` route is `db/reservations/{reservationToken}/upsells/purchased`.
- In implementation, `reservationToken` is parsed directly as a `Guid` and treated as `reservationGuid`.
- There is **no lookup** from DB `resToken` string to a separate `reservationGuid` in this function.

So for upsell purchased summary, the API currently expects `{reservationToken}` to already be a GUID that equals `reservationGuid` used in `UpsellOrderRecords.ReservationGuid`.

---

## 3) Existing upsell functions/services used by Staywell

### Function endpoints currently relevant

1. Purchased upsell summary by token-like GUID:
   - `GET /api/db/reservations/{reservationToken}/upsells/purchased`
   - function: `Api/Upsell/GetReservationUpsellsFunction.cs`
   - service: `IUpsellPurchasedSummaryService`

2. Upsell catalog by apartment item + locale:
   - `GET /api/upsell/{apartmentItemId}/{locale}`
   - function: `Api/Integrations/RentoomApp/PartnerUpsellApi.cs`
   - service: `IUpsellCatalogService`

3. Payment creation / webhook (shared for reservation + upsell flows):
   - `POST /api/tpay/create`
   - `POST /api/tpay/notification`
   - function: `Api/Integrations/TpayFunctions/TpayFunctions.cs`
   - service: `IPaymentOrchestrator` with `Reservation` and `Upsell` flow handlers

### Staywell client-side calls currently in code
In `StayWell/Services/BackendApi.cs`:

- Reservation fetch by token: `GET db/reservations/{token}`
- Purchased upsells fetch: currently calls `GET db/reservations/{token}/upsells` (**without** `/purchased` suffix)

This suggests a route mismatch between Staywell client call and the function route currently implemented.

---

## 4) Existing DTO namespaces and mapping patterns (SharedClasses)

## DTO/model namespaces
Representative namespaces used for reservation/upsell payloads:

- `RentoomBooking.SharedClasses.Models`
  - `RentoomReservation`
- `RentoomBooking.SharedClasses.Models.IdoBooking`
  - reservation structures (`Reservation`, `ReservationDetails`, `ReservationItem`, etc.)
- `RentoomBooking.SharedClasses.Models.Upsell`
  - `UpsellTileDto`, `UpsellOrderRequest`, `UpsellPurchasedSummaryDto`, voucher DTOs
- `RentoomBooking.SharedClasses.Models.Upsell.StayWell`
  - `AvailableUpsellsResponseDto`, `UpsellOfferDto`, `PurchasedUpsellsWithVouchersResponseDto`
- `RentoomBooking.SharedClasses.Models.ReservationWorkflow`
  - reservation workflow/payment status models

### Mapping style/patterns observed

- Mostly manual mapping methods in services/stores (no AutoMapper usage found in reviewed paths).
  - examples: `MapToRecord`, `MapLineToRecord`, `MapVoucherToDto` in `UpsellOrderStore`
- JSON persistence pattern for workflow states:
  - reservation and upsell order state serialized as JSON blobs in DB entities (`ReservationJson`, `UpsellOrderJson`) and deserialized back into state models.
- Function responses are commonly serialized with `JsonConvert.SerializeObject(...)` and returned via `HttpResponseData`.

---

## 5) Recommendation: where to add the new Staywell upsell endpoints

Requested endpoints:

- `GET /api/reservations/{token}/upsells/available`
- `GET /api/reservations/{token}/upsells/purchased`
- `POST /api/reservations/{token}/upsells/orders`
- `POST /api/upsells/orders/{orderGuid}/pay`
- `GET /api/upsells/orders/{orderGuid}/status`

### Recommended placement

Create a dedicated function class under `Api/Upsell/`, e.g.:

- `Api/Upsell/StaywellUpsellApi.cs`

Rationale:

- Keeps Staywell upsell endpoints together with current upsell endpoint (`GetReservationUpsellsFunction`) and upsell integration logic.
- Keeps payment-specific adapter logic in `Api/Integrations/TpayFunctions` while exposing a Staywell-friendly façade in `Api/Upsell`.

### Recommended service wiring (reuse existing services)

Use existing DI services in handlers:

- token resolution + reservation context:
  - `PostgresBookingDatabase` (for token -> `RentoomReservation` lookup)
- upsell availability:
  - `IUpsellCatalogService`
- purchased summary:
  - `IUpsellPurchasedSummaryService`
- order create/pay:
  - `IUpsellOrderWorkflowService`
  - optionally `IPaymentOrchestrator` if pay endpoint wraps/create payment session directly
- order status:
  - `IUpsellOrderStore` (read payment/order status)

### Token resolution recommendation (important)

Before implementing new routes, define one canonical mapping strategy for `{token}`:

1. **If `{token}` is the existing DB `resToken` string** (current Staywell pattern):
   - resolve with `GetRentoomReservationByResTokenAsync`
   - derive a consistent `reservationGuid` for upsell domain (e.g., use `Reservation.RentoomReservationId` when available, otherwise explicitly map and persist)

2. **If `{token}` is intended to be a GUID reservation id**:
   - enforce GUID route constraint and align all Staywell calls + endpoint names/docs accordingly

Current code mixes these concepts (DB `resToken` lookup endpoint vs upsell purchased endpoint requiring GUID parse), so this should be normalized first.

### Suggested route convention

For new Staywell-oriented endpoints, prefer a single bounded context prefix:

- reservation-scoped: `/api/reservations/{token}/upsells/...`
- order-scoped: `/api/upsells/orders/{orderGuid}/...`

This is cleaner than current mixed `db/...` + `upsell/...` style and aligns with the requested contract.

---

## Existing endpoints inventory (Staywell reservation/upsell relevant)

- Reservation by token:
  - `GET /api/db/reservations/{reservationToken}`
- Purchased upsells (current function route):
  - `GET /api/db/reservations/{reservationToken}/upsells/purchased`
- Upsell catalog for apartment item:
  - `GET /api/upsell/{apartmentItemId}/{locale}`
- Tpay create transaction:
  - `POST /api/tpay/create`
- Tpay webhook:
  - `POST /api/tpay/notification`

