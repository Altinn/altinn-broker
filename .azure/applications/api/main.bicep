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
@secure()
@minLength(3)
param apimIp string
param maskinportenTokenExchangeEnvironment string

var image = 'ghcr.io/altinn/altinn-broker:${imageTag}'
var containerAppName = '${namePrefix}-app'
var rotationLeaderEnvironments = [
  'test'
  'staging'
  'production'
]
var rotationEnabled = contains(rotationLeaderEnvironments, environment)

var resourceGroupName = '${namePrefix}-rg'

resource resourceGroup 'Microsoft.Resources/resourceGroups@2024-03-01' existing = {
  name: resourceGroupName
}

resource appIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' existing = {
  name: '${namePrefix}-app-identity'
  scope: resourceGroup
}

var appIdentityId = appIdentity.id
var appIdentityClientId = appIdentity.properties.clientId
var appIdentityPrincipalId = appIdentity.properties.principalId
var appIdentityTenantId = appIdentity.properties.tenantId
var appIdentityName = appIdentity.name

module appDeployToAzureAccess '../../modules/identity/addDeploymentRoles.bicep' = { 
  name: 'appDeployToAzureAccess'
  params: {
    userAssignedIdentityPrincipalId: appIdentityPrincipalId
  }
}

module keyvaultAddReaderRolesAppIdentity '../../modules/keyvault/addReaderRoles.bicep' = {
  name: 'kvreader-${namePrefix}-app'
  scope: resourceGroup
  params: {
    keyvaultName: sourceKeyVaultName
    principals: [{objectId: appIdentityPrincipalId, principalType: 'ServicePrincipal'}]
  }
}

module keyvaultAddSecretsOfficerRoleAppIdentity '../../modules/keyvault/addSecretsOfficerRole.bicep' = if (rotationEnabled) {
  name: 'kv-secrets-officer-${namePrefix}-app'
  scope: resourceGroup
  params: {
    keyvaultName: sourceKeyVaultName
    principalObjectId: appIdentityPrincipalId
    principalType: 'ServicePrincipal'
  }
}

module databaseAccess '../../modules/postgreSql/AddAdministrationAccess.bicep' = {
  name: 'databaseAccess'
  scope: resourceGroup
  dependsOn: [
    keyvaultAddReaderRolesAppIdentity // Timing issue
  ]
  params: {
    tenantId: appIdentityTenantId
    principalId: appIdentityPrincipalId
    principalType: 'ServicePrincipal'
    appName: appIdentityName
    namePrefix: namePrefix
  }
}

resource keyvault 'Microsoft.KeyVault/vaults@2023-07-01' existing = {
  name: sourceKeyVaultName
  scope: resourceGroup
}

module fetchEventGridIpsScript '../../modules/containerApp/fetchEventGridIps.bicep' = {
  name: 'fetchAzureEventGridIpsScript'
  scope: resourceGroup
  dependsOn: [
    appDeployToAzureAccess
    keyvaultAddReaderRolesAppIdentity
    databaseAccess
  ]
  params: {
    location: location
    principal_id: appIdentityId
    subscription_id: subscription().subscriptionId
  }
}

module containerApp '../../modules/containerApp/main.bicep' = {
  name: containerAppName
  scope: resourceGroup
  dependsOn: [
    keyvaultAddReaderRolesAppIdentity
    keyvaultAddSecretsOfficerRoleAppIdentity
    databaseAccess
  ]
  params: {
    eventGridIps: fetchEventGridIpsScript.outputs.eventGridIps!
    namePrefix: namePrefix
    image: image
    location: location
    environment: environment
    apimIp: apimIp
    subscription_id: subscription().subscriptionId
    principal_id: appIdentityId
    platform_base_url: platform_base_url
    keyVaultUrl: keyVaultUrl
    maskinporten_environment: maskinporten_environment
    userIdentityClientId: appIdentityClientId
    containerAppEnvId: keyvault.getSecret('container-app-env-id')
  }
}

output name string = containerApp.outputs.name
output revisionName string = containerApp.outputs.revisionName
