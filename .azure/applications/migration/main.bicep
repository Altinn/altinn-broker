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
var brokerDbReadAdGroupIdSecretName = 'broker-db-read-ad-group-id'
var brokerDbReadAdGroupNameSecretName = 'broker-db-read-ad-group-name'
var brokerDbWriteAdGroupIdSecretName = 'broker-db-write-ad-group-id'
var brokerDbWriteAdGroupNameSecretName = 'broker-db-write-ad-group-name'
var postgresTokenResource = 'https://ossrdbms-aad.${environment().suffixes.sqlServerHostname}'

resource userAssignedIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: '${namePrefix}-migration-identity'
  location: location
  tags: resourceGroup().tags
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

module databaseAccess '../../modules/postgreSql/AddAdministrationAccess.bicep' = {
  name: 'databaseAccess'
  dependsOn: [
    keyvaultAddReaderRolesMigrationIdentity // Timing issue
  ]
  params: {
    tenantId: userAssignedIdentity.properties.tenantId
    principalId: userAssignedIdentity.properties.principalId
    principalType: 'ServicePrincipal'
    appName: userAssignedIdentity.name
    namePrefix: namePrefix
  }
}

var secrets = [
  {
    name: migrationConnectionStringName
    keyVaultUrl: '${keyVaultUrl}/secrets/${migrationConnectionStringName}'
    identity: userAssignedIdentity.id
  }
  {
    name: brokerDbReadAdGroupIdSecretName
    keyVaultUrl: '${keyVaultUrl}/secrets/${brokerDbReadAdGroupIdSecretName}'
    identity: userAssignedIdentity.id
  }
  {
    name: brokerDbReadAdGroupNameSecretName
    keyVaultUrl: '${keyVaultUrl}/secrets/${brokerDbReadAdGroupNameSecretName}'
    identity: userAssignedIdentity.id
  }
  {
    name: brokerDbWriteAdGroupIdSecretName
    keyVaultUrl: '${keyVaultUrl}/secrets/${brokerDbWriteAdGroupIdSecretName}'
    identity: userAssignedIdentity.id
  }
  {
    name: brokerDbWriteAdGroupNameSecretName
    keyVaultUrl: '${keyVaultUrl}/secrets/${brokerDbWriteAdGroupNameSecretName}'
    identity: userAssignedIdentity.id
  }
]

var containerAppEnvVars = [
  {
    name: 'FLYWAY_URL'
    secretRef: migrationConnectionStringName
  }
  {
    name: 'FLYWAY_USER'
    value: '${namePrefix}-migration-identity'
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
  {
    name: 'AZURE_CLIENT_ID'
    value: userAssignedIdentity.properties.clientId
  }
  {
    name: 'POSTGRES_TOKEN_RESOURCE'
    value: postgresTokenResource
  }
  {
    name: 'BROKER_DB_READ_AD_GROUP_ID'
    secretRef: brokerDbReadAdGroupIdSecretName
  }
  {
    name: 'BROKER_DB_READ_AD_GROUP_NAME'
    secretRef: brokerDbReadAdGroupNameSecretName
  }
  {
    name: 'BROKER_DB_WRITE_AD_GROUP_ID'
    secretRef: brokerDbWriteAdGroupIdSecretName
  }
  {
    name: 'BROKER_DB_WRITE_AD_GROUP_NAME'
    secretRef: brokerDbWriteAdGroupNameSecretName
  }
]

resource containerAppEnv 'Microsoft.App/managedEnvironments@2024-03-01' existing = {
  name: containerAppEnvName
}

module containerAppJob '../../modules/migrationJob/main.bicep' = {
  name: containerAppJobName
  dependsOn: [
    keyvaultAddReaderRolesMigrationIdentity
    databaseAccess
  ]
  params: {
    name: containerAppJobName
    location: location
    containerAppEnvId: containerAppEnv.id
    environmentVariables: containerAppEnvVars
    secrets: secrets
    command: [
      '/bin/bash'
      '-c'
      '''
        set -euo pipefail
        FLYWAY_PASSWORD=$(curl -sS -H "X-IDENTITY-HEADER: $IDENTITY_HEADER" --get "$IDENTITY_ENDPOINT" --data-urlencode "resource=$POSTGRES_TOKEN_RESOURCE" --data-urlencode "client_id=$AZURE_CLIENT_ID" --data-urlencode "api-version=2019-08-01" | jq -r '.access_token')
        if [ -z "$FLYWAY_PASSWORD" ] || [ "$FLYWAY_PASSWORD" = "null" ]; then
          echo "Failed to acquire PostgreSQL access token for migration identity"
          exit 1
        fi
        export FLYWAY_PASSWORD
        flyway \
          -placeholders.brokerDbReadAdGroupId="$BROKER_DB_READ_AD_GROUP_ID" \
          -placeholders.brokerDbReadAdGroupName="$BROKER_DB_READ_AD_GROUP_NAME" \
          -placeholders.brokerDbWriteAdGroupId="$BROKER_DB_WRITE_AD_GROUP_ID" \
          -placeholders.brokerDbWriteAdGroupName="$BROKER_DB_WRITE_AD_GROUP_NAME" \
          migrate
      '''
    ]
    image: migrationImage
    principalId: userAssignedIdentity.id
  }
}

output name string = containerAppJob.name
