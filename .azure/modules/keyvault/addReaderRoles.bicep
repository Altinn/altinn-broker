param keyvaultName string
param principalIds array
param tenantId string = subscription().tenantId

var readerAccessPoliciesArray = [
  for principalId in principalIds: {
    objectId: principalId
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
