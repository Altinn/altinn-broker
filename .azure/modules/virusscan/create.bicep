targetScope = 'subscription'

resource StorageAccounts 'Microsoft.Security/pricings@2024-01-01' = {
  name: 'StorageAccounts'
  properties: {
    pricingTier: 'Standard'

    subPlan: 'DefenderForStorageV2'
    extensions: [
      {
        name: 'OnUploadMalwareScanning'
        isEnabled: 'False'
      }
      {
        name: 'SensitiveDataDiscovery'
        isEnabled: 'False'
      }
    ]
  }
}
