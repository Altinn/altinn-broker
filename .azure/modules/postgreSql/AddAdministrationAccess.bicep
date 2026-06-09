param principalId string
type PrincipalType = 'Group' | 'ServicePrincipal' | 'Unknown' | 'User'
param principalType PrincipalType
param tenantId string
param appName string
param namePrefix string

resource databaseServer 'Microsoft.DBforPostgreSQL/flexibleServers@2024-08-01' existing = {
  name: '${namePrefix}-dbserver'
}
resource databaseAccess 'Microsoft.DBforPostgreSQL/flexibleServers/administrators@2024-08-01' = {
  name: principalId
  parent: databaseServer
  properties: {
    principalType: principalType
    tenantId: tenantId
    principalName: appName
  }
}
