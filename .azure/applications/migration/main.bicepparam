using './main.bicep'

param location = 'norwayeast'
param keyVaultName = readEnvironmentVariable('KEY_VAULT_NAME')
param keyVaultUrl = readEnvironmentVariable('KEY_VAULT_URL')
param namePrefix = readEnvironmentVariable('NAME_PREFIX')
