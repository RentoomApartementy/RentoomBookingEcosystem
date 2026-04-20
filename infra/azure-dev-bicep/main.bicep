targetScope = 'subscription'

@allowed([
  'dev'
  'prod'
])
@description('Deployment environment.')
param environment string = 'dev'

@description('Log Analytics workspace name for monitoring.')
param logAnalyticsWorkspaceName string = 'log-dev-rentoombooking'

@description('Application Insights name for StayWell API.')
param staywellApiAppInsightsName string = 'app-insights-dev-api-staywell'

@description('Application Insights name for Rentoom Booking Web.')
param rentoomWebAppInsightsName string = 'app-insights-dev-rentoombooking'

@description('Location for all DEV resources.')
param location string = 'polandcentral'

@description('Resource group for DEV environment.')
param resourceGroupName string = 'rg-dev-rentoom-booking'

@description('App Service name for dev Rentoom Booking Web.')
param rentoomWebAppName string = 'app-dev-rentoombooking'

@description('Static Web App name for dev StayWell.')
param staywellStaticWebAppName string = 'swa-dev-staywell'

@description('Function App name for dev API Staywell.')
param staywellApiFunctionName string = 'func-dev-api-staywell'

@description('App Service plan name for Rentoom Booking Web.')
param webPlanName string = 'asp-dev-rentoombooking'

@description('SKU configuration for the Rentoom Booking Web App Service plan.')
param webPlanSku object = {
  name: 'F1'
  tier: 'Free'
  size: 'F1'
  capacity: 1
}

@description('Flex Consumption plan name for Function App.')
param functionPlanName string = 'asp-dev-api-staywell-fc1'

@description('Storage account prefix (3-18 chars, lowercase + numbers). A unique suffix is appended automatically.')
@minLength(3)
@maxLength(18)
param storagePrefix string = 'devstayapi'

@description('PostgreSQL subscription ID.')
param postgresSubscriptionId string

@description('PostgreSQL resource group name.')
param postgresResourceGroupName string

@description('Existing PostgreSQL Flexible Server name.')
param postgresServerName string

@description('Existing database name on PostgreSQL server.')
param staywellDbName string = 'staywell_dev'

@description('Application database user name.')
param staywellDbAppUser string = 'staywell_dev_app'

@description('Storage account name for Rentoom Booking data.')
param rentoomDataStorageAccountName string = 'storagerentoombookingdev'

@secure()
@description('Application database password.')
param staywellDbAppPassword string

@description('Tpay API base URL.')
param tpayApiBaseUrl string = 'https://openapi.sandbox.tpay.com'

@description('Tpay client ID.')
param tpayClientId string = '01KEA06BFSBX1DZQM0BGYV8SXK-01KEA0CVMFDY6A36CKRTCGBTGB'

@secure()
@description('Tpay client secret.')
param tpayClientSecret string

@secure()
@description('Tpay merchant security code.')
param tpayMerchantSecurityCode string

@description('Expected Tpay JWS certificate prefix.')
param tpayJwsCertPrefix string = 'https://secure.sandbox.tpay.com'

@description('Tpay root CA PEM URL.')
param tpayRootCaPemUrl string = 'https://secure.sandbox.tpay.com/x509/tpay-jws-root.pem'

@description('Success URL setting for Tpay used by Rentoom Booking Web.')
param tpayWebSuccessUrl string = 'rezerwuj/{ReservationTokenGuid}/oplac/success'

@description('Error URL setting for Tpay used by Rentoom Booking Web.')
param tpayWebErrorUrl string = 'https://dev.rentoom.pl/rezerwuj/{ReservationTokenGuid}/oplac/error'

@description('Rentoom site base URL used by Tpay in Rentoom Booking Web.')
param tpayWebRentoomSiteBaseUrl string = 'https://dev.rentoom.pl'

@description('Success URL path used by Tpay in StayWell API.')
param tpayApiSuccessUrl string = 'reservation/{Token}/Vouchers/payment/{UpsellOrderGuid}/Success'

@description('Error URL path used by Tpay in StayWell API.')
param tpayApiErrorUrl string = 'reservation/{Token}/Vouchers/payment/{UpsellOrderGuid}/Error'

@description('Rentoom site base URL used by Tpay in StayWell API.')
param tpayApiRentoomSiteBaseUrl string = 'https://dev.rentoom.pl'

@description('Whether the StayWell API should use dummy IdoBooking behavior.')
param idoBookingUseDummy bool = true

@description('Template key used for dummy IdoBooking reservations.')
param idoBookingDummyReservationTemplateKey string = 'dummy_api_call_template'

@description('IdoBooking API base URL.')
param idoBookingBaseApiUrl string = 'https://client7953.idosell.com/api/'

@description('IdoBooking API system user.')
param idoBookingApiUser string = 'apimaster'

@secure()
@description('IdoBooking API password.')
param idoBookingApiPassword string

@description('Bitrix reservation pipeline name used by Rentoom Booking Web and StayWell API.')
param bitrixReservationPipelineName string = 'Rezerwacje'

@description('Public base URL for StayWell (Static Web App custom domain).')
param staywellBaseUrl string = 'https://dev.staywell.rentoom.pl'

@description('Public base URL for Rentoom Booking Web (Web App custom domain).')
param rentoomWebBaseUrl string = 'https://dev.rentoom.pl'

@description('Public base URL for StayWell API (Function App custom domain).')
param staywellApiBaseUrl string = 'https://dev.api.rentoom.pl'

@description('Cron expression for full apartment sync from IDB executed by StayWell API.')
param cronSyncAllApartmentsFromIdb string = '0 0 */2 * * *'

@description('Function App paths that must bypass App Service Authentication.')
param staywellApiAuthExcludedPaths array = [
  '/api/mail/incoming'
  '/api/tpay/notification'
  '/api/events/query'
]

@description('GitHub organization slug for StayWell Static Web App source repository.')
param staywellGithubOrganization string = 'RentoomApartementy'

@description('GitHub repository for StayWell Static Web App source code.')
param staywellGithubRepositoryName string = 'RentoomBookingEcosystem'

@description('GitHub branch used by StayWell Static Web App.')
param staywellGithubBranch string = 'development-main'

@secure()
@description('GitHub repository token used by Azure Static Web Apps to configure GitHub Actions workflow and secrets.')
param staywellGithubRepositoryToken string

@description('Repository path to the StayWell app for Azure Static Web Apps build.')
param staywellGithubAppLocation string = './StayWell'

@description('Build output path for the StayWell Static Web App.')
param staywellGithubOutputLocation string = 'wwwroot'

@description('GitHub Actions secret name override for the StayWell Static Web App workflow.')
param staywellGithubActionSecretName string = 'AZURE_STATIC_WEB_APPS_API_TOKEN_STAYWELL_DEV'

@description('Blob container used for uploaded files.')
param uploadsStorageContainerName string = 'uploadsdev'

@description('Blob container used for arrival instructions.')
param instructionsStorageContainerName string = 'arrivalinstructions-dev'

@description('Rentoom App database name.')
param rentoomAppDbName string = 'rentoomdb'

@description('Rentoom App database user name.')
param rentoomAppDbUser string = 'RentoomAzureDbAdmin'

@secure()
@description('Rentoom App database password.')
param rentoomAppDbPassword string

@description('Desired max_connections value on the shared PostgreSQL Flexible Server.')
@minValue(1)
param postgresMaxConnections int = 429

@description('Whether PostgreSQL connection pooling should be enabled in application connection strings.')
param postgresPoolingEnabled bool = true

@description('Minimum PostgreSQL pool size applied to application connection strings.')
@minValue(0)
param postgresPoolingMinimumPoolSize int = 0

@description('Maximum PostgreSQL pool size for Rentoom Booking Web.')
@minValue(0)
param rentoomWebPostgresMaximumPoolSize int = 4

@description('Maximum PostgreSQL pool size for StayWell API.')
@minValue(0)
param staywellApiPostgresMaximumPoolSize int = 1

@description('Seconds after which idle PostgreSQL connections can be pruned from the pool.')
@minValue(0)
param postgresPoolingConnectionIdleLifetime int = 60

@description('Seconds between PostgreSQL pool pruning scans.')
@minValue(0)
param postgresPoolingConnectionPruningInterval int = 10

@description('Connection timeout in seconds for PostgreSQL.')
@minValue(0)
param postgresPoolingTimeout int = 15

@description('Command timeout in seconds for PostgreSQL.')
@minValue(0)
param postgresPoolingCommandTimeout int = 30

@description('TTLock client ID.')
param ttlockClientId string = 'ba60c2707447415183df5d6a4c617e09'

@secure()
@description('TTLock client secret.')
param ttlockClientSecret string

@description('TTLock account username.')
param ttlockUsername string = '+48601317506'

@secure()
@description('TTLock account password.')
param ttlockPassword string

@description('Common tags.')
param tags object

var storageAccountName = take(
  toLower('${storagePrefix}${uniqueString(subscription().id, resourceGroupName)}'),
  24
)

resource rg 'Microsoft.Resources/resourceGroups@2025-04-01' = {
  name: resourceGroupName
  location: location
  tags: tags
}

module appStack './modules/app-stack.bicep' = {
  name: 'app-stack-${environment}'
  scope: rg
  params: {
    environment: environment
    location: location
    rentoomWebAppName: rentoomWebAppName
    staywellStaticWebAppName: staywellStaticWebAppName
    staywellApiFunctionName: staywellApiFunctionName
    webPlanName: webPlanName
    webPlanSku: webPlanSku
    functionPlanName: functionPlanName
    storageAccountName: storageAccountName
    postgresSubscriptionId: postgresSubscriptionId
    postgresResourceGroupName: postgresResourceGroupName
    postgresServerName: postgresServerName
    staywellDbName: staywellDbName
    staywellDbAppUser: staywellDbAppUser
    staywellDbAppPassword: staywellDbAppPassword
    tpayApiBaseUrl: tpayApiBaseUrl
    tpayClientId: tpayClientId
    tpayClientSecret: tpayClientSecret
    tpayMerchantSecurityCode: tpayMerchantSecurityCode
    tpayJwsCertPrefix: tpayJwsCertPrefix
    tpayRootCaPemUrl: tpayRootCaPemUrl
    tpayWebSuccessUrl: tpayWebSuccessUrl
    tpayWebErrorUrl: tpayWebErrorUrl
    tpayWebRentoomSiteBaseUrl: tpayWebRentoomSiteBaseUrl
    tpayApiSuccessUrl: tpayApiSuccessUrl
    tpayApiErrorUrl: tpayApiErrorUrl
    tpayApiRentoomSiteBaseUrl: tpayApiRentoomSiteBaseUrl
    idoBookingUseDummy: idoBookingUseDummy
    idoBookingDummyReservationTemplateKey: idoBookingDummyReservationTemplateKey
    idoBookingBaseApiUrl: idoBookingBaseApiUrl
    idoBookingApiUser: idoBookingApiUser
    idoBookingApiPassword: idoBookingApiPassword
    bitrixReservationPipelineName: bitrixReservationPipelineName
    staywellBaseUrl: staywellBaseUrl
    rentoomWebBaseUrl: rentoomWebBaseUrl
    staywellApiBaseUrl: staywellApiBaseUrl
    cronSyncAllApartmentsFromIdb: cronSyncAllApartmentsFromIdb
    staywellApiAuthExcludedPaths: staywellApiAuthExcludedPaths
    staywellGithubOrganization: staywellGithubOrganization
    staywellGithubRepositoryName: staywellGithubRepositoryName
    staywellGithubBranch: staywellGithubBranch
    staywellGithubRepositoryToken: staywellGithubRepositoryToken
    staywellGithubAppLocation: staywellGithubAppLocation
    staywellGithubOutputLocation: staywellGithubOutputLocation
    staywellGithubActionSecretName: staywellGithubActionSecretName
    uploadsStorageContainerName: uploadsStorageContainerName
    instructionsStorageContainerName: instructionsStorageContainerName
    rentoomAppDbName: rentoomAppDbName
    rentoomAppDbUser: rentoomAppDbUser
    rentoomAppDbPassword: rentoomAppDbPassword
    postgresPoolingEnabled: postgresPoolingEnabled
    postgresPoolingMinimumPoolSize: postgresPoolingMinimumPoolSize
    rentoomWebPostgresMaximumPoolSize: rentoomWebPostgresMaximumPoolSize
    staywellApiPostgresMaximumPoolSize: staywellApiPostgresMaximumPoolSize
    postgresPoolingConnectionIdleLifetime: postgresPoolingConnectionIdleLifetime
    postgresPoolingConnectionPruningInterval: postgresPoolingConnectionPruningInterval
    postgresPoolingTimeout: postgresPoolingTimeout
    postgresPoolingCommandTimeout: postgresPoolingCommandTimeout
    ttlockClientId: ttlockClientId
    ttlockClientSecret: ttlockClientSecret
    ttlockUsername: ttlockUsername
    ttlockPassword: ttlockPassword
    rentoomDataStorageAccountName: rentoomDataStorageAccountName
    logAnalyticsWorkspaceName: logAnalyticsWorkspaceName
    staywellApiAppInsightsName: staywellApiAppInsightsName
    rentoomWebAppInsightsName: rentoomWebAppInsightsName
    tags: tags
  }
}

module postgresConfig './modules/postgres-config.bicep' = {
  name: 'postgres-config-${environment}'
  scope: resourceGroup(postgresSubscriptionId, postgresResourceGroupName)
  params: {
    postgresServerName: postgresServerName
    postgresMaxConnections: postgresMaxConnections
  }
}

output resourceGroup string = rg.name
output rentoomBookingWebUrl string = appStack.outputs.rentoomBookingWebUrl
output staywellStaticWebUrl string = appStack.outputs.staywellStaticWebUrl
output staywellApiFunctionsUrl string = appStack.outputs.staywellApiFunctionsUrl
output postgresServerId string = appStack.outputs.postgresServerId
output postgresServerHost string = appStack.outputs.postgresServerHost
output staywellDatabaseName string = appStack.outputs.staywellDatabaseName
output rentoomDataStorageAccountName string = appStack.outputs.rentoomDataStorageAccountName
output rentoomDataStoragePrimaryBlobEndpoint string = appStack.outputs.rentoomDataStoragePrimaryBlobEndpoint
