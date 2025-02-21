param principalId string
param tenantId string
param appName string
param namePrefix string

resource databaseServer 'Microsoft.DBforPostgreSQL/flexibleServers@2023-12-01-preview' existing = {
  name: '${namePrefix}-dbserver'
}
resource databaseAccess 'Microsoft.DBforPostgreSQL/flexibleServers/administrators@2024-08-01' = {
  name: principalId
  parent: databaseServer
  properties: {
    principalType: 'ServicePrincipal'
    tenantId: tenantId
    principalName: appName
  }
}
