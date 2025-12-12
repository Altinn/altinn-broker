targetScope = 'subscription'
@minLength(3)
param location string
@secure()
param brokerPgAdminPassword string
@secure()
param sourceKeyVaultName string
@secure()
param tenantId string
@secure()
param test_client_id string
param environment string
@secure()
param namePrefix string

@secure()
param migrationsStorageAccountName string
@secure()
param maskinportenJwk string
@secure()
param maskinportenClientId string
@secure()
param platformSubscriptionKey string
@secure()
param slackUrl string
@secure()
param statisticsApiKey string
@secure()
param grafanaMonitoringPrincipalId string

import { Sku as KeyVaultSku } from '../modules/keyvault/create.bicep'
param keyVaultSku KeyVaultSku

var resourceGroupName = '${namePrefix}-rg'
var standardTags = {
  finops_environment: environment
  finops_product: 'formidling'
  finops_serviceownercode: 'digdir'
  finops_serviceownerorgnr: '991825827'
  repository: 'https://github.com/Altinn/altinn-broker'
  env: environment
  product: 'formidling'
  org: 'digdir'
}

module grantTestClientSecretsOfficerRole '../modules/keyvault/addSecretsOfficerRole.bicep' = if (environment == 'test') {
  scope: resourceGroup
  name: 'kv-secrets-officer-test-client'
  dependsOn: [ environmentKeyVault ]
  params: {
    keyvaultName: sourceKeyVaultName
    principalObjectId: test_client_id
    principalType: 'Group'
  }
}

var secrets = [
  {
    name: 'maskinporten-client-id'
    value: maskinportenClientId
  }
  {
    name: 'maskinporten-jwk'
    value: maskinportenJwk
  }
  {
    name: 'platform-subscription-key'
    value: platformSubscriptionKey
  }
  {
    name: 'slack-url'
    value: slackUrl
  }
  {
    name: 'statistics-api-key'
    value: statisticsApiKey
  }
]

// Create resource groups
resource resourceGroup 'Microsoft.Resources/resourceGroups@2024-03-01' = {
  name: '${namePrefix}-rg'
  location: location
  tags: standardTags
}

module brokerTagsIdentity '../modules/identity/createUserAssigned.bicep' = {
  scope: resourceGroup
  name: 'broker-tags-identity'
  params: {
    identityName: '${namePrefix}-broker-tags-mi'
    location: location
  }
}

module environmentKeyVault '../modules/keyvault/create.bicep' = {
  scope: resourceGroup
  name: 'keyVault'
  params: {
    vaultName: sourceKeyVaultName
    location: location
    sku: keyVaultSku
    tenant_id: tenantId
  }
}

module keyvaultSecrets '../modules/keyvault/upsertSecrets.bicep' = {
  scope: resourceGroup
  name: 'secrets'
  params: {
    secrets: secrets
    sourceKeyvaultName: environmentKeyVault.outputs.name
  }
}

// #####################################################
// Create resources with dependencies to other resources
// #####################################################

var srcKeyVault = {
  name: sourceKeyVaultName
  subscriptionId: subscription().subscriptionId
  resourceGroupName: resourceGroupName
}

var brokerAdminPasswordSecretName = 'broker-admin-password'
module postgresql '../modules/postgreSql/create.bicep' = {
  scope: resourceGroup
  name: 'postgresql'
  dependsOn: [
    environmentKeyVault
  ]
  params: {
    namePrefix: namePrefix
    location: location
    environmentKeyVaultName: sourceKeyVaultName
    srcKeyVault: srcKeyVault
    srcSecretName: brokerAdminPasswordSecretName
    administratorLoginPassword: brokerPgAdminPassword
    tenantId: tenantId
    environment: environment
  }
}

module migrationsStorageAccount '../modules/storageAccount/create.bicep' = {
  scope: resourceGroup
  name: migrationsStorageAccountName
  params: {
    migrationsStorageAccountName: migrationsStorageAccountName
    location: location
    fileshare: 'migrations'
  }
}

module containerAppEnv '../modules/containerAppEnvironment/main.bicep' = {
  scope: resourceGroup
  name: 'container-app-environment'
  dependsOn: [migrationsStorageAccount]
  params: {
    keyVaultName: sourceKeyVaultName
    location: location
    namePrefix: namePrefix
    migrationsStorageAccountName: migrationsStorageAccountName
  }
}

module virusscan '../modules/virusscan/create.bicep' = {
  name: 'virusscan'
}

module grafanaMonitoringReaderRole '../modules/subscription/addMonitoringReaderRole.bicep' = {
  name: 'grafana-monitoring-reader'
  params: {
    grafanaPrincipalId: grafanaMonitoringPrincipalId
  }
}

module brokerTagsPolicy '../modules/policy/brokerTagsPolicy.bicep' = {
  name: 'broker-standard-tags-definition'
  params: {
    environment: environment
  }
}

module brokerTagsAssignment '../modules/policy/assignBrokerTags.bicep' = {
  name: 'broker-standard-tags-assignment'
  scope: resourceGroup
  params: {
    policyDefinitionId: brokerTagsPolicy.outputs.policyDefinitionId
    userAssignedIdentityName: brokerTagsIdentity.outputs.name
  }
}

output resourceGroupName string = resourceGroup.name
output environmentKeyVaultName string = environmentKeyVault.outputs.name
