targetScope = 'subscription'

@description('Location for the identity resource group and user-assigned managed identity.')
param location string

@description('Resource group that stores the GitHub Actions deployment identity.')
param identityResourceGroupName string

@description('User-assigned managed identity used by GitHub Actions for Function App deployments.')
param managedIdentityName string

@description('Federated credential name on the Function App deployment identity.')
param federatedCredentialName string

@description('User-assigned managed identity used by GitHub Actions for Web App deployments.')
param webAppManagedIdentityName string

@description('Federated credential name on the Web App deployment identity.')
param webAppFederatedCredentialName string

@description('GitHub organization or owner slug from the repository URL.')
param githubOrganizationSlug string

@description('GitHub repository name.')
param githubRepositoryName string

@description('GitHub branch allowed to exchange tokens with Azure.')
param githubBranch string

@description('Subscription ID of the target Function App.')
param targetFunctionAppSubscriptionId string = subscription().subscriptionId

@description('Resource group of the target Function App.')
param targetFunctionAppResourceGroupName string

@description('Name of the target Function App.')
param targetFunctionAppName string

@description('Subscription ID of the target Web App.')
param targetWebAppSubscriptionId string = subscription().subscriptionId

@description('Resource group of the target Web App.')
param targetWebAppResourceGroupName string

@description('Name of the target Web App.')
param targetWebAppName string

@description('Built-in role definition ID assigned to the deployment identity at the Function App scope.')
param deploymentRoleDefinitionId string = 'de139f84-1756-47ae-9be6-808fbbe84772'

@description('Common tags.')
param tags object

var githubIssuer = 'https://token.actions.githubusercontent.com'
var githubAudience = 'api://AzureADTokenExchange'
var githubSubject = 'repo:${githubOrganizationSlug}/${githubRepositoryName}:ref:refs/heads/${githubBranch}'

resource identityResourceGroup 'Microsoft.Resources/resourceGroups@2025-04-01' = {
  name: identityResourceGroupName
  location: location
  tags: tags
}

module functionIdentityResources './modules/identity-resources.bicep' = {
  name: 'function-identity-resources'
  scope: identityResourceGroup
  params: {
    location: location
    managedIdentityName: managedIdentityName
    federatedCredentialName: federatedCredentialName
    githubIssuer: githubIssuer
    githubAudience: githubAudience
    githubSubject: githubSubject
    tags: tags
  }
}

module functionAccess './modules/site-access.bicep' = {
  name: 'function-access'
  scope: resourceGroup(targetFunctionAppSubscriptionId, targetFunctionAppResourceGroupName)
  params: {
    targetSiteName: targetFunctionAppName
    principalId: functionIdentityResources.outputs.managedIdentityPrincipalId
    principalResourceId: functionIdentityResources.outputs.managedIdentityId
    deploymentRoleDefinitionId: deploymentRoleDefinitionId
  }
}

module webAppIdentityResources './modules/identity-resources.bicep' = {
  name: 'webapp-identity-resources'
  scope: identityResourceGroup
  params: {
    location: location
    managedIdentityName: webAppManagedIdentityName
    federatedCredentialName: webAppFederatedCredentialName
    githubIssuer: githubIssuer
    githubAudience: githubAudience
    githubSubject: githubSubject
    tags: tags
  }
}

module webAppAccess './modules/site-access.bicep' = {
  name: 'webapp-access'
  scope: resourceGroup(targetWebAppSubscriptionId, targetWebAppResourceGroupName)
  params: {
    targetSiteName: targetWebAppName
    principalId: webAppIdentityResources.outputs.managedIdentityPrincipalId
    principalResourceId: webAppIdentityResources.outputs.managedIdentityId
    deploymentRoleDefinitionId: deploymentRoleDefinitionId
  }
}

output managedIdentityResourceGroup string = identityResourceGroup.name
output managedIdentityName string = functionIdentityResources.outputs.managedIdentityName
output managedIdentityId string = functionIdentityResources.outputs.managedIdentityId
output managedIdentityClientId string = functionIdentityResources.outputs.managedIdentityClientId
output managedIdentityPrincipalId string = functionIdentityResources.outputs.managedIdentityPrincipalId
output functionManagedIdentityResourceGroup string = identityResourceGroup.name
output functionManagedIdentityName string = functionIdentityResources.outputs.managedIdentityName
output functionManagedIdentityId string = functionIdentityResources.outputs.managedIdentityId
output functionManagedIdentityClientId string = functionIdentityResources.outputs.managedIdentityClientId
output functionManagedIdentityPrincipalId string = functionIdentityResources.outputs.managedIdentityPrincipalId
output webAppManagedIdentityResourceGroup string = identityResourceGroup.name
output webAppManagedIdentityName string = webAppIdentityResources.outputs.managedIdentityName
output webAppManagedIdentityId string = webAppIdentityResources.outputs.managedIdentityId
output webAppManagedIdentityClientId string = webAppIdentityResources.outputs.managedIdentityClientId
output webAppManagedIdentityPrincipalId string = webAppIdentityResources.outputs.managedIdentityPrincipalId
output tenantId string = subscription().tenantId
output githubIssuer string = githubIssuer
output githubAudience string = githubAudience
output githubSubject string = githubSubject
output targetFunctionAppId string = functionAccess.outputs.targetSiteId
output targetWebAppId string = webAppAccess.outputs.targetSiteId
