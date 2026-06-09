# TFoodies 食在呼 — 開發環境啟動指南

食品電商前台（Nuxt 4 SSR）+ 後台 ERP（Vue 3 SPA）+ API（.NET 9 Azure Functions）。

---

## 環境需求

| 工具 | 版本 | 用途 |
|---|---|---|
| .NET SDK | 9.0.200（`global.json` pin） | API + 測試 |
| Node.js | 24+ | 前台 / 後台 |
| Azure Functions Core Tools | 4.x (`npm i -g azure-functions-core-tools@4`) | 本地跑 Functions |
| Azurite | 最新 (`npm i -g azurite`) | 本地模擬 Azure Storage |
| SQL Server | 2019+ 或 Azure SQL | 資料庫（需 `tfoodies` DB） |

---

## 1. 第一次設定

### 1-1. 複製設定檔

```bash
cp appsettings.example.json src/TFoodies.Api.Functions/appsettings.Development.json
```

編輯 `appsettings.Development.json`，填入實際值：

```jsonc
{
  "ConnectionStrings": {
    // 指向本地或遠端 SQL Server，需有 tfoodies 資料庫
    "Tfoodies": "Server=localhost;Database=tfoodies;User Id=sa;Password=YOUR_PASSWORD;TrustServerCertificate=True"
  },
  "Jwt": {
    // 至少 32 個字元的隨機字串
    "SecretKey": "your-super-secret-key-at-least-32-chars",
    "Issuer": "tfoodies",
    "Audience": "tfoodies",
    "ExpiryMinutes": 60,
    "RefreshExpiryDays": 30
  },
  "Order": {
    "FreightLimit": 2000,
    "FreightAmount": 120,
    "AtmExpiryDays": 3,
    "AtmPrefix": "1943"
  }
  // Fisc / EzPay / Sms / AzureBlob 在本地開發可留 REPLACE，不影響基本功能
}
```

`local.settings.json`（已存在）負責 Functions runtime 設定，通常不需修改：
```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated"
  },
  "ConnectionStrings": {
    "Tfoodies": "..."
  }
}
```

### 1-2. Scaffold 資料庫實體（需 DB 連線）

```bash
# 設定連線字串後執行，產生 src/TFoodies.Infrastructure/Persistence/Scaffolded/（72 表）
TFOODIES_CONNSTRING='Server=localhost;Database=tfoodies;User Id=sa;Password=YOUR_PASSWORD;TrustServerCertificate=True' \
  ./scripts/scaffold-db.sh
```

### 1-3. 安裝前端相依

```bash
cd web && npm install
```

---

## 2. 日常開發啟動（三個終端機）

### 終端機 A — Azurite（Azure Storage 模擬器）

```bash
azurite --silent --location /tmp/azurite
```

### 終端機 B — API（Azure Functions）

```bash
cd src/TFoodies.Api.Functions
func start
# 監聽 http://localhost:7071
```

健康檢查：
```bash
curl http://localhost:7071/api/health
# → {"status":"ok"}
```

### 終端機 C — 前台（Nuxt SSR）

```bash
cd web
npm run dev:store
# 監聽 http://localhost:3000
```

### 終端機 D — 後台 SPA（可選）

```bash
cd web
npm run dev:admin
# 監聽 http://localhost:5173
```

後台預設登入：需有 `tfoodies.Admins` 資料表的帳號（舊系統測試帳：`itadmin` / `QQQQQ`，新系統已移除後門）。

---

## 3. 環境變數（前端）

| 檔案 | 變數 | 預設值 | 說明 |
|---|---|---|---|
| `web/store/.env` | `NUXT_PUBLIC_API_BASE` | `http://localhost:7071/api` | Nuxt store 呼叫的 API base |
| `web/admin/.env` | `VITE_API_BASE` | `http://localhost:7071/api` | Admin SPA 呼叫的 API base |

本地開發預設值即可，不需建立 `.env` 檔。正式部署時透過 Azure Static Web Apps 或 CI/CD 環境變數覆蓋。

---

## 4. 測試

```bash
# 全部測試（44 passed：Domain 17 + Application 5 + Infrastructure 12 + 其他）
dotnet test

# 只跑某一個測試專案
dotnet test tests/TFoodies.Domain.Tests
dotnet test tests/TFoodies.Infrastructure.Tests
dotnet test tests/TFoodies.Application.Tests
```

---

## 5. Build

```bash
# .NET — 整個 solution
dotnet build TFoodies.sln

# 前台 Nuxt SSR build
cd web && npm run build:store

# 後台 Vue SPA build
cd web && npm run build:admin
```

---

## 6. 本地預覽（Production build）

```bash
# 先 build
cd web && npm run build:store

# 啟動 Nuxt server
node web/store/.output/server/index.mjs
# 監聽 http://localhost:3000

# 後台 SPA preview
cd web/admin && npx vite preview
# 監聽 http://localhost:4173
```

---

## 7. 效能索引（首次部署）

```bash
# 對 tfoodies DB 跑一次
sqlcmd -S localhost -d tfoodies -U sa -P YOUR_PASSWORD -i scripts/add-indexes.sql
```

---

## 8. 基礎設施（Azure，選用）

```bash
# 驗證 Bicep 語法
az bicep build --file infra/main.bicep

# What-if 預覽（不實際部署）
az deployment sub what-if \
  --location eastasia \
  --template-file infra/main.bicep \
  --parameters environment=dev
```

---

## 9. 專案結構快覽

```
tfoodies/
├── src/
│   ├── TFoodies.Domain/          # 業務規則（Result、Money、TaiwanVat、Enums）
│   ├── TFoodies.Application/     # 介面定義（IOrderService、IAuthService…）
│   ├── TFoodies.Infrastructure/  # DB（Dapper+EF）、金流、SMS、發票
│   ├── TFoodies.Contracts/       # API DTO（ApiResponse、PaginatedResponse）
│   └── TFoodies.Api.Functions/   # Azure Functions host + Controllers + Router
├── tests/                        # xUnit 測試
├── web/
│   ├── store/                    # Nuxt 4 SSR 前台（http://localhost:3000）
│   └── admin/                    # Vue 3 SPA 後台（http://localhost:5173）
├── infra/main.bicep              # Azure 資源（SWA / Functions / SQL / KV…）
├── scripts/
│   ├── scaffold-db.sh            # EF Core scaffold（需 DB）
│   └── add-indexes.sql           # 效能索引
└── appsettings.example.json      # 設定範本
```

---

## 10. 常見問題

**`func start` 報 AzureWebJobsStorage 錯誤**
→ 先啟動 Azurite（`azurite --silent`），或把 `AzureWebJobsStorage` 改為真實 Storage 連線字串。

**`dotnet build` 失敗，找不到 Scaffolded 實體**
→ 執行 `scripts/scaffold-db.sh` 產生 EF 實體，或確認 `tfoodies` DB 可連線。

**前台 API 呼叫全部 500**
→ 確認 `appsettings.Development.json` 的 `Jwt.SecretKey` 長度 ≥ 32 字元。

**後台 SPA 登入後空白頁**
→ 確認 API `POST /auth/admin/login` 回傳 `{ accessToken, username, permissions }`；打開 DevTools > Network 確認請求是否成功。
