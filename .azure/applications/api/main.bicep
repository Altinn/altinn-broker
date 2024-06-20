targetScope = 'subscription'

@minLength(3)
param imageTag string
@minLength(3)
param environment string
@minLength(3)
param location string
@minLength(3)
param platform_base_url string
@minLength(3)
param maskinporten_environment string
@secure()
@minLength(3)
param sourceKeyVaultName string
@secure()
param keyVaultUrl string
@secure()
param namePrefix string

var image = 'ghcr.io/altinn/altinn-broker:${imageTag}'
var containerAppName = '${namePrefix}-app'

var resourceGroupName = '${namePrefix}-rg'
resource resourceGroup 'Microsoft.Resources/resourceGroups@2024-03-01' = {
  name: resourceGroupName
  location: location
}

module appIdentity '../../modules/identity/create.bicep' = {
  name: 'appIdentity'
  scope: resourceGroup
  params: {
    namePrefix: namePrefix
    location: location
  }
}

module addContributorAccess '../../modules/identity/addContributorAccess.bicep' = {
  name: 'appDeployToAzureAccess'
  params: {
    userAssignedIdentityPrincipalId: appIdentity.outputs.principalId
  }
}

module keyVaultReaderAccessPolicyUserIdentity '../../modules/keyvault/addReaderRoles.bicep' = {
  name: 'kvreader-${namePrefix}-app'
  scope: resourceGroup
  params: {
    keyvaultName: sourceKeyVaultName
    tenantId: appIdentity.outputs.tenantId
    principalIds: [appIdentity.outputs.principalId]
  }
}

module databaseAccess '../../modules/postgreSql/AddAdministrationAccess.bicep' = {
  name: 'databaseAccess'
  scope: resourceGroup
  dependsOn: [
    keyVaultReaderAccessPolicyUserIdentity // Timing issue
  ]
  params: {
    tenantId: appIdentity.outputs.tenantId
    principalId: appIdentity.outputs.principalId
    appName: appIdentity.outputs.name
    namePrefix: namePrefix
  }
}

resource keyvault 'Microsoft.KeyVault/vaults@2023-07-01' existing = {
  name: sourceKeyVaultName
  scope: resourceGroup
}

module containerApp '../../modules/containerApp/main.bicep' = {
  name: containerAppName
  scope: resourceGroup
  dependsOn: [keyVaultReaderAccessPolicyUserIdentity, databaseAccess]
  params: {
    namePrefix: namePrefix
    image: image
    location: location
    environment: environment
    subscription_id: subscription().subscriptionId
    principal_id: appIdentity.outputs.id
    platform_base_url: platform_base_url
    keyVaultUrl: keyVaultUrl
    maskinporten_environment: maskinporten_environment
    userIdentityClientId: appIdentity.outputs.clientId
    containerAppEnvId: keyvault.getSecret('container-app-env-id')
  }
}

output name string = containerApp.outputs.name
output revisionName string = containerApp.outputs.revisionName
