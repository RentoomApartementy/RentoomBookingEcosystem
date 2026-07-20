# RentoomBookingEcosystem — Repository Summary

_Generated 2026-07-17. Solution: `RentoomBooking.sln`, single-repo, all projects target `net8.0`._

## Overview

This is a hotel/apartment self-check-in and booking platform ("Rentoom" / "StayWell") made up of:

- A **serverless Azure Functions API** (data + integrations backend)
- A **guest-facing Blazor WebAssembly PWA** ("StayWell") — self check-in, digital lock, upsells, AI chat, live chat
- A **separate public marketing/booking website** (Blazor Web App, Interactive Server render mode)
- Several **shared class libraries** (domain, Razor components, AI chat, live chat)
- A couple of **dev tooling** projects (resx translator, tests)

No `Directory.Build.props` / `Directory.Packages.props` / `global.json` / `nuget.config` exist — each project pins its own package versions independently, and there is already some version drift (e.g. `Microsoft.Extensions.*` mixed 9.0.x/10.0.0). Worth centralizing before further expansion.

## Projects

| Project | Path | SDK | TFM | Purpose |
|---|---|---|---|---|
| RentoomBooking.Api | `Api/` | Azure Functions Worker (isolated) | net8.0 | Serverless backend API consumed by StayWell at `/api/`. Cosmos DB + EF Core + App Insights, Docker/Linux. Subfolders: ChatAI, Cookies, Events, Integrations, ReservationFunctions, Upsell, Terms. |
| **RentoomBooking.StayWell** | `StayWell/` | **Blazor WebAssembly** (standalone, PWA) | net8.0 | **Guest-facing app.** Self check-in, digital lock control, upsells, AI chat, live chat. Deployed as an Azure Static Web App with an attached Functions API. |
| RentoomBookingWeb | `RentoomBookingWeb/` | Blazor Web App, **Interactive Server** render mode | net8.0 | Public marketing/booking website. HttpContext-heavy, cookie-based localization, direct EF Core/Postgres, Tpay payments, Bitrix CRM, GUS lookup. Architecturally distinct from StayWell. |
| RentoomBooking.SharedClasses | `SharedClasses/` | class library | net8.0 | Core domain/data layer: Database, Migrations, Models, Services, Integrations (Bitrix, IdoBooking, Tpay, GUS, Azure Blob/Key Vault). Referenced by nearly everything. |
| RentoomBooking.SharedFrontend | `SharedFrontend/` | Razor Class Library | net8.0 | Shared Razor components + localization, referenced by both StayWell and RentoomBookingWeb. Only depends on SharedClasses — a good template for the new shared UI library. |
| RentoomBooking.ChatAI | `RentoomBooking.ChatAI/` | class library | net8.0 | Azure OpenAI-backed AI chat service (EF Core/Postgres). |
| RentoomBooking.LiveChat | `RentoomBooking.LiveChat/` | class library | net8.0 | Bitrix live chat + Azure Translator integration (EF Core/Postgres). |
| RentoomBooking.ResxTranslator | `RentoomBooking.ResxTranslator/` | console tool | net8.0 | CLI dev-tool that auto-translates `.resx` localization resources. |
| SharedClasses.Tests | `SharedClasses.Tests/` | xUnit | net8.0 | Unit tests for SharedClasses. |
| ~~Api.Tests~~ | `Api.Tests/` | — | — | Orphaned directory (only `obj/` cache, no `.csproj`, not in `.sln`). Dead weight. |

Also present: `infra/` (Bicep IaC for Azure — `azure-dev-bicep`, `bootstrap-identity-bicep`), `docs/` (12 narrow feature-implementation notes, no general architecture doc).

## StayWell — the app targeted for MAUI

`StayWell/RentoomBooking.StayWell.csproj` is `Microsoft.NET.Sdk.BlazorWebAssembly`, standalone WASM (not a Blazor Web App with server/auto render modes) — the whole component tree is already client-side, which is favorable for a Hybrid port.

**Structure:**
```
StayWell/
  App.razor, Program.cs, _Imports.razor
  Components/   (AnimationsComponents, ModalContent, Upsells, FooterNavigation)
  Layouts/      (MainLayout, Empty)
  Pages/        (Home, Instructions, ManageStay, Registration, Upsells/*, ...)
  Services/     (LocalStorageService, ClipboardService, BitrixService, AiChatClientService, ...)
  States/       (13 scoped state containers: ReservationState, ApartmentState, UpsellCartState, ...)
  wwwroot/      (css, js, icons, images, service-worker.js, manifest.webmanifest, index.html)
```

**Auth model:** No ASP.NET cookie auth, no `[Authorize]`, no custom `AuthenticationStateProvider`. Access is gated by an opaque **reservation token embedded in the URL path** (`/reservation/{token}/...`), persisted to `localStorage` and restored on next visit (`App.razor` `OnAfterRenderAsync` logic). No server-side auth to rip out — but the token-in-URL/localStorage session model needs a native-storage redesign for MAUI.

**Browser-coupled surface that needs abstraction:**
- `Services/LocalStorageService.cs` — wraps `IJSRuntime` calls to `localStorage`
- `Services/ClipboardService.cs`, `FrontendTelemetryService.cs`, `GlobalizationService.cs`, `ReservationTokenService.cs`, `BitrixService.cs`, `LiveChatClientService.cs` — all JS-interop based
- `wwwroot/index.html` — hardcoded Application Insights JS snippet, custom `window.*` scroll/viewport helpers (`syncCartBottomPadding`, `initHeaderScroll`, `initGuideCardSwipe`, etc.), PWA service worker registration
- 3 call sites of `NavigationManager.NavigateTo(..., forceLoad: true)`: language switch, and two payment-gateway (Tpay) redirect flows in the upsells checkout — these assume real browser navigation and need a native equivalent (`WebAuthenticator`/browser tab) on mobile
- `wwwroot/service-worker.js`, `manifest.webmanifest`, `staticwebapp.config.json` — PWA/Azure Static Web Apps hosting artifacts, not applicable to MAUI

## Notes / cleanup candidates unrelated to MAUI

- `Api.Tests/` orphaned directory could be deleted
- Stray `RentoomBooking.StayWell.csproj.Backup.tmp` file sitting next to the real `.csproj`
- No centralized package version management — worth a `Directory.Packages.props` before adding a 4th consumer (MAUI) of `SharedClasses`/`SharedFrontend`
