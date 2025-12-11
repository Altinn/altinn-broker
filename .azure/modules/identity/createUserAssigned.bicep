targetScope = 'resourceGroup'

param identityName string
param location string

resource identity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: identityName
  location: location
}

output name string = identity.name
output id string = identity.id
