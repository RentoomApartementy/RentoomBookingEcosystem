# Upsell orders & payments (current architecture) + voucher provisioning hook

> Scope: **documentation only**. This describes the existing persistence and workflow behavior for upsell orders and payment status, and proposes the smallest safe hook for voucher provisioning.

## 1) Data access layer (EF Core)

* **EF Core DbContext**: `PostgresBookingDbContext` defines the tables, including upsell orders and reservation records. It uses `DbSet<T>` + `OnModelCreating` mappings and `IDbContextFactory<PostgresBookingDbContext>` in stores. `EnsureCreatedAsync()` is used in the stores to initialize the schema if needed.【F:SharedClasses/Database/PostgresBookingDbContext.cs†L1-L146】【F:SharedClasses/Services/Upsell/UpsellOrderStore.cs†L29-L42】【F:SharedClasses/Services/Upsell/UpsellOrderStore.cs†L230-L236】
* **Migrations**: Upsell tables are created by EF Core migrations; see `20260206211622_add_Upsell_post_buy_tables.cs` for the schema definitions.【F:SharedClasses/Migrations/20260206211622_add_Upsell_post_buy_tables.cs†L1-L69】
* **Entity mappings**: Table/column mapping for upsell order records/lines and reservation records live in `SQLDatabaseEntities.cs` with `[Table]`, `[Column]`, and `[Timestamp]` attributes.【F:SharedClasses/Models/Database/EFEntitites/SQLDatabaseEntities.cs†L86-L226】

## 2) Existing entities/models

### `upsell_order_records`
* **Entity**: `UpsellOrderRecordEntity` (`[Table("upsell_order_records")]`) with columns like `upsell_order_guid`, `upsell_order_json`, `reservation_guid`, `payment_status`, `provider_transaction_id`, `paid_at_utc`, and `row_version`.【F:SharedClasses/Models/Database/EFEntitites/SQLDatabaseEntities.cs†L136-L178】
* **Table definition (migration)**: Created in `20260206211622_add_Upsell_post_buy_tables.cs` with the same fields and `row_version` concurrency token.【F:SharedClasses/Migrations/20260206211622_add_Upsell_post_buy_tables.cs†L39-L58】

### `upsell_order_lines`
* **Entity**: `UpsellOrderLineEntity` (`[Table("upsell_order_lines")]`) with columns like `upsell_order_line_guid`, `upsell_order_guid`, `partner_service_id`, `line_status`, etc.【F:SharedClasses/Models/Database/EFEntitites/SQLDatabaseEntities.cs†L179-L226】
* **Table definition (migration)**: Created in `20260206211622_add_Upsell_post_buy_tables.cs` with line-level fields and `line_status` length constraint (32).【F:SharedClasses/Migrations/20260206211622_add_Upsell_post_buy_tables.cs†L13-L37】

### `payment_status` enum/values
* **Enum-like constants**: `PaymentStatuses` in `ReservationWorkflowModels.cs` defines: `None`, `Initiated`, `Paid`, `Failed`. These values are used by both reservation and upsell workflows and persisted in the `payment_status` column.【F:SharedClasses/Models/ReservationWorkflow/ReservationWorkflowModels.cs†L164-L170】【F:SharedClasses/Models/Database/EFEntitites/SQLDatabaseEntities.cs†L120-L157】

### `line_status` enum/values
* **Enum-like constants**: `UpsellLineStatuses` in `UpsellOrderModels.cs` defines: `Pending`, `Paid`, `Cancelled`, `Refunded`. These values are persisted in `upsell_order_lines.line_status`.【F:SharedClasses/Models/Upsell/UpsellOrderModels.cs†L47-L53】【F:SharedClasses/Models/Database/EFEntitites/SQLDatabaseEntities.cs†L214-L218】

## 3) Where an upsell order transitions to **Paid**

The **upsell payment workflow** lives in `UpsellOrderWorkflowService`. The Paid transition is handled in two places:

### A) Payment callback (Tpay webhook)
* **Trigger**: `HandleTpayWebhookAsync(UpsellWebhookDto dto)` processes the Tpay notification and marks the order paid.
* **Snippet (short)**:

```csharp
var isPaid = string.Equals(dto.Status, "PAID", StringComparison.OrdinalIgnoreCase);
record.PaymentStatus = isPaid ? PaymentStatuses.Paid : PaymentStatuses.Failed;
record.PaidAtUtc = isPaid ? DateTime.UtcNow : record.PaidAtUtc;
```

* **After payment**: If paid, it loads existing order lines, sets `LineStatus = Paid`, and persists both the order and lines.【F:SharedClasses/Services/Upsell/UpsellOrderWorkflowService.cs†L141-L206】

### B) Immediate paid creation (non-webhook path)
* **Trigger**: `CreatePaidOrderAsync(...)` is a direct path for creating a paid order (e.g., internal or backoffice flow) and sets payment status and line statuses to Paid before persisting updates.【F:SharedClasses/Services/Upsell/UpsellOrderWorkflowService.cs†L51-L83】

**Conclusion**: The canonical payment callback path is **Tpay webhook** (A), which is the safest place to hook provisioning after payment confirmation.【F:SharedClasses/Services/Upsell/UpsellOrderWorkflowService.cs†L141-L206】

## 4) Mapping `reservation_guid` to reservation dates (Start/End)

* `UpsellOrderRecordEntity` stores `reservation_guid`, linking the upsell order to a reservation record (nullable).【F:SharedClasses/Models/Database/EFEntitites/SQLDatabaseEntities.cs†L144-L152】
* The reservation workflow persists reservation state in `reservation_records.reservation_json` as a `ReservationState`, which includes `StartRequest.StartDate` and `StartRequest.EndDate` (both `DateOnly`).【F:SharedClasses/Models/ReservationWorkflow/ReservationWorkflowModels.cs†L15-L33】【F:SharedClasses/Models/ReservationWorkflow/ReservationWorkflowModels.cs†L93-L108】
* To fetch the dates from a `reservation_guid`, call `IReservationStore.GetAsync(reservationGuid)` and read:
  * `record.State.StartRequest.StartDate`
  * `record.State.StartRequest.EndDate`
  The store maps `reservation_records.reservation_json` into `ReservationState` and returns it as `ReservationRecord` with the `StartRequest` included.【F:SharedClasses/Services/ReservationWorkflow/ReservationStore.cs†L48-L77】【F:SharedClasses/Services/ReservationWorkflow/ReservationStore.cs†L109-L139】

## 5) Recommended hook point for voucher provisioning (minimal risk)

**Recommendation**: Place the provisioning call after a successful payment is persisted inside `HandleTpayWebhookAsync` (the primary callback entrypoint) to minimize risk of issuing vouchers before payment is confirmed.

**Exact location**:
* File: `SharedClasses/Services/Upsell/UpsellOrderWorkflowService.cs`
* Method: `HandleTpayWebhookAsync(UpsellWebhookDto dto, ...)`
* Place **after** the line updates for paid orders in the `isPaid` branch (after `_store.ReplaceLinesAsync(...)`), so the order + lines are already saved and marked paid.

**Proposed call (placeholder)**:
```csharp
await EnsureVouchersForOrderAsync(record.UpsellOrderGuid, cancellationToken);
```

Why here?
* The webhook is the authoritative payment confirmation path and already performs the Paid transition plus line updates in a single workflow step.【F:SharedClasses/Services/Upsell/UpsellOrderWorkflowService.cs†L141-L206】
* This minimizes risk of provisioning on unconfirmed payment or on retries (it already guards for `PaymentStatus == Paid`).【F:SharedClasses/Services/Upsell/UpsellOrderWorkflowService.cs†L167-L186】

**Secondary consideration**: If `CreatePaidOrderAsync(...)` is used for internal paid creation, it may need the same hook after `_store.ReplaceLinesAsync(...)` to keep parity with the webhook path.【F:SharedClasses/Services/Upsell/UpsellOrderWorkflowService.cs†L51-L83】

## 6) Existing constraints, row_version, concurrency patterns

* **Row version for optimistic concurrency**:
  * `reservation_records` and `upsell_order_records` include a `row_version` `[Timestamp]` field on the entity and `rowVersion: true` in migrations. This is EF Core’s optimistic concurrency token for updates.【F:SharedClasses/Models/Database/EFEntitites/SQLDatabaseEntities.cs†L126-L134】【F:SharedClasses/Migrations/20260206211622_add_Upsell_post_buy_tables.cs†L51-L56】
* **Concurrency handling in workflow**:
  * `UpsellOrderWorkflowService` catches `DbUpdateConcurrencyException` and retries both payment initiation and webhook handling (loop + delay). This indicates concurrent updates are expected in the payment flow.【F:SharedClasses/Services/Upsell/UpsellOrderWorkflowService.cs†L115-L138】【F:SharedClasses/Services/Upsell/UpsellOrderWorkflowService.cs†L197-L216】
* **Unique indexes**: No explicit indexes or unique constraints are defined for upsell tables in the migration; primary keys are used on the GUIDs only.【F:SharedClasses/Migrations/20260206211622_add_Upsell_post_buy_tables.cs†L13-L58】

---

## Summary of “who does what” (requested mappings)

* **Create `upsell_order_records`**: `UpsellOrderStore.CreateAsync(...)` creates `UpsellOrderRecordEntity` and inserts it into `PostgresBookingDbContext.UpsellOrderRecords`.【F:SharedClasses/Services/Upsell/UpsellOrderStore.cs†L43-L73】
* **Create `upsell_order_lines`**: `UpsellOrderStore.ReplaceLinesAsync(...)` builds `UpsellOrderLineEntity` records and inserts them into `PostgresBookingDbContext.UpsellOrderLines`. It is used by `CreateWithLinesAsync(...)` as well.【F:SharedClasses/Services/Upsell/UpsellOrderStore.cs†L75-L90】【F:SharedClasses/Services/Upsell/UpsellOrderStore.cs†L155-L178】
* **Update `payment_status` to Paid**:
  * `UpsellOrderWorkflowService.HandleTpayWebhookAsync(...)` sets `PaymentStatus = Paid` on confirmed Tpay callback, then updates line status to `Paid` as well.【F:SharedClasses/Services/Upsell/UpsellOrderWorkflowService.cs†L141-L206】
  * `UpsellOrderWorkflowService.CreatePaidOrderAsync(...)` sets `PaymentStatus = Paid` for immediate paid creation flows.【F:SharedClasses/Services/Upsell/UpsellOrderWorkflowService.cs†L51-L83】
* **Link `reservation_guid` to reservations/workflow state**:
  * Upsell order records store `reservation_guid` in `UpsellOrderRecordEntity`, which points to a `reservation_records` row identified by `ReservationRecordEntity.ReservationGuid`.
  * `ReservationStore.GetAsync(reservationGuid)` loads `reservation_records.reservation_json` into `ReservationState`, from which `StartRequest.StartDate` and `StartRequest.EndDate` are read for date mapping.【F:SharedClasses/Models/Database/EFEntitites/SQLDatabaseEntities.cs†L144-L152】【F:SharedClasses/Services/ReservationWorkflow/ReservationStore.cs†L48-L77】【F:SharedClasses/Models/ReservationWorkflow/ReservationWorkflowModels.cs†L15-L33】
