// TFoodies infra — Azure Functions + Static Web Apps ×2
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

// ---------- Storage (Functions runtime 必需) ----------
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

// ---------- Functions host (Flex Consumption) ----------
resource functionApp 'Microsoft.Web/sites@2023-12-01' = {
  name: 'tfoodies-api'
  location: location
  tags: tags
  kind: 'functionapp,linux'
  properties: {
    httpsOnly: true
    siteConfig: {
      ftpsState: 'Disabled'
      minTlsVersion: '1.2'
      appSettings: [
        { name: 'AzureWebJobsStorage', value: 'DefaultEndpointsProtocol=https;AccountName=${storage.name};EndpointSuffix=${environment().suffixes.storage};AccountKey=${storage.listKeys().keys[0].value}' }
        { name: 'WEBSITE_RUN_FROM_PACKAGE', value: '1' }
      ]
    }
  }
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
