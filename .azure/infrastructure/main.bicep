targetScope = 'subscription'
@minLength(3)
param location string
@secure()
@minLength(3)
param brokerPgAdminPassword string
@secure()
@minLength(3)
param sourceKeyVaultName string
@secure()
@minLength(3)
param tenantId string
@secure()
@minLength(3)
param object_id string
@secure()
param test_client_id string
@secure()
param deploySecret string

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

import { Sku as KeyVaultSku } from '../modules/keyvault/create.bicep'
param keyVaultSku KeyVaultSku

import { Sku as PostgresSku } from '../modules/postgreSql/create.bicep'
param postgresSku PostgresSku

var resourceGroupName = '${namePrefix}-rg'

var secrets = [
  {
    name: 'broker-admin-password'
    value: brokerPgAdminPassword
  }
  {
    name: 'deploy-id'
    value: object_id
  }
  {
    name: 'deploy-secret'
    value: deploySecret
  }
  {
    name: 'deploy-tenant-id'
    value: tenantId
  }
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
]

// Create resource groups
resource resourceGroup 'Microsoft.Resources/resourceGroups@2023-07-01' = {
  name: '${namePrefix}-rg'
  location: location
}

module environmentKeyVault '../modules/keyvault/create.bicep' = {
  scope: resourceGroup
  name: 'keyVault'
  params: {
    vaultName: sourceKeyVaultName
    location: location
    sku: keyVaultSku
    tenant_id: tenantId
    environment: environment
    object_id: object_id
    test_client_id: test_client_id
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

var brokerAdminPasswordSecretName = 'broker-admin-password'
module storeAdminPassword '../modules/keyvault/upsertSecret.bicep' = {
  scope: resourceGroup
  name: brokerAdminPasswordSecretName
  dependsOn: [
    environmentKeyVault
  ]
  params: {
    destKeyVaultName: sourceKeyVaultName
    secretName: brokerAdminPasswordSecretName
    secretValue: brokerPgAdminPassword
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
    srcSecretName: 'brokerPgAdminPassword'
    administratorLoginPassword: brokerPgAdminPassword
    sku: postgresSku
    tenantId: tenantId
    test_client_id: test_client_id
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

output resourceGroupName string = resourceGroup.name
output environmentKeyVaultName string = environmentKeyVault.outputs.name
