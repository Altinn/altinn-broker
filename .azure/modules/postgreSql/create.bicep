param namePrefix string
param location string
param environmentKeyVaultName string
param srcSecretName string

@export()
type Sku = {
  name: 'Standard_B1ms' | 'Standard_B2s' | 'Standard_D2ads_v5'
  tier: 'Burstable' | 'GeneralPurpose' | 'MemoryOptimized'
}
param sku Sku
param environment string
@secure()
param srcKeyVault object

@secure()
param administratorLoginPassword string
@secure()
param tenantId string

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

resource postgres 'Microsoft.DBforPostgreSQL/flexibleServers@2022-12-01' = {
  name: '${namePrefix}-dbserver'
  location: location
  properties: {
    version: '15'
    administratorLogin: databaseUser
    administratorLoginPassword: administratorLoginPassword
    storage: {
      storageSizeGB: 32
    }
    backup: { backupRetentionDays: 35 }
    authConfig: {
      activeDirectoryAuth: 'Enabled'
      passwordAuth: 'Enabled'
      tenantId: tenantId
    }
  }
  sku: sku
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
    value: 'UUID-OSSP'
    source: 'user-override'
  }
}

resource allowAzureAccess 'Microsoft.DBforPostgreSQL/flexibleServers/firewallRules@2023-06-01-preview' = {
  name: 'azure-access'
  parent: postgres
  dependsOn: [configurations] // Needs to depend on database to avoid updating at the same time
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

module adoConnectionString '../keyvault/upsertSecret.bicep' = {
  name: 'adoConnectionString'
  params: {
    destKeyVaultName: environmentKeyVaultName
    secretName: 'broker-ado-connection-string'
    secretValue: 'Host=${postgres.properties.fullyQualifiedDomainName};Database=${databaseName};Port=5432;Username=${namePrefix}-app-identity;Ssl Mode=Require;Trust Server Certificate=True;Maximum Pool Size=${poolSize};'
  }
}
