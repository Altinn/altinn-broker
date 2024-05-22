@secure()
param namePrefix string
param location string


resource userAssignedIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: '${namePrefix}-app-identity'
  location: location
}

var roleDefinitionResourceId = 'b24988ac-6180-42a0-ab88-20f7382dd24c' // Contributor role
resource roleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(subscription().id, userAssignedIdentity.id, roleDefinitionResourceId)
  properties: {
    roleDefinitionId: roleDefinitionResourceId
    principalId: userAssignedIdentity.id
    principalType: 'ServicePrincipal'
  }
}

output id string = userAssignedIdentity.id
output clientId string = userAssignedIdentity.properties.clientId
output principalId string = userAssignedIdentity.properties.principalId
output tenantId string = userAssignedIdentity.properties.tenantId
