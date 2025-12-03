param namePrefix string
param location string
@secure()
param environmentKeyVaultName string
param srcSecretName string
param environment string
@secure()
param srcKeyVault object
@secure()
param administratorLoginPassword string
@secure()
param tenantId string

var prodLikeEnvironment = environment != 'test'
var databaseName = 'brokerdb'
var databaseUser = 'adminuser'
var poolSize = environment == 'test' ? 25 : 50

module saveAdmPassword '../keyvault/upsertSecret.bicep' = {
  name: 'Save_${srcSecretName}'
  scope: resourceGroup(srcKeyVault.subscriptionId, srcKeyVault.resourceGroupName)
  params: {
    destKeyVaultName: srcKeyVault.name
    secretName: srcSecretName
    secretValue: administratorLoginPassword
  }
}

var migrationConnectionStringName = 'broker-migration-connection-string'
module saveMigrationConnectionString '../keyvault/upsertSecret.bicep' = {
  name: 'Save_${migrationConnectionStringName}'
  scope: resourceGroup(srcKeyVault.subscriptionId, srcKeyVault.resourceGroupName)
  params: {
    destKeyVaultName: srcKeyVault.name
    secretName: migrationConnectionStringName
    secretValue: 'jdbc:postgresql://${postgres.name}.postgres.database.azure.com/brokerdb?user=${databaseUser}&password=${administratorLoginPassword}'
  }
}

resource postgres 'Microsoft.DBforPostgreSQL/flexibleServers@2023-12-01-preview' = {
  name: '${namePrefix}-dbserver'
  location: location
  properties: {
    version: '16'
    administratorLogin: databaseUser
    administratorLoginPassword: administratorLoginPassword
    storage: {
      storageSizeGB: environment == 'production' ? 128 : 32
      tier: environment == 'test'
        ? 'P4'
        : environment == 'production' ? 'P15' : 'P20'
      autoGrow: environment == 'production' ? 'Enabled' : 'Disabled'
    }
    backup: { backupRetentionDays: 35 }
    authConfig: {
      activeDirectoryAuth: 'Enabled'
      passwordAuth: 'Enabled'
      tenantId: tenantId
    }
    availabilityZone: environment == 'production' ? '3' : null
    highAvailability: environment == 'production' ? {
      mode: 'ZoneRedundant'
      standbyAvailabilityZone: '1'
    } : null
  }
  sku: {
    name: environment == 'test'
    ? 'Standard_B1ms'
    : environment == 'production' ? 'Standard_D16ds_v5' : 'Standard_D8ds_v5'
    tier: environment == 'test' ? 'Burstable' : 'GeneralPurpose'
  }
}

resource database 'Microsoft.DBforPostgreSQL/flexibleServers/databases@2023-12-01-preview' = {
  name: databaseName
  parent: postgres
  properties: {
    charset: 'UTF8'
    collation: 'nb_NO.utf8'
  }
}

resource configurations 'Microsoft.DBforPostgreSQL/flexibleServers/configurations@2022-12-01' = {
  name: 'azure.extensions'
  parent: postgres
  dependsOn: [database]
  properties: {
    value: 'UUID-OSSP,PG_CRON'
    source: 'user-override'
  }
}

resource maxConnectionsConfiguration 'Microsoft.DBforPostgreSQL/flexibleServers/configurations@2024-08-01' = {
  name: 'max_connections'
  parent: postgres
  dependsOn: [database, configurations]
  properties: {
    value: prodLikeEnvironment ? '550' : '50'
    source: 'user-override'
  }
}

resource workMemConfiguration 'Microsoft.DBforPostgreSQL/flexibleServers/configurations@2024-08-01' = {
  name: 'work_mem'
  parent: postgres
  dependsOn: [database, maxConnectionsConfiguration]
  properties: {
    value: prodLikeEnvironment ? '1097151' : '4096'
    source: 'user-override'
  }
}

resource maintenanceWorkMemConfiguration 'Microsoft.DBforPostgreSQL/flexibleServers/configurations@2024-08-01' = {
  name: 'maintenance_work_mem'
  parent: postgres
  dependsOn: [database, workMemConfiguration]
  properties: {
    value: prodLikeEnvironment ? '2097151' : '99328'
    source: 'user-override'
  }
}

resource maxPreparedTransactions 'Microsoft.DBforPostgreSQL/flexibleServers/configurations@2024-08-01' = {
  name: 'max_prepared_transactions'
  parent: postgres
  dependsOn: [database, maintenanceWorkMemConfiguration]
  properties: {
    value: prodLikeEnvironment ? '3000' : '50'
    source: 'user-override'
  }
}

resource allowAzureAccess 'Microsoft.DBforPostgreSQL/flexibleServers/firewallRules@2023-06-01-preview' = {
  name: 'azure-access'
  parent: postgres
  dependsOn: [maxPreparedTransactions] // Needs to depend on database to avoid updating at the same time
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

resource cronDatabaseName 'Microsoft.DBforPostgreSQL/flexibleServers/configurations@2024-08-01' = {
  name: 'cron.database_name'
  parent: postgres
  dependsOn: [database, allowAzureAccess]
  properties: {
    value: 'brokerdb'
    source: 'user-override'
  }
}

module adoConnectionString '../keyvault/upsertSecret.bicep' = {
  name: 'adoConnectionString'
  params: {
    destKeyVaultName: environmentKeyVaultName
    secretName: 'broker-ado-connection-string'
    secretValue: 'Host=${postgres.properties.fullyQualifiedDomainName};Database=${databaseName};Port=5432;Username=${namePrefix}-app-identity;Ssl Mode=Require;Trust Server Certificate=True;Maximum Pool Size=${poolSize};options=-c role=azure_pg_admin;'
  }
}
