# GitHub OIDC Bootstrap (Bicep)

Ten katalog tworzy bootstrap tozsamosci deploymentowej GitHub Actions dla:

- Azure Functions `StayWell API`
- Azure Web App `RentoomBookingWeb`

Bootstrap jest oparty o:

- `user-assigned managed identity`
- `federatedIdentityCredential`
- RBAC `Website Contributor` na konkretnym App Service

To jest osobna warstwa od:

- `C:\Users\macie\source\repos\RentoomApartementy\RentoomBookingEcosystem\infra\azure-dev-bicep`

## Kiedy uruchamiac ten bootstrap

Dopiero po utworzeniu app infra, bo ten deployment nadaje role na istniejacej Function App i Web App.

Kolejnosc:

1. uruchom `azure-dev-bicep`
2. uruchom `bootstrap-identity-bicep`
3. ustaw GitHub repository variables
4. uruchom workflow Functions i Web App

## Struktura

- `main.bicep` - deployment na poziomie subskrypcji
- `modules/identity-resources.bicep` - tworzy UAMI i federated credential w RG identity
- `modules/site-access.bicep` - nadaje RBAC na docelowej Function App albo Web App
- `main.dev.parameters.json` - bootstrap dla `development-main`
- `main.prod.parameters.json` - bootstrap dla `main`
- `deploy.ps1` - wrapper PowerShell dla `az deployment sub validate/create`

## Co tworzy deployment

Per srodowisko:

- Resource Group na identity
- UAMI dla workflowu Functions
- federated credential dla workflowu Functions
- `Website Contributor` na docelowej Function App
- UAMI dla workflowu Web App
- federated credential dla workflowu Web App
- `Website Contributor` na docelowej Web App

## Czego deployment nie robi

- nie tworzy workflowow GitHub Actions
- nie dodaje GitHub repository variables
- nie tworzy App Registration w Entra ID
- nie nadaje uprawnien dla SWA

## Jak dziala subject OIDC

Federated credential jest branch-based.

Subject ma postac:

```text
repo:<githubOrganizationSlug>/<githubRepositoryName>:ref:refs/heads/<githubBranch>
```

Ten sam subject jest uzywany dla identity Functions i Web App w danym srodowisku.

## Domyslne konfiguracje

`dev`

- identity RG: `rg-dev-rentoom-github-identity`
- Functions identity: `uami-dev-gha-func-api-staywell`
- Web App identity: `uami-dev-gha-app-rentoombooking`
- branch: `development-main`
- Function App: `func-dev-api-staywell`
- Web App: `app-dev-rentoombooking`
- app RG: `rg-dev-rentoom-booking`

`prod`

- identity RG: `rg-prod-rentoom-github-identity`
- Functions identity: `uami-prod-gha-func-api-staywell`
- Web App identity: `uami-prod-gha-app-rentoombooking`
- branch: `main`
- Function App: `func-prod-api-staywell`
- Web App: `app-prod-rentoombooking`
- app RG: `rg-prod-rentoom-booking`

## GitHub owner slug

`githubOrganizationSlug` musi byc slugiem z URL repo, nie nazwa wyswietlana organizacji.

W tym repo lokalny `origin` wskazuje na:

```text
https://github.com/RentoomApartementy/RentoomBookingEcosystem
```

Dlatego domyslnie ustawione jest:

```text
githubOrganizationSlug=RentoomApartementy
```

## Jak uruchomic bootstrap

Z katalogu:

- `C:\Users\macie\source\repos\RentoomApartementy\RentoomBookingEcosystem\infra\bootstrap-identity-bicep`

Zaloguj sie:

```powershell
az login
az account set --subscription "<SUBSCRIPTION_ID_OR_NAME>"
```

Walidacja:

```powershell
.\deploy.ps1 -Environment dev -Operation validate
.\deploy.ps1 -Environment prod -Operation validate
```

Tworzenie:

```powershell
.\deploy.ps1 -Environment dev -Operation create
.\deploy.ps1 -Environment prod -Operation create
```

Pelny przebieg:

```powershell
.\deploy.ps1 -Environment dev -Operation validate-create
.\deploy.ps1 -Environment prod -Operation validate-create
```

## Jakie outputy sa potrzebne do GitHub

Po deploymentcie wez z outputow:

- `functionManagedIdentityClientId`
- `webAppManagedIdentityClientId`
- `tenantId`

Subscription ID bierzesz z parametrow:

- `targetFunctionAppSubscriptionId`
- `targetWebAppSubscriptionId`

## Jak ustawic GitHub repository variables

W repo:

- `Settings` -> `Secrets and variables` -> `Actions` -> `Variables`

Ustaw:

`dev`

- `AZURE_CLIENT_ID_FUNC_DEV_API_STAYWELL` = `functionManagedIdentityClientId` z deploymentu `dev`
- `AZURE_CLIENT_ID_APP_DEV_RENTOOMBOOKING` = `webAppManagedIdentityClientId` z deploymentu `dev`
- `AZURE_TENANT_ID_DEV` = `tenantId` z deploymentu `dev`
- `AZURE_SUBSCRIPTION_ID_DEV` = subscription, na ktorej leza `func-dev-api-staywell` i `app-dev-rentoombooking`

`prod`

- `AZURE_CLIENT_ID_FUNC_PROD_API_STAYWELL` = `functionManagedIdentityClientId` z deploymentu `prod`
- `AZURE_CLIENT_ID_APP_PROD_RENTOOMBOOKING` = `webAppManagedIdentityClientId` z deploymentu `prod`
- `AZURE_TENANT_ID_PROD` = `tenantId` z deploymentu `prod`
- `AZURE_SUBSCRIPTION_ID_PROD` = subscription, na ktorej leza `func-prod-api-staywell` i `app-prod-rentoombooking`

Te variables sa uzywane przez workflowy:

- `C:\Users\macie\source\repos\RentoomApartementy\RentoomBookingEcosystem\.github\workflows\development-main_func-dev-api-staywell.yml`
- `C:\Users\macie\source\repos\RentoomApartementy\RentoomBookingEcosystem\.github\workflows\main_func-prod-api-staywell.yml`
- `C:\Users\macie\source\repos\RentoomApartementy\RentoomBookingEcosystem\.github\workflows\development-main_app-dev-rentoombooking.yml`
- `C:\Users\macie\source\repos\RentoomApartementy\RentoomBookingEcosystem\.github\workflows\main_app-prod-rentoombooking.yml`

## Jak sprawdzic, czy bootstrap jest poprawny

Powinienes miec:

- Resource Group identity
- 2 `user-assigned managed identity` per srodowisko
- 2 federated credentials per srodowisko z subject:
  - `repo:RentoomApartementy/RentoomBookingEcosystem:ref:refs/heads/development-main`
  - albo `repo:RentoomApartementy/RentoomBookingEcosystem:ref:refs/heads/main`
- role `Website Contributor` na odpowiedniej Function App
- role `Website Contributor` na odpowiedniej Web App

W GitHub Actions krok `azure/login@v2` powinien przejsc bez publish profile.

## Uwagi

- Ten bootstrap zaklada workflowy branch-based bez `environment:` w GitHub Actions.
- Jesli pozniej dodasz `environment:` do workflowu, obecny subject federacji przestanie pasowac i trzeba bedzie zmienic model federated credentials.
- Rola `Website Contributor` jest nadawana tylko na konkretnej Function App lub Web App, nie na cala Resource Group.
