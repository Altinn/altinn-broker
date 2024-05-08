param location string
@secure()
param namePrefix string
param image string
param environment string
param platform_base_url string
param maskinporten_environment string

@secure()
param subscription_id string
@secure()
param client_id string
@secure()
param tenant_id string
@secure()
param principal_id string
@secure()
param keyVaultUrl string
@secure()
param malwarescan_event_grid_topic_name string
@secure()
param userIdentityTenantId string
@secure()
param userIdentityClientId string
@secure()
param userIdentityPrincipalId string
@secure()
param containerAppEnvId string

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
  { name: 'AzureResourceManagerOptions__ClientId', value: client_id }
  { name: 'AzureResourceManagerOptions__TenantId', value: tenant_id }
  { name: 'AzureResourceManagerOptions__ClientSecret', secretRef: 'deploy-client-secret' }
  { name: 'AzureResourceManagerOptions__ApplicationResourceGroupName', value: '${namePrefix}-rg' }
  { name: 'AzureResourceManagerOptions__MalwareScanEventGridTopicName', value: malwarescan_event_grid_topic_name }
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
  { name: 'MaskinportenSettings__EncodedJwk', secretRef: 'maskinporten-jwk' }
]
resource containerApp 'Microsoft.App/containerApps@2023-05-01' = {
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
      }
      secrets: [
        {
          identity: principal_id
          keyVaultUrl: '${keyVaultUrl}/secrets/deploy-secret'
          name: 'deploy-client-secret'
        }
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
            cpu: json('0.5')
            memory: '1.0Gi'
          }
        }
      ]
    }
  }
}

output name string = containerApp.name
output revisionName string = containerApp.properties.latestRevisionName
output app object = containerApp
