param location string
param namePrefix string
param keyVaultName string
param environment string

// Azure Managed Redis (Microsoft.Cache/redisEnterprise).
// See https://learn.microsoft.com/azure/redis/overview
var redisSkuName = environment == 'test'
  ? 'Balanced_B0'
  : environment == 'staging'
      ? 'Balanced_B1'
      : 'Balanced_B3'

resource redisEnterprise 'Microsoft.Cache/redisEnterprise@2025-07-01' = {
  name: '${namePrefix}-redis'
  location: location
  tags: resourceGroup().tags
  sku: {
    name: redisSkuName
  }
  properties: {
    minimumTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled'
  }
}

resource redisDatabase 'Microsoft.Cache/redisEnterprise/databases@2025-07-01' = {
  parent: redisEnterprise
  name: 'default'
  properties: {
    clientProtocol: 'Encrypted'
    accessKeysAuthentication: 'Enabled'
  }
}

var redisConnectionStringName = 'redis-connection-string'

module redisConnectionStringSecret '../keyvault/upsertSecret.bicep' = {
  name: redisConnectionStringName
  dependsOn: [
    redisDatabase
  ]
  params: {
    destKeyVaultName: keyVaultName
    secretName: redisConnectionStringName
    secretValue: '${redisEnterprise.properties.hostName}:${redisDatabase.properties.port},password=${redisDatabase.listKeys().primaryKey},ssl=True,abortConnect=False'
  }
}

output name string = redisEnterprise.name
output hostName string = redisEnterprise.properties.hostName
output port int = redisDatabase.properties.port
output secretName string = redisConnectionStringName
