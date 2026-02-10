param vaultName string
param location string
@secure()
param tenant_id string
@export()
type Sku = {
  name: 'standard'
  family: 'A'
}
param sku Sku

resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: vaultName
  location: location
  tags: resourceGroup().tags
  properties: {
    enabledForTemplateDeployment: true
    enabledForDiskEncryption: true
    enabledForDeployment: true
    enableSoftDelete: true
    enablePurgeProtection: true
    sku: sku
    tenantId: tenant_id
    enableRbacAuthorization: true
    accessPolicies: []
  }
}

output name string = keyVault.name
