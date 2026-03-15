# Azure IaC (Bicep) - Rentoom Booking Ecosystem

Ten katalog zawiera infrastrukturę Azure opisaną w Bicep dla:

- `RentoomBookingWeb` jako Azure Web App
- StayWell API jako Azure Function App na Flex Consumption
- StayWell Static Web App
- storage, monitoring i konfigurację połączeń

Podejście jest rozdzielone na dwa etapy:

- provisioning infrastruktury przez Bicep
- publikacja artefaktów aplikacji przez osobne pipeline'y / workflow

## Struktura

- `main.bicep` - deployment na poziomie subskrypcji, tworzy Resource Group i uruchamia moduł aplikacyjny
- `modules/app-stack.bicep` - właściwe zasoby aplikacyjne, monitoring, storage i app settings
- `main.dev.parameters.json` - parametry niesekretne dla DEV
- `main.prod.parameters.json` - parametry niesekretne dla PROD
- `deploy.ps1` - wrapper PowerShell uruchamiający `az deployment sub validate` i/lub `create`

## Co faktycznie tworzy deployment

`main.bicep`:

- tworzy Resource Group zdefiniowaną w pliku parametrów
- wylicza nazwę storage account dla runtime Function App jako `storagePrefix + uniqueString(...)`
- przekazuje wszystkie parametry do `modules/app-stack.bicep`

`modules/app-stack.bicep`:

- Log Analytics Workspace
- Application Insights dla StayWell API
- Application Insights dla Rentoom Booking Web
- referencję do istniejącego PostgreSQL Flexible Server
- Storage Account dla runtime Azure Functions
- kontener blob `function-releases` pod pakiety Functions
- osobny Storage Account dla danych Rentoom Booking
- App Service Plan dla Rentoom Booking Web
- Azure Web App dla `RentoomBookingWeb`
- Flex Consumption Plan dla StayWell API
- Linux Function App `.NET 8 isolated`
- app settings dla Web App z konfiguracją współdzieloną z Functions tam, gdzie używa jej też `RentoomBookingWeb`
- app settings dla Function App
- role assignmenty dla managed identity Function App na storage runtime
- Static Web App dla StayWell
- połączenie SWA z GitHub repo `RentoomApartementy/RentoomBookingEcosystem`
- konfigurację branch dla SWA:
  - `development-main` dla `dev`
  - `main` dla `prod`
- `staticSites/linkedBackends` łączący SWA z Function App

## Czego deployment nie robi

- nie tworzy serwera PostgreSQL
- nie publikuje kodu aplikacji do Web App
- nie publikuje paczki Functions do kontenera `function-releases`
- nie konfiguruje Key Vault
- nie tworzy rekordów DNS ani custom domain bindings dla SWA / Web App / Function App

## Aktualna konfiguracja aplikacji

### Rentoom Booking Web

Web App dostaje:

- `ASPNETCORE_ENVIRONMENT=Development`
- `WEBSITE_RUN_FROM_PACKAGE=1`
- `APPLICATIONINSIGHTS_CONNECTION_STRING`
- `ConnectionStrings__POSTGRES_RENTOOM_BOOKING_DB_LOCAL`
- `ConnectionStrings__RentoomDbConnectionString`
- sekcję `Tpay`
  - `Tpay__NotificationUrl` wspólny, wskazuje na API Function
  - `Tpay__SuccessUrl` z parametrów `tpayWebSuccessUrl`
  - `Tpay__ErrorUrl` z parametrów `tpayWebErrorUrl`
  - `Tpay__RentoomSiteBaseUrl` z parametrów `tpayWebRentoomSiteBaseUrl`
- `IdoBooking__UseDummy`
- `IdoBooking__DummyReservationTemplateKey`
- `IDOBOOKING_BASE_API_URL`
- `IDOBOOKING_API_USER`
- `IDOBOOKING_API_PWD`
- `StayWellUrlBase`
- `StayWellReservationUrlBase`
- `Storage__Container`
- `Storage__ConnectionString`
- `Storage__AccountName`

### StayWell API Function App

Function App dostaje:

- `AZURE_FUNCTIONS_ENVIRONMENT=Development`
- host storage przez managed identity:
  - `AzureWebJobsStorage__accountName`
  - `AzureWebJobsStorage__credential=managedidentity`
- `APPLICATIONINSIGHTS_CONNECTION_STRING`
- sekcję `Tpay`
  - `Tpay__NotificationUrl` wspólny, wskazuje na API Function
  - `Tpay__SuccessUrl` z parametrów `tpayApiSuccessUrl`
  - `Tpay__ErrorUrl` z parametrów `tpayApiErrorUrl`
  - `Tpay__RentoomSiteBaseUrl` z parametrów `tpayApiRentoomSiteBaseUrl`
- `IdoBooking__UseDummy`
- `IdoBooking__DummyReservationTemplateKey`
- `IDOBOOKING_BASE_API_URL`
- `IDOBOOKING_API_USER`
- `IDOBOOKING_API_PWD`
- `StayWellUrlBase`
- `StayWellReservationUrlBase`
- `Storage__Container`
- `Storage__ConnectionString`
- `Storage__AccountName`
- `InstructionsStorage__Container`
- `InstructionsStorage__ConnectionString`
- `InstructionsStorage__AccountName`
- `TTLOCK__ClientId`
- `TTLOCK__ClientSecret`
- `TTLOCK__Username`
- `TTLOCK__Password`
- `ConnectionStrings__POSTGRES_RENTOOM_BOOKING_DB_LOCAL`
- `ConnectionStrings__RentoomDbConnectionString`

Ważne:

- dla Flex Consumption runtime jest ustawiany przez `functionAppConfig.runtime`
- template nie ustawia `FUNCTIONS_WORKER_RUNTIME`
- template nie ustawia `FUNCTIONS_EXTENSION_VERSION`
- `StayWellReservationUrlBase` jest wyliczany z parametru `staywellBaseUrl`
- `StayWellUrlBase` jest ustawiany bezpośrednio z parametru `staywellBaseUrl`
- `Tpay__NotificationUrl` jest wyliczany z parametru `staywellApiBaseUrl`
- `RentoomBookingWeb` i `StayWell API` dostają osobne wartości:
  - `tpayWebSuccessUrl` / `tpayApiSuccessUrl`
  - `tpayWebErrorUrl` / `tpayApiErrorUrl`
  - `tpayWebRentoomSiteBaseUrl` / `tpayApiRentoomSiteBaseUrl`
- `Api` i `RentoomBookingWeb` czytają wyłącznie sekcję `Tpay`
- link StayWell w `RentoomBookingWeb` jest budowany z `StayWellReservationUrlBase`, a nie z twardo wpisanego URL

## Parametry i środowiska

Skrypt `deploy.ps1` przyjmuje dwa parametry:

- `-Environment`: `dev` albo `prod`
- `-Operation`: `validate`, `create`, `validate-create`

Na podstawie `-Environment` skrypt wybiera:

- `main.dev.parameters.json`
- `main.prod.parameters.json`

Pliki parametrów zawierają wyłącznie wartości niesekretne. Sekrety są trzymane bezpośrednio w `deploy.ps1` w obiekcie:

```powershell
$secretParameters = [ordered]@{
    staywellDbAppPassword    = 'PROVIDE_STAYWELL_DB_PASSWORD'
    idoBookingApiPassword    = 'PROVIDE_IDOBOOKING_API_PASSWORD'
    rentoomAppDbPassword     = 'PROVIDE_RENTOOM_APP_DB_PASSWORD'
    tpayClientSecret         = 'PROVIDE_TPAY_CLIENT_SECRET'
    tpayMerchantSecurityCode = 'PROVIDE_TPAY_MERCHANT_SECURITY_CODE'
    ttlockClientSecret       = 'PROVIDE_TTLOCK_CLIENT_SECRET'
    ttlockPassword           = 'PROVIDE_TTLOCK_PASSWORD'
    staywellGithubRepositoryToken = 'PROVIDE_STAYWELL_GITHUB_REPOSITORY_TOKEN'
}
```

Skrypt:

- odczytuje `location` z wybranego pliku parametrów
- buduje tymczasowy plik parametrów tylko dla sekretów
- uruchamia `az deployment sub validate` i/lub `az deployment sub create`
- usuwa tymczasowy plik sekretów po zakończeniu

Pliki parametrów zawierają też publiczne bazowe URL-e środowiska:

- `staywellBaseUrl`
- `rentoomWebBaseUrl`
- `staywellApiBaseUrl`
- `staywellGithubOrganization`
- `staywellGithubRepositoryName`
- `staywellGithubBranch`
- `staywellGithubAppLocation`
- `staywellGithubOutputLocation`
- `staywellGithubActionSecretName`
- `tpayWebSuccessUrl`
- `tpayWebErrorUrl`
- `tpayWebRentoomSiteBaseUrl`
- `tpayApiSuccessUrl`
- `tpayApiErrorUrl`
- `tpayApiRentoomSiteBaseUrl`

Domyślne wartości:

`dev`
- `staywellBaseUrl=https://staywell-dev.rentoom.pl`
- `rentoomWebBaseUrl=https://dev.rentoom.pl`
- `staywellApiBaseUrl=https://api-dev.rentoom.pl`
- `staywellGithubOrganization=RentoomApartamenty`
- `staywellGithubRepositoryName=RentoomBookingEcosystem`
- `staywellGithubBranch=development-main`
- `staywellGithubAppLocation=./StayWell`
- `staywellGithubOutputLocation=wwwroot`
- `staywellGithubActionSecretName=AZURE_STATIC_WEB_APPS_API_TOKEN_STAYWELL_DEV`

`prod`
- `staywellBaseUrl=https://staywell.rentoom.pl`
- `rentoomWebBaseUrl=https://rentoom.pl`
- `staywellApiBaseUrl=https://api.rentoom.pl`
- `staywellGithubOrganization=RentoomApartamenty`
- `staywellGithubRepositoryName=RentoomBookingEcosystem`
- `staywellGithubBranch=main`
- `staywellGithubAppLocation=./StayWell`
- `staywellGithubOutputLocation=wwwroot`
- `staywellGithubActionSecretName=AZURE_STATIC_WEB_APPS_API_TOKEN_STAYWELL_PROD`

Te parametry służą do budowy konfiguracji aplikacyjnej oraz do podpięcia repo GitHub do StayWell Static Web App. Obecny Bicep nie tworzy jeszcze samych powiązań custom domain ani certyfikatów w Azure.

## Uruchomienie

Z katalogu `infra/azure-dev-bicep`:

```bash
az login
az account set --subscription "<SUBSCRIPTION_ID_OR_NAME>"
```

Następnie uzupełnij placeholdery w `deploy.ps1` i uruchom:

```powershell
.\deploy.ps1 -Environment dev -Operation validate
.\deploy.ps1 -Environment dev -Operation create
.\deploy.ps1 -Environment dev -Operation validate-create

.\deploy.ps1 -Environment prod -Operation validate
.\deploy.ps1 -Environment prod -Operation create
.\deploy.ps1 -Environment prod -Operation validate-create
```

## Ręczne komendy AZ

Jeśli chcesz pominąć `deploy.ps1`, możesz uruchomić Azure CLI ręcznie:

```bash
az deployment sub validate \
  --location polandcentral \
  --template-file ./main.bicep \
  --parameters ./main.dev.parameters.json \
  --parameters staywellDbAppPassword="<STAYWELL_DB_PASSWORD>" \
  --parameters idoBookingApiPassword="<IDOBOOKING_API_PASSWORD>" \
  --parameters rentoomAppDbPassword="<RENTOOM_APP_DB_PASSWORD>" \
  --parameters tpayClientSecret="<TPAY_CLIENT_SECRET>" \
  --parameters tpayMerchantSecurityCode="<TPAY_MERCHANT_SECURITY_CODE>" \
  --parameters ttlockClientSecret="<TTLOCK_CLIENT_SECRET>" \
  --parameters ttlockPassword="<TTLOCK_PASSWORD>"

az deployment sub create \
  --name rentoom-dev-bootstrap \
  --location polandcentral \
  --template-file ./main.bicep \
  --parameters ./main.dev.parameters.json \
  --parameters staywellDbAppPassword="<STAYWELL_DB_PASSWORD>" \
  --parameters idoBookingApiPassword="<IDOBOOKING_API_PASSWORD>" \
  --parameters rentoomAppDbPassword="<RENTOOM_APP_DB_PASSWORD>" \
  --parameters tpayClientSecret="<TPAY_CLIENT_SECRET>" \
  --parameters tpayMerchantSecurityCode="<TPAY_MERCHANT_SECURITY_CODE>" \
  --parameters ttlockClientSecret="<TTLOCK_CLIENT_SECRET>" \
  --parameters ttlockPassword="<TTLOCK_PASSWORD>"
```

`--location` w `az deployment sub ...` dotyczy lokalizacji metadanych deploymentu na poziomie subskrypcji. Region zasobów kontroluje parametr `location` w pliku parametrów.

## Efektywne nazwy zasobów

Przy uruchomieniu przez `deploy.ps1` obowiązują nazwy z plików parametrów, nie same defaulty z `main.bicep`.

### DEV

- Resource Group: `rg-dev-rentoom-booking`
- Web App: `app-dev-rentoombooking`
- Static Web App: `swa-dev-staywell`
- Function App: `func-dev-api-staywell`
- Web plan: `plan-dev-rentoombooking`
- Function plan: `plan-dev-api-staywell`
- Data storage: `storagerentoombookingdev`
- Log Analytics: `log-dev-rentoombooking`
- API App Insights: `app-insights-dev-api-staywell`
- Web App Insights: `app-insights-dev-rentoombooking`

### PROD

- Resource Group: `rg-prod-rentoom-booking`
- Web App: `app-prod-rentoombooking`
- Static Web App: `swa-prod-staywell`
- Function App: `func-prod-api-staywell`
- Web plan: `plan-prod-rentoombooking`
- Function plan: `plan-prod-api-staywell`
- Data storage: `storrentoombookingprod`
- Log Analytics: `log-prod-rentoombooking`
- API App Insights: `app-insights-prod-api-staywell`
- Web App Insights: `app-insights-prod-rentoombooking`

Nazwa storage account dla runtime Functions jest generowana dynamicznie i nie jest wpisana na sztywno w pliku parametrów.

## Aktualne ograniczenia i niespójności

Ten README opisuje aktualny stan kodu. Obecnie w template są też ważne ograniczenia:

- `main.bicep` ma domyślne `tags.environment = 'dev'`
- jeśli nie nadpiszesz `tags`, deployment `prod` nadal oznaczy zasoby tagiem `environment=dev`
- `main.bicep` używa stałej nazwy deploymentu modułu `app-stack-dev`, także dla `prod`
- Web App zawsze dostaje `ASPNETCORE_ENVIRONMENT=Development`
- Function App zawsze dostaje `AZURE_FUNCTIONS_ENVIRONMENT=Development`
- Static Web App jest tworzony zawsze w `westeurope`, niezależnie od `location` dla reszty zasobów

To nie są uwagi do poprawy dokumentacji - to jest obecne zachowanie skryptów.
