@description('Location for resources.')
param location string

@description('App Service name for dev Rentoom Booking Web.')
param rentoomWebAppName string

@description('Static Web App name for dev StayWell.')
param staywellStaticWebAppName string

@description('Function App name for dev API Staywell.')
param staywellApiFunctionName string

@description('App Service plan name for Rentoom Booking Web (F1).')
param webPlanName string

@description('Flex Consumption plan name for Function App.')
param functionPlanName string

@description('Storage account name for Function App runtime.')
param storageAccountName string

@description('Common tags.')
param tags object

resource storage 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: storageAccountName
  location: location
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  tags: tags
  properties: {
    minimumTlsVersion: 'TLS1_2'
    allowBlobPublicAccess: false
    supportsHttpsTrafficOnly: true
  }
}

resource webPlan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: webPlanName
  location: location
  sku: {
    name: 'F1'
    tier: 'Free'
    size: 'F1'
    capacity: 1
  }
  kind: 'app'
  tags: tags
  properties: {
    reserved: false
  }
}

resource rentoomWeb 'Microsoft.Web/sites@2023-12-01' = {
  name: rentoomWebAppName
  location: location
  tags: tags
  kind: 'app'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: webPlan.id
    httpsOnly: true
    siteConfig: {
      netFrameworkVersion: 'v8.0'
      alwaysOn: false
      appSettings: [
        {
          name: 'ASPNETCORE_ENVIRONMENT'
          value: 'Development'
        }
        {
          name: 'WEBSITE_RUN_FROM_PACKAGE'
          value: '1'
        }
      ]
    }
  }
}

resource functionPlan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: functionPlanName
  location: location
  sku: {
    name: 'FC1'
    tier: 'FlexConsumption'
  }
  kind: 'functionapp'
  tags: tags
  properties: {
    reserved: true
  }
}

resource staywellApi 'Microsoft.Web/sites@2023-12-01' = {
  name: staywellApiFunctionName
  location: location
  tags: tags
  kind: 'functionapp,linux'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: functionPlan.id
    httpsOnly: true
    keyVaultReferenceIdentity: 'SystemAssigned'
    siteConfig: {
      linuxFxVersion: 'DOTNET-ISOLATED|8.0'
      alwaysOn: false
      appSettings: [
        {
          name: 'FUNCTIONS_WORKER_RUNTIME'
          value: 'dotnet-isolated'
        }
        {
          name: 'FUNCTIONS_EXTENSION_VERSION'
          value: '~4'
        }
        {
          name: 'AzureWebJobsStorage__accountName'
          value: storage.name
        }
      ]
    }
    functionAppConfig: {
      runtime: {
        name: 'dotnet-isolated'
        version: '8.0'
      }
      deployment: {
        storage: {
          type: 'blobContainer'
          value: 'https://${storage.name}.blob.core.windows.net/function-releases'
          authentication: {
            type: 'SystemAssignedIdentity'
          }
        }
      }
      scaleAndConcurrency: {
        maximumInstanceCount: 20
        instanceMemoryMB: 2048
      }
    }
  }
}

resource staywellSwa 'Microsoft.Web/staticSites@2023-12-01' = {
  name: staywellStaticWebAppName
  location: location
  tags: tags
  sku: {
    name: 'Free'
    tier: 'Free'
  }
  properties: {}
}

resource swaBackendLink 'Microsoft.Web/staticSites/linkedBackends@2023-12-01' = {
  name: 'staywell-api-backend'
  parent: staywellSwa
  properties: {
    backendResourceId: staywellApi.id
    region: location
  }
}

output rentoomBookingWebUrl string = 'https://${rentoomWeb.properties.defaultHostName}'
output staywellStaticWebUrl string = 'https://${staywellSwa.properties.defaultHostname}'
output staywellApiFunctionsUrl string = 'https://${staywellApi.properties.defaultHostName}/api'
