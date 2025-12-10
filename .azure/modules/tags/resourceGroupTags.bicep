targetScope = 'resourceGroup'

@description('tags to apply to this resource')
param tags object

resource resourceGroupTags 'Microsoft.Resources/tags@2021-04-01' = {
  name: 'default'
  properties: {
    tags: tags
  }
}
