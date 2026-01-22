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

@description('Om soft delete skal være på for vaulten.')
param enableSoftDelete bool = true

@description('Soft delete-retensjon i dager for slettede recovery points.')
@minValue(1)
param softDeleteRetentionInDays int = 14

@description('Om system-assigned managed identity skal aktiveres på vaulten.')
param enableSystemAssignedIdentity bool = true

// Ressursnavn bygget opp av namePrefix (som allerede inneholder miljø)
var backupVaultName = '${namePrefix}-backup-vault'
var backupPolicyName = '${namePrefix}-backup-policy'
var backupInstanceName = last(split(pgDatabaseResourceId, '/'))

// Datasource-type for Azure Database for PostgreSQL – Flexible Server
var pgDatasourceType = 'Microsoft.DBforPostgreSQL/flexibleServers/databases'

// Backup vault med ZRS (ZoneRedundant)
resource backupVault 'Microsoft.DataProtection/backupVaults@2024-03-01' = {
  name: backupVaultName
  location: location
  ...(enableSystemAssignedIdentity ? {
    identity: {
      type: 'SystemAssigned'
    }
  } : {})
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
      // Backup schedule
      {
        name: 'DefaultBackupRule'
        objectType: 'AzureBackupRule'
        backupParameters: {
          objectType: 'AzureBackupParams'
        }
        dataStore: {
          dataStoreType: 'VaultStore'
          objectType: 'DataStoreInfoBase'
        }
        trigger: {
          schedule: {
            repeatingTimeIntervals: [
              // Daglig backup, starter på angitt tidspunkt
              'R/${backupStartTimeUtc}/P1D'
            ]
            timeZone: 'UTC'
          }
          objectType: 'ScheduleBasedTriggerContext'
        }
        retentionTag: 'Default'
      }
      // Retention-regel
      {
        name: 'DefaultRetentionRule'
        objectType: 'AzureRetentionRule'
        isDefault: true
        lifecycles: [
          {
            deleteAfter: {
              objectType: 'AbsoluteDeleteOption'
              // ISO8601 duration P<n>D
              duration: 'P${retentionDays}D'
            }
            sourceDataStore: {
              dataStoreType: 'VaultStore'
              objectType: 'DataStoreInfoBase'
            }
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
      resourceId: pgDatabaseResourceId
      resourceLocation: location
      resourceName: backupInstanceName
      datasourceType: pgDatasourceType
      resourceType: pgDatasourceType
    }
  }
}

output backupVaultNameOut string = backupVault.name
output backupPolicyNameOut string = pgBackupPolicy.name
output backupInstanceNameOut string = pgBackupInstance.name

