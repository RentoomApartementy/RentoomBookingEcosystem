# Azure DEV IaC (Bicep) – Rentoom Booking Ecosystem

To jest wersja **Bicep** (zamiast Terraform), zgodna z rekomendacją Microsoft dla Azure IaC.

## Co jest wdrażane

- **dev rentoom booking**: App Service Plan `F1` + Web App (`app-dev-rentoombooking`) z system-assigned managed identity.
- **dev staywell**: Static Web App Free (`swa-dev-staywell`).
- **dev API staywell**: Function App Linux .NET 8 isolated + system-assigned managed identity na planie **Flex Consumption (`FC1`)**.
- Połączenie SWA ↔ Functions przez `staticSites/linkedBackends`, aby StayWell miał `/api/*`.
- Storage Account dla Functions.

## Struktura

- `main.bicep` – deployment na poziomie subskrypcji (tworzy RG i wywołuje moduł).
- `modules/app-stack.bicep` – właściwe zasoby aplikacyjne.
- `main.dev.parameters.json` – przykładowe parametry DEV.

## Uruchomienie

```bash
# logowanie
az login
az account set --subscription "<SUBSCRIPTION_ID_OR_NAME>"

# walidacja
az deployment sub validate \
  --location westeurope \
  --template-file ./main.bicep \
  --parameters ./main.dev.parameters.json

# wdrożenie
az deployment sub create \
  --name rentoom-dev-bootstrap \
  --location westeurope \
  --template-file ./main.bicep \
  --parameters ./main.dev.parameters.json
```

## Nazwy zgodne z wymaganiem

Domyślne nazwy odpowiadają preferencji:
- `app-dev-rentoombooking`
- `swa-dev-staywell`
- `func-dev-api-staywell`

Jeśli chcesz warianty (`dev staywell`, `dev rentonbooking`, `dev API`) zmień wartości w `main.dev.parameters.json`.
