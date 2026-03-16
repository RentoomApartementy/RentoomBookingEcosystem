targetScope = 'resourceGroup'

@description('Name of the target App Service resource (Web App or Function App).')
param targetSiteName string

@description('Principal ID of the deployment identity.')
param principalId string

@description('Resource ID of the deployment identity.')
param principalResourceId string

@description('Built-in role definition ID assigned to the deployment identity at the target site scope.')
param deploymentRoleDefinitionId string

resource targetSite 'Microsoft.Web/sites@2025-03-01' existing = {
  name: targetSiteName
}

resource targetSiteWebsiteContributor 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(targetSite.id, principalResourceId, deploymentRoleDefinitionId)
  scope: targetSite
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', deploymentRoleDefinitionId)
    principalId: principalId
    principalType: 'ServicePrincipal'
  }
}

output targetSiteId string = targetSite.id
