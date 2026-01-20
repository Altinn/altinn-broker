param namePrefix string
param location string
param appVersion string
param migrationImage string
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

module keyvaultAddReaderRolesMigrationIdentity '../../modules/keyvault/addReaderRoles.bicep' = {
  name: 'kvreader-${namePrefix}-migration'
  params: {
    keyvaultName: keyVaultName
    principals: [
      { objectId: userAssignedIdentity.properties.principalId, principalType: 'ServicePrincipal' }
    ]
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
  {
    name: 'APP_VERSION'
    value: appVersion
  }
]

resource containerAppEnv 'Microsoft.App/managedEnvironments@2024-03-01' existing = {
  name: containerAppEnvName
}

module containerAppJob '../../modules/migrationJob/main.bicep' = {
  name: containerAppJobName
  dependsOn: [
    keyvaultAddReaderRolesMigrationIdentity
  ]
  params: {
    name: containerAppJobName
    location: location
    containerAppEnvId: containerAppEnv.id
    environmentVariables: containerAppEnvVars
    secrets: secrets
    command: ['/bin/bash', '-c', 'flyway migrate;']
    image: migrationImage
    principalId: userAssignedIdentity.id
  }
}

output name string = containerAppJob.name
