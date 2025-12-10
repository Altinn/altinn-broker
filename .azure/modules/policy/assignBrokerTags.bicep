targetScope = 'resourceGroup'

param policyDefinitionId string

resource brokerTagsAssignment 'Microsoft.Authorization/policyAssignments@2025-03-01' = {
  name: 'broker-standard-tags'
  location: resourceGroup().location
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    displayName: 'Ensure standard tags on Broker resources'
    policyDefinitionId: policyDefinitionId
    enforcementMode: 'Default'
  }
}

resource brokerTagsAssignmentRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(resourceGroup().id, 'broker-standard-tags-contributor')
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '4a9ae827-6dc8-4573-8ac7-8239d42aa03f') // Tag Contributor
    principalId: brokerTagsAssignment.identity.principalId
    principalType: 'ServicePrincipal'
  }
}
