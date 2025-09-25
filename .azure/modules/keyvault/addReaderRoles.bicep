param keyvaultName string
param principals array
param tenantId string = subscription().tenantId

var secretsUserRoleId = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '4633458b-17de-408a-b874-0445c86b69e6')
var keyVaultReaderRoleId = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '21090545-7ca7-4776-b22c-e363652d74d2')

var readerAccessPoliciesArray = [
  for principals in principals: {
    objectId: principals.objectId
    tenantId: tenantId
    permissions: {
      certificates: ['get', 'list']
      keys: ['get', 'list']
      secrets: ['get', 'list']
    }
  }
]

resource keyvault 'Microsoft.KeyVault/vaults@2023-07-01' existing = {
  name: keyvaultName
  resource readerAccessPolicies 'accessPolicies' = {
    name: 'add'
    properties: {
      accessPolicies: readerAccessPoliciesArray
    }
  }
}

resource secretsUsers 'Microsoft.Authorization/roleAssignments@2022-04-01' = [for p in principals: {
  name: guid(subscription().id, keyvault.id, p.objectId, secretsUserRoleId)
  scope: keyvault
  properties: {
    roleDefinitionId: secretsUserRoleId
    principalId: p.objectId
    principalType: p.principalType
  }
}]

resource kvReaders 'Microsoft.Authorization/roleAssignments@2022-04-01' = [for p in principals: {
  name: guid(subscription().id, keyvault.id, p.objectId, keyVaultReaderRoleId)
  scope: keyvault
  properties: {
    roleDefinitionId: keyVaultReaderRoleId
    principalId: p.objectId
    principalType: p.principalType
  }
}]
