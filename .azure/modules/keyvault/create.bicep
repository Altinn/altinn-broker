param vaultName string
param location string
param environment string
@secure()
param tenant_id string
@secure()
param test_client_id string
@secure()
param object_id string
@export()
type Sku = {
  name: 'standard'
  family: 'A'
}
param sku Sku

resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: vaultName
  location: location
  properties: {
    enabledForTemplateDeployment: true
    enabledForDiskEncryption: true
    enabledForDeployment: true
    sku: sku
    tenantId: tenant_id
    accessPolicies: environment == 'test'
      ? [
          {
            applicationId: null
            tenantId: tenant_id
            objectId: object_id

            permissions: {
              keys: []
              secrets: [
                'Get'
                'List'
              ]
              certificates: []
            }
          }
          {
            applicationId: null
            tenantId: tenant_id
            objectId: test_client_id
            permissions: {
              keys: []
              secrets: [
                'Get'
                'List'
                'Set'
              ]
              certificates: []
            }
          }
        ]
      : [
          {
            applicationId: null
            tenantId: tenant_id
            objectId: object_id

            permissions: {
              keys: []
              secrets: [
                'Get'
                'List'
              ]
              certificates: []
            }
          }
        ]
  }
}

output name string = keyVault.name
