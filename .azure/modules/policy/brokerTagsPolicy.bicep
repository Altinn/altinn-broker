targetScope = 'subscription'

param environment string

resource brokerTagsPolicy 'Microsoft.Authorization/policyDefinitions@2025-03-01' = {
  name: 'broker-standard-tags-${environment}'
  properties: {
    policyType: 'Custom'
    mode: 'All'
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
            field: 'type'
            exists: 'true' 
          }
          {
            anyOf: [
              {
                field: 'tags[finops_environment]'
                exists: 'false'
              }
              {
                field: 'tags[finops_product]'
                exists: 'false'
              }
              {
                field: 'tags[finops_serviceownercode]'
                exists: 'false'
              }
              {
                field: 'tags[finops_serviceownerorgnr]'
                exists: 'false'
              }
              {
                field: 'tags[repository]'
                exists: 'false'
              }
              {
                field: 'tags[env]'
                exists: 'false'
              }
              {
                field: 'tags[product]'
                exists: 'false'
              }
              {
                field: 'tags[org]'
                exists: 'false'
              }
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
              operation: 'add'
              field: 'tags[finops_environment]'
              value: environment
            }
            {
              operation: 'add'
              field: 'tags[finops_product]'
              value: 'formidling'
            }
            {
              operation: 'add'
              field: 'tags[finops_serviceownercode]'
              value: 'digdir'
            }
            {
              operation: 'add'
              field: 'tags[finops_serviceownerorgnr]'
              value: '991825827'
            }
            {
              operation: 'add'
              field: 'tags[repository]'
              value: 'https://github.com/Altinn/altinn-broker'
            }
            {
              operation: 'add'
              field: 'tags[env]'
              value: environment
            }
            {
              operation: 'add'
              field: 'tags[product]'
              value: 'formidling'
            }
            {
              operation: 'add'
              field: 'tags[org]'
              value: 'digdir'
            }
          ]
        }
      }
    }
  }
}

output policyDefinitionId string = brokerTagsPolicy.id


