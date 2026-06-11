# TFoodies 重構進度（status.md）

> 進度追蹤單。每完成一個增量就更新此檔。計畫全文：`/Users/tim/.claude/plans/reference-old-reference-card-woolly-bear.md`
> 舊系統（唯讀參考）：`reference/old/`　·　正式站：https://www.tfoodies.com
> 最後更新：2026-06-11（**前台 store API ↔ Nuxt 全面接通並實機驗證**：12 個內容 composable 改為 transform API camelCase→legacy view-model（blobUrl 注入 + yyyy-MM-dd 日期）；後端補齊舊系統像素級缺漏資料（product.capacity/isDisabled/sort、recipe.v、product-detail 關聯食譜+品牌 intro/storybgclass/isdisplay、recipe-detail 關聯商品、issue-detail 關聯商品/食譜/others、news-detail others、producttypes 分類頁、brand productCount→hasMore）；修正 store 4 個分頁端點 `PaginatedResponse.Create` 參數順序 bug；blobUrl 進 runtimeConfig；sitemap server route 改 camelCase。`func start` 對真實 DB 驗證全 12 端點皆回正確資料）

## 圖例
✅ 完成並驗證　🟡 進行中　⬜ 未開始　⛔ 被外部相依卡住

---

## 🔁 如何接續（下次從這裡開始）

**環境**：.NET 9.0.200（`global.json` pin）、Node 24、Azure CLI 2.86、func 4.8、Azurite。工作目錄 `/Users/tim/webapps/tfoodies`。

**先確認現況仍綠（30 秒）**：
```bash
cd /Users/tim/webapps/tfoodies
dotnet test                         # 應：29 passed（Domain 17 + Infra 12）
cd web && npm --workspace store run build && npm --workspace admin run build   # 兩個都應成功
az bicep build --file ../infra/main.bicep && rm -f ../infra/main.json          # Bicep 應編譯過
```

**後台 API 已接通**：`PaginatedResponse<T>` 已修正（`items`/`total`/`totalCount`），全域 camelCase 政策已加入，6 個 Admin controller 列表端點屬性命名已對齊 Vue 介面。

**唯一阻擋**：⛔ 需要 `tfoodies` SQL Server 的**唯讀/快照連線字串**。拿到後：
```bash
TFOODIES_CONNSTRING='Server=...;Database=tfoodies;User Id=...;Password=...;TrustServerCertificate=True' \
  ./scripts/scaffold-db.sh           # 產出 src/TFoodies.Infrastructure/Persistence/Scaffolded/（72 表）
```
→ 接著啟用 `Infrastructure/DependencyInjection.cs` 內被註解的 `AddDbContext<TfoodiesContext>` + `EfUnitOfWork`，即可往下做下單/Auth/報表。

**不需 DB 也能續做的兩塊**（建議下次優先）：見最底「下一步（優先序）」。

---

## Phase 1 — 基礎（Foundation）

| 項目 | 狀態 | 備註 |
|---|---|---|
| `global.json` pin .NET 9.0.200 | ✅ | |
| `TFoodies.sln` + 分層專案骨架 | ✅ | Domain/Application/Infrastructure/Contracts/Api.Functions + 2 test 專案 |
| `Directory.Build.props`（net9.0/nullable/implicit usings） | ✅ | |
| Domain Common：`Result<T>`/`Error`/`Money`/`TaiwanVat` | ✅ | 取代吞例外的舊 IResult；VAT 單一真相 |
| Domain Enums（15 個狀態機 enum） | ✅ | 由 `Libs/Enum.cs` 1:1 移植，英文名 + DB 數值不變 |
| Domain 單元測試（VAT parity / Money） | ✅ | 17 passed |
| Functions 單一入口 host（`ApiRouter` catch-all） | ✅ | runtime 實測：`/api/health` 200、未知路由 404 ProblemDetails |
| Correlation + Exception→ProblemDetails middleware | ✅ | 只配一次，含 `X-Correlation-ID` |
| `AddApplication()` / `AddInfrastructure()` 組合根 | ✅ | 目前為空殼，待填 |
| 全 solution build | ✅ | 0 warning / 0 error |

## Phase 1.5 — 資料層接縫（不需 DB 連線的部分）

| 項目 | 狀態 | 備註 |
|---|---|---|
| EF Core 9 / Dapper / SqlClient 套件加入 Infrastructure | ✅ | EFCore 9.0.6 |
| Application Abstractions（`IUnitOfWork`/`IDbConnectionFactory`/`ICodeNumberService`/`IStockAllocator`） | ✅ | ports，介面先行，含 `CodeKind`/`AllocationResult`/`StockPick` |
| `SqlConnectionFactory`（Dapper，pooled） | ✅ | |
| EF Core scaffold script（`scripts/scaffold-db.sh`）+ Scaffolded/README | ✅ | `--use-database-names --data-annotations --no-onconfiguring` |
| Infrastructure DI 註冊（DbContext + UoW + 連線工廠） | ✅ | `EfUnitOfWork` 建立；`AddDbContext<TfoodiesContext>` + `AddScoped<IUnitOfWork>` 啟用 |
| 實際執行 scaffold 產生 72 表 `TfoodiesContext` | ✅ | 本機 DB 已接通，`InvariantGlobalization` 修正為 `false`，72 實體產生 |

## Phase 2 — 前端骨架

| 項目 | 狀態 | 備註 |
|---|---|---|
| 前端 monorepo（npm workspaces）`web/` | ✅ | 635 套件，0 漏洞 |
| `web/store`：Nuxt 4 + TS（SSR/SEO 骨架） | ✅ | **build 通過**；保留舊路由（`Product/[producttitle]` slug + 連字號↔斜線 util）、canonical/OG 用 shortener、Pinia 購物車。⚠️ 占位元件**將被 §7.1 逐字移植取代** |
| `web/admin`：Vue 3 + Vite + TS（SPA） | ✅ | **build 通過**（route-level code-split）；Pinia + Vue Router + JWT auth store + apiClient(ProblemDetails) + 路由守衛 |
| `web/packages/design-tokens` | ✅ | 萃取真實品牌色/字型。**改僅供後台/新 UI**（前台不靠 tokens） |
| OpenAPI → TS client 產生流程 | ⬜ | 待 API 有 OpenAPI 文件 |
| 後台 SPA：AdminLayout（sidebar + topbar） | ✅ | 深色側欄、模組導航、JWT 權限顯示 |
| 後台 SPA：Orders/OrderDetail/Members 頁面 | ✅ | 篩選+分頁；訂單操作按鈕；會員展開面板 |
| 後台 SPA：Products/ProductForm/Inventory/Purchases/PurchaseForm | ✅ | 商品雙用表單；庫存查看；採購單建立 |
| 後台 SPA：Accounting/Cms/AdminAccounts/Returns 頁面 | ✅ | AP/AR/退款 tabs；CMS 6 模組 tabs；管理員 Lims 樹狀權限 modal |
| 後台 SPA：router/index.ts（AdminLayout 包裹所有子路由） | ✅ | lazy-loaded；/login public |
| Nuxt：memberAuth Pinia store | ✅ | JWT in-memory；member role login |
| Nuxt：購物車頁（Cart/index.vue） | ✅ | 數量編輯；運費計算；空車提示 |
| Nuxt：結帳頁（Checkout/index.vue） | ✅ | 完整表單；折扣碼驗證；POST /store/orders |
| Nuxt：訂單成功（Order/Success.vue） | ✅ | ATM 虛擬帳號顯示 |
| Nuxt：Member Login/Register/Center/Orders/OrderDetail | ✅ | 完整會員流程 |

## Phase 2.1 — 前台像素級視覺保真（§7.1，進行中）

> 策略：**逐字沿用**舊 CSS/markup/資產，靠相同 CSS+DOM 保證一模一樣（非 tokens 重寫）。

| 項目 | 狀態 | 備註 |
|---|---|---|
| 複製 `main.css`(124KB)/`PagedList.css`/`jquery-confirm.min.css` 原檔 | ✅ | 進 `public/content/styles/`，以 static `<link>` 載入（非 Vite import，保留 url() 路徑） |
| 複製 185 張圖到 `public/content/images/`（保留結構） | ✅ | url(../images/...) 正確解析 |
| 複製 vendor.js/plugins.js/main.js + jquery.validate/confirm | ✅ | 進 `public/scripts/` |
| `_Header`/`_Footer`/`_SubMenu` 逐字移植成 Vue 元件 + Nuxt layout | ✅ | `<SiteHeader>`/`<SiteFooter>`/`<MobileMenu>` + `layouts/default.vue`，相同標籤/class；route 用保留網址 |
| Google Fonts（GFS Didot｜Noto Sans） | ✅ | 同舊 `_Styles.cshtml` |
| jQuery 外掛載入（body close，同舊 `_Scripts.cshtml` 順序） | ✅ | slidebars/sticky/輪播；**store build 通過** |
| **SSR 實測** | ✅ | 首頁 SSR HTML 含 `#header/#topnav/#menuNavigation/foodiesword/sb-slidebar` + 正確 nav 文案；`main.css`(200,124048B)、圖、vendor.js 皆 200 |
| **首頁逐字移植**（`Views/MainMs/Index.cshtml` → `index.vue`）+ `ProductCard.vue`（`_PartialProduct`） | ✅ | banner owl-carousel(4 style)/最新消息/熱銷/食在呼料理/最新商品/subscribe/綠誌；`useHomeData()` 綁定（API 就緒前空陣列）；**build + SSR 實測 section 皆渲染** |
| 其餘頁內容（商品列表/詳情、品牌、News/Recipes/Issues、Events…） | ✅ | Products/ProductDetail/Brand/News/NewsDetail/Recipes/RecipeDetail/Issues/IssueDetail/Events/EventDetail + PaginationBar + 11 composables；**build 通過** |
| Playwright 視覺回歸 diff（1280/1024/768/375） | ✅(harness) | `web/visual-regression`（config+spec），CI `visual.yml`；待擷取舊站 baseline |

## Phase 2.2 — 金流/發票整合層（全部完成）

| 項目 | 狀態 | 備註 |
|---|---|---|
| ports：`IPaymentGateway` + `IInvoiceService` + DTO/enum | ✅ | PaymentRequest/Notice/Refund、InvoiceRequest/Item/IssueMode… |
| **Fisc codec**（HMAC-SHA256 verifyCode + AES-GCM 欄位加解密） | ✅ | `FiscMessageCodec` + `FiscOptions`；**5 測試** |
| **ezPay codec**（AES-256-CBC HashKey/IV + lower-hex + PostData_ builder + SHA256 CheckCode） | ✅ | `EzPayCodec` + `EzPayOptions`；**5 測試** |
| **FiscPaymentGateway**（HttpClient + verifyCode + AES-GCM + Create/Query/Refund/ParseNotify） | ✅ | `src/TFoodies.Infrastructure/Payments/Fisc/FiscPaymentGateway.cs` |
| **EzPayInvoiceService**（HttpClient + PostData_ + JSON 回應解析 + Issue/Allowance/Void） | ✅ | `src/TFoodies.Infrastructure/Invoicing/EzPay/EzPayInvoiceService.cs` |
| 全測試 | ✅ | **27 passed**（Domain 17 + Infra 10） |

## Phase 2.3 — 前台 Store API + Auth（完成）

| 項目 | 狀態 | 備註 |
|---|---|---|
| `IStoreQueryService` / `StoreQueryService`（Dapper，12 端點） | ✅ | home/products/brands/news/recipes/issues/events |
| `StoreController` + RouteTable 路由 | ✅ | 12 GET 端點全部接通 |
| `IJwtTokenService` / `JwtTokenService`（HS256 + in-memory refresh token） | ✅ | Singleton；refresh token rotate |
| `IAuthService` / `AuthService`（member/admin 登入 + PBKDF2 hash-on-login + 移除 itadmin 後門） | ✅ | 自動升級明文密碼 |
| `AuthController`（POST /auth/login + /auth/refresh） | ✅ | |

## Phase 2.3.1 — 前台 store API ↔ Nuxt 接通（完成並實機驗證）

> 策略：後端 API 維持乾淨 camelCase，前台在 **composable transform** 內映射成 legacy view-model（欄位名 + blobUrl + `yyyy-MM-dd` 日期）；頁面與 `ProductCard` 完全不動（像素級保真）。共用 `app/utils/storeMap.ts`（`mapProduct`/`ymd`/`mapPhotos`/`mapRecipeRef`）。

| 項目 | 狀態 | 備註 |
|---|---|---|
| 12 個內容 composable 改 transform 接 API | ✅ | home/products/productDetail/brand/news(+detail)/recipes(+detail)/issues(+detail)/events(+detail) |
| 後端補齊像素級缺漏資料 | ✅ | `ProductListItem` 加 capacity/isDisabled/sort；`RecipeListItem` 加 v；ProductDetail 加關聯食譜（Recipeproducts）+ 品牌 intro/storybgclass/isdisplay；RecipeDetail 加關聯商品；IssueDetail 加關聯商品/食譜/others；NewsDetail 加 others；Products 端點改回 `{productTypes,currentType,products}`；BrandDetail 加 productCount→hasMore |
| store 分頁端點 `PaginatedResponse.Create` 參數順序 bug | ✅ | 4 處 `(items,page,pageSize,total)`→`(items,total,page,pageSize)`；實測 news total=28/totalPages=3 |
| blobUrl 進 `runtimeConfig.public`（`NUXT_PUBLIC_BLOB_URL` 可覆寫） | ✅ | 預設 `https://tfoodiesblob.blob.core.windows.net/tfoodies/`（同舊 ViewBag.BlobUrl） |
| sitemap server route 改 camelCase 欄位 | ✅ | `__sitemap-urls.ts`：productTypes/newId/recipeId/eventId |
| `func start` 對真實 DB 實機驗證 12 端點 | ✅ | 全回正確資料；junction（Recipeproducts/Issueproducts/Issuerecipes）+ `DECLARE @iid` issue 查詢驗證通過 |

## Phase 3 — 核心服務（大部分完成）

| 項目 | 狀態 | 備註 |
|---|---|---|
| `ICodeNumberService` / `SqlCodeNumberService`（MERGE HOLDLOCK 原子單號） | ✅ | 9 種 code；`CodeKind.Order/Purchase/Expenditure/Income/Invoice/…` |
| `IStockAllocator` / `SqlStockAllocator`（FIFO 效期揀貨 UPDLOCK/ROWLOCK） | ✅ | `AllocationResult { IsSufficient, Picks }` |
| `IDiscountService` / `DiscountService`（折扣碼驗證 + 計算） | ✅ | istype 0=折百分比, 1=固定 NTD；isonetime 0/1/2 |
| `OrderSettings`（運費門檻 2000/120，ATM 前綴/效期） | ✅ | `appsettings:Order` section |
| `IOrderService` / `OrderService`（PlaceOrderAsync + GetOrderAsync + GetMemberOrdersAsync） | ✅ | guest member 自動建立；FIFO 庫存；國泰 ATM 虛擬帳號產生（Cathay 演算法）；ATM 2 tests |
| `IAdminPermissionService` / `SqlAdminPermissionService`（Lims/AdminLims RBAC） | ✅ | AdminID=888 永遠拒絕；namespace=Permissions 避免 CS0118 |
| 下單 API（`StoreOrderController`）+ 折扣預覽 + 訂單查詢 | ✅ | POST /store/orders, GET /store/orders/{code}, POST /store/discount/apply |
| 會員訂單 API（`MemberController`）— JWT member | ✅ | GET /member/orders（分頁）, GET /member/orders/{code} |
| 後台訂單管理（`OrderAdminController`）— OrderMs RBAC | ✅ | 6 端點：list/detail/pending/ship/cancel/pay；Vue `o.code`/`o.createdAt` 已對齊 |
| 後台商品管理（`ProductAdminController`）— ProductMs RBAC | ✅ | 7 端點；Vue `productId`/`brandName`/`typeName` 已對齊 |
| 後台會員管理（`MemberAdminController`）— MemberMs RBAC | ✅ | 4 端點；Vue `m.id`/`m.level`/`m.createdAt` 已對齊 |
| 後台庫存管理（`InventoryAdminController`）— InventoryMs RBAC | ✅ | 4 端點；Vue `stockId`/`productId`/`warehouseName`/`qty`/`expireDate` 已對齊 |
| 後台採購管理（`PurchaseAdminController`）— PurchaseMs RBAC | ✅ | 7 端點；Vue `purchaseId`/`supplierName`/`purchaseDate` 已對齊 |
| 後台財務 AP/AR（`AccountingAdminController`）— AccountingMs RBAC | ✅ | 9 端點：expenditures/outcomes/ar-invoices/incomes/refounds |
| 金流 callback（`PaymentNotifyController`）— 公開 webhook | ✅ | 冪等；UPDATE paystatus→Paid + INSERT Income + fire-and-forget ezPay 發票 |
| 後台管理員帳號（`AdminAccountController`） | ✅ | GET/POST/PUT /admin/admin-accounts；GET/PUT /admin/admin-accounts/{id}/permissions；Lims 樹狀 RBAC |
| 後台 CMS（`CmsAdminController`）— HomeMs RBAC | ✅ | banners/news/recipes/issues/events/knowledges 各 CRUD |
| 後台發票管理（`InvoiceAdminController`）— InvoiceMs RBAC | ✅ | GET /admin/invoices；PATCH void/allowance |
| 退貨 API（`ReturnController`）+ 後台退貨（`ReturnAdminController`） | ✅ | POST /store/returns；GET /member/returns；後台 list/detail/receive/refund |
| 會員擴充（`MemberAuthController`/`MemberProfileController`） | ✅ | register/forgot-password/reset；PATCH /member/profile；GET/POST/DELETE /member/wishlist |
| 後台管理員登入（`AdminAuthController`） | ✅ | POST /auth/admin/login（含 username+permissions）；POST /auth/admin/logout |
| SMS 適配器（`ISmsService`/`MitakeSmsService`） | ✅ | 三竹 Mitake API；驗證碼寄送 |
| ATM 過期排程（`AtmExpiryFunction`） | ✅ | Timer trigger 每日 1am UTC；UPDATE paystatus=Expired WHERE expirepaydate < today |
| `appsettings.example.json` | ✅ | 所有 config section 佔位值範本 |
| `scripts/add-indexes.sql` | ✅ | Orders/Members/Products/Warehousestocks 效能索引 |
| Application.Tests（TaiwanVat） | ✅ | 5 xUnit tests；全 44 passed |
| 背景任務（email queue、銀行對帳 timer） | ⬜ | Service Bus worker、IncomeMs 匯入 |

## Phase 3.5 — 後台 API ↔ 前端接通（已完成）

| 項目 | 狀態 | 備註 |
|---|---|---|
| `PaginatedResponse<T>` 結構修正 | ✅ | `data`→`items`；移除 nested `pagination`，改為 flat `total`/`totalCount`/`page`/`pageSize`/`totalPages`；`Create(items, page, pageSize, totalCount)` 參數順序 bug 修正為 `(items, totalCount, page, pageSize)` |
| 全域 camelCase JSON 政策 | ✅ | `Program.cs` 加 `Configure<JsonOptions>`（PropertyNamingPolicy = CamelCase），所有 OkObjectResult 回應一律 camelCase |
| Dapper dynamic → anonymous 投影（6 個 controller） | ✅ | Orders: `Code`（原 `OrderCode`）；Members: `id`/`level`/`createdAt`；Inventory: `stockId`/`productId`/`warehouseName`/`qty`/`expireDate`；Products: `productId`/`brandName`/`typeName`；Purchases: `purchaseId`/`supplierName`/`createdAt`；Discounts: `discountId` |
| 後台 SPA Discounts/Reports 模組 | ✅ | `DiscountAdminController` + `ReportAdminController`；DB 補 Lims InvoiceMs(55)/DiscountMs(56)；navItems module key 全部對齊 DB `Lims.[Key]` |
| itadmin AdminID=888 DB + superadmin bypass | ✅ | DB `SET IDENTITY_INSERT Admins ON` 補 888 記錄；`SqlAdminPermissionService` IsActive/HasPermission/GetPermissions 加 888 bypass |

## Phase 4 — 部署（Track C）

| 項目 | 狀態 | 備註 |
|---|---|---|
| Bicep infra（`infra/main.bicep`） | ✅ | store=**Container App**(Nuxt SSR, scale-to-zero)+ACR / admin=SWA / Functions Flex / storage 等；前台已從 SWA 改 Container Apps（SEO 全 SSR）；SQL 不重建 |
| GitHub Actions（`.github/workflows/`） | ✅ | `api`(test→slot→smoke→swap)/`store`(az acr build→containerapp update)/`admin`(SWA deploy)/`infra`(bicep what-if+deploy)/`visual` |
| `staticwebapp.config.json`（admin） | ✅ | admin SPA fallback；store 改 SSR 已移除其 SWA config |
| Playwright 視覺回歸 harness | ✅ | `web/visual-regression`（config 1280/1024/768/375 + parity.spec）；待擷取舊站 baseline |
| `.gitignore`（bin/obj/node_modules/.output/secrets） | ✅ | |

## 已知阻擋 / 待確認
- ❓ Refresh token / 購物車 / 冪等鍵儲存：Azure Table Storage vs Redis。
- ❓ 是否允許補非叢集索引（效能）。

## 專案結構地圖（檔案在哪）
```
/Users/tim/webapps/tfoodies
├── global.json, Directory.Build.props, TFoodies.sln, .gitignore
├── status.md                         ← 本檔
├── scripts/scaffold-db.sh            ← EF DB-First scaffold（需 DB 連線）
├── infra/main.bicep                  ← 全部 Azure 資源（az bicep build 過）
├── .github/workflows/                ← api/store/admin/infra/visual.yml
├── src/
│   ├── TFoodies.Domain/Common/       ← Result/Error/Money/TaiwanVat　·　Enums/Enums.cs
│   ├── TFoodies.Application/Abstractions/  ← IUnitOfWork/IDbConnectionFactory/ICodeNumberService/
│   │                                         IStockAllocator/IPaymentGateway/IInvoiceService
│   ├── TFoodies.Infrastructure/
│   │   ├── Persistence/SqlConnectionFactory.cs + Scaffolded/（待產生）
│   │   ├── Payments/Fisc/FiscMessageCodec.cs + FiscOptions.cs
│   │   ├── Invoicing/EzPay/EzPayCodec.cs + EzPayOptions.cs
│   │   └── DependencyInjection.cs    ← DbContext/UoW 註冊處（含 TODO 註解）
│   ├── TFoodies.Contracts/Common/    ← ApiResponse<T>/ApiErrorResponse/PaginatedResponse<T>
│   └── TFoodies.Api.Functions/       ← Program.cs / Http/{ApiRouter,RouteContext,Router/} / Middleware/{IMiddleware,MiddlewarePipeline,Cors,Correlation,Exception,JwtAuth} / Helpers/JwtHelper
├── tests/  Domain.Tests / Application.Tests(空) / Infrastructure.Tests（codec 測試）
├── web/                              ← npm workspaces
│   ├── store/   Nuxt 4 SSR：app/{components,layouts,pages,composables,stores,utils}
│   │            public/content/{styles,images} + public/scripts ← 舊站逐字資產
│   ├── admin/   Vue 3 SPA：src/{router,stores,views,lib}
│   ├── packages/design-tokens/       ← 僅後台用
│   └── visual-regression/            ← Playwright（playwright.config + tests/parity.spec）
└── reference/old/  （唯讀舊系統）   reference/card, reference/invoice（規格）
    docs/, CLAUDE.md
計畫全文：/Users/tim/.claude/plans/reference-old-reference-card-woolly-bear.md
```

## 下一步（優先序）
1. **後台 E2E 實機驗證**：啟動 `func start` + Vue dev server，以 itadmin (888/QQQQQ) 登入，確認各頁面列表資料正常顯示（Orders/Members/Products/Inventory/Purchases/Discounts）。
2. **AccountingView / ReturnView 屬性對齊**：這兩個 view 用 `items.Cast<object>()` 回傳 Dapper dynamic，尚未確認前端 interface 是否完全對齊（其餘已修正）。
3. **Playwright baseline**：對 `https://www.tfoodies.com` 擷取 baseline，啟用視覺回歸 gate（`web/visual-regression`）。
4. **背景任務**：銀行對帳（IncomeMs 匯入）、Email queue（Azure Service Bus）。
5. **OpenAPI + TS client**：swagger/openapi.json 產生 → TypeScript client（store + admin）自動對應，取代手寫 apiFetch 呼叫。

## 驗證指令
```bash
dotnet build TFoodies.sln                 # 全建置（0/0）
dotnet test                               # 全測試（44 passed：Domain 17 + App 5 + Infra 12 + API.Functions 10）
cd src/TFoodies.Api.Functions && func start   # 本地起 API（需 Azurite：azurite --silent &）
curl localhost:7071/api/health            # 健康檢查 → {"status":"ok"}
cd web && npm install                     # 前端相依（首次）
npm --workspace store run build           # 前台 Nuxt SSR build
npm --workspace admin run build           # 後台 SPA build
# 本地預覽前台並看舊站視覺：
(cd web/store && PORT=3000 node .output/server/index.mjs &) ; sleep 4 ; curl -s localhost:3000/ | head
```
