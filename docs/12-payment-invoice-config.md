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
| `Fisc__ApiBaseUrl` | ✅ GitHub var | 本 API 公開基底（含 `/api`）。**後台**刷卡回呼 `AdminAuthResUrl` 由此導出。正式預設 `https://tfoodies-api.azurewebsites.net/api` |
| `Fisc__StoreApiBaseUrl` | ⚙️ 導出（`siteUrl`） | **前台 store 網域基底** = `${siteUrl}/api`。前台刷卡回呼 `AuthResUrl` 由此導出，使「刷卡頁網域 = AuthResURL 網域」一致（見下）。留空 fallback `ApiBaseUrl` |
| `Fisc__StoreSuccessUrl` | ⚙️ 導出（`siteUrl`） | 前台刷卡結果頁 = `${siteUrl}/Order/Success`，正式免單獨設 |
| `Fisc__AdminSuccessUrl` | ⚙️ 導出（`adminSiteUrl`/SWA） | 後台訂單頁基底 = `.../admin/orders`；自訂網域以 GitHub var `ADMIN_SITE_URL` 設 |

### 程式導出（不另設定）

`AuthResUrl` / `AdminAuthResUrl` **不是設定鍵**，在 [FiscOptions.cs](../src/TFoodies.Infrastructure/Payments/Fisc/FiscOptions.cs) 由各自網域基底導出：

- 前台刷卡回呼 `AuthResUrl` = `{StoreApiBaseUrl 或 ApiBaseUrl}/store/payment/return`（**store 網域**）
- 後台刷卡回呼 `AdminAuthResUrl` = `{ApiBaseUrl}/store/payment/return-admin`

WEBPOS 送出欄位（[FiscWebpos.cs](../src/TFoodies.Infrastructure/Payments/Fisc/FiscWebpos.cs)）：
`merID, MerchantID, TerminalID, customize=0, lidm, purchAmt, AuthResURL, enCodeType, PayType=0, AutoCap=1`。

### ⚠️ 同網域送單（還原舊系統條件）— 最關鍵

> 來源：[網路特店技術說明手冊_v2.7.doc](../reference/card/網路特店技術說明手冊_v2.7.doc) + 舊系統實測。

舊系統為**單體**：刷卡 form 與 AuthResURL 端點同在一個網域（前台 `www.tfoodies.com`、後台 `backend.tfoodies.com`），即 **送單來源網域 = AuthResURL 網域 = 財金登錄網域** 三者一致。新系統拆 store/admin/API 三網域會打破此條件，導致財金回「交易資料驗證結果有誤」（即使欄位全對）。

**解法（前台已實作）**：store 以反向代理 [server/routes/api/store/payment/return.post.ts](../web/store/server/routes/api/store/payment/return.post.ts) 把 `/api/store/payment/return` 轉發到 Functions；`AuthResUrl` 用 `StoreApiBaseUrl`(=`${siteUrl}/api`，store 網域)。部署到登錄網域後即還原同網域條件。

- ⚠️ `siteUrl` 須為**財金登錄網域**（現用過渡網域時，要嘛切到 `www.tfoodies.com`，要嘛請財金把過渡網域加登錄）。
- 本機 `localhost` 無法滿足（財金回呼不到）；要本機驗「同網域」假設，需對 **store(3000)** 開 dev tunnel 並把 `Fisc__StoreApiBaseUrl` 指向 tunnel。
- **後台 admin 線上刷卡尚未做同網域**（SWA 需以 linked backend 反代 `/api`，AuthResURL 改用 admin 網域）— 待辦。
- errcode `4`（AuthResURL有誤）為格式檢核；`localhost`/格式不符會中。

## FISC WEBPOS 規格速查（依手冊 v2.7）

> 入口參數 charset 固定 **Big-5**、欄位名**大小寫有別**；用 UTF-8 須帶 `enCodeType=UTF-8`。本系統 [FiscWebpos.cs](../src/TFoodies.Infrastructure/Payments/Fisc/FiscWebpos.cs) 送出 9 欄，與舊系統正式可運作表單一致。

### 送出欄位（3.1.1）與必填

| 欄位 | 型態/長度 | 必填 | 本系統值 |
|---|---|---|---|
| `merID` | N, ≤10 | ✅ | `Fisc__MerID` |
| `MerchantID` | AN, 固定 15 | ✅ | `Fisc__MerchantID` |
| `TerminalID` | AN, 固定 8 | ✅ | `Fisc__TerminalID` |
| `customize` | AN, 固定 1 | 選填 | `0`（不使用客製化授權頁）|
| `lidm` | AN, 信用卡**最大 19** | ✅ | 訂單編號（`O`+8 碼日期+3 碼序，共 12 位）|
| `purchAmt` | N, ≤10（台幣整數）| ✅ | total+freight−discount |
| `AuthResURL` | ANS, ≤512 | 選填 | 前台由 `Fisc__StoreApiBaseUrl`、後台由 `Fisc__ApiBaseUrl` 導出 |
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
| `4` | AuthResURL 有誤（格式） | 用公開網址，勿 `localhost`（前台＝`Fisc__StoreApiBaseUrl`、後台＝`Fisc__ApiBaseUrl`）|
| `10` | 查不到符合條件的訂單資訊 | — |
| `15` | **Can't find this merch in term** | **需財金確認**：merID 是否與財金端「網路特店專用流水號」一致、**測試環境該組代號是否已開通** |
| `3205` | 授權端網址格式有誤 | 同 errcode 4 |

> 「系統檢核異常或逾時」「交易資料驗證結果有誤」**不在手冊也不在舊系統**，研判為測試環境較新 Pay Page 的客戶端 JS（可能即被 CSP 擋的 inline script）所顯示，非本系統可控。優先以上表 errcode 與「**測試環境代號是否開通（errcode 15）**」對焦。

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

## 網域與金流（同網域送單）的關係

舊系統單體：刷卡頁與 AuthResURL 同網域（前台 `www.tfoodies.com`、後台 `backend.tfoodies.com`），即 **送單來源網域 = AuthResURL 網域 = 財金登錄網域**。新系統靠以下機制還原：

- 前台：store 反代 [`/api/store/payment/return`](../web/store/server/routes/api/store/payment/return.post.ts) → Functions；`Fisc__StoreApiBaseUrl` / `Fisc__StoreSuccessUrl` 皆由 **`storePublicUrl`** 導出（[main.bicep](../infra/main.bicep) `var storePublicUrl = storeCustomDomain 優先, 否則 siteUrl`）→ 永遠用「使用者實際所在網域」。
- 後台：**尚未做**（SWA 需 linked backend 反代 `/api`、AuthResURL 改用 admin 網域）。目前 `AdminAuthResUrl` 仍為 `Fisc__ApiBaseUrl`。

### 目前網域（過渡期）
| 用途 | 網域 |
|---|---|
| store（前台） | `tfoodies-store.4webdemo.com`（bicep `storeCustomDomain`）|
| admin（後台） | `tfoodies-admin.4webdemo.com`（GitHub var `ADMIN_SITE_URL`）|
| 財金登錄網域 | 舊系統為 `www.tfoodies.com` / `backend.tfoodies.com` |

> ⚠️ **過渡網域（4webdemo）幾乎未在財金登錄**。若財金鎖「來源/AuthResURL 必須為登錄網域」，在過渡網域刷卡會被擋（回「交易資料驗證結果有誤」），即使欄位與同網域都正確。
> **務必先問財金**：特店 45889852 是否限制送單來源/AuthResURL 網域？能否加登錄 `tfoodies-store.4webdemo.com` / `tfoodies-admin.4webdemo.com`？

### 切換到正式網域時要一起改（金流相關）
1. [main.bicep](../infra/main.bicep)：`storeCustomDomain`、`storeCertName`、`siteUrl` → 正式網域（store 部署細節見 [docs/11](11-store-deployment.md)）。`Fisc__StoreApiBaseUrl`/`StoreSuccessUrl` 會自動跟著 `storePublicUrl` 變，**不需另改**。
2. GitHub var `ADMIN_SITE_URL` → 正式後台網域。
3. 財金端：確認正式網域已登錄（舊系統的 `www.tfoodies.com` 應已登錄；新後台網域若不同需加登錄）。
4. `Fisc__ActionUrl` 切正式 `https://www.focas.fisc.com.tw/...`（測試期間若用 focas-test 才需切回）。
