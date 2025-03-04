param location string

@secure()
param subscription_id string

@secure()
param principal_id string

resource deploymentScript 'Microsoft.Resources/deploymentScripts@2023-08-01' = {
  name: 'fetchAzureEventGridIpsScript'
  location: location
  kind: 'AzurePowerShell'
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${principal_id}': {}
    }
  }
  properties: {
    azPowerShellVersion: '13.0'
    scriptContent: '''
      Set-AzContext -Subscription $subscription_id
      $serviceTags = Get-AzNetworkServiceTag -Location $location
      $EventgridIps = $serviceTags.Values | Where-Object { $_.Name -eq "AzureEventGrid" }
      $output = $EventgridIps.Properties.AddressPrefixes | Where-Object { $_ -notmatch ":" }
      $DeploymentScriptOutputs = @{}
      $DeploymentScriptOutputs['eventGridIps'] = $output
    '''
    arguments: '-location ${location} -subscription_id ${subscription_id}'
    forceUpdateTag: '1'
    retentionInterval: 'PT2H'
  }
}

output eventGridIps array = deploymentScript.properties.outputs.eventGridIps
