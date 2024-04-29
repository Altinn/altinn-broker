targetScope = 'resourceGroup'

@minLength(3)
param imageTag string
@minLength(3)
param environment string
@minLength(3)
param location string
@minLength(3)
param platform_base_url string
@minLength(3)
param maskinporten_environment string
@secure()
@minLength(3)
param sourceKeyVaultName string
@secure()
param keyVaultUrl string

@secure()
param client_id string

@secure()
param tenant_id string
@secure()
param namePrefix string

var baseImageUrl = 'ghcr.io/altinn/altinn-broker'
var containerAppName = '${namePrefix}-app'

resource userAssignedIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: '${namePrefix}-app-identity'
  location: location
}

module keyVaultReaderAccessPolicyUserIdentity '../../modules/keyvault/addReaderRoles.bicep' = {
  name: 'kvreader-${namePrefix}-app'
  params: {
    keyvaultName: sourceKeyVaultName
    tenantId: userAssignedIdentity.properties.tenantId
    principalIds: [userAssignedIdentity.properties.principalId]
  }
}

module databaseAccess '../../modules/postgreSql/AddAdministrationAccess.bicep' = {
  name: 'databaseAccess'
  dependsOn: [
    keyVaultReaderAccessPolicyUserIdentity // Timing issue
  ]
  params: {
    tenantId: userAssignedIdentity.properties.tenantId
    principalId: userAssignedIdentity.properties.principalId
    appName: userAssignedIdentity.name
    namePrefix: namePrefix
  }
}

resource keyvault 'Microsoft.KeyVault/vaults@2023-07-01' existing = {
  name: sourceKeyVaultName
}

module containerApp '../../modules/containerApp/main.bicep' = {
  name: containerAppName
  dependsOn: [keyVaultReaderAccessPolicyUserIdentity, databaseAccess]
  params: {
    namePrefix: namePrefix
    image: '${baseImageUrl}:${imageTag}'
    location: location
    environment: environment
    client_id: client_id
    tenant_id: tenant_id
    subscription_id: subscription().subscriptionId
    principal_id: userAssignedIdentity.id
    platform_base_url: platform_base_url
    keyVaultUrl: keyVaultUrl
    maskinporten_environment: maskinporten_environment
    malwarescan_event_grid_topic_name: eventgrid_topic.name
    userIdentityTenantId: userAssignedIdentity.properties.tenantId
    userIdentityClientId: userAssignedIdentity.properties.clientId
    userIdentityPrincipalId: userAssignedIdentity.properties.principalId
    containerAppEnvId: keyvault.getSecret('container-app-env-id')
  }
}

resource eventgrid_topic 'Microsoft.EventGrid/topics@2022-06-15' = {
  name: '${namePrefix}-malware-scan-event-topic'
  location: location
}

resource eventgrid_event_subscription 'Microsoft.EventGrid/topics/eventSubscriptions@2022-06-15' = {
  name: '${namePrefix}-malware-scan-event-subscription'
  parent: eventgrid_topic
  dependsOn: [containerApp]
  properties: {
    destination: {
      endpointType: 'WebHook'
      properties: {
        endpointUrl: 'https://${containerApp.outputs.app.properties.configuration.ingress.fqdn}/broker/api/v1/webhooks/malwarescanresults'
      }
    }
  }
}

output name string = containerApp.outputs.name
output revisionName string = containerApp.outputs.revisionName
