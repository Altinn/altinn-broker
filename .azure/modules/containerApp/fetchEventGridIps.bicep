param location string

@secure()
param subscription_id string

resource deploymentScript 'Microsoft.Resources/deploymentScripts@2023-08-01' = {
  name: 'fetchAzureEventGridIpsScript'
  location: location
  kind: 'AzureCLI'
  properties: {
    azCliVersion: '2.31.0'
    scriptContent: '''
      az network list-service-tags --location $1 --subscription $2 | jq '{eventGridIps: [.values[] | select(.name=="AzureEventGrid") | .properties.addressPrefixes[] | select(test(":") | not)]}' > $AZ_SCRIPTS_OUTPUT_PATH
    '''
    arguments: '${location} ${subscription_id}'
    forceUpdateTag: '1'
    retentionInterval: 'PT2H'
  }
}

output eventGridIps array = deploymentScript.properties.outputs.eventGridIps!
