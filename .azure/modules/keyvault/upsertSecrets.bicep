param sourceKeyvaultName string
param secrets { name: string, value: string }[]

var baseName = 'secret${uniqueString(resourceGroup().id)}'

module keyvaultSecret './upsertSecret.bicep' = [for i in range(0, length(secrets)): {
  name: '${i}deploy${baseName}'
  params: {
    destKeyVaultName: sourceKeyvaultName
    secretName: secrets[i].name
    secretValue: secrets[i].value
  }
}]

output keyvaultUris array = [for i in range(0, length(secrets)): {
  endpoint: keyvaultSecret[i].outputs.secretUri
}]
