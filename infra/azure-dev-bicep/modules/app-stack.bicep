@description('Log Analytics workspace name for monitoring.')
param logAnalyticsWorkspaceName string

@description('Application Insights name for StayWell API.')
param staywellApiAppInsightsName string

@description('Application Insights name for Rentoom Booking Web.')
param rentoomWebAppInsightsName string

@description('Storage account name for Rentoom Booking data.')
param rentoomDataStorageAccountName string

@allowed([
  'dev'
  'prod'
])
@description('Deployment environment.')
param environment string

@description('Location for resources.')
param location string

@description('App Service name for dev Rentoom Booking Web.')
param rentoomWebAppName string

@description('Static Web App name for dev StayWell.')
param staywellStaticWebAppName string

@description('Function App name for dev API Staywell.')
param staywellApiFunctionName string

@description('App Service plan name for Rentoom Booking Web.')
param webPlanName string

@description('SKU configuration for the Rentoom Booking Web App Service plan.')
param webPlanSku object

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

@description('Tpay API base URL.')
param tpayApiBaseUrl string

@description('Tpay client ID.')
param tpayClientId string

@secure()
@description('Tpay client secret.')
param tpayClientSecret string

@secure()
@description('Tpay merchant security code.')
param tpayMerchantSecurityCode string

@description('Expected Tpay JWS certificate prefix.')
param tpayJwsCertPrefix string

@description('Tpay root CA PEM URL.')
param tpayRootCaPemUrl string

@description('Success URL setting for Tpay used by Rentoom Booking Web.')
param tpayWebSuccessUrl string

@description('Error URL setting for Tpay used by Rentoom Booking Web.')
param tpayWebErrorUrl string

@description('Rentoom site base URL used by Tpay in Rentoom Booking Web.')
param tpayWebRentoomSiteBaseUrl string

@description('Success URL path used by Tpay in StayWell API.')
param tpayApiSuccessUrl string

@description('Error URL path used by Tpay in StayWell API.')
param tpayApiErrorUrl string

@description('Rentoom site base URL used by Tpay in StayWell API.')
param tpayApiRentoomSiteBaseUrl string

@description('Whether the StayWell API should use dummy IdoBooking behavior.')
param idoBookingUseDummy bool

@description('Template key used for dummy IdoBooking reservations.')
param idoBookingDummyReservationTemplateKey string

@description('IdoBooking API base URL.')
param idoBookingBaseApiUrl string

@description('IdoBooking API system user.')
param idoBookingApiUser string

@secure()
@description('IdoBooking API password.')
param idoBookingApiPassword string

@description('Bitrix reservation pipeline name used by Rentoom Booking Web and StayWell API.')
param bitrixReservationPipelineName string

@description('Public base URL for StayWell (Static Web App custom domain).')
param staywellBaseUrl string

@description('Public base URL for Rentoom Booking Web (Web App custom domain).')
param rentoomWebBaseUrl string

@description('Public base URL for StayWell API (Function App custom domain).')
param staywellApiBaseUrl string

@description('Function App paths that must bypass App Service Authentication.')
param staywellApiAuthExcludedPaths array

@description('GitHub organization for StayWell Static Web App source repository.')
param staywellGithubOrganization string

@description('GitHub repository for StayWell Static Web App source code.')
param staywellGithubRepositoryName string

@description('GitHub branch used by StayWell Static Web App.')
param staywellGithubBranch string

@secure()
@description('GitHub repository token used by Azure Static Web Apps to configure GitHub Actions workflow and secrets.')
param staywellGithubRepositoryToken string

@description('Repository path to the StayWell app for Azure Static Web Apps build.')
param staywellGithubAppLocation string

@description('Build output path for the StayWell Static Web App.')
param staywellGithubOutputLocation string

@description('GitHub Actions secret name override for the StayWell Static Web App workflow.')
param staywellGithubActionSecretName string

@description('Blob container used for uploaded files.')
param uploadsStorageContainerName string

@description('Blob container used for arrival instructions.')
param instructionsStorageContainerName string

@description('Rentoom App database name.')
param rentoomAppDbName string

@description('Rentoom App database user name.')
param rentoomAppDbUser string

@secure()
@description('Rentoom App database password.')
param rentoomAppDbPassword string

@description('Whether PostgreSQL connection pooling should be enabled in application connection strings.')
param postgresPoolingEnabled bool

@description('Minimum PostgreSQL pool size applied to application connection strings.')
@minValue(0)
param postgresPoolingMinimumPoolSize int

@description('Maximum PostgreSQL pool size for Rentoom Booking Web.')
@minValue(0)
param rentoomWebPostgresMaximumPoolSize int

@description('Maximum PostgreSQL pool size for StayWell API.')
@minValue(0)
param staywellApiPostgresMaximumPoolSize int

@description('Seconds after which idle PostgreSQL connections can be pruned from the pool.')
@minValue(0)
param postgresPoolingConnectionIdleLifetime int

@description('Seconds between PostgreSQL pool pruning scans.')
@minValue(0)
param postgresPoolingConnectionPruningInterval int

@description('Connection timeout in seconds for PostgreSQL.')
@minValue(0)
param postgresPoolingTimeout int

@description('Command timeout in seconds for PostgreSQL.')
@minValue(0)
param postgresPoolingCommandTimeout int

@description('TTLock client ID.')
param ttlockClientId string

@secure()
@description('TTLock client secret.')
param ttlockClientSecret string

@description('TTLock account username.')
param ttlockUsername string

@secure()
@description('TTLock account password.')
param ttlockPassword string

@description('Common tags.')
param tags object

var deploymentStorageContainerName = 'function-releases'
var normalizedStaywellBaseUrl = endsWith(staywellBaseUrl, '/') ? substring(staywellBaseUrl, 0, length(staywellBaseUrl) - 1) : staywellBaseUrl
var normalizedRentoomWebBaseUrl = endsWith(rentoomWebBaseUrl, '/') ? substring(rentoomWebBaseUrl, 0, length(rentoomWebBaseUrl) - 1) : rentoomWebBaseUrl
var normalizedStaywellApiCustomBaseUrl = endsWith(staywellApiBaseUrl, '/') ? substring(staywellApiBaseUrl, 0, length(staywellApiBaseUrl) - 1) : staywellApiBaseUrl
var staywellGithubRepositoryUrl = 'https://github.com/${staywellGithubOrganization}/${staywellGithubRepositoryName}'
var normalizedTpayWebRentoomSiteBaseUrl = endsWith(tpayWebRentoomSiteBaseUrl, '/') ? substring(tpayWebRentoomSiteBaseUrl, 0, length(tpayWebRentoomSiteBaseUrl) - 1) : tpayWebRentoomSiteBaseUrl
var normalizedTpayApiRentoomSiteBaseUrl = endsWith(tpayApiRentoomSiteBaseUrl, '/') ? substring(tpayApiRentoomSiteBaseUrl, 0, length(tpayApiRentoomSiteBaseUrl) - 1) : tpayApiRentoomSiteBaseUrl
var staywellReservationUrlBase = '${normalizedStaywellBaseUrl}/reservation/{resToken}/HomePage'
var staywellUrlBase = normalizedStaywellBaseUrl
var tpayNotificationUrl = '${normalizedStaywellApiCustomBaseUrl}/api/tpay/notification'
var applicationRuntimeEnvironment = environment == 'prod' ? 'Production' : 'Development'
var rentoomWebIsLinux = environment == 'prod'
var idoBookingUseDummySetting = idoBookingUseDummy ? 'True' : 'False'
var postgresPoolingEnabledSetting = postgresPoolingEnabled ? 'true' : 'false'
var staywellDbConnectionString = 'Server=${existingPostgres.name}.postgres.database.azure.com;Database=${staywellDbName};Port=5432;User Id=${staywellDbAppUser};Password=${staywellDbAppPassword};Ssl Mode=VerifyFull;Include Error Detail=True'
var rentoomAppDbConnectionString = 'Server=${existingPostgres.name}.postgres.database.azure.com;Database=${rentoomAppDbName};Port=5432;User Id=${rentoomAppDbUser};Password=${rentoomAppDbPassword};Ssl Mode=VerifyFull;Include Error Detail=True'

// Built-in role IDs
var storageBlobDataOwnerRoleId = 'b7e6dc6d-f1e8-4753-8033-0f276bb0955b'
var storageQueueDataContributorRoleId = '974c5e8b-45b9-4653-ba55-5f855dd0fb88'
var storageAccountContributorRoleId = '17d1049b-9a84-46fb-8f53-869881c3d3ab'


//app Insights and log Analytics

resource logAnalyticsWorkspace 'Microsoft.OperationalInsights/workspaces@2025-07-01' = {
  name: logAnalyticsWorkspaceName
  location: location
  tags: tags
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30
  }
}

resource staywellApiAppInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: staywellApiAppInsightsName
  location: location
  tags: tags
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logAnalyticsWorkspace.id
  }
}

resource rentoomWebAppInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: rentoomWebAppInsightsName
  location: location
  tags: tags
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logAnalyticsWorkspace.id
  }
}


//postgres from rentoomApp subscription

resource existingPostgres 'Microsoft.DBforPostgreSQL/flexibleServers@2025-08-01' existing = {
  name: postgresServerName
  scope: resourceGroup(postgresSubscriptionId, postgresResourceGroupName)
}

//resources for staywell API and rentoom booking web app

resource storage 'Microsoft.Storage/storageAccounts@2025-06-01' = {
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


resource storageBlobService 'Microsoft.Storage/storageAccounts/blobServices@2025-06-01' = {
  parent: storage
  name: 'default'
  properties: {}
}

resource deploymentContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2025-06-01' = {
  parent: storageBlobService
  name: deploymentStorageContainerName
  properties: {
    publicAccess: 'None'
  }
}

resource rentoomDataStorage 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: rentoomDataStorageAccountName
  location: location
  kind: 'StorageV2'
  sku: {
    name: 'Standard_LRS'
  }
  tags: union(tags, {
    purpose: 'rentoombooking-files'
  })
  properties: {
    supportsHttpsTrafficOnly: true
    minimumTlsVersion: 'TLS1_2'
    allowBlobPublicAccess: false
    publicNetworkAccess: 'Enabled'
  }
}



resource webPlan 'Microsoft.Web/serverfarms@2025-03-01' = {
  name: webPlanName
  location: location
  kind: rentoomWebIsLinux ? 'linux' : 'app'
  sku: {
    name: webPlanSku.name
    tier: webPlanSku.tier
    size: webPlanSku.size
    capacity: webPlanSku.capacity
  }
  tags: tags
  properties: {
    reserved: rentoomWebIsLinux
  }
}

resource rentoomWeb 'Microsoft.Web/sites@2025-03-01' = {
  name: rentoomWebAppName
  location: location
  kind: rentoomWebIsLinux ? 'app,linux' : 'app'
  tags: tags
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: webPlan.id
    httpsOnly: true
    siteConfig: rentoomWebIsLinux ? {
      linuxFxVersion: 'DOTNETCORE|8.0'
      alwaysOn: false
      appSettings: [
        {
          name: 'ASPNETCORE_ENVIRONMENT'
          value: applicationRuntimeEnvironment
        }
        {
          name: 'WEBSITE_RUN_FROM_PACKAGE'
          value: '1'
        }
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: rentoomWebAppInsights.properties.ConnectionString
        }
        {
          name: 'ConnectionStrings__POSTGRES_RENTOOM_BOOKING_DB_LOCAL'
          value: staywellDbConnectionString
        }
        {
          name: 'ConnectionStrings__RentoomDbConnectionString'
          value: rentoomAppDbConnectionString
        }
        {
          name: 'PostgresPooling__Enabled'
          value: postgresPoolingEnabledSetting
        }
        {
          name: 'PostgresPooling__MinimumPoolSize'
          value: string(postgresPoolingMinimumPoolSize)
        }
        {
          name: 'PostgresPooling__MaximumPoolSize'
          value: string(rentoomWebPostgresMaximumPoolSize)
        }
        {
          name: 'PostgresPooling__ConnectionIdleLifetime'
          value: string(postgresPoolingConnectionIdleLifetime)
        }
        {
          name: 'PostgresPooling__ConnectionPruningInterval'
          value: string(postgresPoolingConnectionPruningInterval)
        }
        {
          name: 'PostgresPooling__Timeout'
          value: string(postgresPoolingTimeout)
        }
        {
          name: 'PostgresPooling__CommandTimeout'
          value: string(postgresPoolingCommandTimeout)
        }
        {
          name: 'Tpay__ApiBaseUrl'
          value: tpayApiBaseUrl
        }
        {
          name: 'Tpay__ClientId'
          value: tpayClientId
        }
        {
          name: 'Tpay__ClientSecret'
          value: tpayClientSecret
        }
        {
          name: 'Tpay__MerchantSecurityCode'
          value: tpayMerchantSecurityCode
        }
        {
          name: 'Tpay__JwsCertPrefix'
          value: tpayJwsCertPrefix
        }
        {
          name: 'Tpay__RootCaPemUrl'
          value: tpayRootCaPemUrl
        }
        {
          name: 'Tpay__NotificationUrl'
          value: tpayNotificationUrl
        }
        {
          name: 'Tpay__RentoomSiteBaseUrl'
          value: normalizedTpayWebRentoomSiteBaseUrl
        }
        {
          name: 'Tpay__SuccessUrl'
          value: tpayWebSuccessUrl
        }
        {
          name: 'Tpay__ErrorUrl'
          value: tpayWebErrorUrl
        }
        {
          name: 'IdoBooking__UseDummy'
          value: idoBookingUseDummySetting
        }
        {
          name: 'IdoBooking__DummyReservationTemplateKey'
          value: idoBookingDummyReservationTemplateKey
        }
        {
          name: 'IDOBOOKING_BASE_API_URL'
          value: idoBookingBaseApiUrl
        }
        {
          name: 'IDOBOOKING_API_USER'
          value: idoBookingApiUser
        }
        {
          name: 'IDOBOOKING_API_PWD'
          value: idoBookingApiPassword
        }
        {
          name: 'StayWellUrlBase'
          value: staywellUrlBase
        }
        {
          name: 'StayWellReservationUrlBase'
          value: staywellReservationUrlBase
        }
        {
          name: 'Bitrix__ReservationPipelineName'
          value: bitrixReservationPipelineName
        }
        {
          name: 'Storage__Container'
          value: uploadsStorageContainerName
        }
        {
          name: 'Storage__ConnectionString'
          value: ''
        }
        {
          name: 'Storage__AccountName'
          value: rentoomDataStorage.name
        }
      ]
    } : {
      netFrameworkVersion: 'v8.0'
      alwaysOn: false
      appSettings: [
        {
          name: 'ASPNETCORE_ENVIRONMENT'
          value: applicationRuntimeEnvironment
        }
        {
          name: 'WEBSITE_RUN_FROM_PACKAGE'
          value: '1'
        }
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: rentoomWebAppInsights.properties.ConnectionString
        }
        {
          name: 'ConnectionStrings__POSTGRES_RENTOOM_BOOKING_DB_LOCAL'
          value: staywellDbConnectionString
        }
        {
          name: 'ConnectionStrings__RentoomDbConnectionString'
          value: rentoomAppDbConnectionString
        }
        {
          name: 'PostgresPooling__Enabled'
          value: postgresPoolingEnabledSetting
        }
        {
          name: 'PostgresPooling__MinimumPoolSize'
          value: string(postgresPoolingMinimumPoolSize)
        }
        {
          name: 'PostgresPooling__MaximumPoolSize'
          value: string(rentoomWebPostgresMaximumPoolSize)
        }
        {
          name: 'PostgresPooling__ConnectionIdleLifetime'
          value: string(postgresPoolingConnectionIdleLifetime)
        }
        {
          name: 'PostgresPooling__ConnectionPruningInterval'
          value: string(postgresPoolingConnectionPruningInterval)
        }
        {
          name: 'PostgresPooling__Timeout'
          value: string(postgresPoolingTimeout)
        }
        {
          name: 'PostgresPooling__CommandTimeout'
          value: string(postgresPoolingCommandTimeout)
        }
        {
          name: 'Tpay__ApiBaseUrl'
          value: tpayApiBaseUrl
        }
        {
          name: 'Tpay__ClientId'
          value: tpayClientId
        }
        {
          name: 'Tpay__ClientSecret'
          value: tpayClientSecret
        }
        {
          name: 'Tpay__MerchantSecurityCode'
          value: tpayMerchantSecurityCode
        }
        {
          name: 'Tpay__JwsCertPrefix'
          value: tpayJwsCertPrefix
        }
        {
          name: 'Tpay__RootCaPemUrl'
          value: tpayRootCaPemUrl
        }
        {
          name: 'Tpay__NotificationUrl'
          value: tpayNotificationUrl
        }
        {
          name: 'Tpay__RentoomSiteBaseUrl'
          value: normalizedTpayWebRentoomSiteBaseUrl
        }
        {
          name: 'Tpay__SuccessUrl'
          value: tpayWebSuccessUrl
        }
        {
          name: 'Tpay__ErrorUrl'
          value: tpayWebErrorUrl
        }
        {
          name: 'IdoBooking__UseDummy'
          value: idoBookingUseDummySetting
        }
        {
          name: 'IdoBooking__DummyReservationTemplateKey'
          value: idoBookingDummyReservationTemplateKey
        }
        {
          name: 'IDOBOOKING_BASE_API_URL'
          value: idoBookingBaseApiUrl
        }
        {
          name: 'IDOBOOKING_API_USER'
          value: idoBookingApiUser
        }
        {
          name: 'IDOBOOKING_API_PWD'
          value: idoBookingApiPassword
        }
        {
          name: 'StayWellUrlBase'
          value: staywellUrlBase
        }
        {
          name: 'StayWellReservationUrlBase'
          value: staywellReservationUrlBase
        }
        {
          name: 'Bitrix__ReservationPipelineName'
          value: bitrixReservationPipelineName
        }
        {
          name: 'Storage__Container'
          value: uploadsStorageContainerName
        }
        {
          name: 'Storage__ConnectionString'
          value: ''
        }
        {
          name: 'Storage__AccountName'
          value: rentoomDataStorage.name
        }
      ]
    }
  }
}

resource functionPlan 'Microsoft.Web/serverfarms@2025-03-01' = {
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

resource staywellApi 'Microsoft.Web/sites@2025-03-01' = {
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
    AZURE_FUNCTIONS_ENVIRONMENT: applicationRuntimeEnvironment

    // Azure Functions host storage via managed identity
    AzureWebJobsStorage__accountName: storage.name
    AzureWebJobsStorage__credential: 'managedidentity'

    // Application Insights
    APPLICATIONINSIGHTS_CONNECTION_STRING: staywellApiAppInsights.properties.ConnectionString

    // Tpay configuration shared by the Functions API and RentoomBookingWeb.
    Tpay__ApiBaseUrl: tpayApiBaseUrl
    Tpay__ClientId: tpayClientId
    Tpay__ClientSecret: tpayClientSecret
    Tpay__MerchantSecurityCode: tpayMerchantSecurityCode
    Tpay__JwsCertPrefix: tpayJwsCertPrefix
    Tpay__RootCaPemUrl: tpayRootCaPemUrl
    Tpay__NotificationUrl: tpayNotificationUrl
    Tpay__RentoomSiteBaseUrl: normalizedTpayApiRentoomSiteBaseUrl
    Tpay__SuccessUrl: tpayApiSuccessUrl
    Tpay__ErrorUrl: tpayApiErrorUrl
    

    // IdoBooking dummy mode
    IdoBooking__UseDummy: idoBookingUseDummySetting
    IdoBooking__DummyReservationTemplateKey: idoBookingDummyReservationTemplateKey
    IDOBOOKING_BASE_API_URL: idoBookingBaseApiUrl
    IDOBOOKING_API_USER: idoBookingApiUser
    IDOBOOKING_API_PWD: idoBookingApiPassword
    StayWellUrlBase: staywellUrlBase
    StayWellReservationUrlBase: staywellReservationUrlBase
    Bitrix__ReservationPipelineName: bitrixReservationPipelineName

    // Blob storage configuration used by the app
    Storage__Container: uploadsStorageContainerName
    Storage__ConnectionString: ''
    Storage__AccountName: rentoomDataStorage.name
    InstructionsStorage__Container: instructionsStorageContainerName
    InstructionsStorage__ConnectionString: ''
    InstructionsStorage__AccountName: rentoomDataStorage.name

    // TTLock configuration
    TTLOCK__ClientId: ttlockClientId
    TTLOCK__ClientSecret: ttlockClientSecret
    TTLOCK__Username: ttlockUsername
    TTLOCK__Password: ttlockPassword

    // Database connection strings used by the app startup
    ConnectionStrings__POSTGRES_RENTOOM_BOOKING_DB_LOCAL: staywellDbConnectionString
    ConnectionStrings__RentoomDbConnectionString: rentoomAppDbConnectionString
    PostgresPooling__Enabled: postgresPoolingEnabledSetting
    PostgresPooling__MinimumPoolSize: string(postgresPoolingMinimumPoolSize)
    PostgresPooling__MaximumPoolSize: string(staywellApiPostgresMaximumPoolSize)
    PostgresPooling__ConnectionIdleLifetime: string(postgresPoolingConnectionIdleLifetime)
    PostgresPooling__ConnectionPruningInterval: string(postgresPoolingConnectionPruningInterval)
    PostgresPooling__Timeout: string(postgresPoolingTimeout)
    PostgresPooling__CommandTimeout: string(postgresPoolingCommandTimeout)
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

resource staywellSwa 'Microsoft.Web/staticSites@2025-03-01' = {
  name: staywellStaticWebAppName
  location: 'westeurope'
  tags: tags
  sku: {
    name: 'Standard'
    tier: 'Standard'
  }
  properties: {
    repositoryUrl: staywellGithubRepositoryUrl
    repositoryToken: staywellGithubRepositoryToken
    branch: staywellGithubBranch
    provider: 'GitHub'
    buildProperties: {
      appLocation: staywellGithubAppLocation
      outputLocation: staywellGithubOutputLocation
      githubActionSecretNameOverride: staywellGithubActionSecretName
      skipGithubActionWorkflowGeneration: false
    }
  }
}

resource swaBackendLink 'Microsoft.Web/staticSites/linkedBackends@2025-03-01' = {
  parent: staywellSwa
  name: 'linkedBackend'
  properties: {
    backendResourceId: staywellApi.id
    region: location
  }
}

resource staywellApiAuthSettings 'Microsoft.Web/sites/config@2024-11-01' = {
  parent: staywellApi
  name: 'authsettingsV2'
  dependsOn: [
    swaBackendLink
  ]
  properties: {
    platform: {
      enabled: true
      runtimeVersion: '~1'
    }
    globalValidation: {
      requireAuthentication: true
      unauthenticatedClientAction: 'RedirectToLoginPage'
      excludedPaths: staywellApiAuthExcludedPaths
    }
    httpSettings: {
      requireHttps: true
      routes: {
        apiPrefix: '/.auth'
      }
      forwardProxy: {
        convention: 'NoProxy'
      }
    }
    identityProviders: {
      azureStaticWebApps: {
        enabled: true
        registration: {
          clientId: staywellSwa.properties.defaultHostname
        }
      }
    }
  }
}

output rentoomBookingWebUrl string = normalizedRentoomWebBaseUrl
output staywellStaticWebUrl string = normalizedStaywellBaseUrl
output staywellApiFunctionsUrl string = '${normalizedStaywellApiCustomBaseUrl}/api'
output postgresServerId string = existingPostgres.id
output postgresServerHost string = '${existingPostgres.name}.postgres.database.azure.com'
output staywellDatabaseName string = staywellDbName
output rentoomDataStorageAccountName string = rentoomDataStorage.name
output rentoomDataStoragePrimaryBlobEndpoint string = rentoomDataStorage.properties.primaryEndpoints.blob
output logAnalyticsWorkspaceId string = logAnalyticsWorkspace.id
output staywellApiAppInsightsName string = staywellApiAppInsights.name
output staywellApiAppInsightsConnectionString string = staywellApiAppInsights.properties.ConnectionString
output rentoomWebAppInsightsName string = rentoomWebAppInsights.name
output rentoomWebAppInsightsConnectionString string = rentoomWebAppInsights.properties.ConnectionString
