targetScope = 'subscription'

@description('Location for all DEV resources.')
param location string = 'westeurope'

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

@description('Common tags.')
param tags object = {
  environment: 'dev'
  system: 'RentoomBookingEcosystem'
  managedBy: 'bicep'
}

var storageAccountName = take(toLower('${storagePrefix}${uniqueString(subscription().id, resourceGroupName)}'), 24)

resource rg 'Microsoft.Resources/resourceGroups@2023-07-01' = {
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
    tags: tags
  }
}

output resourceGroup string = rg.name
output rentoomBookingWebUrl string = appStack.outputs.rentoomBookingWebUrl
output staywellStaticWebUrl string = appStack.outputs.staywellStaticWebUrl
output staywellApiFunctionsUrl string = appStack.outputs.staywellApiFunctionsUrl
