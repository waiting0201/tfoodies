# 12 · 金流（FISC）／電子發票（ezPay）設定參數對照

> 新系統金流與電子發票的**真正參數名稱**與三層對照（API 設定鍵 ↔ bicep 參數 ↔ GitHub var/secret）。
> 前端（store 結帳、admin）**不持有任何 FISC/ezPay 設定**——一律呼叫 API 取回刷卡欄位後送單；設定只存在 API 一處。
> 部署時 Function App 的 appSettings 為**整包覆蓋**（見 [docs/06](06-cross-cutting.md) 與記憶 `project-appsettings-bicep-fullreplace`），新增鍵必須同步本表四欄。

## 命名原則（四層一致）

> **GitHub var/secret 名稱 = bicep 參數名 = appSetting 鍵 = local.settings 鍵**，完全相同字串（含雙底線與大小寫）。
> 因為 .NET 設定綁定要求 appSetting 為 `Section__Key` 格式，故統一以此為唯一名稱貫穿四層。
> GitHub var/secret 名稱大小寫不敏感，`${{ vars.Fisc__ActionUrl }}` / `${{ secrets.EzPay__HashKey }}` 皆可解析。
> **例外**：`adminSiteUrl`（→導出 `Fisc__AdminSuccessUrl`）與 `siteUrl`（→導出 `Fisc__StoreSuccessUrl`）為導出來源、且與非金流用途（admin SWA、store SEO）共用，維持原名。

## FISC 財金 FOCAS_WEBPOS 信用卡金流

API 設定類別 `Fisc`（[FiscOptions.cs](../src/TFoodies.Infrastructure/Payments/Fisc/FiscOptions.cs)）。

| 參數名稱（四層一致） | 直接設定？ | 意義 / 預設 |
|---|---|---|
| `Fisc__ActionUrl` | ✅ GitHub var | 刷卡頁 URL；**測試/正式唯一開關**。正式 `https://www.focas.fisc.com.tw/FOCAS_WEBPOS/online/`、測試 `https://www.focas-test.fisc.com.tw/FOCAS_WEBPOS/online/` |
| `Fisc__MerchantID` | ✅ GitHub var | 特店代號（15 位）。測試與正式相同 |
| `Fisc__TerminalID` | ✅ GitHub var | 機台代號（8 位）。測試與正式相同 |
| `Fisc__MerID` | ✅ GitHub var | 網站特店自訂代碼（≤10 位）。測試與正式相同 |
| `Fisc__ApiBaseUrl` | ✅ GitHub var | **本 API 公開基底（含 `/api`）**。兩個授權回呼網址由此導出（見下）。正式預設 `https://tfoodies-api.azurewebsites.net/api`；本機需 dev tunnel 的 https（`localhost` 不可） |
| `Fisc__StoreSuccessUrl` | ⚙️ 導出（`siteUrl`） | 前台刷卡結果頁 = `${siteUrl}/Order/Success`，正式免單獨設 |
| `Fisc__AdminSuccessUrl` | ⚙️ 導出（`adminSiteUrl`/SWA） | 後台訂單頁基底 = `.../admin/orders`；自訂網域以 GitHub var `ADMIN_SITE_URL` 設 |

### 程式導出（單一來源，不另設定）

`AuthResUrl` / `AdminAuthResUrl` **不是設定鍵**，由 `ApiBaseUrl` 在 [FiscOptions.cs](../src/TFoodies.Infrastructure/Payments/Fisc/FiscOptions.cs) 導出，避免兩條網址重複定義/失準：

- 前台刷卡回呼 `AuthResUrl` = `{ApiBaseUrl}/store/payment/return`
- 後台刷卡回呼 `AdminAuthResUrl` = `{ApiBaseUrl}/store/payment/return-admin`

WEBPOS 送出欄位（[FiscWebpos.cs](../src/TFoodies.Infrastructure/Payments/Fisc/FiscWebpos.cs)）與舊系統正式可運作表單一字不差：
`merID, MerchantID, TerminalID, lidm, purchAmt, AuthResURL, enCodeType, PayType=0, AutoCap=1`。**不送 `MerchantName`**（已移除，舊可運作表單未含此欄）。

### ⚠️ AuthResURL 注意

- 必須是**公開可達的 HTTPS**；`localhost` 財金回呼不到 → 本機測試需 dev tunnel/ngrok。
- 財金通常**白名單 AuthResURL 網域**到該商店代號（舊系統登錄 `www.tfoodies.com` / `backend.tfoodies.com`）。`FISC_API_BASE_URL` 的網域須在財金登錄，否則刷卡頁回「交易資料驗證結果有誤」。

## ezPay／NewebPay 電子發票

API 設定類別 `EzPay`（[EzPayOptions.cs](../src/TFoodies.Infrastructure/Invoicing/EzPay/EzPayOptions.cs)）。金鑰驗證延後到實際開票（[EzPayCodec.cs](../src/TFoodies.Infrastructure/Invoicing/EzPay/EzPayCodec.cs)），未設定不影響刷卡/付款。

| 參數名稱（四層一致） | 種類 | 意義 / 預設 |
|---|---|---|
| `EzPay__BaseUrl` | GitHub var | API base；正式 `https://inv.ezpay.com.tw/Api`、測試 `https://cinv.ezpay.com.tw/Api` |
| `EzPay__MerchantId` | GitHub var | ezPay 商店代號 |
| `EzPay__HashKey` | GitHub **Secret** | AES-256 金鑰（32 字） |
| `EzPay__HashIV` | GitHub **Secret** | AES IV（16 字） |

## 需在 GitHub 設定的最小清單（名稱即上表，含雙底線與大小寫）

- **必設 Variables**：`Fisc__MerchantID`、`Fisc__TerminalID`、`Fisc__MerID`、`EzPay__MerchantId`
- **必設 Secrets**：`EzPay__HashKey`、`EzPay__HashIV`
- **選填 Variables**（有預設）：`Fisc__ActionUrl`（測試站時設）、`Fisc__ApiBaseUrl`（財金白名單網域時設）、`ADMIN_SITE_URL`（後台自訂網域時設）、`EzPay__BaseUrl`（測試環境時設 cinv）

> 改動本表任一鍵：同名同步五處 —— [FiscOptions.cs](../src/TFoodies.Infrastructure/Payments/Fisc/FiscOptions.cs)/[EzPayOptions.cs](../src/TFoodies.Infrastructure/Invoicing/EzPay/EzPayOptions.cs)（屬性）+ [main.bicep](../infra/main.bicep)（param + appSettings）+ [infra.yml](../.github/workflows/infra.yml)（兩個部署區塊的左右兩側）+ GitHub var/secret + [local.settings.json](../src/TFoodies.Api.Functions/local.settings.json)，否則部署整包覆蓋時被洗掉。
