// TFoodies infra — Azure Functions (Flex Consumption) + Container App (前台 Nuxt SSR) +
// Static Web App (後台 admin SPA)。
// NOTE: Azure SQL Database 不在此建立；其連線字串與其他必需 App Settings（Jwt/AzureBlob/Sms…）
//       由本範本的參數帶入（秘密走 @secure() 參數 + GitHub Secrets），不要再到 Portal 手動設定，
//       否則下次部署會被整包覆蓋而消失。
//
// 前台 store 已從 Static Web App 改為 Container App（完整 SSR for SEO）。Bicep 只建立資源並
// 帶 placeholder image；實際映像由 .github/workflows/store.yml 以 `az acr build` + `az
// containerapp update` 部署。

targetScope = 'resourceGroup'

@allowed(['dev', 'staging', 'prod'])
param env string = 'dev'

param location string = resourceGroup().location

// ---------- Function App 設定（由 infra.yml 從 GitHub Secrets/Variables 傳入） ----------
// ⚠️ Microsoft.Web/sites 的 appSettings 是「整包覆蓋」：bicep 沒列的設定，部署當下會被刪除。
//    因此所有 Function App 必需的 App Settings 都要在此宣告，否則每次 infra 部署都會把
//    Portal 手動設的值清空（曾導致 JwtTokenService 因 Jwt:Secret 為空而啟動 500）。
//    秘密以 @secure() 參數帶入，不在範本內留明文。

@secure()
@description('JWT HMAC 簽章金鑰（≥32 字元）。缺值會導致 API 啟動即 500。')
param jwtSecret string

@secure()
@description('Azure SQL（tfoodies）連線字串。')
param sqlConnectionString string

@secure()
@description('圖檔 Azure Blob 儲存體連線字串。')
param blobConnectionString string

@secure()
@description('Application Insights 連線字串（選填；空字串=不接 telemetry）。')
param appInsightsConnectionString string = ''

@secure()
@description('三竹簡訊密碼（選填）。')
param smsPassword string = ''

param jwtIssuer string = 'tfoodies'
param jwtAudience string = 'tfoodies'
param blobContainerName string = 'tfoodies'
@description('圖檔 Blob 對外基底網址，例如 https://<account>.blob.core.windows.net/tfoodies')
param blobBaseUrl string = ''
param smsUsername string = ''
param smsApiUrl string = 'https://sms.mitake.com.tw/b2c/mtk/SmSend'
param jwtExpiryMinutes string = '60'
param jwtRefreshExpiryDays string = '30'
param orderFreightLimit string = '2000'
param orderFreightAmount string = '120'
param orderAtmExpiryDays string = '3'
param orderAtmPrefix string = '1943'

// FISC 金流（尚未啟用；有正式金鑰時填對應 GitHub Secret/Variable）
param fiscBaseUrl string = 'https://www.focas.fisc.com.tw/FOCAS_WS/API20/V1/FISCII'
param fiscMerchantId string = ''
param fiscTerminalId string = ''
param fiscAcqBank string = ''
@secure()
param fiscVerificationKeyHex string = ''
@secure()
param fiscFieldKeyHex string = ''

// ezPay 電子發票（尚未啟用；有正式金鑰時填對應 GitHub Secret/Variable）
param ezpayBaseUrl string = 'https://inv.ezpay.com.tw/Api'
param ezpayMerchantId string = ''
@secure()
param ezpayHashKey string = ''
@secure()
param ezpayHashIV string = ''

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
        // Functions runtime / 部署（storage 由本範本管理）
        { name: 'AzureWebJobsStorage', value: storageConnectionString }
        { name: 'DEPLOYMENT_STORAGE_CONNECTION_STRING', value: storageConnectionString }
        { name: 'APPLICATIONINSIGHTS_CONNECTION_STRING', value: appInsightsConnectionString }
        // Auth（缺 Jwt__Secret 會啟動即 500）
        { name: 'Jwt__Secret', value: jwtSecret }
        { name: 'Jwt__Issuer', value: jwtIssuer }
        { name: 'Jwt__Audience', value: jwtAudience }
        { name: 'Jwt__ExpiryMinutes', value: jwtExpiryMinutes }
        { name: 'Jwt__RefreshExpiryDays', value: jwtRefreshExpiryDays }
        // 資料庫（Dapper + EF Core 共用）
        { name: 'ConnectionStrings__Tfoodies', value: sqlConnectionString }
        // 圖檔 Blob
        { name: 'AzureBlob__ConnectionString', value: blobConnectionString }
        { name: 'AzureBlob__ContainerName', value: blobContainerName }
        { name: 'AzureBlob__BaseUrl', value: blobBaseUrl }
        // 訂單 / 運費 / ATM
        { name: 'Order__FreightLimit', value: orderFreightLimit }
        { name: 'Order__FreightAmount', value: orderFreightAmount }
        { name: 'Order__AtmExpiryDays', value: orderAtmExpiryDays }
        { name: 'Order__AtmPrefix', value: orderAtmPrefix }
        // 三竹簡訊
        { name: 'Sms__Username', value: smsUsername }
        { name: 'Sms__Password', value: smsPassword }
        { name: 'Sms__ApiUrl', value: smsApiUrl }
        // FISC 金流
        { name: 'Fisc__BaseUrl', value: fiscBaseUrl }
        { name: 'Fisc__MerchantId', value: fiscMerchantId }
        { name: 'Fisc__TerminalId', value: fiscTerminalId }
        { name: 'Fisc__AcqBank', value: fiscAcqBank }
        { name: 'Fisc__VerificationKeyHex', value: fiscVerificationKeyHex }
        { name: 'Fisc__FieldKeyHex', value: fiscFieldKeyHex }
        // ezPay 電子發票
        { name: 'EzPay__BaseUrl', value: ezpayBaseUrl }
        { name: 'EzPay__MerchantId', value: ezpayMerchantId }
        { name: 'EzPay__HashKey', value: ezpayHashKey }
        { name: 'EzPay__HashIV', value: ezpayHashIV }
      ]
    }
  }
  dependsOn: [deploymentContainer]
}

// ---------- 前台 store：Container Registry + Container App (Nuxt SSR) ----------
param siteUrl string = 'https://www.tfoodies.com'

// CI 會以實際映像覆蓋；首次建立用公開 placeholder 以避免 image-not-found。
var storePlaceholderImage = 'mcr.microsoft.com/azuredocs/containerapps-helloworld:latest'

// 圖檔對外基底：與後端 / admin 共用同一組 blobBaseUrl + blobContainerName 參數
// （皆源自 GitHub 變數 BLOB_BASE_URL / BLOB_CONTAINER）。store 需要「合併且結尾帶 /」的形狀
// （Nuxt 直接以 blobUrl + 檔名 組圖），故在此組合。blobBaseUrl 為空時退回正式帳號，避免 bootstrap 部署壞掉。
var storeBlobPublicBase = empty(blobBaseUrl) ? 'https://tfoodiesblob.blob.core.windows.net' : blobBaseUrl
var storeBlobPublicUrl = '${storeBlobPublicBase}/${blobContainerName}/'

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
            { name: 'NUXT_PUBLIC_BLOB_URL', value: storeBlobPublicUrl }
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
