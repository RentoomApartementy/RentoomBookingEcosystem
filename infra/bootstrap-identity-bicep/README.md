# GitHub OIDC Bootstrap (Bicep)

Ten katalog tworzy osobny bootstrap dla tozsamosci deploymentowej GitHub Actions opartej o `user-assigned managed identity`.

Zakres tego bootstrapu:

- tworzy osobny Resource Group dla identity
- tworzy `user-assigned managed identity`
- tworzy `federatedIdentityCredential` dla jednego brancha GitHub
- nadaje tej identity role `Website Contributor` na konkretnej Azure Function App

To jest osobna warstwa od `C:\Users\macie\source\repos\RentoomApartementy\RentoomBookingEcosystem\infra\azure-dev-bicep`.

## Struktura

- `main.bicep` - deployment na poziomie subskrypcji
- `modules/identity-resources.bicep` - tworzy UAMI i federated credential w RG identity
- `modules/function-access.bicep` - nadaje RBAC na docelowej Function App
- `main.dev.parameters.json` - bootstrap identity dla `development-main`
- `main.prod.parameters.json` - bootstrap identity dla `main`
- `deploy.ps1` - wrapper PowerShell dla `az deployment sub validate/create`

## Co tworzy deployment

Per srodowisko:

- Resource Group na identity
- `Microsoft.ManagedIdentity/userAssignedIdentities`
- `Microsoft.ManagedIdentity/userAssignedIdentities/federatedIdentityCredentials`
- role assignment `Website Contributor` na docelowej Function App

## Czego deployment nie robi

- nie tworzy workflowow GitHub Actions
- nie dodaje GitHub repository variables
- nie tworzy App Registration w Entra ID
- nie tworzy tozsamosci dla Web App ani Static Web App

## Jak to dziala

Federated credential jest branch-based.

Subject ma postac:

```text
repo:<githubOrganizationSlug>/<githubRepositoryName>:ref:refs/heads/<githubBranch>
```

Domyslne konfiguracje:

`dev`
- identity RG: `rg-dev-rentoom-github-identity`
- identity: `uami-dev-gha-func-api-staywell`
- branch: `development-main`
- Function App: `func-dev-api-staywell`
- Function RG: `rg-dev-rentoom-booking`

`prod`
- identity RG: `rg-prod-rentoom-github-identity`
- identity: `uami-prod-gha-func-api-staywell`
- branch: `main`
- Function App: `func-prod-api-staywell`
- Function RG: `rg-prod-rentoom-booking`

## GitHub owner slug

Parametr `githubOrganizationSlug` musi byc slugiem z URL repo, nie nazwa wyswietlana organizacji.

W tym repo lokalny `origin` wskazuje na:

```text
https://github.com/RentoomApartementy/RentoomBookingEcosystem
```

Dlatego w plikach parametrow ustawiony jest:

```text
githubOrganizationSlug=RentoomApartementy
```

Jesli slug w GitHub jest inny niz lokalny `origin`, popraw ten parametr przed deploymentem.

## Uruchomienie

Z katalogu `infra/bootstrap-identity-bicep`:

```powershell
az login
az account set --subscription "<SUBSCRIPTION_ID_OR_NAME>"

.\deploy.ps1 -Environment dev -Operation validate
.\deploy.ps1 -Environment dev -Operation create

.\deploy.ps1 -Environment prod -Operation validate
.\deploy.ps1 -Environment prod -Operation create
```

## Po deploymentcie

Z outputow deploymentu wez:

- `managedIdentityClientId`
- `tenantId`

Nastepnie ustaw GitHub Actions Variables:

`dev`
- `AZURE_CLIENT_ID_FUNC_DEV_API_STAYWELL`
- `AZURE_TENANT_ID_DEV`
- `AZURE_SUBSCRIPTION_ID_DEV`

`prod`
- `AZURE_CLIENT_ID_FUNC_PROD_API_STAYWELL`
- `AZURE_TENANT_ID_PROD`
- `AZURE_SUBSCRIPTION_ID_PROD`

Sa one uzywane przez workflowy:

- `C:\Users\macie\source\repos\RentoomApartementy\RentoomBookingEcosystem\.github\workflows\development-main_func-dev-api-staywell.yml`
- `C:\Users\macie\source\repos\RentoomApartementy\RentoomBookingEcosystem\.github\workflows\main_func-prod-api-staywell.yml`

## Uwagi

- Ten bootstrap zaklada workflowy branch-based bez `environment:` w GitHub Actions.
- Jesli pozniej dodasz `environment:` do workflowu, obecny subject federacji przestanie pasowac i trzeba bedzie zmienic model federated credentials.
- Rola `Website Contributor` jest nadawana tylko na konkretnej Function App, nie na cala Resource Group.
