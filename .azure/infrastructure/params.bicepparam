using './main.bicep'

param namePrefix = readEnvironmentVariable('NAME_PREFIX')
param location = 'norwayeast'
param environment = readEnvironmentVariable('ENVIRONMENT')

// secrets
param brokerPgAdminPassword = readEnvironmentVariable('BROKER_PG_ADMIN_PASSWORD')
param tenantId = readEnvironmentVariable('TENANT_ID')
param object_id = readEnvironmentVariable('CLIENT_ID')
param test_client_id = readEnvironmentVariable('TEST_CLIENT_ID')
param sourceKeyVaultName = readEnvironmentVariable('KEY_VAULT_NAME')
param migrationsStorageAccountName = readEnvironmentVariable('MIGRATION_STORAGE_ACCOUNT_NAME')
param deploySecret = readEnvironmentVariable('CLIENT_SECRET')
param maskinportenJwk = readEnvironmentVariable('MASKINPORTEN_JWK')
param maskinportenClientId = readEnvironmentVariable('MASKINPORTEN_CLIENT_ID')
param platformSubscriptionKey = readEnvironmentVariable('PLATFORM_SUBSCRIPTION_KEY')
param notificationEmail = readEnvironmentVariable('NOTIFICATION_EMAIL')

// SKUs
param keyVaultSku = {
  name: 'standard'
  family: 'A'
}
param postgresSku = {
  name: readEnvironmentVariable('ENVIRONMENT') == 'test'
    ? 'Standard_B1ms'
    : readEnvironmentVariable('ENVIRONMENT') == 'staging' ? 'Standard_B2s' : 'Standard_D2ads_v5'
  tier: readEnvironmentVariable('ENVIRONMENT') == 'production' ? 'GeneralPurpose' : 'Burstable'
}
