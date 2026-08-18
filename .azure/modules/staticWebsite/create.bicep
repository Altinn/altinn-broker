@description('Globally unique storage account name (3-24 lowercase alphanumeric characters).')
param storageAccountName string

param location string

resource storageAccount 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: storageAccountName
  location: location
  tags: resourceGroup().tags
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {
    accessTier: 'Hot'
    minimumTlsVersion: 'TLS1_2'
    supportsHttpsTrafficOnly: true
    allowBlobPublicAccess: true
    allowSharedKeyAccess: false
  }
}

var staticWebsiteEndpoint = storageAccount.properties.primaryEndpoints.web

@description('Hostname of the static website endpoint, e.g. myaccount.z16.web.core.windows.net')
output staticWebsiteHostName string = replace(replace(staticWebsiteEndpoint, 'https://', ''), '/', '')
output storageAccountName string = storageAccount.name
output storageAccountId string = storageAccount.id
