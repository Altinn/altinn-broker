param namePrefix string
param location string

@secure()
param keyVaultUrl string

@secure()
param keyVaultName string

var containerAppJobName = '${namePrefix}-migration'
var containerAppEnvName = '${namePrefix}-env'
var migrationConnectionStringName = 'broker-migration-connection-string'

resource userAssignedIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: '${namePrefix}-migration-identity'
  location: location
}

module addKeyvaultRead '../../modules/keyvault/addReaderRoles.bicep' = {
  name: 'kvreader-${namePrefix}-migration'
  params: {
    keyvaultName: keyVaultName
    tenantId: userAssignedIdentity.properties.tenantId
    principalIds: [userAssignedIdentity.properties.principalId]
  }
}

var secrets = [
  {
    name: migrationConnectionStringName
    keyVaultUrl: '${keyVaultUrl}/secrets/${migrationConnectionStringName}'
    identity: userAssignedIdentity.id
  }
]

var containerAppEnvVars = [
  {
    name: 'FLYWAY_URL'
    secretRef: migrationConnectionStringName
  }
  {
    name: 'FLYWAY_CONNECT_RETRIES'
    value: '3'
  }
  {
    name: 'FLYWAY_VALIDATE_MIGRATION_NAMING'
    value: 'true'
  }
]

var volumes = [
  {
    name: 'migrations'
    storageName: 'migrations'
    storageType: 'AzureFile'
    mountOptions: 'cache=none'
  }
]

var volumeMounts = [
  {
    volumeName: 'migrations'
    mountPath: '/flyway/sql'
    subPath: ''
  }
]

resource containerAppEnv 'Microsoft.App/managedEnvironments@2023-11-02-preview' existing = {
  name: containerAppEnvName
}

module containerAppJob '../../modules/containerAppJob/main.bicep' = {
  name: containerAppJobName
  dependsOn: [
    addKeyvaultRead
  ]
  params: {
    name: containerAppJobName
    location: location
    containerAppEnvId: containerAppEnv.id
    environmentVariables: containerAppEnvVars
    secrets: secrets
    command: ['/bin/bash', '-c', 'flyway migrate;']
    image: 'flyway/flyway:latest'
    volumes: volumes
    volumeMounts: volumeMounts
    principalId: userAssignedIdentity.id
  }
}

output name string = containerAppJob.outputs.name
