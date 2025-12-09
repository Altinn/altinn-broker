targetScope = 'subscription'

param environment string

resource brokerTagsPolicy 'Microsoft.Authorization/policyDefinitions@2025-03-01' = {
  name: 'broker-standard-tags-${environment}'
  properties: {
    policyType: 'Custom'
    mode: 'Indexed'
    displayName: 'Ensure standard tags on Broker resources'
    description: 'Adds or updates standard FinOps and repository tags on Broker resource groups and resources.'
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
              {
                allOf: [
                  {
                    field: 'tags[finops_environment]'
                    exists: 'false'
                  }
                  {
                    value: '[resourceGroup().tags.finops_environment]'
                    notEquals: ''
                  }
                ]
              }
              {
                allOf: [
                  {
                    field: 'tags[finops_product]'
                    exists: 'false'
                  }
                  {
                    value: '[resourceGroup().tags.finops_product]'
                    notEquals: ''
                  }
                ]
              }
              {
                allOf: [
                  {
                    field: 'tags[finops_serviceownercode]'
                    exists: 'false'
                  }
                  {
                    value: '[resourceGroup().tags.finops_serviceownercode]'
                    notEquals: ''
                  }
                ]
              }
              {
                allOf: [
                  {
                    field: 'tags[finops_serviceownerorgnr]'
                    exists: 'false'
                  }
                  {
                    value: '[resourceGroup().tags.finops_serviceownerorgnr]'
                    notEquals: ''
                  }
                ]
              }
              {
                allOf: [
                  {
                    field: 'tags[repository]'
                    exists: 'false'
                  }
                  {
                    value: '[resourceGroup().tags.repository]'
                    notEquals: ''
                  }
                ]
              }
              {
                allOf: [
                  {
                    field: 'tags[env]'
                    exists: 'false'
                  }
                  {
                    value: '[resourceGroup().tags.env]'
                    notEquals: ''
                  }
                ]
              }
              {
                allOf: [
                  {
                    field: 'tags[product]'
                    exists: 'false'
                  }
                  {
                    value: '[resourceGroup().tags.product]'
                    notEquals: ''
                  }
                ]
              }
              {
                allOf: [
                  {
                    field: 'tags[org]'
                    exists: 'false'
                  }
                  {
                    value: '[resourceGroup().tags.org]'
                    notEquals: ''
                  }
                ]
              }
            ]
          }
        ]
      }
      then: {
        effect: 'modify'
        details: {
          roleDefinitionIds: [
            '/providers/Microsoft.Authorization/roleDefinitions/4a9ae827-6dc8-4573-8ac7-8239d42aa03f' // Contributor
          ]
          operations: [
            {
              operation: 'add'
              field: 'tags[finops_environment]'
              value: '[resourceGroup().tags.finops_environment]'
            }
            {
              operation: 'add'
              field: 'tags[finops_product]'
              value: '[resourceGroup().tags.finops_product]'
            }
            {
              operation: 'add'
              field: 'tags[finops_serviceownercode]'
              value: '[resourceGroup().tags.finops_serviceownercode]'
            }
            {
              operation: 'add'
              field: 'tags[finops_serviceownerorgnr]'
              value: '[resourceGroup().tags.finops_serviceownerorgnr]'
            }
            {
              operation: 'add'
              field: 'tags[repository]'
              value: '[resourceGroup().tags.repository]'
            }
            {
              operation: 'add'
              field: 'tags[env]'
              value: '[resourceGroup().tags.env]'
            }
            {
              operation: 'add'
              field: 'tags[product]'
              value: '[resourceGroup().tags.product]'
            }
            {
              operation: 'add'
              field: 'tags[org]'
              value: '[resourceGroup().tags.org]'
            }
          ]
        }
      }
    }
  }
}

output policyDefinitionId string = brokerTagsPolicy.id


