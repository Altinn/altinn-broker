targetScope = 'subscription'

param environment string

resource brokerTagsPolicy 'Microsoft.Authorization/policyDefinitions@2025-03-01' = {
  name: 'broker-standard-tags-${environment}'
  properties: {
    policyType: 'Custom'
    mode: 'Indexed'
    displayName: 'Ensure standard tags on Broker resources'
    description: 'Inherits standard tags from the resource group and applies them to Broker resources.'
    metadata: {
      category: 'Tags'
    }
    policyRule: {
      if: {
        allOf: [
          {
            field: 'type'
            notEquals: 'Microsoft.Authorization/policyAssignments'
          }
          {
            anyOf: [
              { field: 'tags[finops_environment]', exists: 'false' }
              { field: 'tags[finops_product]', exists: 'false' }
              { field: 'tags[finops_serviceownercode]', exists: 'false' }
              { field: 'tags[finops_serviceownerorgnr]', exists: 'false' }
              { field: 'tags[repository]', exists: 'false' }
              { field: 'tags[env]', exists: 'false' }
              { field: 'tags[product]', exists: 'false' }
              { field: 'tags[org]', exists: 'false' }
            ]
          }
        ]
      }
      then: {
        effect: 'modify'
        details: {
          roleDefinitionIds: [
            '/providers/Microsoft.Authorization/roleDefinitions/4a9ae827-6dc8-4573-8ac7-8239d42aa03f' // Tag Contributor
          ]
          operations: [
            {
              operation: 'addOrReplace'
              field: 'tags[finops_environment]'
              value: '''[resourceGroup().tags['finops_environment']]'''
            }
            {
              operation: 'addOrReplace'
              field: 'tags[finops_product]'
              value: '''[resourceGroup().tags['finops_product']]'''
            }
            {
              operation: 'addOrReplace'
              field: 'tags[finops_serviceownercode]'
              value: '''[resourceGroup().tags['finops_serviceownercode']]'''
            }
            {
              operation: 'addOrReplace'
              field: 'tags[finops_serviceownerorgnr]'
              value: '''[resourceGroup().tags['finops_serviceownerorgnr']]'''
            }
            {
              operation: 'addOrReplace'
              field: 'tags[repository]'
              value: '''[resourceGroup().tags['repository']]'''
            }
            {
              operation: 'addOrReplace'
              field: 'tags[env]'
              value: '''[resourceGroup().tags['env']]'''
            }
            {
              operation: 'addOrReplace'
              field: 'tags[product]'
              value: '''[resourceGroup().tags['product']]'''
            }
            {
              operation: 'addOrReplace'
              field: 'tags[org]'
              value: '''[resourceGroup().tags['org']]'''
            }
          ]
        }
      }
    }
  }
}

output policyDefinitionId string = brokerTagsPolicy.id


