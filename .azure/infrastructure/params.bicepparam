using './main.bicep'

param namePrefix = readEnvironmentVariable('NAME_PREFIX')
param location = 'norwayeast'
param environment = readEnvironmentVariable('ENVIRONMENT')

// secrets
param brokerPgAdminPassword = readEnvironmentVariable('BROKER_PG_ADMIN_PASSWORD')
param tenantId = readEnvironmentVariable('TENANT_ID')
param test_client_id = readEnvironmentVariable('TEST_CLIENT_ID')
param sourceKeyVaultName = readEnvironmentVariable('KEY_VAULT_NAME')
param migrationsStorageAccountName = readEnvironmentVariable('MIGRATION_STORAGE_ACCOUNT_NAME')
param maskinportenJwk = readEnvironmentVariable('MASKINPORTEN_JWK')
param maskinportenClientId = readEnvironmentVariable('MASKINPORTEN_CLIENT_ID')
param platformSubscriptionKey = readEnvironmentVariable('PLATFORM_SUBSCRIPTION_KEY')
param slackUrl = readEnvironmentVariable('SLACK_URL')
param statisticsApiKey = readEnvironmentVariable('STATISTICS_API_KEY')

// SKUs
param keyVaultSku = {
  name: 'standard'
  family: 'A'
}
