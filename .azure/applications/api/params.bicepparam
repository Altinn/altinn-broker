using './main.bicep'

param namePrefix = readEnvironmentVariable('NAME_PREFIX')
param location = 'norwayeast'
param imageTag = readEnvironmentVariable('IMAGE_TAG')
param platform_base_url = readEnvironmentVariable('PLATFORM_BASE_URL')
param maskinporten_environment = 'test'
param environment = readEnvironmentVariable('ENVIRONMENT')
param apimIp = (environment == 'test' ? '51.120.88.69' : environment == 'staging' ? 'IP_FOR_STAGING' : environment == 'production' ? 'IP_FOR_PROD' : 'DEFAULT_IP')

// secrets
param sourceKeyVaultName = readEnvironmentVariable('KEY_VAULT_NAME')
param keyVaultUrl = readEnvironmentVariable('KEY_VAULT_URL')
