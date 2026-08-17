# Product

<!-- impeccable:product-schema 1 -->

## Platform

web

## Users

Three distinct audiences across the ecosystem's two consumer-facing apps:

- **Checked-in guests (StayWell)** — someone who already booked a Rentoom apartment and is on-site or about to arrive. Job: self-check-in, unlock the door, find stay instructions, buy upsells, ask AI/live chat for help, and eventually check out — mostly on a phone, often on hotel wifi/mobile data with imperfect connectivity.
- **Prospective guests (RentoomBookingWeb, booking pages)** — a traveler deciding where to stay and booking directly. Job: find and book a Rentoom apartment, pay (Tpay), and receive confirmation, without going through a third-party OTA.
- **Prospective property partners (RentoomBookingWeb, cooperation/`wspolpraca` pages)** — an apartment owner evaluating whether to list their unit with Rentoom for management. Job: understand the payout model/economics (per the cooperation-page calculator), compare terms, and submit a lead/registration (GUS company lookup is used in this flow).

## Product Purpose

Rentoom Apartamenty operates and manages its own portfolio of short-stay apartments and runs the self-check-in technology stack (StayWell) that lets its guests check in, control the digital door lock, and manage their stay without staff present. RentoomBookingWeb is the public site where travelers book directly and where apartment owners are recruited into Rentoom's managed-property program. Success is a guest who checks in and stays without needing a human, and a booking/partner funnel that converts without OTA fees.

## Positioning

Rentoom owns and directly operates its apartments and builds its own self-check-in/digital-lock/upsell/AI-chat stack in-house, rather than licensing a third-party PMS check-in product or acting as a multi-host marketplace like Airbnb/Booking.com. The differentiation is the pairing of "we run these apartments ourselves" with first-party unattended-checkin technology across the whole guest lifecycle (pre-arrival → lock access → upsells → in-stay support → checkout).

## Operating Context

- **StayWell** is a standalone Blazor WebAssembly PWA (Azure Static Web App), guest-facing, mostly single-session phone use. Access is gated by an opaque reservation token in the URL (`/reservation/{token}/...`), persisted to `localStorage` — there is no login/password flow for guests.
- **RentoomBookingWeb** is a Blazor Web App (Interactive Server), architecturally separate from StayWell, integrating direct EF Core/Postgres, Tpay payments, Bitrix CRM (partner/lead pipeline), and GUS (Polish business registry) lookups for partner onboarding.
- Backend is a shared serverless Azure Functions API (`Api/`) plus shared domain/UI libraries (`SharedClasses`, `SharedFrontend`) consumed by both apps.
- A `docs/staywell-maui-migration-plan.md` exists but no MAUI project exists in the solution; StayWell is confirmed to remain a mobile web PWA for now — do not design or build against native iOS/Android affordances for StayWell.

## Capabilities and Constraints

- StayWell: self check-in flow, digital lock control ("open apartment"), pre-arrival/instructions content, upsells (browse/cart/purchase), AI chat and live chat (Bitrix-backed), parking info, terms, loyalty programme (early/stub).
- RentoomBookingWeb: apartment browse/booking, Tpay checkout, partner cooperation funnel with a payout calculator and GUS-backed registration.
- Extensive localization: StayWell/RentoomBookingWeb ship 30+ language `.resx` resource sets; primary/base market is Poland (`pl-PL`), with English as a secondary complete locale. Any new UI copy must go through the localization resource pattern already in use, not hardcoded strings.
- No ASP.NET cookie auth or `[Authorize]` in StayWell — access control is the reservation-token model described above; do not design flows that assume a persistent authenticated identity for guests.
- Three navigation flows in StayWell force real browser reloads (`NavigateTo(..., forceLoad: true)`): language switch and two Tpay payment-redirect flows in upsell checkout — these are real full-page navigations, not SPA transitions, and any redesign of those flows must account for that page-reload boundary.

## Brand Commitments

- Brand name: **Rentoom Apartamenty**; guest app is branded **StayWell** (PWA manifest: "StayWell - Rentoom Apartamenty" / short name "StayWell Rentoom").
- Existing manifest theme color `#03173d` (dark navy) and a green accent used for Rentoom's data/brand column on RentoomBookingWeb tables (per `RentoomBookingWeb/design-qa.md`); logo asset `rentoom-logo-color.svg`.
- These are incumbent facts to preserve under a refinement request; a redesign brief may treat them as a starting point per [[new-work]], not an untouchable constraint.

## Evidence on Hand

- `docs/repo-summary.md` — architecture/project inventory across the solution.
- `docs/staywell-maui-migration-plan.md` — a MAUI migration plan that is not currently active (see Operating Context).
- `RentoomBookingWeb/design-qa.md` — prior design QA notes for the `/pl/wspolpraca/v3` cooperation page (viewport behavior, hero layout, cost-comparison tables), useful as evidence of established layout/breakpoint conventions on that surface.
- No customer testimonials, case studies, press, or usage-metrics files were found in the repo; do not fabricate these if a surface calls for them.

## Product Principles

1. Guests are usually mid-trip on a phone with a token-based session, not a logged-in account — design for zero-friction, no-signup access, and never assume desktop or persistent login.
2. Everything ships in 30+ languages from day one — layouts must tolerate long translated strings and RTL is not currently in scope but string expansion is.
3. Rentoom operates its own apartments; the story is operational trust and direct control, not marketplace breadth — avoid OTA-marketplace visual/UX tropes (host ratings grids, multi-vendor comparison) that don't fit a single-operator brand.
4. StayWell stays mobile web (PWA), not native, until further notice — build and audit against browser/PWA constraints, not native platform affordances.
5. The reservation-token and forced full-page-reload payment/language flows are structural, not incidental — any StayWell redesign must design around them rather than assuming they'll be refactored away.

## Accessibility & Inclusion

No product-specific accessibility standard was established in this session; none was found documented in the repo.
