using './main.bicep'

param namePrefix = readEnvironmentVariable('NAME_PREFIX')
param location = 'norwayeast'
param imageTag = readEnvironmentVariable('IMAGE_TAG')
param platform_base_url = readEnvironmentVariable('PLATFORM_BASE_URL')
param frontend_base_url = readEnvironmentVariable('FRONTEND_BASE_URL')
param maskinporten_environment = readEnvironmentVariable('MASKINPORTEN_ENVIRONMENT')
param environment = readEnvironmentVariable('ENVIRONMENT')
param apimIp = readEnvironmentVariable('APIM_IP')
param maskinportenTokenExchangeEnvironment = readEnvironmentVariable('MASKINPORTEN_TOKEN_EXCHANGE_ENVIRONMENT')

// secrets
param sourceKeyVaultName = readEnvironmentVariable('KEY_VAULT_NAME')
param keyVaultUrl = readEnvironmentVariable('KEY_VAULT_URL')
