targetScope = 'subscription'

param userAssignedIdentityPrincipalId string

var userAccessAdminRoleDefinitionId = '18d7d88d-d35e-4fb5-a5c3-7773c20a72d9'

resource userAccessRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(subscription().id, userAssignedIdentityPrincipalId, userAccessAdminRoleDefinitionId)
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', userAccessAdminRoleDefinitionId)
    principalId: userAssignedIdentityPrincipalId
    principalType: 'ServicePrincipal'
  }
}

var contributorRoleDefinitionId = 'b24988ac-6180-42a0-ab88-20f7382dd24c'
resource contributorRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(subscription().id, userAssignedIdentityPrincipalId, contributorRoleDefinitionId)
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', contributorRoleDefinitionId)
    principalId: userAssignedIdentityPrincipalId
    principalType: 'ServicePrincipal'
  }
}
