param location string
@secure()
param namePrefix string
@secure()
param keyVaultName string
@secure()
param emailReceiver string
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
  }
}
resource containerAppEnvironment 'Microsoft.App/managedEnvironments@2023-11-02-preview' = {
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
resource application_insights_action 'Microsoft.Insights/actionGroups@2023-01-01' =
  if (emailReceiver != null && emailReceiver != '') {
    name: '${namePrefix}-action'
    location: 'global' // action group locations is limited, change to use location variable when new locations is added
    dependsOn: [application_insights, containerAppEnvironment]
    properties: {
      groupShortName: 'broker-alert'
      enabled: true
      emailReceivers: [
        {
          name: 'emailReceiverForAlert'
          emailAddress: emailReceiver
        }
      ]
    }
  }
resource exceptionOccuredAlertRule 'Microsoft.Insights/scheduledQueryRules@2023-03-15-preview' =
  if (emailReceiver != null && emailReceiver != '') {
    name: '${namePrefix}-500-exception-occured'
    location: location
    properties: {
      description: 'Alert for 500 errors in broker'
      enabled: true
      severity: 1
      evaluationFrequency: 'PT5M'
      windowSize: 'PT5M'
      scopes: [log_analytics_workspace.id]
      autoMitigate: false
      targetResourceTypes: [
        'microsoft.insights/components'
      ]
      criteria: {
        allOf: [
          {
            query: 'AppExceptions | where Properties.StatusCode startswith "5" '
            operator: 'GreaterThan'
            threshold: 0
            timeAggregation: 'Count'
            failingPeriods: {
              numberOfEvaluationPeriods: 1
              minFailingPeriodsToAlert: 1
            }
          }
        ]
      }
      actions: {
        actionGroups: [
          application_insights_action.id
        ]
      }
    }
  }

resource storageAccount 'Microsoft.Storage/storageAccounts@2023-04-01' existing = {
  name: migrationsStorageAccountName
}

resource containerAppEnvironmentStorage 'Microsoft.App/managedEnvironments/storages@2023-11-02-preview' = {
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

output containerAppEnvironmentId string = containerAppEnvironment.id
