using './main.bicep'

param namePrefix = readEnvironmentVariable('NAME_PREFIX')
param location = 'norwayeast'
param environment = readEnvironmentVariable('ENVIRONMENT')
param existingBackupPolicyName = readEnvironmentVariable('EXISTING_BACKUP_POLICY_NAME')

// secrets
param tenantId = readEnvironmentVariable('TENANT_ID')
param test_client_id = readEnvironmentVariable('TEST_CLIENT_ID')
param sourceKeyVaultName = readEnvironmentVariable('KEY_VAULT_NAME')
param migrationsStorageAccountName = readEnvironmentVariable('MIGRATION_STORAGE_ACCOUNT_NAME')
param backupStorageAccountName = readEnvironmentVariable('BACKUP_STORAGE_ACCOUNT_NAME')
param maskinportenJwk = readEnvironmentVariable('MASKINPORTEN_JWK')
param maskinportenClientId = readEnvironmentVariable('MASKINPORTEN_CLIENT_ID')
param platformSubscriptionKey = readEnvironmentVariable('PLATFORM_SUBSCRIPTION_KEY')
param slackUrl = readEnvironmentVariable('SLACK_URL')
param statisticsApiKey = readEnvironmentVariable('STATISTICS_API_KEY')
param grafanaMonitoringPrincipalId = readEnvironmentVariable('GRAFANA_MONITORING_PRINCIPAL_ID')
param brokerDbReadAdGroupId = readEnvironmentVariable('BROKER_DB_READ_AD_GROUP_ID')
param brokerDbReadAdGroupName = readEnvironmentVariable('BROKER_DB_READ_AD_GROUP_NAME')
param brokerDbWriteAdGroupId = readEnvironmentVariable('BROKER_DB_WRITE_AD_GROUP_ID')
param brokerDbWriteAdGroupName = readEnvironmentVariable('BROKER_DB_WRITE_AD_GROUP_NAME')

// SKUs
param keyVaultSku = {
  name: 'standard'
  family: 'A'
}
