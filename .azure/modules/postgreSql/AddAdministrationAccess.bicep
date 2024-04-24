param principalId string
param tenantId string
param appName string
param namePrefix string

resource database 'Microsoft.DBforPostgreSQL/flexibleServers@2022-03-08-preview' existing = {
  name: '${namePrefix}-pgflex'
}
resource databaseAccess 'Microsoft.DBforPostgreSQL/flexibleServers/administrators@2022-03-08-preview' = {
  name: principalId
  parent: database
  properties: {
    principalType: 'ServicePrincipal'
    tenantId: tenantId
    principalName: appName
  }
}
