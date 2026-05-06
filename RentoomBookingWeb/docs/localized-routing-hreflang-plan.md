# Implementation Plan: Localized Routing and Hreflang (SEO)

## Main Objective
Refactor the navigation system in the `RentoomBookingWeb` project to support SEO-friendly, multi-language URLs (e.g., `/pl/apartamenty` vs `/en/apartments`) and automatically generate `<link rel="alternate" hreflang="..." />` tags.

## Source of Truth for Languages
The system supports 33+ languages. The source of truth for culture codes is the `SharedFrontend/Localization/supported-languages.json` file and the `SupportedLanguagesProvider` class.

## Design Guidelines
1. **No External Dependencies Modification:** All changes must be strictly limited to the `RentoomBookingWeb` project.
2. **Protect Critical Paths (Safe-list):**
   - Payment routes (`/oplac/*`) and Tpay integration.
   - Technical routes (API, webhooks, `/regulaminy`).
   - Static assets (images, JS, CSS).
   These paths must remain untouched and will not be localized at the URL level.
3. **Backward Compatibility (Legacy Redirects):** All existing links (e.g., `/apartamenty/123/name`) must continue to function by performing a 301 redirect to the new localized equivalent.
4. **Centralized Routing:** Avoid hardcoding `@page "/en/..."` across components. We will use a smart routing approach based on a centralized dictionary.

---

## Phase 1: Route Architecture (Mapping)
**Objective:** Centralize path management.
1. **Route Registry (LocalizedRouteRegistry):**
   - Create a service (e.g., `RouteLocalizationService`) to store a mapping of "Page Key" -> "Localized Slug".
   - This dictionary will define structures like: `PageKey: "Contact" -> pl: "kontakt", en: "contact", de: "kontakt"`.
2. **Link Generation Helper:**
   - Replace direct code calls like `NavigationManager.NavigateTo("/kontakt")` with a new approach, such as a method that builds the URL from the registry based on the current culture.

## Phase 2: Request Handling (Middleware & Routing)
**Objective:** Intercept requests with prefixes and localize the user session.
1. **URL Culture Middleware (C#):**
   - Add custom middleware in `Program.cs` (or extend `RequestLocalizationOptions`) that recognizes paths like `/{culture}/...`.
   - If the middleware detects a prefix (e.g., `/pl/`), it dynamically sets the `CultureInfo` for the application.
2. **Blazor Routing:**
   - Adjust `@page` directives in main views (e.g., contacts, apartments) to accept an optional language parameter, e.g., `@page "/{Culture?}/kontakt"`. To avoid duplicating pages for 33 languages, we will use a generic approach that leverages the `LocalizedRouteRegistry` to validate if the provided "slug" is correct for the given language.

## Phase 3: SEO and Hreflang
**Objective:** Full compliance with Google guidelines.
1. **HreflangHead Component:**
   - Create a component `<HreflangTags PageKey="Contact" />` to be used within `<HeadContent>`.
   - This component will generate `<link rel="alternate" hreflang="xx" href="..." />` tags for all 33 cultures by reading from our registry.
2. **SitemapController:**
   - Update the `sitemap.xml` generation logic.
   - As per Google specifications, we will add XHTML entries, e.g., `<xhtml:link rel="alternate" hreflang="de" href="https://domain/de/kontakt"/>` inside each URL block.

## Phase 4: Component Implementation
**Objective:** Replace old links and addresses across the UI.
1. Update `<NavLink>` and `<a>` elements in global components such as `Menu.razor` and `Footer.razor`.
2. Apply the new link generation method in apartment views.
3. Test and protect the reservation workflow (`/rezerwuj/...`) from parsing errors.

## Final Verification
- Does visiting `/de/kontakt` display the German version with the correct `hreflang` tags?
- Does navigating to an old route (e.g., home `/`) properly set the context to the default language?
- Does the generated sitemap contain language attributes acceptable by Google Search Console?
- Does the payment process return successfully from Tpay without breaking?

## Current Issues & Debugging Status (As of 2026-05-05)

### 1. Static File Serving Failure (CRITICAL)
- **Problem:** When accessing localized routes (e.g., `/pl-PL/`), static assets like `app.css`, `mapInterop.js`, and `blazor.web.js` are failing to load.
- **Symptoms:** 
  - Browser console shows `Uncaught SyntaxError: Unexpected token '<'` for multiple `.js` files. This indicates the server is returning an HTML 404 page instead of the actual JavaScript content.
  - MIME type errors: `Expected a JavaScript-or-Wasm module script but the server responded with a MIME type of "text/html"`.
  - The website appears completely unstyled (no CSS).
- **Status:** Attempted to move `app.UseStaticFiles()` to the top of the pipeline in `Program.cs` and changed all paths in `App.razor` to absolute (starting with `/`), but the issue persists.

### 2. Middleware & Routing Interference
- **Investigation:** There is a conflict between the `CultureMappingMiddleware` (or Blazor's internal routing) and the static file provider. 
- **Observations:** `curl` requests for static files returned `405 Method Not Allowed`, suggesting that these requests might be hitting a route that doesn't support GET or is being intercepted incorrectly.
- **Temporary Solution:** `CultureMappingMiddleware.cs` was updated to explicitly ignore any path containing a dot (`.`) or `/_blazor`, but the browser still receives HTML for these assets.

### 3. CultureDispatcher Complexity
- **Note:** The `CultureDispatcher.razor` was introduced to handle the `/{Culture}/{Slug}` pattern dynamically. While it correctly identifies pages, the interaction with Blazor's base path and static file resolution under prefixed URLs needs further refinement.

### Next Steps for Later:
1. Verify `IWebHostEnvironment.WebRootPath` value at runtime.
2. Check if any `RewriteOptions` or global filters are active in the project.
3. Test if removing the `CultureMappingMiddleware` entirely allows static files to load (to confirm it is indeed the cause).
4. Investigate why Kestrel returns 405 for simple GET requests to CSS files.