param storageAccountName string
param fileShareName string
param principalId string
param principalType string = 'ServicePrincipal'

var storageFileDataSMBShareElevatedContributorRoleId = '0c9e92f7-afc1-4faf-b480-8445d8f5e335'

resource storageAccount 'Microsoft.Storage/storageAccounts@2023-05-01' existing = {
  name: storageAccountName
}

resource fileServices 'Microsoft.Storage/storageAccounts/fileServices@2023-05-01' existing = {
  name: 'default'
  parent: storageAccount
}

resource fileShare 'Microsoft.Storage/storageAccounts/fileServices/shares@2023-05-01' existing = {
  name: fileShareName
  parent: fileServices
}

resource roleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(subscription().id, fileShare.id, principalId, storageFileDataSMBShareElevatedContributorRoleId)
  scope: fileShare
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', storageFileDataSMBShareElevatedContributorRoleId)
    principalId: principalId
    principalType: principalType
  }
}

