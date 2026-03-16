targetScope = 'subscription'

@description('Location for the identity resource group and user-assigned managed identity.')
param location string

@description('Resource group that stores the GitHub Actions deployment identity.')
param identityResourceGroupName string

@description('User-assigned managed identity used by GitHub Actions.')
param managedIdentityName string

@description('Federated credential name on the user-assigned managed identity.')
param federatedCredentialName string

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

module identityResources './modules/identity-resources.bicep' = {
  name: 'identity-resources'
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

module functionAccess './modules/function-access.bicep' = {
  name: 'function-access'
  scope: resourceGroup(targetFunctionAppSubscriptionId, targetFunctionAppResourceGroupName)
  params: {
    targetFunctionAppName: targetFunctionAppName
    principalId: identityResources.outputs.managedIdentityPrincipalId
    principalResourceId: identityResources.outputs.managedIdentityId
    deploymentRoleDefinitionId: deploymentRoleDefinitionId
  }
}

output managedIdentityResourceGroup string = identityResourceGroup.name
output managedIdentityName string = identityResources.outputs.managedIdentityName
output managedIdentityId string = identityResources.outputs.managedIdentityId
output managedIdentityClientId string = identityResources.outputs.managedIdentityClientId
output managedIdentityPrincipalId string = identityResources.outputs.managedIdentityPrincipalId
output tenantId string = subscription().tenantId
output githubIssuer string = githubIssuer
output githubAudience string = githubAudience
output githubSubject string = githubSubject
output targetFunctionAppId string = functionAccess.outputs.targetFunctionAppId
