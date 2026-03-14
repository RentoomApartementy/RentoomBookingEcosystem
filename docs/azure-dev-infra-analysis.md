# Analiza kodu i propozycja IaC dla środowiska DEV na Azure

## 1. Co wynika z aktualnego kodu

- `StayWell` jest aplikacją Blazor WebAssembly i w `Program.cs` dla środowiska nie-DEV buduje adres API jako `.../api/`, z komentarzem że API Functions jest podpięte do Static Web App.
- `RentoomBookingWeb` jest projektem ASP.NET Core Web (`Microsoft.NET.Sdk.Web`) – pasuje do hostingu w App Service.
- `RentoomBooking.Api` jest Azure Functions (.NET 8, Isolated worker, Functions v4) – pasuje do Azure Functions.

Wniosek: Twoje założenie architektoniczne (App Service + SWA + Functions) jest spójne z kodem.

## 2. Proponowany układ DEV (free-first)

- **dev rentoom booking** → App Service Plan `F1` + Windows Web App (`app-dev-rentoombooking`).
- **dev staywell** → Static Web App `Free` (`swa-dev-staywell`).
- **dev API staywell** → Linux Function App .NET 8 isolated na planie **Flex Consumption (`FC1`)** + Storage Account.
- Rejestracja Function App w Static Web App (żeby `/api/*` działało zgodnie z kodem frontu StayWell).

## 3. Gotowe IaC (Bicep)

Dodałem setup Bicep w: `infra/azure-dev-bicep/`.

Zawiera:
- deployment na poziomie subskrypcji (tworzy resource group),
- moduł z zasobami aplikacyjnymi,
- plan `F1` dla web,
- plan Flex Consumption `FC1` dla Functions,
- managed identity dla Web App i Function App,
- link SWA ↔ Functions,
- przykładowe parametry DEV.

## 4. Rekomendacje operacyjne

1. Trzymaj sekrety (connection stringi, API keys) wyłącznie w App Settings/Key Vault, nie w repo.
2. Dodaj osobne pipeline’y deploymentu dla trzech aplikacji, kierujące na nowe zasoby DEV.
3. Włącz alerty budżetowe dla Functions i monitoruj cold start / limity planu Free.
