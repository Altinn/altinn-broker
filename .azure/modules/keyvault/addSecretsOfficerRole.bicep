param keyvaultName string
param principalType string = 'Group'
param principalObjectId string

var secretsOfficerRoleId = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'b86a8fe4-44ce-4948-aee5-eccb2c155cd7')

resource kv 'Microsoft.KeyVault/vaults@2023-07-01' existing = {
  name: keyvaultName
}

resource secretsOfficer 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(subscription().id, kv.id, principalObjectId, secretsOfficerRoleId)
  scope: kv
  properties: {
    roleDefinitionId: secretsOfficerRoleId
    principalId: principalObjectId
    principalType: principalType
  }
}
