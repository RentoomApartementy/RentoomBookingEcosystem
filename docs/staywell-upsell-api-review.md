# StayWell Upsell API review (Azure Functions)

## 1) Function app structure + DI/startup pattern

- **Project/host layout:** The Azure Functions app lives in `Api/` as an isolated worker (.NET v4) project. Functions are defined as classes with `[Function]` attributes and `[HttpTrigger]` bindings (example in the Upsell and Reservation functions).【F:Api/Upsell/GetReservationUpsellsFunction.cs†L1-L63】【F:Api/GetReservationsFunction.cs†L1-L116】
- **Startup/DI registration:** There is no `FunctionsStartup`; instead the app uses the isolated-worker `Program.cs` pattern with `FunctionsApplication.CreateBuilder(args)` and `builder.ConfigureFunctionsWebApplication()` to configure the host and register services into DI. Key services for reservation workflow and upsell are registered here (e.g., `IReservationWorkflowService`, `IUpsellCatalogService`, `IUpsellOrderWorkflowService`, `IUpsellPurchasedSummaryService`, and upsell voucher services).【F:Api/Program.cs†L1-L155】

## 2) Reservation token mapping (reservationTokenGuid → reservationGuid + reservation context)

- **Token storage and lookup:** Reservation tokens are stored as `ResToken` in the PostgreSQL-backed reservation record. `PostgresBookingDatabase.GetRentoomReservationByResTokenAsync` looks up a reservation by `ResToken` and deserializes the `RentoomReservation` payload, which includes the IdoSell reservation details and the token itself.【F:SharedClasses/Services/BookingDatabaseService/PostgresBookingDatabase.cs†L184-L213】【F:SharedClasses/Models/RentoomReservation.cs†L1-L18】
- **How tokens are created:** When saving a reservation, `PostgresBookingDatabase.SaveReservationJsonAsync` assigns `ResToken` to the provided `existingResToken` or generates a new GUID (`Guid.NewGuid().ToString("N")`). This is the token returned to the client for later lookups.【F:SharedClasses/Services/BookingDatabaseService/PostgresBookingDatabase.cs†L213-L266】
- **Reservation workflow token = reservationGuid (StayWell):** In the reservation workflow, `ReservationWorkflowService.StartAsync` returns `record.ReservationGuid` and explicitly notes it as the StayWell reservation token. Later, when a payment is confirmed, the same `ReservationGuid` is passed as `existingResToken` when pulling the IdoSell reservation into the DB, ensuring a 1:1 link between `reservationGuid` and `ResToken` in storage.【F:SharedClasses/Services/ReservationWorkflow/ReservationWorkflowService.cs†L72-L88】【F:SharedClasses/Services/ReservationWorkflow/ReservationWorkflowService.cs†L420-L432】
- **Reservation context (dates, guests, apartment/objectId):** The reservation context returned to StayWell is the serialized IdoSell `Reservation` object inside `RentoomReservation.Reservation`. Key fields include:
  - `ReservationDetails.dateFrom/dateTo` (arrival/departure) via helper methods `getDateFrom/getDateTo`.【F:SharedClasses/Models/IdoBooking/ReservationObject.cs†L77-L132】
  - `ReservationItem.objectId/objectItemId`, and adult/child counts (`numberOfAdults`, `numberOfSmallChildren`).【F:SharedClasses/Models/IdoBooking/ReservationObject.cs†L164-L192】
  - Guest list in `ClientModel.Guests` (for per-guest data).【F:SharedClasses/Models/IdoBooking/ReservationObject.cs†L134-L161】

## 3) Existing upsell-related functions/services used by StayWell

- **StayWell client usage:** StayWell calls `GET db/reservations/{token}/upsells` to fetch purchased upsells for a reservation token (GUID).【F:StayWell/Services/BackendApi.cs†L63-L76】
- **Upsell summary endpoint:** `GetReservationUpsellsFunction` handles that route and validates the token as a GUID before delegating to `IUpsellPurchasedSummaryService`.【F:Api/Upsell/GetReservationUpsellsFunction.cs†L21-L62】
- **Upsell catalog endpoint:** `PartnerUpsellApi.GetAvailableUpsellServicesForApartmentItemId` exposes `GET upsell/{apartmentItemId}/{locale}` to return available upsell tiles from `IUpsellCatalogService`.【F:Api/Integrations/RentoomApp/PartnerUpsellApi.cs†L12-L58】
- **Upsell services registered in DI:** Upsell-related services wired in DI include catalog retrieval, order workflow, purchased summary, and voucher provisioning/query/redeem services. These are the building blocks for new endpoints around orders and post-purchase workflows.【F:Api/Program.cs†L78-L109】

## 4) Existing DTO namespaces and mapping patterns (SharedClasses)

- **DTO namespaces:**
  - Reservation workflow DTOs live in `RentoomBooking.SharedClasses.Models.ReservationWorkflow` (e.g., `StartReservationRequest`, `ReservationSummaryDto`, `PaymentStateDto`).【F:SharedClasses/Models/ReservationWorkflow/ReservationWorkflowModels.cs†L1-L140】
  - Upsell DTOs live in `RentoomBooking.SharedClasses.Models.Upsell` (e.g., `UpsellTileDto`, `UpsellPurchasedSummaryDto`, `UpsellPurchasedOrderDto`, `UpsellPurchasedLineDto`).【F:SharedClasses/Models/Upsell/UpsellDtos.cs†L1-L120】
  - IdoSell reservation payloads are in `RentoomBooking.SharedClasses.Models.IdoBooking` (e.g., `Reservation`, `ReservationDetails`, `ReservationItem`).【F:SharedClasses/Models/IdoBooking/ReservationObject.cs†L66-L192】
- **Mapping pattern:** DTOs are built by explicit projection in services (no auto-mapper). Example: `UpsellPurchasedSummaryService` pulls records from `IUpsellOrderStore`, filters paid orders, and manually projects each order/line into `UpsellPurchasedSummaryDto` → `UpsellPurchasedOrderDto` → `UpsellPurchasedLineDto`.【F:SharedClasses/Services/Upsell/UpsellPurchasedSummaryService.cs†L10-L60】

## 5) Recommendation for where to add new functions

**Recommended placement:** add new HTTP-trigger Azure Functions under `Api/Upsell/` alongside `GetReservationUpsellsFunction`, or under a new `Api/Upsell/Orders/` folder if you want a clearer grouping for order/payment endpoints. This keeps upsell APIs together and leverages the existing DI services registered in `Api/Program.cs`.【F:Api/Upsell/GetReservationUpsellsFunction.cs†L1-L63】【F:Api/Program.cs†L78-L109】

**Suggested routes (aligned with the requested REST paths):**

- `GET /api/reservations/{token}/upsells/available`
  - Backing service: `IUpsellCatalogService` (likely requires apartment item/context from reservation). Use `RentoomReservation.Reservation` to derive apartment item/object and date range from `ReservationDetails`/`ReservationItem` if needed.【F:SharedClasses/Models/RentoomReservation.cs†L1-L18】【F:SharedClasses/Models/IdoBooking/ReservationObject.cs†L77-L192】
- `GET /api/reservations/{token}/upsells/purchased`
  - Backing service: `IUpsellPurchasedSummaryService` (similar to existing `GET db/reservations/{token}/upsells`).【F:Api/Upsell/GetReservationUpsellsFunction.cs†L21-L62】【F:SharedClasses/Services/Upsell/UpsellPurchasedSummaryService.cs†L10-L60】
- `POST /api/reservations/{token}/upsells/orders`
  - Backing service: `IUpsellOrderWorkflowService` (create order from reservation context + selected upsells).【F:Api/Program.cs†L78-L109】
- `POST /api/upsells/orders/{orderGuid}/pay`
  - Backing services: `IUpsellOrderWorkflowService` (prepare payment), `IPaymentOrchestrator` or `ITpayGateway` depending on how the order is initiated in the existing payment flow wiring.【F:Api/Program.cs†L101-L109】
- `GET /api/upsells/orders/{orderGuid}/status`
  - Backing service: `IUpsellOrderStore` or `IUpsellOrderWorkflowService` to fetch the current state and payment status for the order.【F:Api/Program.cs†L78-L109】

**Notes for token handling:** Use GUID parsing (as in `GetReservationUpsellsFunction`) and map it to the reservation GUID / token stored in `ResToken`. That keeps alignment with how StayWell currently treats `reservationGuid` as the token.【F:Api/Upsell/GetReservationUpsellsFunction.cs†L33-L46】【F:SharedClasses/Services/ReservationWorkflow/ReservationWorkflowService.cs†L72-L88】
