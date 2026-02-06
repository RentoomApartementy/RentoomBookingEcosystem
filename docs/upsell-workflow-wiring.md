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

## 5) Architectural advice for StayWell payment flows (reservation vs upsells)

The key difference is that reservation-related payments must synchronize with IdoBooking, while upsell-only payments should remain independent. The cleanest separation is to treat both as a single **Payment Orchestrator** entry point that routes to specialized flow handlers and publishes a **normalized payment event**. Downstream, one subscriber handles IdoBooking updates (reservation payments only) and another handles StayWell-specific fulfillment for upsells. This keeps payment creation consistent while avoiding cross-contamination of business rules.

### Recommended structure (conceptual)

**A) Payment Orchestrator (single entry point)**
* Accepts a `PaymentIntent` that includes `FlowType` (Reservation / Upsell) plus metadata (`ReservationId`, `UpsellOrderId`, etc.).
* Resolves the proper flow handler and returns a flow-specific `RedirectUrl`, `SuccessReturnUrl`, and `NotificationUrl`.

**B) Flow Handlers (two separate implementations)**
* **ReservationPaymentFlow**
  * Knows how to calculate the reservation amount.
  * On success, emits a `PaymentSettled` event that includes `ReservationId` and `PaymentReference`.
  * Subscriber updates IdoBooking using that event.
* **UpsellPaymentFlow**
  * Calculates upsell totals and creates a standalone payment session.
  * On success, emits `PaymentSettled` with `UpsellOrderId` and optional `ReservationId` (if tied to a stay).
  * Subscriber triggers upsell fulfillment and internal invoice records (no IdoBooking update unless you explicitly want it).

**C) Event-driven separation**
* Use a common event envelope so all flows can be monitored uniformly.
* The IdoBooking sync logic should be an isolated subscriber that **only** reacts to reservation-type events.

### Pseudocode (documentation only; not implementation)

```text
enum PaymentFlowType { Reservation, Upsell }

PaymentIntent {
  FlowType
  Amount
  Currency
  ReservationId?       // required for Reservation flow
  UpsellOrderId?       // required for Upsell flow
  ReturnUrls { Success, Cancel }
  Metadata
}

PaymentOrchestrator.CreatePayment(intent):
  handler = PaymentFlowRegistry.Resolve(intent.FlowType)
  session = handler.CreateSession(intent)
  return PaymentSessionResponse {
    RedirectUrl: session.RedirectUrl,
    SuccessReturnUrl: handler.SuccessReturnUrl(intent),
    NotificationUrl: handler.NotificationUrl(intent)
  }

ReservationPaymentFlow.CreateSession(intent):
  ensure intent.ReservationId present
  amount = ReservationPricing.Calculate(intent.ReservationId)
  return PaymentGateway.CreateSession(amount, intent.ReturnUrls, metadata = { ReservationId })

UpsellPaymentFlow.CreateSession(intent):
  ensure intent.UpsellOrderId present
  amount = UpsellPricing.Calculate(intent.UpsellOrderId)
  return PaymentGateway.CreateSession(amount, intent.ReturnUrls, metadata = { UpsellOrderId })

WebhookHandler.OnPaymentSettled(notification):
  event = PaymentEvent.Normalize(notification)
  Publish(event)

IdoBookingSubscriber.OnPaymentSettled(event):
  if event.FlowType == Reservation:
    IdoBooking.RegisterPayment(event.ReservationId, event.Amount, event.PaymentReference)

UpsellFulfillmentSubscriber.OnPaymentSettled(event):
  if event.FlowType == Upsell:
    UpsellOrders.MarkPaid(event.UpsellOrderId, event.PaymentReference)
    UpsellServices.Fulfill(event.UpsellOrderId)
```

### Why this approach works well

* **Different return/notification URLs are natural** because the flow handler owns them.
* **IdoBooking is isolated** so upsell payments never trigger a reservation sync.
* **Metrics and auditing remain unified** via a common `PaymentSettled` event, even if you change providers later.
