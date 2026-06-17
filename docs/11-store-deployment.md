# 11 · 前台 Store 部署操作手冊（Container Apps SSR）

> 前台 `web/store` 為 **Nuxt 完整 SSR**，以 Docker 映像部署到 **Azure Container Apps**（scale-to-zero）。
> 後台 admin 仍是 Static Web App，不受本手冊影響。
>
> **最快路徑（建議）**：GitHub → Actions → **store-bootstrap** → Run workflow（選 `prod`）。這支
> [store-bootstrap.yml](../.github/workflows/store-bootstrap.yml) 一次做完 A+B（註冊 provider → 部署 infra →
> build 映像 → 切換 Container App → 驗證）。之後日常部署走 [store.yml](../.github/workflows/store.yml)
> （push `web/store/**` 自動觸發；ACR 名稱自動解析，無需設變數）。
>
> 下面 A/B 段保留**手動 az CLI** 版本，供除錯或不想用 Actions 時使用。C 段（網域切換 + 退役 SWA）一定要手動。

## 0. 前置需求

- Resource Group：**`WeyproUS`**（與 Functions / admin SWA 同一個；workflow 取 `vars.RESOURCE_GROUP`，未設則 fallback `WeyproUS`）。
- GitHub 已設定 OIDC（既有 `api`/`infra` workflow 已在用）：
  - **Secrets**：`AZURE_CLIENT_ID`、`AZURE_TENANT_ID`、`AZURE_SUBSCRIPTION_ID`
  - **Variables**：`RESOURCE_GROUP`(=`WeyproUS`)、`FUNCTION_APP_NAME`
  - ⚠️ **不需要** `ACR_NAME`：store workflow 會自動從 RG 解析 ACR 名稱。
- 規模設定：Container App **scale-to-zero（minReplicas=0）** 省成本，閒置縮到 0；已寫在 Bicep。
- ⚠️ **一次性：訂閱層註冊 resource providers（需 Owner/Contributor）**。GitHub 部署用的 OIDC service
  principal 通常只在 RG `WeyproUS` 範圍有權限，**無法**做訂閱層的 `Microsoft.App/register/action`
  （bootstrap 會報 `AuthorizationFailed`）。請一位**訂閱 Owner/Contributor** 先跑一次（或在 Portal →
  訂閱 → 資源提供者 註冊）：
  ```bash
  az account set --subscription <SUBSCRIPTION_ID>
  az provider register --namespace Microsoft.App --wait
  az provider register --namespace Microsoft.ContainerRegistry --wait
  az provider register --namespace Microsoft.OperationalInsights --wait
  ```
  註冊是訂閱層、一次到位；之後 RG 範圍的部署身分即可建立所有資源。確認狀態：
  ```bash
  az provider show -n Microsoft.App --query registrationState -o tsv   # 應為 Registered
  ```
- 手動跑 az CLI 部署時：`az login` + `az account set --subscription <SUBSCRIPTION_ID>`。

---

## A+B. 一鍵 bootstrap（建議）

GitHub → Actions → **store-bootstrap** → Run workflow → 選 `prod`。

它會依序：註冊 provider → `az deployment group create`（建 ACR + Container Apps 環境 + Container App）→
讀 outputs → `az acr build` 建 Nuxt 映像 → `az containerapp update` 切換 → curl 驗證 robots/sitemap。
跑完 log 末段會印出 `Store is live at: https://<fqdn>`。

> 之後改 `web/store/**` 推上 `main`，[store.yml](../.github/workflows/store.yml) 會自動 build + 部署，
> ACR 名稱自動解析，**不必設任何變數**。

---

## A. 建立基礎建設（手動 az CLI）

[infra/main.bicep](../infra/main.bicep) 已包含 `acr` / `caEnv` / `storeApp`（首次帶 placeholder 映像）。

```bash
RG=WeyproUS
az deployment group create \
  --resource-group "$RG" \
  --name store-bootstrap-prod \
  --template-file infra/main.bicep \
  --parameters env=prod \
  --query "properties.outputs.{acr:acrName.value, fqdn:storeFqdn.value}" -o table
```
- `acr` → 下一步 build 用；`fqdn` → Container App 對外網址（驗證用）。
- ACR 名稱由 Bicep 以 `tfoodiesacr<env><hash>` 動態產生，從 output 取得即可。

---

## B. 首次部署映像（手動 az CLI）

此時 Container App 還跑著 placeholder（hello-world），需 build 真正的 Nuxt 映像並切換。

```bash
RG=WeyproUS
ACR=<上一步 output 的 acr>
TAG=$(git rev-parse --short HEAD)

# 1) 在 ACR 雲端 build 映像（build context = web/，免本機 Docker）
az acr build --registry "$ACR" --image store:$TAG --image store:latest \
  --file web/store/Dockerfile web

# 2) 切換 Container App 到新映像
az containerapp update --name tfoodies-store --resource-group "$RG" \
  --image "$ACR.azurecr.io/store:$TAG"
```

---

## 驗證

```bash
FQDN=<storeFqdn>     # 例：tfoodies-store.xxxx.azurecontainerapps.io
curl -s "https://$FQDN/" | grep -E '<title>|og:title|og:image'   # SSR 真 HTML + meta
curl -s "https://$FQDN/robots.txt"                                # Disallow + Sitemap 行
curl -s "https://$FQDN/sitemap.xml" | grep -c '<loc>'            # 含動態商品/文章 URL（>靜態 6 筆）
```
- sitemap 動態 URL 需 Container App env `NUXT_PUBLIC_API_BASE` 連得到 Functions API（Bicep 已注入）。
- 社群預覽：Facebook Sharing Debugger / LINE 測商品頁；結構化資料：Google Rich Results Test。
- 冷啟動：scale-to-zero（minReplicas=0），閒置後首個請求約 1–3s。若影響 SEO/體驗：
  ```bash
  az containerapp update -n tfoodies-store -g "$RG" --min-replicas 1
  ```

---

## C.（可選）自訂網域切換 + 退役舊 SWA

> 🎯 **切換正式網域（未來待辦）**：目前對外用的是過渡網域 `tfoodies-store.4webdemo.com`，
> 但 `siteUrl` 已預先設成 `https://www.tfoodies.com`（SSR canonical + 金流回跳網址）。**兩者目前不一致**。
>
> **正式上線要綁 4 個 hostname、全部直接服務（不做 301 轉址）**：`www.tfoodies.com`（**主網域/canonical**）、
> `tfoodies.com`（apex）、`www.tfoodies.com.tw`、`tfoodies.com.tw`（apex）。canonical 由 `useSeo()` 以絕對
> `siteUrl` 組出，不論使用者從哪個網域進來，頁面 `<link rel="canonical">`/og:url 都指向 `www.tfoodies.com`，
> Google 據此去重 → **app 端不需改**。
>
> | 位置 | 參數 / 鍵 | 影響 |
> |---|---|---|
> | [main.bicep](../infra/main.bicep) `storeCustomDomains` | ingress 綁定的網域**清單**（每項 `{domain, certName}`） | 各網域實際可連、HTTPS 憑證。**第 1 項放主網域**（`storePublicUrl` 取它） |
> | （上者每項的）`certName` | 該網域受管憑證名稱 | 每個網域第一次 bind 後各自產生一張，需逐一填回 |
> | [main.bicep](../infra/main.bicep) `siteUrl` | `https://www.tfoodies.com` | `NUXT_PUBLIC_SITE_URL`（SEO canonical）+ `Fisc__StoreSuccessUrl`（刷卡成功回跳） |
> | DNS | www → CNAME；**apex → A 記錄指向環境靜態 IP**；各域 asuid TXT | 指向 store、供憑證驗證 |
>
> ⚠️ **apex 裸域（`tfoodies.com` / `tfoodies.com.tw`）綁定較麻煩**：Container Apps 不能用 CNAME 綁 apex，需
> 用 **A 記錄指向 managed environment 的靜態入口 IP**（`az containerapp env show -n tfoodies-cae-dev -g "$RG"
> --query properties.staticIp -o tsv`）＋ `asuid.<apex>` TXT 驗證。www 子網域仍用 CNAME。
>
> ✅ **多網域＋金流的跨域漏單：已用動態回跳解決**（不需額外設定，自動跟著網域清單走）。`create` 時前端帶
> `returnOrigin`（=當前網域），後端附進 `AuthResURL?origin=...`；`/store/payment/return` 讀回 `origin`、經
> `Fisc__AllowedStoreOrigins`（bicep 從 `storeCustomDomains` 自動導出）**白名單驗證**後同網域導回 `{origin}/Order/Success`，
> 使用者留在原網域、`purchase` 追蹤的 sessionStorage 不跨域 → 不漏單。**安全網**：FISC 若不保留 query 或 origin 不在
> 白名單，則退回 `Fisc__StoreSuccessUrl`（=主網域，等同舊行為，且防 open redirect）。流程細節見
> [docs/13 §前台刷卡](13-payment-invoice-flow.md)。`fiscAuthResUrl`（API 端網址）不受影響、無需更動。
>
> **切換步驟**：① DNS：www 設 CNAME、apex 設 A（靜態 IP），各域加 asuid TXT → ② 對**每個** hostname 跑下方步驟 1 的
> `hostname add`/`bind` 完成驗證並各自產生受管憑證 → ③ 用 `certificate list` 取得每張憑證名 →
> ④ 把 `storeCustomDomains` 清單一次填成 4 項（主網域擺第 1 項）、`siteUrl` 設 `https://www.tfoodies.com` →
> ⑤ commit + push `infra/**`，部署後即冪等固定 → ⑥ 確認無誤後移除過渡網域的舊綁定（從清單拿掉即可）。

> 只有當網域目前綁在舊 SWA、要切到 Container App 時才做。**先綁新、確認、再刪舊**，否則網站會斷線。
>
> ⚠️ **整包部署會洗掉手動綁的網域**：infra 部署（`az deployment group create`）對 Container App 做整包 PUT，
> 若 `ingress.customDomains` 沒寫在 bicep 裡，手動 `hostname bind` 的網域就會在下次 `infra/**` push 時消失。
> **因此自訂網域與憑證引用已寫進 [main.bicep](../infra/main.bicep)**（`storeCustomDomains` 清單參數 +
> `ingress.customDomains` 以迴圈展開），目前清單只有一項 `tfoodies-store.4webdemo.com`、引用既有受管憑證
> `tfoodies-store.4webdemo.com-tfoodies-260611010259`（環境 `tfoodies-cae-dev`）。**換網域時改 `storeCustomDomains`
> 清單預設值**，不要只在 CLI 手動綁。每個新網域第一次仍需先做下方步驟 1~2 完成 DNS 驗證、產生受管憑證，再把名稱填回清單。

1. 在 Container App 綁定自訂網域並啟用受管憑證（首次驗證用；之後靠 bicep 維持）：
   ```bash
   # 對每個 hostname 各做一次（www 用 CNAME；apex 用 A 記錄指向環境靜態 IP）：
   az containerapp hostname add    -n tfoodies-store -g "$RG" --hostname <新網域>
   # 依指示在 DNS 加 CNAME / A + asuid TXT 驗證後：
   az containerapp hostname bind   -n tfoodies-store -g "$RG" --hostname <新網域> --environment tfoodies-cae-dev
   # 取得各受管憑證名稱，逐一填回 main.bicep 的 storeCustomDomains 清單對應項的 certName：
   az containerapp env certificate list -n tfoodies-cae-dev -g "$RG" --managed-certificates-only \
     --query "[].name" -o tsv
   ```
2. DNS：把網域的 CNAME 指向 Container App FQDN（過渡期可先用低 TTL）。
3. 確認新網域 HTTPS 正常服務後，**才**刪除舊 SWA：
   ```bash
   az staticwebapp delete --name tfoodies-store --resource-group "$RG"
   ```
4. 移除已無用的 GitHub secret `AZURE_SWA_TOKEN_STORE`（store workflow 已不參照）。

---

## 疑難排解

| 症狀 | 處理 |
|---|---|
| store workflow 報 `No ACR found in <RG>` | RG 內還沒有 ACR — 先跑一次 **store-bootstrap**（或手動步驟 A）。 |
| **push 後自訂網域不見了** | infra 整包部署洗掉了 `ingress.customDomains`。已修：網域寫進 [main.bicep](../infra/main.bicep) 的 `storeCustomDomains` 清單（迴圈展開），下次部署會自動加回。換網域請改清單參數，勿只用 CLI 手綁。 |
| **push 後映像被打回 hello-world** | infra bicep 寫死 `storePlaceholderImage`，每次 `infra/**` 部署都會重設運行映像。暫以重跑 **store.yml**（或 `containerapp update --image`）復原；根治需把實際映像改由 store.yml 維持、infra 部署不覆蓋。 |
| Container App 一直跑 hello-world | bootstrap/步驟 B 尚未成功；或 `containerapp update` 的 image tag 不存在。 |
| 頁面 200 但內容空 / sitemap 無動態 URL | `NUXT_PUBLIC_API_BASE` 連不到 Functions，或 API 未上線；檢查 Container App 環境變數與 Functions 狀態。 |
| 首次請求很慢 | scale-to-zero 冷啟動，屬正常；必要時 `--min-replicas 1`。 |
| `az acr build` 權限不足 | 登入身分需 ACR `Contributor`/`AcrPush`；OIDC service principal 已在 infra 設定範圍內。 |
