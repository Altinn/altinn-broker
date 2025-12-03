param location string
@secure()
param namePrefix string
param image string
param environment string
param platform_base_url string
param maskinporten_environment string
param eventGridIps array

@secure()
param subscription_id string
@secure()
param principal_id string
@secure()
param keyVaultUrl string
@secure()
param userIdentityClientId string
@secure()
param containerAppEnvId string
@secure()
param apimIp string

var probes = [
  {
    httpGet: {
      port: 2525
      path: '/health'
    }
    type: 'Startup'
  }
]

var containerAppEnvVars = [
  { name: 'ASPNETCORE_ENVIRONMENT', value: environment }
  { name: 'APPLICATIONINSIGHTS_CONNECTION_STRING', secretRef: 'application-insights-connection-string' }
  { name: 'DatabaseOptions__ConnectionString', secretRef: 'broker-ado-connection-string' }
  { name: 'AzureResourceManagerOptions__SubscriptionId', value: subscription_id }
  { name: 'AzureResourceManagerOptions__Location', value: 'norwayeast' }
  { name: 'AzureResourceManagerOptions__Environment', value: environment }
  { name: 'AzureResourceManagerOptions__ApplicationResourceGroupName', value: '${namePrefix}-rg' }
  { name: 'AzureResourceManagerOptions__ContainerAppName', value: '${namePrefix}-app' }
  { name: 'AzureResourceManagerOptions__ApimIP', value: apimIp }
  { name: 'AzureResourceManagerOptions__MalwareScanEventGridTopicName', value: eventgrid_topic.name }
  { name: 'AZURE_CLIENT_ID', value: userIdentityClientId }
  {
    name: 'AltinnOptions__OpenIdWellKnown'
    value: '${platform_base_url}/authentication/api/v1/openid/.well-known/openid-configuration'
  }
  { name: 'AltinnOptions__PlatformGatewayUrl', value: platform_base_url }
  { name: 'AltinnOptions__PlatformSubscriptionKey', secretRef: 'platform-subscription-key' }
  { name: 'MaskinportenSettings__Environment', value: maskinporten_environment }
  { name: 'MaskinportenSettings__ClientId', secretRef: 'maskinporten-client-id' }
  {
    name: 'MaskinportenSettings__Scope'
    value: 'altinn:events.publish altinn:events.publish.admin altinn:register/partylookup.admin altinn:authorization/authorize.admin'
  }
  { 
    name: 'MaskinportenSettings__ExhangeToAltinnToken'
    value: 'true'
  }
  { name: 'MaskinportenSettings__EncodedJwk', secretRef: 'maskinporten-jwk' }
  { name: 'GeneralSettings__SlackUrl', secretRef: 'slack-url' }
  { name: 'GeneralSettings__ApplicationInsightsConnectionString', secretRef: 'application-insights-connection-string' }
  { name: 'StatisticsApiKey', secretRef: 'statistics-api-key' }
  { name: 'AzureStorageOptions__BlockSize', value: '33554432' }
  { name: 'AzureStorageOptions__ConcurrentUploadThreads', value: '3' }
  { name: 'AzureStorageOptions__BlocksBeforeCommit', value: '1000' }
  { name: 'ReportStorageOptions__ConnectionString', secretRef: 'storage-connection-string' }
]

var EventGridIpRestrictions = map(eventGridIps, (ipRange, index) => {
  name: 'AzureEventGrid'
  action: 'Allow'
  ipAddressRange: ipRange!
})

var apimIpRestrictions = empty(apimIp)
  ? []
  : [
      {
        name: 'apim'
        action: 'Allow'
        ipAddressRange: apimIp!
      }
    ]
var ipSecurityRestrictions = concat(apimIpRestrictions, EventGridIpRestrictions)

resource containerApp 'Microsoft.App/containerApps@2024-03-01' = {
  name: '${namePrefix}-app'
  location: location
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${principal_id}': {}
    }
  }
  properties: {
    configuration: {
      ingress: {
        targetPort: 2525
        external: true
        transport: 'Auto'
        ipSecurityRestrictions: ipSecurityRestrictions
      }
      secrets: [
        {
          identity: principal_id
          keyVaultUrl: '${keyVaultUrl}/secrets/platform-subscription-key'
          name: 'platform-subscription-key'
        }
        {
          identity: principal_id
          keyVaultUrl: '${keyVaultUrl}/secrets/maskinporten-client-id'
          name: 'maskinporten-client-id'
        }
        {
          identity: principal_id
          keyVaultUrl: '${keyVaultUrl}/secrets/maskinporten-jwk'
          name: 'maskinporten-jwk'
        }
        {
          identity: principal_id
          keyVaultUrl: '${keyVaultUrl}/secrets/broker-ado-connection-string'
          name: 'broker-ado-connection-string'
        }
        {
          identity: principal_id
          keyVaultUrl: '${keyVaultUrl}/secrets/application-insights-connection-string'
          name: 'application-insights-connection-string'
        }
        {
          identity: principal_id
          keyVaultUrl: '${keyVaultUrl}/secrets/slack-url'
          name: 'slack-url'
        } 
        {
          identity: principal_id
          keyVaultUrl: '${keyVaultUrl}/secrets/statistics-api-key'
          name: 'statistics-api-key'
        }
        {
          identity: principal_id
          keyVaultUrl: '${keyVaultUrl}/secrets/storage-connection-string'
          name: 'storage-connection-string'
        }
      ]
    }

    environmentId: containerAppEnvId
    template: {
      scale: {
        minReplicas: environment == 'test' ? 1 : 2 // set to 2 for redundancy in staging and production
        maxReplicas: environment == 'test' ? 1 : 4
        rules: environment == 'test'
          ? []
          : [
              {
                name: 'cpuscalingrule'
                custom: {
                  type: 'cpu'
                  metadata: {
                    type: 'Utilization'
                    value: '80'
                  }
                }
              }
              {
                name: 'httpscalingrule'
                http: {
                  metadata: {
                    concurrentRequests: '60'
                  }
                }
              }
              {
                name: 'memoryscalingrule'
                custom: {
                  type: 'memory'
                  metadata: {
                    type: 'Utilization'
                    value: '80'
                  }
                }
              }
            ]
      }
      containers: [
        {
          name: 'app'
          image: image
          env: containerAppEnvVars
          probes: probes
          resources: {
            cpu: environment == 'production' ? json('2.0') : json('0.5')
            memory: environment == 'production' ? '4.0Gi' : '1.0Gi'
          }
        }
      ]
    }
  }
}

resource eventgrid_topic 'Microsoft.EventGrid/topics@2022-06-15' = {
  name: '${namePrefix}-malware-scan-event-topic'
  location: location
}

resource eventgrid_event_subscription 'Microsoft.EventGrid/topics/eventSubscriptions@2022-06-15' = {
  name: '${namePrefix}-malware-scan-event-subscription'
  parent: eventgrid_topic
  properties: {
    destination: {
      endpointType: 'WebHook'
      properties: {
        endpointUrl: 'https://${containerApp.properties.configuration.ingress.fqdn}/broker/api/v1/webhooks/malwarescanresults'
      }
    }
  }
}

output name string = containerApp.name
output revisionName string = containerApp.properties.latestRevisionName
output app object = containerApp
output eventGridTopicName string = eventgrid_topic.name
