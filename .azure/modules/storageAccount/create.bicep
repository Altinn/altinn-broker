@secure()
param migrationsStorageAccountName string
param fileshare string
param location string

resource storageAccount 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: migrationsStorageAccountName
  location: location
  tags: resourceGroup().tags
  kind: 'StorageV2'
  sku: {
    name: 'Standard_LRS'
  }
  properties: {
    accessTier: 'Cool'
    minimumTlsVersion: 'TLS1_2'
    supportsHttpsTrafficOnly: true
    allowBlobPublicAccess: false
  }
}

resource storageAccountFileServices 'Microsoft.Storage/storageAccounts/fileServices@2023-05-01' = {
  name: 'default'
  parent: storageAccount
}


resource storageAccountFileShare 'Microsoft.Storage/storageAccounts/fileServices/shares@2023-05-01' = {
  name: fileshare
  parent: storageAccountFileServices
}

output storageAccountId string = storageAccount.id
