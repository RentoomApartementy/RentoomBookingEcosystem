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