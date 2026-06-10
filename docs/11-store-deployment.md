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

> 只有當 `www.tfoodies.com` 目前綁在舊 SWA、要切到 Container App 時才做。**先綁新、確認、再刪舊**，否則網站會斷線。

1. 在 Container App 綁定自訂網域並啟用受管憑證：
   ```bash
   az containerapp hostname add    -n tfoodies-store -g "$RG" --hostname www.tfoodies.com
   # 依指示在 DNS 加 CNAME / TXT 驗證後：
   az containerapp hostname bind   -n tfoodies-store -g "$RG" --hostname www.tfoodies.com --environment tfoodies-cae-prod
   ```
2. DNS：把 `www.tfoodies.com` 的 CNAME 指向 Container App FQDN（過渡期可先用低 TTL）。
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
| Container App 一直跑 hello-world | bootstrap/步驟 B 尚未成功；或 `containerapp update` 的 image tag 不存在。 |
| 頁面 200 但內容空 / sitemap 無動態 URL | `NUXT_PUBLIC_API_BASE` 連不到 Functions，或 API 未上線；檢查 Container App 環境變數與 Functions 狀態。 |
| 首次請求很慢 | scale-to-zero 冷啟動，屬正常；必要時 `--min-replicas 1`。 |
| `az acr build` 權限不足 | 登入身分需 ACR `Contributor`/`AcrPush`；OIDC service principal 已在 infra 設定範圍內。 |
