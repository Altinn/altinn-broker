targetScope = 'subscription'

param grafanaPrincipalId string

var monitoringReaderRoleDefinitionId = '43d0d8ad-25c7-4714-9337-8ba2594b49b5'

resource monitoringReaderRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(subscription().id, grafanaPrincipalId, monitoringReaderRoleDefinitionId)
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', monitoringReaderRoleDefinitionId)
    principalId: grafanaPrincipalId
    principalType: 'ServicePrincipal'
  }
}

