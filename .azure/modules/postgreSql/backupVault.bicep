@description('Name prefix som allerede inneholder miljø (f.eks. broker-dev, broker-prod).')
param namePrefix string

@description('Miljønavn (f.eks. dev, test, prod).')
param environment string

@description('Location for Backup vault og tilhørende ressurser.')
param location string

@description('Resource ID til PostgreSQL-databasen (Flexible Server) som skal ha backup).')
@minLength(1)
param pgDatabaseResourceId string

@description('Antall dager backup skal beholdes. Standard er 365 dager (1 år) for prod/production-lignende miljøer, 90 dager for test.')
param retentionDays int = (environment == 'production' || environment == 'prod') ? 365 : environment == 'test' ? 90 : 180

@description('ISO8601 starttidspunkt (UTC) for første backup-kjøring. Brukes som start på R/<start>/P1D.')
param backupStartTimeUtc string = '2024-01-01T22:00:00Z'

@description('Om immutability (låsing av recovery points) skal være på for vaulten.')
param enableImmutability bool = true

@description('Om soft delete skal være på for vaulten. TODO: REVERSER - Satt til false for testing, skal tilbake til true.')
param enableSoftDelete bool = false

@description('Soft delete-retensjon i dager for slettede recovery points.')
@minValue(1)
param softDeleteRetentionInDays int = 14

@description('Om system-assigned managed identity skal aktiveres på vaulten.')
param enableSystemAssignedIdentity bool = true

@description('Hvis satt, bruker eksisterende backup policy med dette navnet. Hvis tom, opprettes ny policy.')
param existingBackupPolicyName string = ''

// Ressursnavn bygget opp av namePrefix (som allerede inneholder miljø)
var backupVaultName = '${namePrefix}-broker-backup-vault'
var backupPolicyName = existingBackupPolicyName != '' ? existingBackupPolicyName : '${namePrefix}-broker-backup-policy'
var useExistingPolicy = existingBackupPolicyName != ''
// Backup instance navn må være på formatet: {serverName}-{databaseName}
// Vi henter server-navnet fra resource ID (nest siste del) og database-navnet (siste del)
var resourceIdParts = split(pgDatabaseResourceId, '/')
var serverName = resourceIdParts[length(resourceIdParts) - 2]
var databaseName = resourceIdParts[length(resourceIdParts) - 1]
var backupInstanceName = '${serverName}-${databaseName}'

// Datasource-type for Azure Database for PostgreSQL – Flexible Server
// For backup vault, datasource type skal være uten /databases
var pgDatasourceType = 'Microsoft.DBforPostgreSQL/flexibleServers'

// Backup vault med ZRS (ZoneRedundant)
resource backupVault 'Microsoft.DataProtection/backupVaults@2024-03-01' = {
  name: backupVaultName
  location: location
  identity: enableSystemAssignedIdentity ? {
    type: 'SystemAssigned'
  } : null
  properties: {
    storageSettings: [
      {
        datastoreType: 'VaultStore'
        type: 'ZoneRedundant'
      }
    ]
    securitySettings: {
      crossSubscriptionRestoreSettings: {
        state: 'Disabled'
      }
      softDeleteSettings: {
        state: enableSoftDelete ? 'On' : 'Off'
        retentionDurationInDays: softDeleteRetentionInDays
      }
      immutabilitySettings: {
        state: enableImmutability ? 'Unlocked' : 'Disabled'
      }
    }
  }
}

// Backup policy for Azure Database for PostgreSQL – Flexible Server
// Bruk eksisterende policy hvis spesifisert, ellers opprett ny
resource pgBackupPolicyExisting 'Microsoft.DataProtection/backupVaults/backupPolicies@2024-03-01' existing = if (useExistingPolicy) {
  parent: backupVault
  name: backupPolicyName
}

resource pgBackupPolicy 'Microsoft.DataProtection/backupVaults/backupPolicies@2024-03-01' = if (!useExistingPolicy) {
  parent: backupVault
  name: backupPolicyName
  properties: {
    objectType: 'BackupPolicy'
    datasourceTypes: [
      pgDatasourceType // Microsoft.DBforPostgreSQL/flexibleServers
    ]
    policyRules: [
      {
        name: 'BackupDaily'
        objectType: 'AzureBackupRule'
        backupParameters: {
          objectType: 'AzureBackupParams'
          backupType: 'Full'
        }
        dataStore: {
          dataStoreType: 'VaultStore'
          objectType: 'DataStoreInfoBase'
        }
        trigger: {
          objectType: 'ScheduleBasedTriggerContext'
          schedule: {
            repeatingTimeIntervals: [
              'R/${backupStartTimeUtc}/P1D'
            ]
            timeZone: 'UTC'
          }
          taggingCriteria: [
            {
              isDefault: true
              taggingPriority: 99
              tagInfo: {
                tagName: 'Default'
                id: 'Default_'
              }
              // NB: ingen "criteria" på Default for PG Flex
            }
          ]
        }
      }
      {
        name: 'Default'
        objectType: 'AzureRetentionRule'
        isDefault: true
        lifecycles: [
          {
            deleteAfter: {
              objectType: 'AbsoluteDeleteOption'
              duration: 'P${retentionDays}D'
            }
            sourceDataStore: {
              dataStoreType: 'VaultStore'
              objectType: 'DataStoreInfoBase'
            }
            targetDataStoreCopySettings: []
          }
        ]
      }
    ]
  }
}

// Backup instance som knytter databasen til vault + policy
resource pgBackupInstance 'Microsoft.DataProtection/backupVaults/backupInstances@2024-03-01' = {
  parent: backupVault
  name: backupInstanceName
  properties: {
    objectType: 'BackupInstance'
    policyInfo: {
      policyId: useExistingPolicy ? pgBackupPolicyExisting.id : pgBackupPolicy.id
    }
    dataSourceInfo: {
      objectType: 'Datasource'
      resourceID: pgDatabaseResourceId
      resourceLocation: location
      resourceName: databaseName
      datasourceType: pgDatasourceType
      resourceType: 'Microsoft.DBforPostgreSQL/flexibleServers/databases'
    }
  }
}

// Role assignments for backup vault på resource group-nivå
// Reader role
var readerRoleDefinitionId = 'acdd72a7-3385-48ef-bd42-f606fba81ae7'
resource readerRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (enableSystemAssignedIdentity) {
  name: guid(resourceGroup().id, backupVault.name, readerRoleDefinitionId, 'backup-vault-reader')
  scope: resourceGroup()
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', readerRoleDefinitionId)
    principalId: backupVault.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

// PostgreSQL Flexible Server Long Term Retention Backup Role
var pgLtrBackupRoleDefinitionId = 'c088a766-074b-43ba-90d4-1fb21feae531'
resource pgLtrBackupRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (enableSystemAssignedIdentity) {
  name: guid(resourceGroup().id, backupVault.name, pgLtrBackupRoleDefinitionId, 'backup-vault-pg-ltr')
  scope: resourceGroup()
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', pgLtrBackupRoleDefinitionId)
    principalId: backupVault.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

output backupVaultNameOut string = backupVault.name
output backupPolicyNameOut string = useExistingPolicy ? pgBackupPolicyExisting.name : pgBackupPolicy.name
output backupInstanceNameOut string = pgBackupInstance.name

