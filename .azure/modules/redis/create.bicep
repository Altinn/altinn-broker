param location string
param namePrefix string
param keyVaultName string
param environment string

resource userAssignedIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: '${namePrefix}-redis-identity'
  location: location
  tags: resourceGroup().tags
}

resource redis 'Microsoft.Cache/redis@2024-11-01' = {
  name: '${namePrefix}-redis'
  location: location
  tags: resourceGroup().tags
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${userAssignedIdentity.id}': {}
    }
  }
  properties: {
    enableNonSslPort: false
    minimumTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled'
    sku: {
      name: 'Standard'
      family: 'C'
      capacity: environment == 'test' ? 0 : environment == 'staging' ? 1 : 2
    }
  }
}

resource redisFirewallAllowAzureServices 'Microsoft.Cache/redis/firewallRules@2024-11-01' = {
  parent: redis
  name: 'AllowAzureServices'
  properties: {
    startIP: '0.0.0.0'
    endIP: '0.0.0.0'
  }
}

var redisConnectionStringName = 'redis-connection-string'

module redisConnectionStringSecret '../keyvault/upsertSecret.bicep' = {
  name: redisConnectionStringName
  params: {
    destKeyVaultName: keyVaultName
    secretName: redisConnectionStringName
    secretValue: '${redis.properties.hostName}:6380,password=${redis.listKeys().primaryKey},ssl=True,abortConnect=False'
  }
}

output name string = redis.name
output hostName string = redis.properties.hostName
output secretName string = redisConnectionStringName
