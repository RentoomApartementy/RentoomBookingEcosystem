# Azure DEV IaC (Bicep) – Rentoom Booking Ecosystem

Ten katalog zawiera infrastrukturę Azure opisaną w **Bicep**. Obecne podejście to:

- deployment na poziomie **subskrypcji**
- utworzenie Resource Group z `main.bicep`
- wdrożenie właściwych zasobów przez moduł `modules/app-stack.bicep`
- oddzielenie **provisioningu infrastruktury** od **wdrożenia artefaktów aplikacji**

Nie ma tu osobnych skryptów build/deploy dla infrastruktury. Bazowy sposób uruchomienia to komendy `az deployment sub ...` uruchamiane ręcznie lub z pipeline.

## Co faktycznie tworzy deployment

`main.bicep`:

- tworzy Resource Group `rg-dev-rentoom-booking` w `polandcentral`
- wylicza nazwę storage konta dla runtime Functions na podstawie `storagePrefix + uniqueString(...)`
- przekazuje parametry do modułu aplikacyjnego

`modules/app-stack.bicep`:

- Log Analytics Workspace dla monitoringu
- dwa zasoby Application Insights:
  - dla StayWell API
  - dla Rentoom Booking Web
- referencję do istniejącego PostgreSQL Flexible Server
  - serwer PostgreSQL **nie jest tworzony** przez ten deployment
  - deployment zakłada istniejący serwer w innej subskrypcji / RG wskazanej parametrami
- Storage Account dla runtime Azure Functions
- kontener blob `function-releases` dla pakietów deploymentu Functions
- osobny Storage Account dla danych Rentoom Booking
- App Service Plan `F1` dla Rentoom Booking Web
- Web App `app-dev-rentoombooking` z system-assigned managed identity
- Flex Consumption Plan `FC1` dla StayWell API
- Linux Function App `.NET 8 isolated` z system-assigned managed identity
- app settings dla Function App:
  - `AZURE_FUNCTIONS_ENVIRONMENT=Development`
  - storage hosta Functions przez managed identity
  - connection string do Application Insights
  - ustawienia połączenia do PostgreSQL
- role assignments na storage dla managed identity Function App:
  - `Storage Blob Data Owner`
  - `Storage Queue Data Contributor`
  - `Storage Account Contributor`
- Static Web App `swa-dev-staywell` w regionie `westeurope`
- połączenie SWA -> Function App przez `staticSites/linkedBackends`

## Ważne cechy obecnego podejścia

- Template tworzy infrastrukturę, ale **nie publikuje kodu aplikacji**.
- Kontener `function-releases` jest przygotowany pod pakiety Functions, ale sam Bicep nie uploaduje tam artefaktów.
- Web App ma `WEBSITE_RUN_FROM_PACKAGE=1`, więc zakłada późniejsze dostarczenie paczki aplikacji.
- Function App na Flex Consumption ma runtime ustawiony przez `functionAppConfig.runtime`.
- W app settings dla Flex Consumption nie są używane `FUNCTIONS_WORKER_RUNTIME` ani `FUNCTIONS_EXTENSION_VERSION`.
- Static Web App jest tworzony w `westeurope`, nawet jeśli reszta zasobów ma `location=polandcentral`.

## Struktura

- `main.bicep` - deployment subskrypcyjny i bootstrap Resource Group
- `modules/app-stack.bicep` - właściwe zasoby aplikacyjne i monitoring
- `main.dev.parameters.json` - przykładowy zestaw parametrów dla DEV bez sekretów

## Parametry

Najważniejsze parametry wejściowe:

- `location`
- `resourceGroupName`
- `rentoomWebAppName`
- `staywellStaticWebAppName`
- `staywellApiFunctionName`
- `webPlanName`
- `functionPlanName`
- `storagePrefix`
- `rentoomDataStorageAccountName`
- `logAnalyticsWorkspaceName`
- `staywellApiAppInsightsName`
- `rentoomWebAppInsightsName`
- `postgresSubscriptionId`
- `postgresResourceGroupName`
- `postgresServerName`
- `staywellDbName`
- `staywellDbAppUser`
- `staywellDbAppPassword`

Uwagi:

- `staywellDbAppPassword` jest parametrem `@secure()` i nie jest zapisany w `main.dev.parameters.json`.
- `rentoomDataStorageAccountName` musi być globalnie unikalne w Azure Storage.
- nazwa storage konta dla runtime Functions jest generowana automatycznie i nie jest wpisywana na sztywno w parametrze końcowym

## Uruchomienie

Przykład z katalogu `infra/azure-dev-bicep`:

```bash
az login
az account set --subscription "<SUBSCRIPTION_ID_OR_NAME>"

az deployment sub validate \
  --location polandcentral \
  --template-file ./main.bicep \
  --parameters ./main.dev.parameters.json \
  --parameters staywellDbAppPassword="<DB_PASSWORD>"

az deployment sub create \
  --name rentoom-dev-bootstrap \
  --location polandcentral \
  --template-file ./main.bicep \
  --parameters ./main.dev.parameters.json \
  --parameters staywellDbAppPassword="<DB_PASSWORD>"
```

`--location` w `az deployment sub ...` dotyczy lokalizacji metadanych deploymentu na poziomie subskrypcji. Domyślny region zasobów kontroluje parametr `location` w Bicep.

## Domyślne nazwy w obecnym stanie

- `rg-dev-rentoom-booking`
- `app-dev-rentoombooking`
- `swa-dev-staywell`
- `func-dev-api-staywell`
- `asp-dev-rentoombooking-f1`
- `asp-dev-api-staywell-fc1`
- `storagerentoombookingdev`
- `log-dev-rentoombooking`
- `app-insights-dev-api-staywell`
- `app-insights-dev-rentoombooking`

Jeśli chcesz inne nazwy środowiskowe, zmień odpowiednie wartości w `main.dev.parameters.json` lub w domyślnych parametrach `main.bicep`.
