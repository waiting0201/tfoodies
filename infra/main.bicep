// TFoodies infra — Azure Functions (Flex Consumption) + Container App (前台 Nuxt SSR) +
// Static Web App (後台 admin SPA)。
// NOTE: Azure SQL Database 不在此建立，連線字串透過 Function App 的 App Settings 手動設定。
//
// 前台 store 已從 Static Web App 改為 Container App（完整 SSR for SEO）。Bicep 只建立資源並
// 帶 placeholder image；實際映像由 .github/workflows/store.yml 以 `az acr build` + `az
// containerapp update` 部署。

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
        // AzureBlob:ConnectionString / ContainerName / BaseUrl 於 Portal 手動設定
      ]
    }
  }
  dependsOn: [deploymentContainer]
}

// ---------- 前台 store：Container Registry + Container App (Nuxt SSR) ----------
param siteUrl string = 'https://www.tfoodies.com'

// CI 會以實際映像覆蓋；首次建立用公開 placeholder 以避免 image-not-found。
var storePlaceholderImage = 'mcr.microsoft.com/azuredocs/containerapps-helloworld:latest'

resource acr 'Microsoft.ContainerRegistry/registries@2023-07-01' = {
  name: toLower('tfoodiesacr${env}${take(suffix, 8)}')
  location: location
  tags: tags
  sku: { name: 'Basic' }
  properties: {
    adminUserEnabled: true
  }
}

resource caEnv 'Microsoft.App/managedEnvironments@2024-03-01' = {
  name: 'tfoodies-cae-${env}'
  location: location
  tags: tags
  properties: {}
}

resource storeApp 'Microsoft.App/containerApps@2024-03-01' = {
  name: 'tfoodies-store'
  location: location
  tags: tags
  properties: {
    managedEnvironmentId: caEnv.id
    configuration: {
      ingress: {
        external: true
        targetPort: 3000
        transport: 'auto'
      }
      registries: [
        {
          server: acr.properties.loginServer
          username: acr.listCredentials().username
          passwordSecretRef: 'acr-password'
        }
      ]
      secrets: [
        {
          name: 'acr-password'
          value: acr.listCredentials().passwords[0].value
        }
      ]
    }
    template: {
      containers: [
        {
          name: 'store'
          image: storePlaceholderImage
          resources: {
            cpu: json('0.25')
            memory: '0.5Gi'
          }
          env: [
            { name: 'NUXT_PUBLIC_API_BASE', value: 'https://${functionApp.properties.defaultHostName}/api' }
            { name: 'NUXT_PUBLIC_SITE_URL', value: siteUrl }
          ]
        }
      ]
      scale: {
        minReplicas: 0   // scale-to-zero（最省成本；若冷啟動影響 SEO 改 1）
        maxReplicas: 3
      }
    }
  }
}

// ---------- 後台 admin：Static Web App ----------
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
output acrName string = acr.name
output acrLoginServer string = acr.properties.loginServer
output storeFqdn string = storeApp.properties.configuration.ingress.fqdn
output adminHostname string = swaAdmin.properties.defaultHostname
