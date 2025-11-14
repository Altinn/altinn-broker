param location string
@secure()
param namePrefix string
@secure()
param keyVaultName string
param migrationsStorageAccountName string

resource log_analytics_workspace 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: '${namePrefix}-log'
  location: location
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30
  }
}

resource application_insights 'Microsoft.Insights/components@2020-02-02' = {
  name: '${namePrefix}-ai'
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: log_analytics_workspace.id
    DisableIpMasking: true
  }
}
resource containerAppEnvironment 'Microsoft.App/managedEnvironments@2024-03-01' = {
  name: '${namePrefix}-env'
  location: location
  properties: {
    infrastructureResourceGroup: '${namePrefix}-rg'
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: log_analytics_workspace.properties.customerId
        sharedKey: log_analytics_workspace.listKeys().primarySharedKey
      }
    }
  }
}


resource storageAccount 'Microsoft.Storage/storageAccounts@2023-05-01' existing = {
  name: migrationsStorageAccountName
}

resource containerAppEnvironmentStorage 'Microsoft.App/managedEnvironments/storages@2024-03-01' = {
  name: 'migrations'
  parent: containerAppEnvironment
  properties: {
    azureFile: {
      accessMode: 'ReadOnly'
      accountKey: storageAccount.listKeys().keys[0].value
      accountName: migrationsStorageAccountName
      shareName: 'migrations'
    }
  }
}

var applicationInsightsSecretName = 'application-insights-connection-string'
module applicationInsightsConnectionStringSecret '../keyvault/upsertSecret.bicep' = {
  name: applicationInsightsSecretName
  params: {
    destKeyVaultName: keyVaultName
    secretName: applicationInsightsSecretName
    secretValue: application_insights.properties.ConnectionString
  }
}

var containerAppEnvironmentIdSecretName = 'container-app-env-id'
module containerAppEnvIdSecret '../keyvault/upsertSecret.bicep' = {
  name: containerAppEnvironmentIdSecretName
  params: {
    destKeyVaultName: keyVaultName
    secretName: containerAppEnvironmentIdSecretName
    secretValue: containerAppEnvironment.id
  }
}

var storageAccountConnectionStringSecretName = 'storage-connection-string'
module storageAccountConnectionStringSecret '../keyvault/upsertSecret.bicep' = {
  name: storageAccountConnectionStringSecretName
  params: {
    destKeyVaultName: keyVaultName
    secretName: storageAccountConnectionStringSecretName
    secretValue: 'DefaultEndpointsProtocol=https;AccountName=${migrationsStorageAccountName};AccountKey=${storageAccount.listKeys().keys[0].value};EndpointSuffix=core.windows.net'
  }
}

output containerAppEnvironmentId string = containerAppEnvironment.id
