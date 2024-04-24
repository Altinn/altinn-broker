param location string
param name string
param image string
param containerAppEnvId string
param command string[]
param environmentVariables { name: string, value: string?, secretRef: string? }[] = []
param secrets { name: string, keyVaultUrl: string, identity: string }[] = []
param volumes { name: string, storageName: string, storageType: string, mountOptions: string}[] = []
param volumeMounts { mountPath: string, subPath: string, volumeName: string }[] = []
param principalId string

resource job 'Microsoft.App/jobs@2023-11-02-preview' = {
  name: name
  location: location
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${principalId}': {}
    }
  }
  properties: {
    configuration: {
      secrets: secrets
      manualTriggerConfig: {
        parallelism: 1
        replicaCompletionCount: 1
      }
      replicaRetryLimit: 1
      replicaTimeout: 120
      triggerType: 'Manual'
    }
    environmentId: containerAppEnvId
    template: {
      containers: [
        {
          env: environmentVariables
          image: image
          name: name
          command: command
          volumeMounts: volumeMounts
        }
      ]
      volumes: volumes
    }
  }
}

output name string = job.name
