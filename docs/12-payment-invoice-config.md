# 12 · 金流（FISC / LINE Pay）／電子發票（ezPay）設定參數對照

> 新系統金流與電子發票的**真正參數名稱**與三層對照（API 設定鍵 ↔ bicep 參數 ↔ GitHub var/secret）。
> 前端（store 結帳、admin）**不持有任何 FISC/LINE Pay/ezPay 設定**——一律呼叫 API 取回刷卡欄位或付款網址後送單；設定只存在 API 一處。
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
| `Fisc__ApiBaseUrl` | ✅ GitHub var | 本 API 公開基底（含 `/api`）。**前台+後台**刷卡回呼（AuthResUrl/AdminAuthResUrl）皆由此導出。正式預設 `https://tfoodies-api.azurewebsites.net/api` |
| `Fisc__StoreSuccessUrl` | ⚙️ 導出（`storePublicUrl`） | 前台刷卡結果頁**fallback** = `{storeCustomDomains[0] 或 siteUrl}/Order/Success`（清單第 1 項=主網域），正式免單獨設 |
| `Fisc__AllowedStoreOrigins` | ⚙️ 導出（`storeCustomDomains`） | 動態回跳白名單（分號分隔所有服務網域的 https origin）。`return` 依使用者結帳網域同網域導回前先驗此清單（防 open redirect），不在清單退回 StoreSuccessUrl。自動跟網域清單走 |
| `Fisc__AdminSuccessUrl` | ⚙️ 導出（`adminSiteUrl`/SWA） | 後台訂單頁基底 = `.../admin/orders`；自訂網域以 GitHub var `ADMIN_SITE_URL` 設 |

### 程式導出（不另設定）

`AuthResUrl` / `AdminAuthResUrl` / `PayLinkAuthResUrl` / `StoreOrigin` **都不是設定鍵**，在 [FiscOptions.cs](../src/TFoodies.Infrastructure/Payments/Fisc/FiscOptions.cs) 由既有設定導出：

- 前台刷卡回呼 `AuthResUrl` = `{ApiBaseUrl}/store/payment/return`
- 後台刷卡回呼 `AdminAuthResUrl` = `{ApiBaseUrl}/store/payment/return-admin`
- 收款連結回呼 `PayLinkAuthResUrl` = `{ApiBaseUrl}/store/payment/return-paylink`
- LINE Pay 訂單回跳 `LinePayConfirmUrl` / `LinePayCancelUrl` = `{ApiBaseUrl}/store/payment/linepay/confirm`｜`/cancel`
- LINE Pay 收款連結回跳 `LinePayPaylinkConfirmUrl` / `LinePayPaylinkCancelUrl` = `{ApiBaseUrl}/store/payment/linepay/confirm-paylink`｜`/cancel-paylink`
- store 對外 origin `StoreOrigin` = `StoreSuccessUrl` 的 scheme://host[:port]（用於組收款連結網址與其結果頁 fallback）

> 💡 **收款連結與 LINE Pay 的回跳網址都未新增設定鍵**——刻意全部由 `ApiBaseUrl` / `StoreSuccessUrl` 導出，避免同名設定在「程式 / bicep / infra.yml / GitHub secret」四層間漂移（新增鍵漏改任一層就會被整包部署覆蓋洗掉）。
>
> ⚠️ 因此 `Fisc__ApiBaseUrl` / `Fisc__StoreSuccessUrl` / `Fisc__AllowedStoreOrigins` 實為**站台層級**設定（與金流廠商無關），LINE Pay 亦共用之，區段名稱與用途已不完全相符。待出現第三家金流再抽共用的 `PaymentSiteOptions`。

WEBPOS 送出欄位（[FiscWebpos.cs](../src/TFoodies.Infrastructure/Payments/Fisc/FiscWebpos.cs)，共 9 欄，與舊系統 ShoppingProfile.cshtml:344-353 一致）：
`merID, MerchantID, TerminalID, lidm, purchAmt, AuthResURL, enCodeType, PayType=0, AutoCap=1`。
✅ **實測成功**（前台刷卡通）：不需 `customize`、不需 `MerchantName`、不需同網域。

### 同網域送單：實測**非必要**

曾假設財金檢核「送單來源網域 = AuthResURL 網域」（舊單體系統剛好如此），一度為前台加 store 反代。但**實測前台刷卡成功**：AuthResURL 用 API 網域（`ApiBaseUrl`）、來源為 store 網域即可，**不需同網域、不需反代**。該反代已移除，前台/後台 AuthResURL 皆由 `ApiBaseUrl` 導出。`StoreSuccessUrl` 仍用實際服務網域（`storePublicUrl`），確保付款後 302 導回使用者所在站。

## FISC WEBPOS 規格速查（依手冊 v2.7）

> 入口參數 charset 固定 **Big-5**、欄位名**大小寫有別**；用 UTF-8 須帶 `enCodeType=UTF-8`。本系統 [FiscWebpos.cs](../src/TFoodies.Infrastructure/Payments/Fisc/FiscWebpos.cs) 送出 9 欄，與舊系統正式可運作表單一致。

### 送出欄位（3.1.1）與必填

| 欄位 | 型態/長度 | 必填 | 本系統值 |
|---|---|---|---|
| `merID` | N, ≤10 | ✅ | `Fisc__MerID` |
| `MerchantID` | AN, 固定 15 | ✅ | `Fisc__MerchantID` |
| `TerminalID` | AN, 固定 8 | ✅ | `Fisc__TerminalID` |
| `customize` | AN, 固定 1 | 選填 | **不送**（實測不需）|
| `lidm` | AN, 信用卡**最大 19** | ✅ | 訂單編號（`O`+8 碼日期+3 碼序，共 12 位）|
| `purchAmt` | N, ≤10（台幣整數）| ✅ | total+freight−discount |
| `AuthResURL` | ANS, ≤512 | 選填 | 前台/後台皆由 `Fisc__ApiBaseUrl` 導出 |
| `enCodeType` | ANS, ≤10（預設 BIG5）| 選填 | `UTF-8` |
| `PayType` | AN, 固定 1（0=一般）| 選填 | `0` |
| `AutoCap` | AN, 固定 1（1=自動請款）| 選填 | `1` |

### 授權成功判定（3.1.2）

`status == "0"` **且** `authCode` 非空 → 授權成功（本系統 [PaymentController.ParseFields](../src/TFoodies.Api.Functions/Controllers/PaymentController.cs) 已照此判定）。主動通知字串 `AuthResp={...}` 內以半形逗號分隔，含 `errcode/authCode/lidm/lastPan4/status/xid`。

### 入口檢核錯誤碼（debug 對照）

| errcode | 意義 | 排查方向 |
|---|---|---|
| `1` | merID/MerchantID/TerminalID 輸入不完整 | 三個代號設定值是否齊全 |
| `2` | 未指定訂單編號（lidm） | — |
| `3` | 未指定訂單金額（purchAmt） | — |
| `4` | AuthResURL 有誤（格式） | `Fisc__ApiBaseUrl` 用公開網址，勿 `localhost` |
| `10` | 查不到符合條件的訂單資訊 | — |
| `15` | **Can't find this merch in term** | **需財金確認**：merID 是否與財金端「網路特店專用流水號」一致、**測試環境該組代號是否已開通** |
| `3205` | 授權端網址格式有誤 | 同 errcode 4 |

> 「系統檢核異常或逾時」「交易資料驗證結果有誤」**不在手冊也不在舊系統**，研判為測試環境較新 Pay Page 的客戶端 JS（可能即被 CSP 擋的 inline script）所顯示，非本系統可控。優先以上表 errcode 與「**測試環境代號是否開通（errcode 15）**」對焦。

## LINE Pay Online API v4（直連）

API 設定類別 `LinePay`（[LinePayOptions.cs](../src/TFoodies.Infrastructure/Payments/LinePay/LinePayOptions.cs)）。
與 FISC 最大的差異：**需後端 HttpClient + HMAC-SHA256 簽章**（WEBPOS 是純前端 form POST、無金鑰）。

> **為何用 v4 不用 v3**：v4 是為台灣《電子支付機構管理條例》(EPI) 而生的版本。
> 對本系統而言 **v4 與 v3 的簽章方式、標頭、request/confirm body 欄位完全相同**，差別只有路徑前綴
> （`/v3/…` → `/v4/…`，由 [LinePayClient.cs](../src/TFoodies.Infrastructure/Payments/LinePay/LinePayClient.cs) 的 `ApiVersion` 常數控制）。
> v4 另在 confirm 回應多一個 `info.paymentProvider`（TSP/EPI），但官方註明 **Online API 一律回 TSP**
> （EPI 交易不適用線上情境），故本實作不讀該欄位；新增的 `options.regPayRequest`（記憶付款）亦未使用。

| 參數名稱（四層一致） | 種類 | 意義 / 預設 |
|---|---|---|
| `LinePay__Enabled` | GitHub var | 總開關（`true`/`false`）。false 時前台不顯示 LINE Pay、後端端點一律拒絕、後台無法建立 LINE Pay 收款連結。預設 `false` |
| `LinePay__ChannelId` | GitHub var | LINE Pay 商店 Channel ID（送 `X-LINE-ChannelId` 標頭） |
| `LinePay__ChannelSecret` | GitHub **Secret** | Channel Secret。**同時是 HMAC 金鑰與被簽訊息的前綴**（LINE Pay 的規定，非筆誤） |
| `LinePay__BaseUrl` | GitHub var | **沙箱/正式唯一開關**。沙箱 `https://sandbox-api-pay.line.me`、正式 `https://api-pay.line.me`。預設沙箱 |

回跳網址（confirm/cancel）不設鍵，由 `Fisc__ApiBaseUrl` 導出（見上節）。

### 簽章規格

```
X-LINE-Authorization-Nonce : UUID（每次請求唯一）
X-LINE-Authorization       : Base64( HMAC-SHA256( ChannelSecret,
                                      ChannelSecret + requestUri + requestBody + nonce ) )
```

`requestUri` 為**不含網域的路徑**（`/v4/payments/request`、`/v4/payments/{transactionId}/confirm`）；GET 請求則以 query string 取代 `requestBody`（本系統只用 POST）；
`requestBody` 必須是**實際送出的 JSON 字串本身**（重新序列化可能產生不同字串而驗簽失敗），
故 [LinePayClient.cs](../src/TFoodies.Infrastructure/Payments/LinePay/LinePayClient.cs) 一律「先序列化成字串 → 簽章 → 送出同一字串」。
簽章純函式在 [LinePaySigner.cs](../src/TFoodies.Infrastructure/Payments/LinePay/LinePaySigner.cs)，有固定向量測試鎖行為。

### returnCode 對照（debug）

| code | 意義 | 本系統處理 |
|---|---|---|
| `0000` | 成功 | 正常流程 |
| `1172` | **該筆交易已完成**（重複 confirm） | **視同成功**，保 confirm 回跳被重放時的冪等 |
| `1104` / `1105` | 商店不存在／無法使用 | 設定或商店狀態問題，回可讀訊息 |
| `1124` / `1183` | 金額有誤 | 金額一律由 DB 重算，出現代表資料異常 |
| `1133` / `1150` | 查無交易 | 回跳參數異常或交易已失效 |
| `1170` / `1142` | 餘額不足 | 顧客端問題 |
| `1177` | 超過付款可用時間 | 顧客未在時限內完成，需重新下單 |

完整中文訊息對照見 [LinePayErrors.cs](../src/TFoodies.Infrastructure/Payments/LinePay/LinePayErrors.cs)；未列舉的代碼會退回 LINE Pay 原始 `returnMessage`。

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
- **啟用 LINE Pay 才需要**：Variables `LinePay__Enabled=true`、`LinePay__ChannelId`、`LinePay__BaseUrl`（正式時設 `https://api-pay.line.me`）；Secret `LinePay__ChannelSecret`。四者未設時 `LinePay__Enabled` 預設 false，LINE Pay 整條路徑關閉、不影響既有金流

> 改動本表任一鍵：同名同步五處 —— [FiscOptions.cs](../src/TFoodies.Infrastructure/Payments/Fisc/FiscOptions.cs)/[LinePayOptions.cs](../src/TFoodies.Infrastructure/Payments/LinePay/LinePayOptions.cs)/[EzPayOptions.cs](../src/TFoodies.Infrastructure/Invoicing/EzPay/EzPayOptions.cs)（屬性）+ [main.bicep](../infra/main.bicep)（param + appSettings）+ [infra.yml](../.github/workflows/infra.yml)（兩個部署區塊的左右兩側）+ GitHub var/secret + [local.settings.json](../src/TFoodies.Api.Functions/local.settings.json)，否則部署整包覆蓋時被洗掉。

## 網域對照與切換

### 目前網域（過渡期）
| 用途 | 網域 |
|---|---|
| store（前台） | `tfoodies-store.4webdemo.com`（bicep `storeCustomDomains` 清單第 1 項）|
| admin（後台） | `tfoodies-admin.4webdemo.com`（GitHub var `ADMIN_SITE_URL`）|

AuthResURL（前台/後台）皆 = `Fisc__ApiBaseUrl`（API 網域）；`Fisc__StoreSuccessUrl` 由 `storePublicUrl`（= `storeCustomDomains` 清單第 1 項）導出，作為**回跳 fallback**。✅ 多網域服務時前台採**動態回跳**：`create` 帶 `returnOrigin`→ `AuthResURL?origin=`→ `return` 經 `Fisc__AllowedStoreOrigins` 白名單驗證後同網域導回，讀不到/不在白名單才退回 `StoreSuccessUrl`（防 open redirect）。見 [docs/13](13-payment-invoice-flow.md)。

### 切換到正式網域時要一起改（金流相關）
1. [main.bicep](../infra/main.bicep)：`storeCustomDomains` 清單（4 網域，主網域擺第 1 項）、`siteUrl` → 正式網域（store 部署細節見 [docs/11](11-store-deployment.md)）。`Fisc__StoreSuccessUrl` 會自動跟著 `storePublicUrl` 變，**不需另改**。
2. GitHub var `ADMIN_SITE_URL` → 正式後台網域。
3. 財金端：確認正式網域已登錄（舊系統的 `www.tfoodies.com` 應已登錄；新後台網域若不同需加登錄）。
4. `Fisc__ActionUrl` 切正式 `https://www.focas.fisc.com.tw/...`（測試期間若用 focas-test 才需切回）。
