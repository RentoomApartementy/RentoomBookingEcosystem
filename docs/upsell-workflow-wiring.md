# Upsell workflow wiring notes

## 1) StartReservationRequest definition + persistence/serialization

* **Definition:** `StartReservationRequest` lives in `SharedClasses/Models/ReservationWorkflow/ReservationWorkflowModels.cs`. It currently includes dates, guest counts, offer info, currency, and `SelectedAddons`.【F:SharedClasses/Models/ReservationWorkflow/ReservationWorkflowModels.cs†L11-L31】
* **ApartmentPage -> StartAsync:** The request is assembled in `RentoomBookingWeb/Components/Features/ReservationWorkflow/Pages/ApartmentPage.razor` inside `GoToPayment`, then sent to `ReservationWorkflowService.StartAsync(request)` to begin the workflow.【F:RentoomBookingWeb/Components/Features/ReservationWorkflow/Pages/ApartmentPage.razor†L799-L847】
* **Persistence/serialization:** `ReservationStore.CreateAsync` wraps the request in a `ReservationState`, serializes it to JSON via `JsonConvert.SerializeObject(state)`, and stores it on `ReservationRecordEntity.ReservationJson`. This is the source of truth for the draft workflow and later loads are deserialized back into `ReservationState` in `MapToRecord`.【F:SharedClasses/Services/ReservationWorkflow/ReservationStore.cs†L34-L107】

## 2) Where ReservationSummaryDto is built and what it currently contains

* **Builder:** `ReservationWorkflowService.BuildSummaryAsync` and `BuildDraftSummaryAsync` both call `BuildSummaryFromRecord`, which maps the `ReservationRecord` into `ReservationSummaryDto`.【F:SharedClasses/Services/ReservationWorkflow/ReservationWorkflowService.cs†L78-L119】
* **Current fields:** `ReservationSummaryDto` includes the reservation guid, start request, client/invoice info, Ido reservation info, offer price, currency, and payment status. It does not include any upsell-specific totals/lines today.【F:SharedClasses/Models/ReservationWorkflow/ReservationWorkflowModels.cs†L55-L66】【F:SharedClasses/Services/ReservationWorkflow/ReservationWorkflowService.cs†L103-L119】

## 3) Where payment amount is computed

* **Payment amount:** `ReservationWorkflowService.InitiatePaymentAsync` computes the payment amount from `record.State.StartRequest?.OfferPrice` and uses that to create the payment session via `_tpayGateway.CreatePaymentAsync(...)`. This is the only current payment amount calculation in the workflow service.【F:SharedClasses/Services/ReservationWorkflow/ReservationWorkflowService.cs†L123-L168】
* **Payment confirmation:** The webhook handler uses the same `OfferPrice` to register a payment in Ido when marked paid (`AddIdoPaymentAsync`).【F:SharedClasses/Services/ReservationWorkflow/ReservationWorkflowService.cs†L199-L226】

## 4) Minimal DTO/model changes to pass SelectedUpsells + computed totals/lines

To carry selected upsells through `StartAsync`, draft summary, payment initiation, and final summary, only the shared workflow DTOs/state need to be extended. This keeps wiring minimal and consistent with current persistence/serialization patterns.

**A) Extend the request + persisted state**

* Add a `SelectedUpsells` collection to `StartReservationRequest` alongside `SelectedAddons`. This ensures the selection flows from `ApartmentPage` to `StartAsync`, and persists via `ReservationState.StartRequest` JSON serialization without new tables or storage logic.【F:SharedClasses/Models/ReservationWorkflow/ReservationWorkflowModels.cs†L11-L31】【F:SharedClasses/Services/ReservationWorkflow/ReservationStore.cs†L34-L107】
* Introduce a `SelectedUpsellDto` (new DTO in the same file) that captures the upsell selection and pricing context. MVP fields could include:
  * `UpsellServiceId` (catalog id)
  * `PricingModel` (PerPersonPerDay, PerApartmentPerDay, PerStay, OneTime)
  * `UnitPriceGross` and `VatRate` (display-only VAT)
  * `Quantity`, `TotalGuests`, `Nights`

**B) Extend summaries to carry computed lines/totals**

* Add fields to `ReservationSummaryDto` to carry computed upsell lines and totals, for example:
  * `List<UpsellLineDto> UpsellLines`
  * `decimal? UpsellTotalGross`
* `BuildSummaryFromRecord` can later map these from the persisted `StartRequest.SelectedUpsells` (or from a calculator). For now, documenting the placeholders is enough to wire through both draft and final summaries.【F:SharedClasses/Models/ReservationWorkflow/ReservationWorkflowModels.cs†L55-L66】【F:SharedClasses/Services/ReservationWorkflow/ReservationWorkflowService.cs†L103-L119】

**C) Extend payment amount calculation (future change; not implemented now)**

* When upsells are added, the minimal wiring point for payment amount is still in `InitiatePaymentAsync`. The new amount should be `OfferPrice + UpsellTotalGross` (discount display-only; VAT informational only). The wiring is localized to this method and the Ido payment registration in `HandleTpayWebhookAsync` / `AddIdoPaymentAsync`.【F:SharedClasses/Services/ReservationWorkflow/ReservationWorkflowService.cs†L123-L168】【F:SharedClasses/Services/ReservationWorkflow/ReservationWorkflowService.cs†L199-L226】

**D) Surface in UI pages (future wiring)**

* `ClientInfo.razor` uses `BuildDraftSummaryAsync`, and `Summary.razor` / `TransactionSummary.razor` use `BuildSummaryAsync`. Once `ReservationSummaryDto` carries upsell lines/totals, those pages have a single place to render them without new service calls.【F:RentoomBookingWeb/Components/Features/ReservationWorkflow/Pages/ClientInfo.razor†L146-L154】【F:RentoomBookingWeb/Components/Features/ReservationWorkflow/Pages/Summary.razor†L186-L234】【F:RentoomBookingWeb/Components/Features/ReservationWorkflow/Pages/TransactionSummary.razor†L188-L203】
