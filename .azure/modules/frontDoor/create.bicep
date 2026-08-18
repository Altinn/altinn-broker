@description('Globally unique Front Door profile name.')
param frontDoorProfileName string

@description('Hostname of the static website origin.')
param originHostName string

param endpointName string = 'default'

resource frontDoorProfile 'Microsoft.Cdn/profiles@2023-05-01' = {
  name: frontDoorProfileName
  location: 'global'
  tags: resourceGroup().tags
  sku: {
    name: 'Standard_AzureFrontDoor'
  }
}

resource afdEndpoint 'Microsoft.Cdn/profiles/afdEndpoints@2023-05-01' = {
  parent: frontDoorProfile
  name: endpointName
  location: 'global'
  properties: {
    enabledState: 'Enabled'
  }
}

resource originGroup 'Microsoft.Cdn/profiles/originGroups@2023-05-01' = {
  parent: frontDoorProfile
  name: 'frontend-origin-group'
  properties: {
    loadBalancingSettings: {
      sampleSize: 4
      successfulSamplesRequired: 3
    }
    healthProbeSettings: {
      probePath: '/index.html'
      probeRequestType: 'HEAD'
      probeProtocol: 'Https'
      probeIntervalInSeconds: 100
    }
  }
}

resource origin 'Microsoft.Cdn/profiles/originGroups/origins@2023-05-01' = {
  parent: originGroup
  name: 'storage-static-website'
  properties: {
    hostName: originHostName
    httpPort: 80
    httpsPort: 443
    originHostHeader: originHostName
    priority: 1
    weight: 1000
    enabledState: 'Enabled'
  }
}

resource route 'Microsoft.Cdn/profiles/afdEndpoints/routes@2023-05-01' = {
  parent: afdEndpoint
  name: 'frontend-route'
  dependsOn: [
    origin
  ]
  properties: {
    originGroup: {
      id: originGroup.id
    }
    supportedProtocols: [
      'Http'
      'Https'
    ]
    patternsToMatch: [
      '/*'
    ]
    forwardingProtocol: 'HttpsOnly'
    linkToDefaultDomain: 'Enabled'
    httpsRedirect: 'Enabled'
    enabledState: 'Enabled'
  }
}

output profileName string = frontDoorProfile.name
output endpointName string = afdEndpoint.name
output endpointHostName string = afdEndpoint.properties.hostName
