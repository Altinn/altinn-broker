@description('Name prefix som allerede inneholder miljø (f.eks. broker-dev, broker-prod).')
param namePrefix string

@description('Miljønavn (f.eks. dev, test, prod).')
param environment string

@description('Location for Backup vault og tilhørende ressurser.')
param location string

@description('Resource ID til PostgreSQL-databasen (Flexible Server) som skal ha backup).')
@minLength(1)
param pgDatabaseResourceId string

@description('Antall dager backup skal beholdes. Standard er 3650 dager (10 år) for prod/production-lignende miljøer, 90 dager for test.')
param retentionDays int = (environment == 'production' || environment == 'prod') ? 3650 : environment == 'test' ? 90 : 180

@description('ISO8601 starttidspunkt (UTC) for første backup-kjøring. Brukes som start på R/<start>/P1D.')
param backupStartTimeUtc string = '2024-01-01T22:00:00Z'

@description('Om immutability (låsing av recovery points) skal være på for vaulten.')
param enableImmutability bool = false

@description('Om soft delete skal være på for vaulten. TODO: REVERSER - Satt til false for testing, skal tilbake til true.')
param enableSoftDelete bool = false

@description('Soft delete-retensjon i dager for slettede recovery points.')
@minValue(1)
param softDeleteRetentionInDays int = 14

@description('Om system-assigned managed identity skal aktiveres på vaulten.')
param enableSystemAssignedIdentity bool = true

// TODO: REVERSER DETTE - Hardkodet navn for testing, skal tilbake til namePrefix-basert
// Ressursnavn bygget opp av namePrefix (som allerede inneholder miljø)
// var backupVaultName = '${namePrefix}-backup-vault'
// var backupPolicyName = '${namePrefix}-backup-policy'
var backupVaultName = 'test-backup-vault-v3'
var backupPolicyName = 'test-backup-policy-v3'
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
resource pgBackupPolicy 'Microsoft.DataProtection/backupVaults/backupPolicies@2024-03-01' = {
  parent: backupVault
  name: backupPolicyName
  properties: {
    objectType: 'BackupPolicy'
    datasourceTypes: [
      pgDatasourceType
    ]
    policyRules: [
      {
        name: 'DefaultBackupRule'
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
              tagInfo: {
                tagName: 'Default'
              }
              taggingPriority: 99
              isDefault: true
              criteria: [
                {
                  objectType: 'ScheduleBasedBackupCriteria'
                  absoluteCriteria: [
                    'AllBackup'
                  ]
                }
              ]
            }
          ]
        }
      }
      {
        // match tagName
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
      policyId: pgBackupPolicy.id
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

output backupVaultNameOut string = backupVault.name
output backupPolicyNameOut string = pgBackupPolicy.name
output backupInstanceNameOut string = pgBackupInstance.name

