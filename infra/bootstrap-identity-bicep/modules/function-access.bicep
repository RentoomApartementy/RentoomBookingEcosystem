targetScope = 'resourceGroup'

@description('Name of the target Function App.')
param targetFunctionAppName string

@description('Principal ID of the deployment identity.')
param principalId string

@description('Resource ID of the deployment identity.')
param principalResourceId string

@description('Built-in role definition ID assigned to the deployment identity at the Function App scope.')
param deploymentRoleDefinitionId string

resource targetFunctionApp 'Microsoft.Web/sites@2025-03-01' existing = {
  name: targetFunctionAppName
}

resource targetFunctionAppWebsiteContributor 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(targetFunctionApp.id, principalResourceId, deploymentRoleDefinitionId)
  scope: targetFunctionApp
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', deploymentRoleDefinitionId)
    principalId: principalId
    principalType: 'ServicePrincipal'
  }
}

output targetFunctionAppId string = targetFunctionApp.id
