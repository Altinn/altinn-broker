@secure()
param migrationsStorageAccountName string
param fileshare string
param location string

resource storageAccount 'Microsoft.Storage/storageAccounts@2021-04-01' = {
  name: migrationsStorageAccountName
  location: location
  kind: 'StorageV2'
  sku: {
    name: 'Standard_LRS'
  }
  properties: {
    accessTier: 'Cool'
  }
}

resource storageAccountFileServices 'Microsoft.Storage/storageAccounts/fileServices@2023-01-01' = {
  name: 'default'
  parent: storageAccount
}


resource storageAccountFileShare 'Microsoft.Storage/storageAccounts/fileServices/shares@2023-01-01' = {
  name: fileshare
  parent: storageAccountFileServices
}

output storageAccountId string = storageAccount.id
