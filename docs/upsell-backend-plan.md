# Upsell backend foundations plan

## A) Existing patterns found

- **DbContext & entities**: `PostgresBookingDbContext` in `SharedClasses/Database` uses `DbSet<>` mappings for booking data, with entities centralized in `SharedClasses/Models/Database/EFEntitites/SQLDatabaseEntities.cs` and `OnModelCreating` configuring JSONB columns and defaults for booking records. This is the established pattern for Postgres-backed persistence. 
- **Migrations**: EF Core migrations live in `SharedClasses/Migrations/` and are tied to `PostgresBookingDbContext` (snapshot + incremental migrations). That is the standard place to add schema changes. 
- **Function routing style**: Azure Functions use `[Function]` + `[HttpTrigger]` with `AuthorizationLevel.Anonymous`, explicit HTTP verbs, and route templates like `ido/payments`, `db/terms/...`, `apartments/...`, etc. The responses are built via `HttpRequestData.CreateResponse()` and JSON serialization with `JsonConvert`. 
- **DI + configuration**: `Api/Program.cs` wires services with `AddScoped` and `AddDbContextFactory<PostgresBookingDbContext>()`, registers integration services (Tpay, Bitrix, IdoSell), and sets default JSON camelCase settings. This is the consistent place to add new services and options.

## B) What already exists that we can reuse

- **Pricing + offers**: `IIdoOfferService` and `IRentoomOfferService` already fetch pricing offers and merge with apartment data. These can be leveraged to compute upsell catalog pricing and availability rules. 
- **Payments**: `IdoSellService` plus `PaymentsFunction` expose payment add/edit/confirm flows; `Tpay` integration handles payment sessions and notifications. The upsell purchase flow can reuse payment session conventions and statuses used by `ReservationWorkflow`. 
- **Persistence patterns**: `ReservationRecordEntity` and `DefinedAddonEntity` show JSONB payloads and extension via new tables. `ApartmentRepository` and `PostgresBookingDatabase` provide repository patterns to read/write from Postgres. 
- **Integrations**: existing Integrations folders include Bitrix and RentoomApp contexts (QrMaint, Partners) and demonstrate service + DbContext organization for external systems. These can guide any voucher/CRM integration work if needed.

## C) Minimal list of new services/DTOs/entities/endpoints (aligned to conventions)

**Entities (SharedClasses/Models/Database/EFEntitites/SQLDatabaseEntities.cs)**
- `UpsellCatalogItemEntity` (table `upsell_catalog_items`) — stores catalog definitions (likely JSONB payload + updated_at).
- `UpsellQuoteEntity` (table `upsell_quotes`) — stores quote request/response (JSONB payload, status, created_at).
- `UpsellPurchaseEntity` (table `upsell_purchases`) — stores purchase records (quote_id, payment status, provider ids, created_at).
- `UpsellVoucherEntity` (table `upsell_vouchers`) — stores generated voucher codes (purchase_id, code, status, payload).

**DTOs (SharedClasses/Models/Upsell/)**
- `UpsellCatalogRequest` / `UpsellCatalogResponse`
- `UpsellQuoteRequest` / `UpsellQuoteResponse`
- `UpsellPurchaseRequest` / `UpsellPurchaseResponse`
- `UpsellVoucherResponse` (or `VoucherIssueResponse`)

**Services (SharedClasses/Services/Upsell/)**
- `IUpsellCatalogService` + `UpsellCatalogService` (read-only catalog + availability/pricing)
- `IUpsellQuoteService` + `UpsellQuoteService` (quote computation + persistence)
- `IUpsellPurchaseService` + `UpsellPurchaseService` (purchase persistence + payment status updates)
- `IUpsellVoucherService` + `UpsellVoucherService` (voucher generation + persistence)

**Repositories (SharedClasses/Database/)**
- `UpsellCatalogRepository`, `UpsellQuoteRepository`, `UpsellPurchaseRepository`, `UpsellVoucherRepository` (following the existing repository + `IDbContextFactory<PostgresBookingDbContext>` pattern)

**Azure Functions endpoints (Api/)**
- `GET upsell/catalog` (catalog read)
- `POST upsell/quote` (quote creation)
- `POST upsell/purchase` (purchase persistence)
- `GET upsell/vouchers/{voucherCode}` or `GET upsell/purchases/{purchaseId}/voucher` (voucher retrieval)

## D) Step-by-step PR-sized roadmap (Prompts 2–10)

**Prompt 2 — Schema + DTO scaffolding**
- Add Upsell entities to `SQLDatabaseEntities.cs` and wire them into `PostgresBookingDbContext`.
- Create minimal DTOs under `SharedClasses/Models/Upsell/` (request/response contracts).
- Add EF Core migration under `SharedClasses/Migrations/` for new tables.

**Prompt 3 — Repositories + DI**
- Implement Upsell repositories in `SharedClasses/Database/` using `IDbContextFactory<PostgresBookingDbContext>`.
- Register repositories and new Upsell services in `Api/Program.cs` (AddScoped).

**Prompt 4 — Catalog read foundation**
- Implement `IUpsellCatalogService` with read-only catalog retrieval (from Postgres JSONB payloads).
- Add `GET upsell/catalog` Azure Function following existing HttpTrigger style.

**Prompt 5 — Quote service + endpoint**
- Implement `IUpsellQuoteService` to compute/store quotes using pricing data from `IRentoomOfferService`/`IIdoOfferService` where needed.
- Add `POST upsell/quote` function that validates payload and returns quote response.

**Prompt 6 — Purchase persistence**
- Implement `IUpsellPurchaseService` to create purchase records, reuse payment status patterns from `ReservationWorkflow`.
- Add `POST upsell/purchase` function to store purchase records and return status.

**Prompt 7 — Voucher generation**
- Implement `IUpsellVoucherService` to create voucher codes and persist them in `upsell_vouchers`.
- Add endpoint for retrieving voucher data by purchase id or voucher code.

**Prompt 8 — Payment provider alignment**
- Wire optional payment session fields to match existing `Tpay`/`PaymentStatuses` patterns; no new payment provider logic, just aligned status transitions.

**Prompt 9 — Observability + validation**
- Add structured logging to upsell services and endpoints (consistent with existing `ILogger` usage).
- Add lightweight validation errors consistent with other functions (400 with message).

**Prompt 10 — Documentation + integration notes**
- Update docs with examples of request/response payloads and endpoint list.
- Capture data contracts for front-end to consume (StayWell / RentoomBookingWeb).
