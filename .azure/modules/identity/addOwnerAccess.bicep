targetScope = 'subscription'

param userAssignedIdentityPrincipalId string

var roleDefinitionResourceId = '8e3af657-a8ff-443c-a75c-2fe8c4bcb635' // Owner role
resource roleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(subscription().id, userAssignedIdentityPrincipalId, roleDefinitionResourceId)
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roleDefinitionResourceId)
    principalId: userAssignedIdentityPrincipalId
    principalType: 'ServicePrincipal'
  }
}
