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

@description('PostgreSQL subscription ID.')
param postgresSubscriptionId string

@description('PostgreSQL resource group name.')
param postgresResourceGroupName string

@description('Existing PostgreSQL Flexible Server name.')
param postgresServerName string

@description('Existing database name on PostgreSQL server.')
param staywellDbName string

@description('Application database user name.')
param staywellDbAppUser string

@secure()
@description('Application database password.')
param staywellDbAppPassword string

@description('Common tags.')
param tags object

var deploymentStorageContainerName = 'function-releases'

// Built-in role IDs
var storageBlobDataOwnerRoleId = 'b7e6dc6d-f1e8-4753-8033-0f276bb0955b'
var storageQueueDataContributorRoleId = '974c5e8b-45b9-4653-ba55-5f855dd0fb88'
var storageAccountContributorRoleId = '17d1049b-9a84-46fb-8f53-869881c3d3ab'

resource existingPostgres 'Microsoft.DBforPostgreSQL/flexibleServers@2022-12-01' existing = {
  name: postgresServerName
  scope: resourceGroup(postgresSubscriptionId, postgresResourceGroupName)
}

resource storage 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: storageAccountName
  location: location
  kind: 'StorageV2'
  sku: {
    name: 'Standard_LRS'
  }
  tags: tags
  properties: {
    supportsHttpsTrafficOnly: true
    minimumTlsVersion: 'TLS1_2'
    allowBlobPublicAccess: false
    defaultToOAuthAuthentication: true
    publicNetworkAccess: 'Enabled'
  }
}

resource storageBlobService 'Microsoft.Storage/storageAccounts/blobServices@2024-01-01' = {
  parent: storage
  name: 'default'
  properties: {}
}

resource deploymentContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2024-01-01' = {
  parent: storageBlobService
  name: deploymentStorageContainerName
  properties: {
    publicAccess: 'None'
  }
}

resource webPlan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: webPlanName
  location: location
  kind: 'app'
  sku: {
    name: 'F1'
    tier: 'Free'
    size: 'F1'
    capacity: 1
  }
  tags: tags
  properties: {
    reserved: false
  }
}

resource rentoomWeb 'Microsoft.Web/sites@2023-12-01' = {
  name: rentoomWebAppName
  location: location
  kind: 'app'
  tags: tags
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

resource functionPlan 'Microsoft.Web/serverfarms@2024-04-01' = {
  name: functionPlanName
  location: location
  kind: 'functionapp'
  sku: {
    name: 'FC1'
    tier: 'FlexConsumption'
  }
  tags: tags
  properties: {
    reserved: true
  }
}

resource staywellApi 'Microsoft.Web/sites@2024-04-01' = {
  name: staywellApiFunctionName
  location: location
  kind: 'functionapp,linux'
  tags: tags
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: functionPlan.id
    httpsOnly: true
    siteConfig: {
      minTlsVersion: '1.2'
    }
    functionAppConfig: {
      runtime: {
        name: 'dotnet-isolated'
        version: '8.0'
      }
      deployment: {
        storage: {
          type: 'blobContainer'
          value: 'https://${storage.name}.blob.core.windows.net/${deploymentStorageContainerName}'
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

resource staywellApiAppSettings 'Microsoft.Web/sites/config@2024-04-01' = {
  parent: staywellApi
  name: 'appsettings'
  properties: {
    ASPNETCORE_ENVIRONMENT: 'Development'
    FUNCTIONS_WORKER_RUNTIME: 'dotnet-isolated'
    FUNCTIONS_EXTENSION_VERSION: '~4'

    // Azure Functions host storage via managed identity
    AzureWebJobsStorage__accountName: storage.name
    AzureWebJobsStorage__credential: 'managedidentity'

    // PostgreSQL connection settings
    StaywellDb__Host: '${existingPostgres.name}.postgres.database.azure.com'
    StaywellDb__Port: '5432'
    StaywellDb__Database: staywellDbName
    StaywellDb__Username: staywellDbAppUser
    StaywellDb__Password: staywellDbAppPassword
    StaywellDb__SslMode: 'Require'
    StaywellDb__TrustServerCertificate: 'false'
  }
}

resource roleAssignmentStorageBlobDataOwner 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(subscription().id, storage.id, staywellApi.id, 'Storage Blob Data Owner')
  scope: storage
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', storageBlobDataOwnerRoleId)
    principalId: staywellApi.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

resource roleAssignmentStorageQueueDataContributor 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(subscription().id, storage.id, staywellApi.id, 'Storage Queue Data Contributor')
  scope: storage
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', storageQueueDataContributorRoleId)
    principalId: staywellApi.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

resource roleAssignmentStorageAccountContributor 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(subscription().id, storage.id, staywellApi.id, 'Storage Account Contributor')
  scope: storage
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', storageAccountContributorRoleId)
    principalId: staywellApi.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

resource staywellSwa 'Microsoft.Web/staticSites@2023-12-01' = {
  name: staywellStaticWebAppName
  location: location
  tags: tags
  sku: {
    name: 'Standard'
    tier: 'Standard'
  }
  properties: {}
}

resource swaBackendLink 'Microsoft.Web/staticSites/linkedBackends@2023-12-01' = {
  parent: staywellSwa
  name: 'linkedBackend'
  properties: {
    backendResourceId: staywellApi.id
    region: location
  }
}

output rentoomBookingWebUrl string = 'https://${rentoomWeb.properties.defaultHostName}'
output staywellStaticWebUrl string = 'https://${staywellSwa.properties.defaultHostname}'
output staywellApiFunctionsUrl string = 'https://${staywellApi.properties.defaultHostName}/api'
output postgresServerId string = existingPostgres.id
output postgresServerHost string = '${existingPostgres.name}.postgres.database.azure.com'
output staywellDatabaseName string = staywellDbName