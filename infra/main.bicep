// TFoodies infra — Azure Functions (Flex Consumption) + Static Web Apps ×2
// NOTE: Azure SQL Database 不在此建立，連線字串透過 Function App 的 App Settings 手動設定。

targetScope = 'resourceGroup'

@allowed(['dev', 'staging', 'prod'])
param env string = 'dev'

param location string = resourceGroup().location

var tags = {
  app: 'tfoodies'
  env: env
}
var suffix = uniqueString(resourceGroup().id, env)
var saName = toLower('tfoodiesfn${env}${take(suffix, 8)}')
var storageConnectionString = 'DefaultEndpointsProtocol=https;AccountName=${storage.name};EndpointSuffix=${environment().suffixes.storage};AccountKey=${storage.listKeys().keys[0].value}'

// ---------- Storage (Functions runtime + deployment 必需) ----------
resource storage 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: saName
  location: location
  tags: tags
  sku: { name: 'Standard_LRS' }
  kind: 'StorageV2'
  properties: {
    minimumTlsVersion: 'TLS1_2'
    allowBlobPublicAccess: false
    supportsHttpsTrafficOnly: true
  }
}

// Flex Consumption 需要一個 blob container 存放部署套件
resource deploymentContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = {
  name: '${storage.name}/default/deployments'
  properties: {
    publicAccess: 'None'
  }
}

// ---------- Functions host (Flex Consumption) ----------
resource functionApp 'Microsoft.Web/sites@2023-12-01' = {
  name: 'tfoodies-api'
  location: location
  tags: tags
  kind: 'functionapp,linux'
  properties: {
    httpsOnly: true
    functionAppConfig: {
      deployment: {
        storage: {
          type: 'blobContainer'
          value: '${storage.properties.primaryEndpoints.blob}deployments'
          authentication: {
            type: 'StorageAccountConnectionString'
            storageAccountConnectionStringName: 'DEPLOYMENT_STORAGE_CONNECTION_STRING'
          }
        }
      }
      scaleAndConcurrency: {
        maximumInstanceCount: 100
        instanceMemoryMB: 2048
      }
      runtime: {
        name: 'dotnet-isolated'
        version: '10.0'
      }
    }
    siteConfig: {
      ftpsState: 'Disabled'
      minTlsVersion: '1.2'
      appSettings: [
        { name: 'AzureWebJobsStorage', value: storageConnectionString }
        { name: 'DEPLOYMENT_STORAGE_CONNECTION_STRING', value: storageConnectionString }
      ]
    }
  }
  dependsOn: [deploymentContainer]
}

// ---------- Static Web Apps ----------
resource swaStore 'Microsoft.Web/staticSites@2023-12-01' = {
  name: 'tfoodies-store'
  location: location
  tags: tags
  sku: { name: 'Free', tier: 'Free' }
  properties: {
    allowConfigFileUpdates: true
  }
}

resource swaAdmin 'Microsoft.Web/staticSites@2023-12-01' = {
  name: 'tfoodies-admin'
  location: location
  tags: tags
  sku: { name: 'Free', tier: 'Free' }
  properties: {
    allowConfigFileUpdates: true
  }
}

output functionAppName string = functionApp.name
output functionAppHostname string = functionApp.properties.defaultHostName
output storeHostname string = swaStore.properties.defaultHostname
output adminHostname string = swaAdmin.properties.defaultHostname
