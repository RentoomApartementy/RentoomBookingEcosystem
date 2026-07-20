# StayWell → .NET MAUI Blazor Hybrid Migration Plan

Target: ship StayWell (currently a standalone Blazor WebAssembly PWA at `StayWell/`) as native Android/iOS apps via **.NET MAUI Blazor Hybrid**, sharing UI components and business logic with the existing web app through a Razor Class Library (RCL).

This plan is written against the actual repo state — see [docs/repo-summary.md](repo-summary.md) for the full audit. Key fact that shapes the whole plan: StayWell is *already* standalone WASM (not Server/Auto render mode), has *no* ASP.NET cookie auth, and its "session" is a reservation token carried in the URL + `localStorage`. That's the main thing to redesign; everything else is mostly move-and-relink.

---

## 1. Repository Analysis (recap)

- **Hosting model**: `StayWell/RentoomBooking.StayWell.csproj` uses SDK `Microsoft.NET.Sdk.BlazorWebAssembly`, standalone WASM PWA (`WebAssemblyHostBuilder` + `RootComponents.Add<App>("#app")`), **not** a Blazor Web App — no `RenderMode.InteractiveAuto`/`InteractiveServer`/`Routes.razor`. `TargetFramework=net8.0`.
- **Reusable UI**: `StayWell/Components/`, `StayWell/Layouts/`, `StayWell/Pages/` (all `.razor` + co-located `.razor.css`), plus `StayWell/App.razor`, `_Imports.razor`.
- **Shared assets** (`StayWell/wwwroot/`): `css/app.css`, `css/bootstrap/`, `icons/*.svg`, `images/`, `js/bitrixchat2.js`, `js/StayWellAiChat.js`, `js/upsellstrip.js`, `js/wheelPicker.js`. Not shareable as-is: `manifest.webmanifest`, `service-worker.js`/`service-worker.published.js`, `staticwebapp.config.json` (PWA/Azure SWA-only).
- **Web-specific logic needing DI abstraction**:
  - `Services/LocalStorageService.cs` (JSInterop → `localStorage`) → replace with an `IAppStorageService` abstraction (`Preferences`/`SecureStorage` on MAUI).
  - `Services/ReservationTokenService.cs` + `App.razor`'s URL-token parsing logic (session/deep-link handling) → needs a platform-aware navigation/session strategy.
  - `Services/ClipboardService.cs`, `FrontendTelemetryService.cs` (App Insights via raw `<script>` in `index.html`), `GlobalizationService.cs` (culture via `blazorCulture.get/set` JS interop in `Program.cs`).
  - `wwwroot/index.html` custom `window.*` scroll/viewport JS helpers (`syncCartBottomPadding`, `initHeaderScroll`, `initGuideCardSwipe`, `scrollGuideTrack`, etc.) — DOM-coupled but still work under `BlazorWebView` since it hosts a real web view; verify per-platform quirks (WebView2 on Windows, WKWebView on iOS, Android System WebView) — worth a manual test pass instead of a rewrite.
  - 3 `NavigationManager.NavigateTo(..., forceLoad:true)` call sites: `Components/ModalContent/LanguageModalContent.razor:36` (language switch reload), `Pages/Upsells/UpsellsCartPage.razor:547` and `Pages/Upsells/UpsellPaymentWorkflow/PaymentSuccess.razor:54` (Tpay payment-gateway redirects) — these need a native browser flow (`WebAuthenticator`/`Browser.OpenAsync`) on mobile, not a full-page reload.
  - No cookie/`HttpContext`/`[Authorize]` auth to migrate — good news, one less thing to abstract.

---

## 2. Project Restructuring Plan (Razor Class Library)

You already have `SharedFrontend/RentoomBooking.SharedFrontend.csproj` — a Razor Class Library referenced by both StayWell and RentoomBookingWeb, containing only generic shared components/localization. **Don't dump StayWell's app-specific UI into that library** (it's also consumed by the unrelated marketing site). Instead create a **new RCL dedicated to StayWell's UI**, e.g. `RentoomBooking.StayWell.Shared`.

### 2.1 Create the RCL

```powershell
dotnet new razorclasslib -n RentoomBooking.StayWell.Shared -o StayWell.Shared
dotnet sln RentoomBooking.sln add StayWell.Shared\RentoomBooking.StayWell.Shared.csproj
```

Edit `StayWell.Shared/RentoomBooking.StayWell.Shared.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk.Razor">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Blazored.LocalStorage" Version="4.5.0" Condition="false" /> <!-- see note below: don't reference WASM-only storage pkgs here -->
    <PackageReference Include="Microsoft.AspNetCore.Components.Web" Version="8.0.18" />
    <PackageReference Include="Microsoft.Extensions.Http" Version="10.0.0" />
    <PackageReference Include="Microsoft.Extensions.Localization" Version="9.0.9" />
    <PackageReference Include="Newtonsoft.Json" Version="13.0.4" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\SharedClasses\RentoomBooking.SharedClasses.csproj" />
    <ProjectReference Include="..\RentoomBooking.ChatAI\RentoomBooking.ChatAI.csproj" />
  </ItemGroup>
</Project>
```

Note the RCL must reference `Microsoft.AspNetCore.Components.Web` (generic), **not** `Microsoft.AspNetCore.Components.WebAssembly` — the WASM-specific package (and `Blazor.LocalStorage.WebAssembly`, which is WASM-only) stays in the `StayWell` head project only, since MAUI can't use it.

### 2.2 Move files from `StayWell/` into `StayWell.Shared/`

| Move | From | To |
|---|---|---|
| Root shell | `StayWell/App.razor` | `StayWell.Shared/App.razor` |
| Imports | `StayWell/_Imports.razor` | `StayWell.Shared/_Imports.razor` (merge with the RCL's default one) |
| Components | `StayWell/Components/**` | `StayWell.Shared/Components/**` |
| Layouts | `StayWell/Layouts/**` | `StayWell.Shared/Layouts/**` |
| Pages | `StayWell/Pages/**` | `StayWell.Shared/Pages/**` |
| Services (all, since they're pure logic already) | `StayWell/Services/**` | `StayWell.Shared/Services/**` |
| States | `StayWell/States/**` | `StayWell.Shared/States/**` |
| Models/Utils | `StayWell/Models/`, `StayWell/Utils/` | same names under `StayWell.Shared/` |
| Localization | `StayWell/Resources/**` | `StayWell.Shared/Resources/**` |
| Shareable static assets | `StayWell/wwwroot/css`, `icons`, `images`, `js` | `StayWell.Shared/wwwroot/{css,icons,images,js}` |

**Stays in `StayWell/` (the WASM head project), do not move:**
- `Program.cs` (WASM-specific bootstrap)
- `wwwroot/index.html`, `manifest.webmanifest`, `service-worker*.js`, `staticwebapp.config.json`
- `wwwroot/appsettings.json` (keep a copy; MAUI needs its own config source, see §5)

**Services that need to become interfaces before moving** (concrete web implementation stays out of the RCL):
- `LocalStorageService.cs` → split into `IAppStorageService` (interface, moves to RCL) + `Services/LocalStorageAppStorageService.cs` (JSInterop impl, stays in `StayWell/`) + a MAUI-side `PreferencesAppStorageService` (new, in the MAUI project).
- `ClipboardService.cs`, `FrontendTelemetryService.cs`, `GlobalizationService.cs`, `ReservationTokenService.cs` — same treatment: extract an interface into the RCL, keep/rewrite the concrete implementation per-head-project. See §5 for the actual interface shapes.

### 2.3 Fix namespaces and routing

- Update the root namespace: in `StayWell.Shared.csproj`, Razor SDK defaults `RootNamespace` to the assembly name (`RentoomBooking.StayWell.Shared`). Every moved `.razor`/`.cs` file that had `@namespace RentoomBooking.StayWell.Xyz` or implicit `RentoomBooking.StayWell` should become `RentoomBooking.StayWell.Shared.Xyz` — do a solution-wide find/replace of `RentoomBooking.StayWell.Components`, `RentoomBooking.StayWell.Pages`, `RentoomBooking.StayWell.Services`, etc. → `RentoomBooking.StayWell.Shared.*`.
- `_Imports.razor` in the RCL needs `@using RentoomBooking.StayWell.Shared` and per-folder usings (`.Components`, `.Layouts`, `.Services`, `.States`) so pages/components resolve without fully-qualified names.
- Routing: since this app has no `Routes.razor` (routing lives in `App.razor`'s `<Router AppAssembly="...">`), the `AppAssembly` parameter must point at the RCL's assembly (`typeof(SomeRclComponent).Assembly`) from **both** consuming host projects — this is the key wiring point in §3 and §4 below. `@page` routes on moved pages don't need to change.
- Static assets: any `<link>`/`<script>` referencing `css/app.css`, `js/StayWellAiChat.js`, etc. from *within* RCL-hosted components (e.g. component-scoped `.razor.css` isolation, or explicit `<script src="...">` in a moved `.razor` file) must be updated to the RCL's static-web-assets path: `_content/RentoomBooking.StayWell.Shared/css/app.css` when referenced from a host project's `index.html`. Component-scoped CSS isolation (`.razor.css`) is handled automatically by the Razor SDK's bundling — no manual path change needed there, just make sure the host's `<head>` references the RCL's bundled CSS: `_content/RentoomBooking.StayWell.Shared/RentoomBooking.StayWell.Shared.bundle.scoped.css`.

---

## 3. Refactoring the Existing Web App (`StayWell/`)

### 3.1 Reference the RCL

```powershell
dotnet add StayWell\RentoomBooking.StayWell.csproj reference StayWell.Shared\RentoomBooking.StayWell.Shared.csproj
```

`StayWell.csproj` keeps its WASM-only packages (`Microsoft.AspNetCore.Components.WebAssembly*`, `Blazor.LocalStorage.WebAssembly`) and drops the `ProjectReference`s that moved into the RCL's own references (`SharedClasses`, `ChatAI` stay referenced transitively via the RCL, but keep them direct too if `StayWell.csproj`'s own remaining code — `Program.cs` — still touches them).

### 3.2 `Program.cs` changes

`StayWell/Program.cs` no longer defines `App` locally — it now comes from the RCL:

```csharp
using RentoomBooking.StayWell.Shared; // App.razor now lives here

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");   // App resolved from the RCL assembly
builder.RootComponents.Add<HeadOutlet>("head::after");

// register the WASM-specific implementation of the abstracted services:
builder.Services.AddScoped<IAppStorageService, LocalStorageAppStorageService>();
builder.Services.AddScoped<ITelemetryService, WebTelemetryService>();
// ...existing DI registrations for States/Services from §2.2 that stayed as-is
```

No `AdditionalAssemblies` call is needed here specifically because `App.razor`'s own `<Router AppAssembly="typeof(App).Assembly">` already points at the RCL assembly once `App` itself lives there — that's the mechanism that makes routing "just work" for a project that (like this one) has no `Routes.razor`/multi-assembly discovery today. If you later add extra RCLs whose pages should also be routable, add them explicitly: `<Router AppAssembly="typeof(App).Assembly" AdditionalAssemblies="new[] { typeof(SomeOtherLib.Marker).Assembly }">`.

### 3.3 `index.html` / static asset paths

`StayWell/wwwroot/index.html` needs its `<link>`/`<script>` tags pointed at the RCL's `_content/` path instead of the local `wwwroot` path for anything that moved:

```html
<!-- before -->
<link href="css/app.css" rel="stylesheet" />
<script src="js/StayWellAiChat.js"></script>

<!-- after -->
<link href="_content/RentoomBooking.StayWell.Shared/css/app.css" rel="stylesheet" />
<script src="_content/RentoomBooking.StayWell.Shared/js/StayWellAiChat.js"></script>
<link href="_content/RentoomBooking.StayWell.Shared/RentoomBooking.StayWell.Shared.bundle.scoped.css" rel="stylesheet" />
```

Things that stay referenced locally (unchanged): `manifest.webmanifest`, `service-worker.js`, the App Insights inline `<script>` snippet, `appsettings.json`.

### 3.4 Verify

```powershell
dotnet build RentoomBooking.sln
dotnet run --project StayWell\RentoomBooking.StayWell.csproj
```
Smoke-test the reservation-token deep link flow, upsells cart, AI chat panel, and language switch — these are exactly the flows touching the services being abstracted.

---

## 4. Creating the .NET MAUI Blazor Hybrid App

### 4.1 Create the project

Install the MAUI workload once if not already present, then scaffold:

```powershell
dotnet workload install maui
dotnet new maui-blazor -n RentoomBooking.StayWell.Mobile -o StayWell.Mobile
dotnet sln RentoomBooking.sln add StayWell.Mobile\RentoomBooking.StayWell.Mobile.csproj
```

(Visual Studio equivalent: File → New → Project → **.NET MAUI Blazor App**, name `RentoomBooking.StayWell.Mobile`, place under the solution root, target `net8.0-android`/`net8.0-ios`/`net8.0-maccatalyst` as needed.)

### 4.2 Reference the RCL and shared libraries

```powershell
dotnet add StayWell.Mobile\RentoomBooking.StayWell.Mobile.csproj reference StayWell.Shared\RentoomBooking.StayWell.Shared.csproj
```

(`SharedClasses` and `ChatAI` come in transitively through the RCL; add them directly too only if `MauiProgram.cs` needs types from them for DI setup.)

### 4.3 Delete default scaffolded files to avoid duplication

The `maui-blazor` template scaffolds its own placeholder Blazor app — delete these, since the real UI now comes from `StayWell.Shared`:

```
StayWell.Mobile/Components/   (entire folder — Layout/MainLayout.razor, Pages/Home.razor, Pages/Counter.razor, Pages/Weather.razor, _Imports.razor, Routes.razor)
StayWell.Mobile/wwwroot/css/app.css      (replaced by RCL's bundled CSS reference)
StayWell.Mobile/wwwroot/css/bootstrap/   (if template scaffolded its own copy — use the RCL's instead)
```

Keep: `StayWell.Mobile/wwwroot/index.html` (MAUI's own copy — becomes the BlazorWebView's host page, edited per §4.5), `MauiProgram.cs`, `MainPage.xaml(.cs)`, `App.xaml(.cs)`, `Platforms/`.

### 4.4 `MauiProgram.cs`

```csharp
using Microsoft.Extensions.Logging;
using RentoomBooking.StayWell.Shared; // App.razor + services

namespace RentoomBooking.StayWell.Mobile;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()   // MAUI's own App (Platforms shell), NOT the Blazor App.razor — same name, different type/namespace, disambiguate with alias if needed
            .ConfigureFonts(fonts => fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular"));

        builder.Services.AddMauiBlazorWebView();
#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
#endif

        // Shared HttpClient pointed at the Functions API (see §5 for base-address handling)
        builder.Services.AddHttpClient("FunctionsApi", client =>
        {
            client.BaseAddress = new Uri("https://<your-functions-api-host>/api/");
        });

        // Platform implementations of the abstracted services from §2.2:
        builder.Services.AddSingleton<IAppStorageService, PreferencesAppStorageService>();
        builder.Services.AddSingleton<ITelemetryService, MauiTelemetryService>();
        builder.Services.AddSingleton<IPaymentRedirectService, MauiPaymentRedirectService>();

        // ...same States/Services registrations StayWell/Program.cs has, minus WASM-only ones

        return builder.Build();
    }
}
```

Note the `App` naming collision: MAUI's template names its root XAML-backed application class `App` (`App.xaml`/`App.xaml.cs`) in namespace `RentoomBooking.StayWell.Mobile`, while the Blazor root component you moved is also called `App` in namespace `RentoomBooking.StayWell.Shared`. They don't collide at compile time (different namespaces) but **do** require a `using` alias or fully-qualified reference wherever both are in scope (typically only `MainPage.xaml.cs`, shown next) — don't rename either; it's the standard MAUI Blazor Hybrid convention and renaming would diverge from template expectations.

### 4.5 `MainPage.xaml`

```xml
<?xml version="1.0" encoding="utf-8" ?>
<ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
             x:Class="RentoomBooking.StayWell.Mobile.MainPage"
             BackgroundColor="{DynamicResource PageBackgroundColor}">

    <BlazorWebView x:Name="blazorWebView" HostPage="wwwroot/index.html">
        <BlazorWebView.RootComponents>
            <RootComponent Selector="#app" ComponentType="{x:Type shared:App}" />
        </BlazorWebView.RootComponents>
    </BlazorWebView>

</ContentPage>
```

Add the XML namespace for the RCL at the top of the file:
```xml
xmlns:shared="clr-namespace:RentoomBooking.StayWell.Shared;assembly=RentoomBooking.StayWell.Shared"
```

`MainPage.xaml.cs` stays essentially template-default (no code-behind changes needed unless you wire up deep-link handling here — see §5.3).

### 4.6 `wwwroot/index.html` (MAUI copy)

Trim to the essentials — no service worker, no manifest, no App Insights JS snippet (replace with native telemetry, §5), reference the RCL's bundled CSS:

```html
<!DOCTYPE html>
<html>
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1" />
    <link rel="stylesheet" href="_content/RentoomBooking.StayWell.Shared/css/app.css" />
    <link rel="stylesheet" href="_content/RentoomBooking.StayWell.Shared/RentoomBooking.StayWell.Shared.bundle.scoped.css" />
    <link rel="stylesheet" href="css/app.css" /> <!-- MAUI-only overrides, if any -->
</head>
<body>
    <div id="app">Loading...</div>
    <div id="blazor-error-ui">
        An unhandled error has occurred.
        <a href="" class="reload">Reload</a>
        <a class="dismiss">🗙</a>
    </div>
    <script src="_framework/blazor.webview.js" autostart="false"></script>
    <script src="_content/RentoomBooking.StayWell.Shared/js/StayWellAiChat.js"></script>
    <script src="_content/RentoomBooking.StayWell.Shared/js/upsellstrip.js"></script>
    <script src="_content/RentoomBooking.StayWell.Shared/js/wheelPicker.js"></script>
</body>
</html>
```
(`blazor.webview.js` replaces `blazor.webassembly.js` — the template scaffolds this already; don't add both.)

### 4.7 Build & run

```powershell
dotnet build StayWell.Mobile\RentoomBooking.StayWell.Mobile.csproj -f net8.0-android
dotnet build StayWell.Mobile\RentoomBooking.StayWell.Mobile.csproj -f net8.0-ios   # requires a Mac build host or Windows+paired Mac
```
Run via Visual Studio's Android emulator / iOS simulator (Windows can run Android directly; iOS needs "Pair to Mac"), or `dotnet build -t:Run -f net8.0-android`.

---

## 5. Handling Platform-Specific Features & Services

### 5.1 Services needing different implementations

| Service (interface, in RCL) | Web (`StayWell/`) | MAUI (`StayWell.Mobile/`) |
|---|---|---|
| `IAppStorageService` | `LocalStorageAppStorageService` — `IJSRuntime` → `localStorage` | `PreferencesAppStorageService` — `Microsoft.Maui.Storage.Preferences` (or `SecureStorage` for the reservation token specifically, since it's effectively a bearer credential) |
| `ITelemetryService` | `WebTelemetryService` — bridges to the App Insights JS snippet via JSInterop | `MauiTelemetryService` — Application Insights .NET SDK (`Microsoft.ApplicationInsights`) or App Center Crashes/Analytics, called directly, no JS involved |
| `IClipboardService` | `ClipboardService` — JS `navigator.clipboard` interop | wrap `Microsoft.Maui.ApplicationModel.DataTransfer.Clipboard` |
| `ICultureService` (was `GlobalizationService`) | reads/writes culture via `blazorCulture.get/set` JS interop + `localStorage` | reads/writes via `Preferences`, sets `CultureInfo.CurrentCulture` directly (no JS bridge needed at all — simpler on MAUI) |
| `IPaymentRedirectService` (new — wraps the 3 `forceLoad:true` sites) | `NavigationManager.NavigateTo(url, forceLoad:true)` | `Microsoft.Maui.Authentication.WebAuthenticator.AuthenticateAsync(...)` or `Browser.OpenAsync(url, BrowserLaunchMode.SystemPreferred)` for the Tpay redirect; handle the return via a registered callback URL scheme |
| Reservation "session" (`ReservationTokenService` + URL-token logic in `App.razor`) | token parsed from the browser URL path, persisted to `localStorage`, restored via `NavigationManager.NavigateTo($"/reservation/{token}/")` on next visit | no browser URL bar — token instead: (a) entered manually / scanned via QR on first launch, or (b) delivered via a **deep link** (`staywell://reservation/{token}`) registered in `Platforms/Android/AndroidManifest.xml` (intent-filter) and `Platforms/iOS/Info.plist` (`CFBundleURLTypes`), handled in `MainPage.xaml.cs`/`AppDelegate`/`MainActivity` and routed into the Blazor `NavigationManager` once the WebView is ready. Persist to `SecureStorage` instead of `Preferences`. |

### 5.2 Example: storage abstraction end-to-end

**In the RCL** (`StayWell.Shared/Services/IAppStorageService.cs`):
```csharp
public interface IAppStorageService
{
    Task<string?> GetAsync(string key);
    Task SetAsync(string key, string value);
    Task RemoveAsync(string key);
}
```

**Web implementation** (`StayWell/Services/LocalStorageAppStorageService.cs`, stays in the WASM head project since it needs `IJSRuntime`):
```csharp
public class LocalStorageAppStorageService(IJSRuntime js) : IAppStorageService
{
    public async Task<string?> GetAsync(string key) =>
        await js.InvokeAsync<string?>("localStorage.getItem", key);

    public async Task SetAsync(string key, string value) =>
        await js.InvokeVoidAsync("localStorage.setItem", key, value);

    public async Task RemoveAsync(string key) =>
        await js.InvokeVoidAsync("localStorage.removeItem", key);
}
```

**MAUI implementation** (`StayWell.Mobile/Services/PreferencesAppStorageService.cs`):
```csharp
public class PreferencesAppStorageService : IAppStorageService
{
    public Task<string?> GetAsync(string key) =>
        Task.FromResult<string?>(Preferences.Default.Get<string?>(key, null));

    public Task SetAsync(string key, string value)
    {
        Preferences.Default.Set(key, value);
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key)
    {
        Preferences.Default.Remove(key);
        return Task.CompletedTask;
    }
}
```

Every component in the RCL that currently injects `LocalStorageService` directly should instead inject `IAppStorageService` — a mechanical rename across `StayWell.Shared/**`.

### 5.3 API base address per platform

`StayWell/Program.cs` currently switches the `FunctionsApi` `HttpClient` base address between `https://localhost:7238/api/` (dev) and a relative `api/` path (prod, same-origin Azure Static Web App). MAUI has no "same origin" — `MauiProgram.cs` must always use the **absolute** Functions API URL (both for local dev against an emulator/device, and prod), and per §5.1 handle Android emulator's special-cased `10.0.2.2` loopback address if pointing at a local Functions host during development.

### 5.4 Suggested `appsettings`/config handling

`StayWell/wwwroot/appsettings.json` is fetched by the WASM host at startup. MAUI's `BlazorWebView` can still serve a copy from its own `wwwroot/appsettings.json` (bundled as a `MauiAsset`) and read it the same way via `HttpClient`, or — more idiomatically for MAUI — move config values (`ApiBaseUrl`, `FeatureFlags`, `AccessTime`) into `MauiProgram.cs`-level configuration (`builder.Configuration.AddJsonStream(...)` from an embedded resource) so no HTTP round-trip is needed before the first render.

---

## Sequenced Checklist

1. [ ] Create `StayWell.Shared` RCL project, add to `.sln`.
2. [ ] Move `Components/`, `Layouts/`, `Pages/`, `Services/`, `States/`, `Models/`, `Utils/`, `Resources/`, `App.razor`, `_Imports.razor` from `StayWell/` → `StayWell.Shared/`.
3. [ ] Move shareable `wwwroot/{css,icons,images,js}` into `StayWell.Shared/wwwroot/`.
4. [ ] Fix namespaces (`RentoomBooking.StayWell.*` → `RentoomBooking.StayWell.Shared.*`) solution-wide.
5. [ ] Extract interfaces for `LocalStorageService`, `ClipboardService`, `FrontendTelemetryService`, `GlobalizationService` into the RCL; keep/rewrite web-specific `IJSRuntime`-based implementations in `StayWell/`.
6. [ ] Add `IPaymentRedirectService` abstraction; refactor the 3 `forceLoad:true` call sites to use it.
7. [ ] Add `StayWell.Shared` project reference to `StayWell.csproj`; update `Program.cs` and `index.html` asset paths (`_content/...`).
8. [ ] Build + smoke-test the web app end-to-end (deep-link/token flow, upsells checkout, AI chat, language switch).
9. [ ] Scaffold `StayWell.Mobile` via `dotnet new maui-blazor`; add to `.sln`.
10. [ ] Delete template-default `Components/`, default `wwwroot/css`; reference `StayWell.Shared`.
11. [ ] Wire `MauiProgram.cs` DI: platform storage/telemetry/clipboard/payment-redirect implementations, absolute `FunctionsApi` HttpClient base address.
12. [ ] Update `MainPage.xaml` to host `RootComponent` from `StayWell.Shared.App`; add MAUI copy of `index.html`.
13. [ ] Implement deep-link handling (Android intent-filter, iOS `CFBundleURLTypes`) for the reservation-token entry flow; wire `SecureStorage` persistence.
14. [ ] Build/run on Android emulator; verify local Functions API reachability (`10.0.2.2` loopback if testing against localhost).
15. [ ] Build/run on iOS simulator (via Mac or paired Mac); repeat smoke tests.
16. [ ] Add MAUI-specific entries to `.gitignore` (`*.ipa`, `*.apk`, `*.aab`, signing keystores).
17. [ ] (Optional but recommended) Add `Directory.Packages.props` for centralized version management now that a 4th consumer (`StayWell.Mobile`) depends on `SharedClasses`/`SharedFrontend`-style shared libraries.
