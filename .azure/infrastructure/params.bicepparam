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
param maskinportenClientId = readEnvironmentVariable('MASKINPORTEN_CLIENT_ID')
param idportenClientId = readEnvironmentVariable('IDPORTEN_CLIENT_ID')
param idportenClientSecret = readEnvironmentVariable('IDPORTEN_CLIENT_SECRET')
param platformSubscriptionKey = readEnvironmentVariable('PLATFORM_SUBSCRIPTION_KEY')
param slackUrl = readEnvironmentVariable('SLACK_URL')
param statisticsApiKey = readEnvironmentVariable('STATISTICS_API_KEY')
param grafanaMonitoringPrincipalId = readEnvironmentVariable('GRAFANA_MONITORING_PRINCIPAL_ID')

// SKUs
param keyVaultSku = {
  name: 'standard'
  family: 'A'
}
