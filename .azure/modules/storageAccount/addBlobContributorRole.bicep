param storageAccountName string

@description('Object ID of the principal that uploads frontend assets during deployment.')
param principalObjectId string

@description('Storage Blob Data Owner is required to enable static website hosting (set blob service properties) and upload assets.')
var storageBlobDataOwnerRoleDefinitionId = 'b7e6dc6d-f1e8-4753-8033-0f276bb0955b'

resource storageAccount 'Microsoft.Storage/storageAccounts@2023-05-01' existing = {
  name: storageAccountName
}

resource storageBlobDataOwner 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(storageAccount.id, principalObjectId, storageBlobDataOwnerRoleDefinitionId)
  scope: storageAccount
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', storageBlobDataOwnerRoleDefinitionId)
    principalId: principalObjectId
    principalType: 'ServicePrincipal'
  }
}
