# Azure IaC (Bicep) - Rentoom Booking Ecosystem

Ten katalog zawiera glowny deployment infrastruktury aplikacyjnej dla:

- `RentoomBookingWeb` jako Azure Web App
- `StayWell API` jako Azure Function App na Flex Consumption
- `StayWell` Static Web App
- storage, monitoring i konfiguracje aplikacyjne

To jest warstwa `app infra`.
Bootstrap tozsamosci GitHub OIDC dla workflowow Functions i Web App jest osobno w:

- `C:\Users\macie\source\repos\RentoomApartementy\RentoomBookingEcosystem\infra\bootstrap-identity-bicep`

## Pelny flow end-to-end

Kolejnosc dla nowego srodowiska:

1. Uruchom glowny deployment infra z tego katalogu:
   - tworzy Function App, Web App, SWA, storage i monitoring
2. Uruchom bootstrap OIDC z `bootstrap-identity-bicep`:
   - tworzy osobne `user-assigned managed identity` dla Functions i Web App
   - tworzy `federatedIdentityCredential` dla obu workflowow
   - nadaje `Website Contributor` na Function App i Web App
3. Skonfiguruj GitHub repo variables i secrets
4. Uruchom workflowy GitHub Actions

Powod tej kolejnosci:

- bootstrap OIDC nadaje dostep do istniejacej Function App i Web App, wiec oba zasoby musza juz istniec
- SWA integration z GitHub jest konfigurowana podczas deploymentu z tego katalogu, wiec `staywellGithubRepositoryToken` musi byc podany juz na etapie tworzenia app infra

## Struktura

- `main.bicep` - deployment na poziomie subskrypcji, tworzy Resource Group i uruchamia modul aplikacyjny
- `modules/app-stack.bicep` - zasoby aplikacyjne, monitoring, storage i app settings
- `modules/postgres-config.bicep` - ustawia parametry na istniejacym Azure Database for PostgreSQL Flexible Server
- `main.dev.parameters.json` - parametry niesekretne dla `dev`, w tym `environment`, `webPlanSku` i `tags`
- `main.prod.parameters.json` - parametry niesekretne dla `prod`, w tym `environment`, `webPlanSku` i `tags`
- `deploy.ps1` - wrapper PowerShell uruchamiajacy `az deployment sub validate` i/lub `create`, przyjmujacy osobny plik parametrow z sekretami; przed deploymentem listuje subskrypcje i wymaga recznego przelaczenia na wlasciwa

## Co faktycznie tworzy deployment

`main.bicep`:

- tworzy Resource Group zdefiniowana w pliku parametrow
- wylicza nazwe storage account dla runtime Function App jako `storagePrefix + uniqueString(...)`
- przekazuje wszystkie parametry do `modules/app-stack.bicep`
- uruchamia `modules/postgres-config.bicep` na RG PostgreSQL, aby ustawic `max_connections`

`modules/app-stack.bicep`:

- Log Analytics Workspace
- Application Insights dla StayWell API
- Application Insights dla Rentoom Booking Web
- referencje do istniejacego PostgreSQL Flexible Server
- Storage Account dla runtime Azure Functions
- kontener blob `function-releases`
- osobny Storage Account dla danych Rentoom Booking
- App Service Plan dla Rentoom Booking Web
- Azure Web App `.NET 8` dla `RentoomBookingWeb`
- Flex Consumption Plan dla StayWell API
- Linux Function App `.NET 8 isolated`
- app settings dla Web App
- app settings dla Function App
- app settings `PostgresPooling__*` dla Web App i Function App
- role assignmenty dla managed identity Function App na runtime storage
- Static Web App dla StayWell
- polaczenie SWA z GitHub repo `RentoomApartementy/RentoomBookingEcosystem`
- branch SWA:
  - `development-main` dla `dev`
  - `main` dla `prod`
- `staticSites/linkedBackends` laczacy SWA z Function App

## Czego ten deployment nie robi

- nie tworzy serwera PostgreSQL
- nie publikuje kodu aplikacji do Web App
- nie publikuje paczki Functions do kontenera `function-releases`
- nie tworzy bootstrap OIDC dla workflowow Functions i Web App
- nie tworzy rekordow DNS ani custom domain bindings dla SWA / Web App / Function App
- nie konfiguruje Key Vault

## Nowe zmiany: PostgreSQL i pooling

Do infra i kodu aplikacji zostalo dodane zarzadzanie polaczeniami PostgreSQL w dwoch warstwach:

1. Serwer PostgreSQL:
   - `modules/postgres-config.bicep` ustawia parametr `max_connections` na istniejacym Flexible Server
   - wartosc jest sterowana parametrem `postgresMaxConnections`
2. Aplikacje `RentoomBookingWeb` i `StayWell API`:
   - Bicep ustawia app settings `PostgresPooling__*`
   - kod aplikacji czyta te wartosci i naklada je na connection string przez `NpgsqlConnectionStringBuilder`
   - dzieje sie to centralnie w:
     - `C:\Users\macie\source\repos\RentoomApartementy\RentoomBookingEcosystem\SharedClasses\Configuration\PostgresConnectionStringProvider.cs`

Po co to zostalo dodane:

- sam upgrade SKU PostgreSQL nie wystarcza, jesli `max_connections` nie zostanie faktycznie ustawione
- samo podniesienie `max_connections` nie wystarcza, jesli aplikacje otwieraja za duze poole
- deploy Bicep ma od teraz utrzymywac oba aspekty spojnie:
  - limit po stronie serwera
  - limity pooli po stronie aplikacji

## Nowe parametry Bicep

W `main.bicep` oraz plikach `main.dev.parameters.json` i `main.prod.parameters.json` dodano:

- `postgresMaxConnections`
  - docelowa wartosc `max_connections` na PostgreSQL Flexible Server
- `postgresPoolingEnabled`
  - wlacza lub wylacza pooling w Npgsql
- `postgresPoolingMinimumPoolSize`
  - minimalny rozmiar poola
- `rentoomWebPostgresMaximumPoolSize`
  - maksymalny rozmiar poola dla `RentoomBookingWeb`
- `staywellApiPostgresMaximumPoolSize`
  - maksymalny rozmiar poola dla `StayWell API`
- `postgresPoolingConnectionIdleLifetime`
  - po ilu sekundach idle connection moze zostac zamkniete
- `postgresPoolingConnectionPruningInterval`
  - jak czesto pool sprawdza idle connections
- `postgresPoolingTimeout`
  - timeout otwierania polaczenia
- `postgresPoolingCommandTimeout`
  - timeout komendy SQL

## Jakie app settings sa teraz zarzadzane przez Bicep

Dla `RentoomBookingWeb` i `StayWell API` Bicep ustawia:

- `PostgresPooling__Enabled`
- `PostgresPooling__MinimumPoolSize`
- `PostgresPooling__MaximumPoolSize`
- `PostgresPooling__ConnectionIdleLifetime`
- `PostgresPooling__ConnectionPruningInterval`
- `PostgresPooling__Timeout`
- `PostgresPooling__CommandTimeout`

Sa to zwykle Azure App Settings mapowane przez .NET Configuration na sekcje:

- `PostgresPooling:Enabled`
- `PostgresPooling:MinimumPoolSize`
- itd.

## Aktualna polityka wartosci

W parametrach Bicep ustawiono obecnie:

- `postgresMaxConnections=429`
- `rentoomWebPostgresMaximumPoolSize=4`
- `staywellApiPostgresMaximumPoolSize=1`
- `postgresPoolingMinimumPoolSize=0`
- `postgresPoolingConnectionIdleLifetime=60`
- `postgresPoolingConnectionPruningInterval=10`
- `postgresPoolingTimeout=15`
- `postgresPoolingCommandTimeout=30`

Dlaczego `Web=4` i `Api=1`, a nie od razu wyzej:

- Azure control plane pokazuje juz `max_connections=429`
- ale runtime PostgreSQL moze nadal przez chwile pracowac z poprzednia wartoscia, dopoki serwer nie zostanie zrestartowany
- bezpieczny deploy nie powinien zakladac, ze nowy limit dziala, dopoki `SHOW max_connections;` nie zwroci `429`

Docelowe wartosci po potwierdzeniu runtime `429`:

- `rentoomWebPostgresMaximumPoolSize=20`
- `staywellApiPostgresMaximumPoolSize=6`

## Krok po kroku: jak bezpiecznie wdrozyc zmiany PostgreSQL

### Wariant A: stan przejsciowy po zmianie SKU lub parametru

Uzyj tego wariantu, jesli:

- Azure pokazuje zapisany `max_connections=429`
- ale serwer nadal odpowiada `SHOW max_connections = 50`

Kroki:

1. Utrzymaj bezpieczne poole w parametrach:
   - `rentoomWebPostgresMaximumPoolSize=4`
   - `staywellApiPostgresMaximumPoolSize=1`
2. Uruchom deployment Bicep:
   - `.\deploy.ps1 -Environment prod -Operation validate-create -SecretParameterFile .\main.prod.secrets.parameters.json`
3. Sprawdz zapisany parametr w Azure:
   - `az postgres flexible-server parameter show --subscription "Subskrypcja Rentoom" --resource-group "RentoomApp" --server-name "rentoom-server-db" --name max_connections`
4. Zrestartuj PostgreSQL Flexible Server, jesli runtime nadal pokazuje stara wartosc:
   - `az postgres flexible-server restart --subscription "Subskrypcja Rentoom" --resource-group "RentoomApp" --name "rentoom-server-db"`
5. Zweryfikuj ponownie runtime:
   - przez `SHOW max_connections;`
6. Dopiero po potwierdzeniu runtime `429` przejdz do wariantu B

### Wariant B: stan docelowy po potwierdzonym runtime `429`

Kroki:

1. Zmien w pliku parametrow srodowiska:
   - `rentoomWebPostgresMaximumPoolSize` z `4` na `20`
   - `staywellApiPostgresMaximumPoolSize` z `1` na `6`
2. Uruchom walidacje:
   - `.\deploy.ps1 -Environment prod -Operation validate -SecretParameterFile .\main.prod.secrets.parameters.json`
3. Uruchom deployment:
   - `.\deploy.ps1 -Environment prod -Operation create -SecretParameterFile .\main.prod.secrets.parameters.json`
4. Sprawdz app settings po deployu:
   - `az webapp config appsettings list --subscription "Subskrypcja RentoomBooking Prod" --resource-group "rg-prod-rentoom-booking" --name "app-prod-rentoombooking"`
   - `az functionapp config appsettings list --subscription "Subskrypcja RentoomBooking Prod" --resource-group "rg-prod-rentoom-booking" --name "func-prod-api-staywell"`
5. Obserwuj metryki:
   - `active_connections`
   - ewentualne timeouty lub `too many clients`

## Krok po kroku: jak sprawdzic aktualny stan

1. Sprawdz wartosc zapisana w Azure:

```powershell
az postgres flexible-server parameter show --subscription "Subskrypcja Rentoom" --resource-group "RentoomApp" --server-name "rentoom-server-db" --name max_connections
```

2. Sprawdz app settings Web App:

```powershell
az webapp config appsettings list --subscription "Subskrypcja RentoomBooking Prod" --resource-group "rg-prod-rentoom-booking" --name "app-prod-rentoombooking"
```

3. Sprawdz app settings Function App:

```powershell
az functionapp config appsettings list --subscription "Subskrypcja RentoomBooking Prod" --resource-group "rg-prod-rentoom-booking" --name "func-prod-api-staywell"
```

4. Sprawdz metryke aktywnych polaczen:

```powershell
az monitor metrics list --subscription "Subskrypcja Rentoom" --resource "/subscriptions/cc504c76-e00a-4964-a54c-36ff58726690/resourceGroups/RentoomApp/providers/Microsoft.DBforPostgreSQL/flexibleServers/rentoom-server-db" --metric active_connections --interval PT1H --aggregation Average Maximum
```

5. Jesli masz dostep SQL do bazy, sprawdz runtime:

```sql
show max_connections;
```

To jest najwazniejszy krok, bo pokazuje faktyczna wartosc dzialajacego serwera, a nie tylko zapisany override w Azure.

## Wymagania przed deploymentem

Potrzebujesz:

- Azure CLI (`az`)
- uprawnien do deploymentu na poziomie subskrypcji
- istniejacego PostgreSQL server wskazanego w parametrach
- GitHub repository token dla integracji SWA z repo

Token GitHub do SWA:

- musi miec dostep do repo `RentoomApartementy/RentoomBookingEcosystem`
- powinien miec scope pozwalajacy Azure SWA skonfigurowac workflow i secret w repo
- jest przekazywany jako `staywellGithubRepositoryToken` w osobnym pliku parametrow z sekretami

## Sekrety w osobnym pliku parametrow

`deploy.ps1` wymaga teraz osobnego pliku JSON z sekretami, podawanego przez parametr `-SecretParameterFile`.

Przykladowe nazwy:

- `main.dev.secrets.parameters.json`
- `main.prod.secrets.parameters.json`

Przykladowa struktura:

```json
{
  "$schema": "https://schema.management.azure.com/schemas/2019-04-01/deploymentParameters.json#",
  "contentVersion": "1.0.0.0",
  "parameters": {
    "staywellDbAppPassword": {
      "value": "<STAYWELL_DB_PASSWORD>"
    },
    "idoBookingApiPassword": {
      "value": "<IDOBOOKING_API_PASSWORD>"
    },
    "rentoomAppDbPassword": {
      "value": "<RENTOOM_APP_DB_PASSWORD>"
    },
    "tpayClientSecret": {
      "value": "<TPAY_CLIENT_SECRET>"
    },
    "tpayMerchantSecurityCode": {
      "value": "<TPAY_MERCHANT_SECURITY_CODE>"
    },
    "ttlockClientSecret": {
      "value": "<TTLOCK_CLIENT_SECRET>"
    },
    "ttlockPassword": {
      "value": "<TTLOCK_PASSWORD>"
    },
    "staywellGithubRepositoryToken": {
      "value": "<STAYWELL_GITHUB_REPOSITORY_TOKEN>"
    }
  }
}
```

Skrypt sprawdza, czy wszystkie wymagane sekrety sa obecne i maja niepuste wartosci.
Tych plikow nie nalezy commitowac do repo.

## Jak uruchomic deployment infra

Z katalogu:

- `C:\Users\macie\source\repos\RentoomApartementy\RentoomBookingEcosystem\infra\azure-dev-bicep`

Zaloguj sie:

```powershell
az login
az account set --subscription "<SUBSCRIPTION_ID_OR_NAME>"
```

Skrypt przed walidacja lub create:

- wypisuje `az account list -o table`
- pokazuje aktualna subskrypcje
- porownuje ja z oczekiwana dla `dev` lub `prod`
- przerywa dzialanie, jesli jestes na zlej subskrypcji

Mapowanie:

- `dev` -> `c079185e-8eeb-40dc-90b4-01cee2fa7e21`
- `prod` -> `687d8cbd-fea7-4ae4-a70f-8cb4629c43c6`

Walidacja:

```powershell
.\deploy.ps1 -Environment dev -Operation validate -SecretParameterFile .\main.dev.secrets.parameters.json
.\deploy.ps1 -Environment prod -Operation validate -SecretParameterFile .\main.prod.secrets.parameters.json
```

Tworzenie:

```powershell
.\deploy.ps1 -Environment dev -Operation create -SecretParameterFile .\main.dev.secrets.parameters.json
.\deploy.ps1 -Environment prod -Operation create -SecretParameterFile .\main.prod.secrets.parameters.json
```

Pelny przebieg:

```powershell
.\deploy.ps1 -Environment dev -Operation validate-create -SecretParameterFile .\main.dev.secrets.parameters.json
.\deploy.ps1 -Environment prod -Operation validate-create -SecretParameterFile .\main.prod.secrets.parameters.json
```

## Parametry niesekretne per srodowisko

Pliki parametrow:

- `C:\Users\macie\source\repos\RentoomApartementy\RentoomBookingEcosystem\infra\azure-dev-bicep\main.dev.parameters.json`
- `C:\Users\macie\source\repos\RentoomApartementy\RentoomBookingEcosystem\infra\azure-dev-bicep\main.prod.parameters.json`

Zawieraja m.in.:

- publiczne URL-e:
  - `staywellBaseUrl`
  - `rentoomWebBaseUrl`
  - `staywellApiBaseUrl`
- typ srodowiska:
  - `environment`
- konfiguracje Web App planu:
  - `webPlanName`
  - `webPlanSku.name`
  - `webPlanSku.tier`
  - `webPlanSku.size`
  - `webPlanSku.capacity`
- wspolne tagi zasobow:
  - `tags.environment`
  - `tags.system`
  - `tags.managedBy`
- ustawienia repo SWA:
  - `staywellGithubOrganization`
  - `staywellGithubRepositoryName`
  - `staywellGithubBranch`
  - `staywellGithubAppLocation`
  - `staywellGithubOutputLocation`
  - `staywellGithubActionSecretName`
- osobne ustawienia Tpay dla Web App i API Functions
- ustawienia PostgreSQL:
  - `postgresMaxConnections`
  - `postgresPoolingEnabled`
  - `postgresPoolingMinimumPoolSize`
  - `rentoomWebPostgresMaximumPoolSize`
  - `staywellApiPostgresMaximumPoolSize`
  - `postgresPoolingConnectionIdleLifetime`
  - `postgresPoolingConnectionPruningInterval`
  - `postgresPoolingTimeout`
  - `postgresPoolingCommandTimeout`

Domyslne wartosci:

`dev`
- `staywellBaseUrl=https://dev.staywell.rentoom.pl`
- `rentoomWebBaseUrl=https://dev.rentoom.pl`
- `staywellApiBaseUrl=https://api-dev.rentoom.pl`
- `webPlanSku=F1/Free`
- `RentoomBookingWeb=Windows`
- `tags.environment=dev`
- `staywellGithubOrganization=RentoomApartementy`
- `staywellGithubRepositoryName=RentoomBookingEcosystem`
- `staywellGithubBranch=development-main`
- `staywellGithubAppLocation=./StayWell`
- `staywellGithubOutputLocation=wwwroot`
- `staywellGithubActionSecretName=AZURE_STATIC_WEB_APPS_API_TOKEN_STAYWELL_DEV`

`prod`
- `staywellBaseUrl=https://staywell.rentoom.pl`
- `rentoomWebBaseUrl=https://rentoom.pl`
- `staywellApiBaseUrl=https://api.rentoom.pl`
- `webPlanSku=B2/Basic`
- `RentoomBookingWeb=Linux`
- `postgresMaxConnections=429`
- `rentoomWebPostgresMaximumPoolSize=4`
- `staywellApiPostgresMaximumPoolSize=1`
- `tags.environment=prod`
- `staywellGithubOrganization=RentoomApartementy`
- `staywellGithubRepositoryName=RentoomBookingEcosystem`
- `staywellGithubBranch=main`
- `staywellGithubAppLocation=./StayWell`
- `staywellGithubOutputLocation=wwwroot`
- `staywellGithubActionSecretName=AZURE_STATIC_WEB_APPS_API_TOKEN_STAYWELL_PROD`

## Konfiguracja GitHub po deploymentcie

Po deploymentcie z tego katalogu trzeba miec w GitHub poprawnie ustawione secrets i variables dla workflowow.

### Repository Variables dla Functions i Web App

Te variables pochodza z outputow bootstrapu OIDC:

- `AZURE_CLIENT_ID_FUNC_DEV_API_STAYWELL`
- `AZURE_CLIENT_ID_APP_DEV_RENTOOMBOOKING`
- `AZURE_TENANT_ID_DEV`
- `AZURE_SUBSCRIPTION_ID_DEV`
- `AZURE_CLIENT_ID_FUNC_PROD_API_STAYWELL`
- `AZURE_CLIENT_ID_APP_PROD_RENTOOMBOOKING`
- `AZURE_TENANT_ID_PROD`
- `AZURE_SUBSCRIPTION_ID_PROD`

Sa one uzywane przez workflowy:

- `C:\Users\macie\source\repos\RentoomApartementy\RentoomBookingEcosystem\.github\workflows\development-main_func-dev-api-staywell.yml`
- `C:\Users\macie\source\repos\RentoomApartementy\RentoomBookingEcosystem\.github\workflows\main_func-prod-api-staywell.yml`
- `C:\Users\macie\source\repos\RentoomApartementy\RentoomBookingEcosystem\.github\workflows\development-main_app-dev-rentoombooking.yml`
- `C:\Users\macie\source\repos\RentoomApartementy\RentoomBookingEcosystem\.github\workflows\main_app-prod-rentoombooking.yml`

### Repository Secrets dla SWA

Aktualne workflowy SWA oczekuja:

- `AZURE_STATIC_WEB_APPS_API_TOKEN_STAYWELL_DEV`
- `AZURE_STATIC_WEB_APPS_API_TOKEN_STAYWELL_PROD`

Sa one uzywane przez:

- `C:\Users\macie\source\repos\RentoomApartementy\RentoomBookingEcosystem\.github\workflows\development-main_swa-dev-staywell.yml`
- `C:\Users\macie\source\repos\RentoomApartementy\RentoomBookingEcosystem\.github\workflows\azure-static-web-apps-gray-mud-05545df03.yml`

Uwaga:

- przy tworzeniu SWA Azure probuje samo skonfigurowac repo i secret na podstawie `staywellGithubRepositoryToken`
- jesli secret nie pojawi sie automatycznie, trzeba go dodac recznie w GitHub repo

## Jakie workflowy istnieja obecnie

`dev`

- Web App:
  - `C:\Users\macie\source\repos\RentoomApartementy\RentoomBookingEcosystem\.github\workflows\development-main_app-dev-rentoombooking.yml`
- Functions API:
  - `C:\Users\macie\source\repos\RentoomApartementy\RentoomBookingEcosystem\.github\workflows\development-main_func-dev-api-staywell.yml`
- Static Web App:
  - `C:\Users\macie\source\repos\RentoomApartementy\RentoomBookingEcosystem\.github\workflows\development-main_swa-dev-staywell.yml`

`prod`

- Web App:
  - `C:\Users\macie\source\repos\RentoomApartementy\RentoomBookingEcosystem\.github\workflows\main_app-prod-rentoombooking.yml`
- Functions API:
  - `C:\Users\macie\source\repos\RentoomApartementy\RentoomBookingEcosystem\.github\workflows\main_func-prod-api-staywell.yml`
- Static Web App:
  - `C:\Users\macie\source\repos\RentoomApartementy\RentoomBookingEcosystem\.github\workflows\azure-static-web-apps-gray-mud-05545df03.yml`

## Kolejnosc wdrozenia dla `dev`

1. Przygotuj plik sekretow, np.:
   - `C:\Users\macie\source\repos\RentoomApartementy\RentoomBookingEcosystem\infra\azure-dev-bicep\main.dev.secrets.parameters.json`
2. Uruchom:
   - `C:\Users\macie\source\repos\RentoomApartementy\RentoomBookingEcosystem\infra\azure-dev-bicep\deploy.ps1 -Environment dev -Operation validate-create -SecretParameterFile C:\Users\macie\source\repos\RentoomApartementy\RentoomBookingEcosystem\infra\azure-dev-bicep\main.dev.secrets.parameters.json`
3. Uruchom bootstrap:
   - `C:\Users\macie\source\repos\RentoomApartementy\RentoomBookingEcosystem\infra\bootstrap-identity-bicep\deploy.ps1 -Environment dev -Operation validate-create`
4. Z outputow bootstrapu ustaw GitHub repository variables:
   - `AZURE_CLIENT_ID_FUNC_DEV_API_STAYWELL`
   - `AZURE_CLIENT_ID_APP_DEV_RENTOOMBOOKING`
   - `AZURE_TENANT_ID_DEV`
   - `AZURE_SUBSCRIPTION_ID_DEV`
5. Sprawdz w GitHub repo secrets:
   - `AZURE_STATIC_WEB_APPS_API_TOKEN_STAYWELL_DEV`
6. Uruchom workflowy lub zrob push na `development-main`

## Kolejnosc wdrozenia dla `prod`

1. Przygotuj plik sekretow, np.:
   - `C:\Users\macie\source\repos\RentoomApartementy\RentoomBookingEcosystem\infra\azure-dev-bicep\main.prod.secrets.parameters.json`
2. Uruchom:
   - `C:\Users\macie\source\repos\RentoomApartementy\RentoomBookingEcosystem\infra\azure-dev-bicep\deploy.ps1 -Environment prod -Operation validate-create -SecretParameterFile C:\Users\macie\source\repos\RentoomApartementy\RentoomBookingEcosystem\infra\azure-dev-bicep\main.prod.secrets.parameters.json`
3. Uruchom bootstrap:
   - `C:\Users\macie\source\repos\RentoomApartementy\RentoomBookingEcosystem\infra\bootstrap-identity-bicep\deploy.ps1 -Environment prod -Operation validate-create`
4. Z outputow bootstrapu ustaw GitHub repository variables:
   - `AZURE_CLIENT_ID_FUNC_PROD_API_STAYWELL`
   - `AZURE_CLIENT_ID_APP_PROD_RENTOOMBOOKING`
   - `AZURE_TENANT_ID_PROD`
   - `AZURE_SUBSCRIPTION_ID_PROD`
5. Sprawdz w GitHub repo secrets:
   - `AZURE_STATIC_WEB_APPS_API_TOKEN_STAYWELL_PROD`
6. Uruchom workflow dla Functions, Web App i SWA lub zrob push na `main`

## Manualne komendy `az`

Jesli chcesz pominac `deploy.ps1`, mozesz uruchomic Azure CLI recznie:

```powershell
az deployment sub validate `
  --location polandcentral `
  --template-file .\main.bicep `
  --parameters .\main.dev.parameters.json `
  --parameters .\main.dev.secrets.parameters.json
```

## Aktualne ograniczenia i niespojnosci

Ten README opisuje aktualny stan kodu.

Obecne ograniczenia:

- Static Web App jest tworzony zawsze w `westeurope`, niezaleznie od `location`
- zapis `max_connections=429` w Azure nie oznacza jeszcze automatycznie, ze runtime PostgreSQL juz pracuje z `429`; po zmianie parametru moze byc wymagany restart serwera
