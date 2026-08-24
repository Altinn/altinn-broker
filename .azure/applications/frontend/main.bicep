targetScope = 'subscription'

@minLength(3)
param location string

@minLength(3)
param environment string

@secure()
@minLength(3)
param namePrefix string

@description('Object ID of the GitHub Actions deployment service principal.')
param deploymentPrincipalObjectId string

@secure()
@description('Globally unique storage account name (3-24 lowercase alphanumeric characters).')
@minLength(3)
@maxLength(24)
param storageAccountName string

@description('APIM gateway hostname (e.g. altinn-dev-api.azure-api.net). When set, Front Door forwards /broker/* to APIM.')
param apiOriginHostName string = ''

var resourceGroupName = '${namePrefix}-rg'
var frontDoorProfileName = '${namePrefix}-frontend-fd'

resource resourceGroup 'Microsoft.Resources/resourceGroups@2024-03-01' existing = {
  name: resourceGroupName
}

module staticWebsite '../../modules/staticWebsite/create.bicep' = {
  scope: resourceGroup
  name: 'frontend-static-website'
  params: {
    storageAccountName: storageAccountName
    location: location
  }
}

module frontDoor '../../modules/frontDoor/create.bicep' = {
  scope: resourceGroup
  name: 'frontend-front-door'
  params: {
    frontDoorProfileName: frontDoorProfileName
    originHostName: staticWebsite.outputs.staticWebsiteHostName
    apiOriginHostName: apiOriginHostName
    // Must stay 'default' — existing hostname is default-{hash}.azurefd.net.
    // A different endpoint name creates a second unused endpoint without /broker routing.
    endpointName: 'default'
  }
}

module deploymentStorageAccess '../../modules/storageAccount/addBlobContributorRole.bicep' = if (!empty(deploymentPrincipalObjectId)) {
  scope: resourceGroup
  name: 'frontend-deploy-storage-access'
  params: {
    storageAccountName: staticWebsite.outputs.storageAccountName
    principalObjectId: deploymentPrincipalObjectId
  }
}

output staticWebsiteHostName string = staticWebsite.outputs.staticWebsiteHostName
output frontDoorProfileName string = frontDoor.outputs.profileName
output frontDoorEndpointName string = frontDoor.outputs.endpointName
output frontDoorEndpointHostName string = frontDoor.outputs.endpointHostName
output resourceGroupName string = resourceGroupName
output environment string = environment
