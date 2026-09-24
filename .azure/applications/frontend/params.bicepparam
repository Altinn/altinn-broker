using './main.bicep'

param namePrefix = readEnvironmentVariable('NAME_PREFIX')
param location = 'norwayeast'
param environment = readEnvironmentVariable('ENVIRONMENT')
param deploymentPrincipalObjectId = readEnvironmentVariable('DEPLOYMENT_PRINCIPAL_OBJECT_ID')
param storageAccountName = readEnvironmentVariable('FRONTEND_STORAGE_ACCOUNT')
param apiOriginHostName = readEnvironmentVariable('API_ORIGIN_HOST_NAME', '')
param customDomainName = readEnvironmentVariable('FRONT_DOOR_CUSTOM_DOMAIN_NAME', '')
