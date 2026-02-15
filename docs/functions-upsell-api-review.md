# Functions upsell API review (Azure Functions / `Api` project)

## Scope
Documentation-only review of the current Azure Functions app (`Api`) to place new upsell order endpoints and reuse existing services consistently.

---

## 1) Where to add the new endpoints

## Recommended location
Add a new function class under:

- `Api/Upsell/` (same area as existing reservation upsell and voucher functions)

Suggested file name:

- `Api/Upsell/UpsellOrdersFunction.cs`

This keeps all reservation-scoped upsell API surfaces together with:

- `GetReservationUpsellsFunction` (`db/reservations/{token}/upsells/...`)
- `UpsellVouchersFunction` (`db/upsells/vouchers/...`)

## Recommended routes to add
Use `HttpTrigger` routes in the same style as current code (explicit `Route = "..."`):

- `POST reservations/{token}/upsells/orders`
- `POST upsells/orders/{orderGuid}/pay`
- `GET  upsells/orders/{orderGuid}/status`

Notes on route base:

- Functions use route templates without `api/` in attributes.
- Runtime base prefix is `/api` (default Azure Functions prefix), and StayWell already calls the API under `/api/`.

---

## 2) Existing services to reuse

## Token resolution (`token` -> reservation + reservationGuid)
Reuse:

- `PostgresBookingDatabase.GetRentoomReservationByResTokenAsync(...)` for lookup by reservation token string in `db.reservations` payload.
- The GUID handling approach already used in upsell functions (`Guid.TryParse`, fallback candidate formats `D`/`N`) where token may already be GUID-like.

Practical pattern for new endpoints:

1. Resolve reservation by token string from DB payload (`GetRentoomReservationByResTokenAsync`).
2. Derive/confirm `reservationGuid` used by upsell orders.
3. Keep same normalization behavior as current upsell available endpoint (handles different GUID string formats).

## Catalog read
Reuse:

- `IUpsellCatalogService` (already used by `GetReservationUpsellsFunction` and partner upsell function).

## Order store
Reuse:

- `IUpsellOrderStore` for reads/status (`GetAsync`, `GetByReservationGuidAsync`, `GetLinesAsync`).
- `IUpsellOrderWorkflowService` for creating orders and initiating payment (`CreateOrderAsync`, `InitiatePaymentAsync`, `CreateOrderAndInitiatePaymentAsync`).

## Voucher query / paid-summary context
Current active read path is:

- `IUpsellPurchasedSummaryService` (internally uses `IUpsellOrderStore` and line/voucher snapshots).

Voucher service note:

- `IUpsellVoucherQueryService` exists as a commented-out prototype and is **not registered**.
- Active voucher validation/redeem path is `IUpsellVoucherRedeemService`.

## Payment
For new API endpoints use payment orchestration already in place:

- `IPaymentOrchestrator` + `PaymentIntentRequest` (`FlowType = Upsell`).
- `UpsellPaymentFlowHandler` then delegates to `IUpsellOrderWorkflowService`.

Per requirement: **new upsell pay/order endpoints should accept/build `PaymentIntentRequest`**, not ad-hoc payment payloads.

---

## 3) Existing DTO namespaces and mapping conventions

## Namespaces already used by Functions/StayWell upsell flow

- `RentoomBooking.SharedClasses.Models.Upsell`
  - `UpsellOrderRequest`, `UpsellOrderRecord`, `UpsellOrderLineRecord`, `UpsellPurchasedSummaryDto`, `RedeemResultDto`, etc.
- `RentoomBooking.SharedClasses.Models.Upsell.StayWell`
  - `AvailableUpsellsResponseDto`, `PurchasedUpsellsWithVouchersResponseDto`.
- `RentoomBooking.SharedClasses.Models.Payments`
  - `PaymentIntentRequest`, `PaymentSessionResponse`, `PaymentFlowType`.
- `RentoomBooking.SharedClasses.Models.ReservationWorkflow`
  - payment statuses and reservation state models.

## Mapping conventions in current backend

- Manual static mapper methods (no AutoMapper in this flow), e.g. `UpsellOrderMapper`:
  - entity -> record (`MapToRecord`, `MapLineToRecord`)
  - record -> entity (`MapLineToEntity`)
  - voucher entity -> dto (`MapVoucherToDto`)
- JSON state persistence pattern:
  - reservation state as JSON (`ReservationJson`)
  - upsell order state as JSON (`UpsellOrderJson`)
- Function responses are serialized explicitly (commonly `JsonConvert.SerializeObject(...)`).

---

## 4) Reservation record update requirement (`SelectedUpsells`)

Requirement: when new upsell order is created, ensure reservation record is updated and new upsell selection is appended into existing JSON list `SelectedUpsells`.

Implementation guideline (for new endpoint handlers):

1. Resolve `reservationGuid` for token.
2. Load reservation workflow record via `IReservationStore.GetAsync(reservationGuid)`.
3. Update `record.State.StartRequest.SelectedUpsells` by appending/merging newly ordered lines.
4. Persist using `IReservationStore.UpdateAsync(record)`.

Why this matches current architecture:

- Reservation workflow state is serialized as JSON and written back by `ReservationStore.UpdateAsync`.
- `SelectedUpsells` already exists in `StartReservationRequest` and is part of this persisted JSON contract.

---

## 5) Payment contract requirement (`PaymentIntentRequest`)

For new APIs:

- `POST reservations/{token}/upsells/orders`
  - create order payload using `UpsellOrderRequest`, then build `PaymentIntentRequest` when payment should be initialized in same flow.
- `POST upsells/orders/{orderGuid}/pay`
  - construct `PaymentIntentRequest` with:
    - `FlowType = PaymentFlowType.Upsell`
    - `OrderId = orderGuid`
    - optional `SuccessUrl` / `ErrorUrl`
  - call `IPaymentOrchestrator.CreatePaymentAsync(intent, ct)`.
- `GET upsells/orders/{orderGuid}/status`
  - read via `IUpsellOrderStore.GetAsync(orderGuid)` and return payment/order status projection.

This aligns with existing `tpay/create` flow, which already converts incoming payload into `PaymentIntentRequest` before orchestration.

---

## 6) StayWell site changes to include (success/error pages)

Include these front-end updates in StayWell when wiring new endpoints:

1. Add dedicated payment result pages:
   - Upsell payment success page (e.g. `/upsells/payment/success`)
   - Upsell payment error/cancel page (e.g. `/upsells/payment/error`)
2. Ensure `SuccessUrl` / `ErrorUrl` passed in upsell order/payment requests point to those pages.
3. Extend `StayWell/Services/BackendApi` with methods for:
   - `POST reservations/{token}/upsells/orders`
   - `POST upsells/orders/{orderGuid}/pay`
   - `GET upsells/orders/{orderGuid}/status`
4. Keep using the existing `FunctionsApi` base address pattern (`.../api/`), so function routes remain relative without hardcoding host-specific prefixes.

---

## Existing endpoint inventory (relevant to this work)

Reservation token and upsells:

- `GET /api/db/reservations/{reservationToken}`
- `GET /api/db/reservations/{reservationToken}/upsells/purchased`
- `GET /api/db/reservations/{reservationTokenGuid}/upsells/available`

Upsell vouchers:

- `POST /api/db/upsells/vouchers/validate`
- `POST /api/db/upsells/vouchers/redeem`

Payment integration:

- `POST /api/tpay/create`
- `POST /api/tpay/notification`
