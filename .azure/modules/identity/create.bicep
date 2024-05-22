@secure()
param namePrefix string
param location string


resource userAssignedIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: '${namePrefix}-app-identity'
  location: location
}
output id string = userAssignedIdentity.id
output clientId string = userAssignedIdentity.properties.clientId
output principalId string = userAssignedIdentity.properties.principalId
output tenantId string = userAssignedIdentity.properties.tenantId
