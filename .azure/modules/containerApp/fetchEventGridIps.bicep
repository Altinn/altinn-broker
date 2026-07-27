param location string

@secure()
param principal_id string

@secure()
param subscription_id string

resource deploymentScript 'Microsoft.Resources/deploymentScripts@2023-08-01' = {
  name: 'fetchAzureEventGridIpsScript'
  location: location
  tags: resourceGroup().tags
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
      param(
        [string] $location,
        [string] $subscriptionId
      )

      $ErrorActionPreference = 'Stop'

      function Fail([string] $message) {
        Write-Error $message
        throw $message
      }

      Write-Host "fetchAzureEventGridIpsScript starting"
      Write-Host "Parameters: location=$location, subscriptionId=$subscriptionId"

      $context = Get-AzContext
      if (-not $context) {
        Fail "No Azure context after managed identity login. Verify the deployment principal has Managed Identity Operator on the user-assigned identity."
      }

      Write-Host "Context after login: Account=$($context.Account.Id), Subscription=$($context.Subscription.Id), Tenant=$($context.Tenant.Id)"

      if (-not $context.Subscription) {
        Write-Host "No subscription in context. Available contexts:"
        Get-AzContext -ListAvailable | ForEach-Object {
          Write-Host "  Subscription=$($_.Subscription.Id) Name=$($_.Subscription.Name)"
        }
        if (-not $subscriptionId) {
          Fail "No subscription in Azure context and subscriptionId parameter was not provided."
        }
        Write-Host "Setting context to subscription $subscriptionId"
        $context = Set-AzContext -SubscriptionId $subscriptionId
      }
      elseif ($subscriptionId -and $context.Subscription.Id -ne $subscriptionId) {
        Write-Host "Switching context from $($context.Subscription.Id) to $subscriptionId"
        $context = Set-AzContext -SubscriptionId $subscriptionId
      }

      Write-Host "Using subscription $($context.Subscription.Id) ($($context.Subscription.Name))"

      try {
        $serviceTags = Get-AzNetworkServiceTag -Location $location
      }
      catch {
        Fail "Get-AzNetworkServiceTag failed for location '$location': $($_.Exception.Message). Ensure the identity has Microsoft.Network/locations/serviceTags/read at subscription scope."
      }

      if (-not $serviceTags) {
        Fail "Get-AzNetworkServiceTag returned null for location '$location'."
      }

      $valueCount = @($serviceTags.Values).Count
      Write-Host "Received $valueCount service tag entries"

      if ($valueCount -eq 0) {
        Fail "Get-AzNetworkServiceTag returned zero entries. The managed identity may lack subscription-level read access (Reader or Contributor)."
      }

      $eventGridEntries = @(
        $serviceTags.Values | Where-Object {
          $_.Name -eq 'AzureEventGrid' -or $_.Id -eq 'AzureEventGrid'
        }
      )
      Write-Host "Found $($eventGridEntries.Count) AzureEventGrid service tag entry(ies)"

      if ($eventGridEntries.Count -eq 0) {
        $sampleNames = @(
          $serviceTags.Values | Select-Object -First 20 | ForEach-Object { $_.Name }
        ) -join ', '
        Fail "AzureEventGrid service tag not found. Sample tag names from API: $sampleNames"
      }

      $output = @(
        $eventGridEntries | ForEach-Object { $_.Properties.AddressPrefixes } | Where-Object { $_ -and $_ -notmatch ':' }
      )
      Write-Host "Found $($output.Count) IPv4 prefixes for AzureEventGrid"

      if ($output.Count -eq 0) {
        $allPrefixes = @(
          $eventGridEntries | ForEach-Object { $_.Properties.AddressPrefixes }
        )
        Fail "AzureEventGrid found but no IPv4 prefixes. All prefixes: $($allPrefixes -join ', ')"
      }

      Write-Host "IPv4 prefixes: $($output -join ', ')"

      $DeploymentScriptOutputs = @{}
      $DeploymentScriptOutputs['eventGridIps'] = $output
      Write-Host "fetchAzureEventGridIpsScript completed successfully with $($output.Count) IP ranges"
    '''
    arguments: '-location ${location} -subscriptionId ${subscription_id}'
    forceUpdateTag: '2'
    retentionInterval: 'P1D'
    cleanupPreference: 'OnSuccess'
  }
}

output eventGridIps array = deploymentScript.properties.outputs.eventGridIps
output scriptLastRunStatus string = deploymentScript.properties.status.lastRunStatus
output scriptError object = deploymentScript.properties.status.error
