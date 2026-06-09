// TFoodies infra — single-file Bicep (modularise later per plan §部署).
// Resources: Static Web Apps ×2 (store/admin), Functions (Elastic Premium EP1) + storage,
// App Insights + Log Analytics, Key Vault (RBAC), App Configuration, Redis, Cosmos (Mongo).
// NOTE: the frozen Azure SQL is NOT created here — it is referenced via Key Vault secret.

targetScope = 'resourceGroup'

@description('Short prefix for resource names, e.g. tfoodies')
param namePrefix string = 'tfoodies'

@allowed(['dev', 'staging', 'prod'])
param env string = 'dev'

param location string = resourceGroup().location

var tags = {
  app: 'tfoodies'
  env: env
}
var suffix = uniqueString(resourceGroup().id, env)
var fnName = '${namePrefix}-fn-${env}-${suffix}'
var planName = '${namePrefix}-plan-${env}'
var saName = toLower('${namePrefix}fn${env}${take(suffix, 8)}')
var kvName = toLower('${namePrefix}-kv-${env}-${take(suffix, 6)}')
var acName = '${namePrefix}-appcfg-${env}-${take(suffix, 6)}'
var redisName = '${namePrefix}-redis-${env}-${take(suffix, 6)}'
var cosmosName = toLower('${namePrefix}-mongo-${env}-${take(suffix, 6)}')
var lawName = '${namePrefix}-law-${env}'
var aiName = '${namePrefix}-ai-${env}'

// ---------- Observability ----------
resource law 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: lawName
  location: location
  tags: tags
  properties: {
    sku: { name: 'PerGB2018' }
    retentionInDays: 30
  }
}

resource ai 'Microsoft.Insights/components@2020-02-02' = {
  name: aiName
  location: location
  tags: tags
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: law.id
  }
}

// ---------- Storage (Functions runtime + queues/tables) ----------
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

// ---------- Functions host (Elastic Premium EP1) ----------
resource plan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: planName
  location: location
  tags: tags
  sku: { name: 'EP1', tier: 'ElasticPremium' }
  kind: 'elastic'
  properties: {
    reserved: true // Linux
    maximumElasticWorkerCount: 5
  }
}

resource functionApp 'Microsoft.Web/sites@2023-12-01' = {
  name: fnName
  location: location
  tags: tags
  kind: 'functionapp,linux'
  identity: { type: 'SystemAssigned' }
  properties: {
    serverFarmId: plan.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: 'DOTNET-ISOLATED|10.0'
      ftpsState: 'Disabled'
      minTlsVersion: '1.2'
      use32BitWorkerProcess: false
      appSettings: [
        { name: 'FUNCTIONS_EXTENSION_VERSION', value: '~4' }
        { name: 'FUNCTIONS_WORKER_RUNTIME', value: 'dotnet-isolated' }
        { name: 'AzureWebJobsStorage', value: 'DefaultEndpointsProtocol=https;AccountName=${storage.name};EndpointSuffix=${environment().suffixes.storage};AccountKey=${storage.listKeys().keys[0].value}' }
        { name: 'APPLICATIONINSIGHTS_CONNECTION_STRING', value: ai.properties.ConnectionString }
        { name: 'AppConfig__Endpoint', value: appConfig.properties.endpoint }
      ]
    }
  }
}

// ---------- Key Vault (RBAC) ----------
resource kv 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: kvName
  location: location
  tags: tags
  properties: {
    sku: { family: 'A', name: 'standard' }
    tenantId: subscription().tenantId
    enableRbacAuthorization: true
    enableSoftDelete: true
  }
}

// Function App MI → Key Vault Secrets User
resource kvSecretsUser 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(kv.id, functionApp.id, 'kv-secrets-user')
  scope: kv
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '4633458b-17de-408a-b874-0445c86b69e6')
    principalId: functionApp.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

// ---------- App Configuration ----------
resource appConfig 'Microsoft.AppConfiguration/configurationStores@2023-03-01' = {
  name: acName
  location: location
  tags: tags
  sku: { name: 'standard' }
  properties: {}
}

// ---------- Redis (refresh tokens / cart / idempotency / cache) ----------
resource redis 'Microsoft.Cache/redis@2023-08-01' = {
  name: redisName
  location: location
  tags: tags
  properties: {
    sku: { name: 'Basic', family: 'C', capacity: 0 }
    minimumTlsVersion: '1.2'
    enableNonSslPort: false
  }
}

// ---------- Cosmos DB (Mongo API) for viewlogs ----------
resource cosmos 'Microsoft.DocumentDB/databaseAccounts@2024-05-15' = {
  name: cosmosName
  location: location
  tags: tags
  kind: 'MongoDB'
  properties: {
    databaseAccountOfferType: 'Standard'
    apiProperties: { serverVersion: '4.2' }
    capabilities: [ { name: 'EnableMongo' } ]
    locations: [ { locationName: location, failoverPriority: 0 } ]
  }
}

// ---------- Static Web Apps (store SSR + admin SPA) ----------
resource swaStore 'Microsoft.Web/staticSites@2023-12-01' = {
  name: '${namePrefix}-store-${env}'
  location: location
  tags: tags
  sku: { name: 'Standard', tier: 'Standard' }
  properties: {
    allowConfigFileUpdates: true
  }
}

resource swaAdmin 'Microsoft.Web/staticSites@2023-12-01' = {
  name: '${namePrefix}-admin-${env}'
  location: location
  tags: tags
  sku: { name: 'Standard', tier: 'Standard' }
  properties: {
    allowConfigFileUpdates: true
  }
}

// Link the Functions app as the store SWA backend (so /api/* is same-origin).
resource swaStoreBackend 'Microsoft.Web/staticSites/linkedBackends@2023-12-01' = {
  parent: swaStore
  name: 'api'
  properties: {
    backendResourceId: functionApp.id
    region: location
  }
}

// Link the Functions app as the admin SWA backend (so /api/* is same-origin).
resource swaAdminBackend 'Microsoft.Web/staticSites/linkedBackends@2023-12-01' = {
  parent: swaAdmin
  name: 'api'
  properties: {
    backendResourceId: functionApp.id
    region: location
  }
}

output functionAppName string = functionApp.name
output functionAppHostname string = functionApp.properties.defaultHostName
output storeHostname string = swaStore.properties.defaultHostname
output adminHostname string = swaAdmin.properties.defaultHostname
output keyVaultName string = kv.name
output appConfigEndpoint string = appConfig.properties.endpoint
