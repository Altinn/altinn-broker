@description('Globally unique Front Door profile name.')
param frontDoorProfileName string

@description('Hostname of the static website origin.')
param originHostName string

@description('APIM gateway hostname (e.g. altinn-dev-api.azure-api.net). When set, /broker/* is forwarded to APIM.')
param apiOriginHostName string = ''

@description('AFD endpoint name. Hostname is {endpointName}-{hash}.azurefd.net — must match the endpoint already in use.')
param endpointName string = 'default'

var hasApiOrigin = !empty(apiOriginHostName)

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

resource frontendOriginGroup 'Microsoft.Cdn/profiles/originGroups@2023-05-01' = {
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

resource frontendOrigin 'Microsoft.Cdn/profiles/originGroups/origins@2023-05-01' = {
  parent: frontendOriginGroup
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

resource apiOriginGroup 'Microsoft.Cdn/profiles/originGroups@2023-05-01' = if (hasApiOrigin) {
  parent: frontDoorProfile
  name: 'api-origin-group'
  properties: {
    loadBalancingSettings: {
      sampleSize: 4
      successfulSamplesRequired: 3
    }
    healthProbeSettings: {
      // Built-in APIM health endpoint (always available on the gateway).
      probePath: '/status-0123456789abcdef'
      probeRequestType: 'GET'
      probeProtocol: 'Https'
      probeIntervalInSeconds: 100
    }
  }
}

resource apiOrigin 'Microsoft.Cdn/profiles/originGroups/origins@2023-05-01' = if (hasApiOrigin) {
  parent: apiOriginGroup
  name: 'apim-gateway'
  properties: {
    hostName: apiOriginHostName
    httpPort: 80
    httpsPort: 443
    originHostHeader: apiOriginHostName
    priority: 1
    weight: 1000
    enabledState: 'Enabled'
  }
}

// Safety net: if /* wins over /broker/*, still force APIM for broker paths.
resource brokerApiRuleSet 'Microsoft.Cdn/profiles/ruleSets@2023-05-01' = if (hasApiOrigin) {
  parent: frontDoorProfile
  name: 'brokerapi'
}

resource brokerToApimRule 'Microsoft.Cdn/profiles/ruleSets/rules@2023-05-01' = if (hasApiOrigin) {
  parent: brokerApiRuleSet
  name: 'ForwardBrokerToApim'
  properties: {
    order: 1
    matchProcessingBehavior: 'Stop'
    conditions: [
      {
        name: 'UrlPath'
        parameters: {
          typeName: 'DeliveryRuleUrlPathMatchConditionParameters'
          operator: 'BeginsWith'
          negateCondition: false
          // AFD UrlPath may be with or without a leading slash depending on SKU/version.
          matchValues: [
            'broker'
            '/broker'
          ]
          transforms: [
            'Lowercase'
          ]
        }
      }
    ]
    actions: [
      {
        name: 'RouteConfigurationOverride'
        parameters: {
          typeName: 'DeliveryRuleRouteConfigurationOverrideActionParameters'
          // Omit cacheConfiguration → caching disabled for overridden requests.
          originGroupOverride: {
            originGroup: {
              id: apiOriginGroup!.id
            }
            forwardingProtocol: 'HttpsOnly'
          }
        }
      }
    ]
  }
}

// More specific than /* so /broker/* is matched first when route selection works.
resource apiRoute 'Microsoft.Cdn/profiles/afdEndpoints/routes@2023-05-01' = if (hasApiOrigin) {
  parent: afdEndpoint
  name: 'api-route'
  dependsOn: [
    apiOrigin
  ]
  properties: {
    originGroup: {
      id: apiOriginGroup!.id
    }
    supportedProtocols: [
      'Http'
      'Https'
    ]
    patternsToMatch: [
      '/broker/*'
    ]
    forwardingProtocol: 'HttpsOnly'
    linkToDefaultDomain: 'Enabled'
    httpsRedirect: 'Enabled'
    enabledState: 'Enabled'
  }
}

resource frontendRoute 'Microsoft.Cdn/profiles/afdEndpoints/routes@2023-05-01' = {
  parent: afdEndpoint
  name: 'frontend-route'
  dependsOn: [
    frontendOrigin
  ]
  properties: {
    originGroup: {
      id: frontendOriginGroup.id
    }
    ruleSets: hasApiOrigin
      ? [
          {
            id: brokerApiRuleSet!.id
          }
        ]
      : []
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
