targetScope = 'subscription'

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

@description('App Service plan name for Rentoom Booking Web (F1).')
param webPlanName string = 'asp-dev-rentoombooking-f1'

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

@description('Relative success URL path used by Tpay.')
param tpaySuccessUrl string = 'reservation/{Token}/Vouchers/payment/{UpsellOrderGuid}/Success'

@description('Relative error URL path used by Tpay.')
param tpayErrorUrl string = 'reservation/{Token}/Vouchers/payment/{UpsellOrderGuid}/Error'

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

@description('Public base URL for StayWell (Static Web App custom domain).')
param staywellBaseUrl string = 'https://dev.staywell.rentoom.pl'

@description('Public base URL for Rentoom Booking Web (Web App custom domain).')
param rentoomWebBaseUrl string = 'https://dev.rentoom.pl'

@description('Public base URL for StayWell API (Function App custom domain).')
param staywellApiBaseUrl string = 'https://dev.api.rentoom.pl'

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
param tags object = {
  environment: 'dev'
  system: 'RentoomBookingEcosystem'
  managedBy: 'bicep'
}

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
  name: 'app-stack-dev'
  scope: rg
  params: {
    location: location
    rentoomWebAppName: rentoomWebAppName
    staywellStaticWebAppName: staywellStaticWebAppName
    staywellApiFunctionName: staywellApiFunctionName
    webPlanName: webPlanName
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
    tpaySuccessUrl: tpaySuccessUrl
    tpayErrorUrl: tpayErrorUrl
    idoBookingUseDummy: idoBookingUseDummy
    idoBookingDummyReservationTemplateKey: idoBookingDummyReservationTemplateKey
    idoBookingBaseApiUrl: idoBookingBaseApiUrl
    idoBookingApiUser: idoBookingApiUser
    idoBookingApiPassword: idoBookingApiPassword
    staywellBaseUrl: staywellBaseUrl
    rentoomWebBaseUrl: rentoomWebBaseUrl
    staywellApiBaseUrl: staywellApiBaseUrl
    uploadsStorageContainerName: uploadsStorageContainerName
    instructionsStorageContainerName: instructionsStorageContainerName
    rentoomAppDbName: rentoomAppDbName
    rentoomAppDbUser: rentoomAppDbUser
    rentoomAppDbPassword: rentoomAppDbPassword
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

output resourceGroup string = rg.name
output rentoomBookingWebUrl string = appStack.outputs.rentoomBookingWebUrl
output staywellStaticWebUrl string = appStack.outputs.staywellStaticWebUrl
output staywellApiFunctionsUrl string = appStack.outputs.staywellApiFunctionsUrl
output postgresServerId string = appStack.outputs.postgresServerId
output postgresServerHost string = appStack.outputs.postgresServerHost
output staywellDatabaseName string = appStack.outputs.staywellDatabaseName
output rentoomDataStorageAccountName string = appStack.outputs.rentoomDataStorageAccountName
output rentoomDataStoragePrimaryBlobEndpoint string = appStack.outputs.rentoomDataStoragePrimaryBlobEndpoint
