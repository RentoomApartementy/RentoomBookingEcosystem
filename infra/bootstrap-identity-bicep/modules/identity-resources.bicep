targetScope = 'resourceGroup'

@description('Location for the user-assigned managed identity.')
param location string

@description('User-assigned managed identity used by GitHub Actions.')
param managedIdentityName string

@description('Federated credential name on the user-assigned managed identity.')
param federatedCredentialName string

@description('OIDC issuer used by GitHub Actions.')
param githubIssuer string

@description('OIDC audience used by GitHub Actions.')
param githubAudience string

@description('OIDC subject allowed to exchange tokens with Azure.')
param githubSubject string

@description('Common tags.')
param tags object

resource githubDeploymentIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2024-11-30' = {
  name: managedIdentityName
  location: location
  tags: tags
}

resource githubFederatedCredential 'Microsoft.ManagedIdentity/userAssignedIdentities/federatedIdentityCredentials@2024-11-30' = {
  parent: githubDeploymentIdentity
  name: federatedCredentialName
  properties: {
    audiences: [
      githubAudience
    ]
    issuer: githubIssuer
    subject: githubSubject
  }
}

output managedIdentityName string = githubDeploymentIdentity.name
output managedIdentityId string = githubDeploymentIdentity.id
output managedIdentityClientId string = githubDeploymentIdentity.properties.clientId
output managedIdentityPrincipalId string = githubDeploymentIdentity.properties.principalId
