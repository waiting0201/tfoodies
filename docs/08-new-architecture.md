# 08 · 新系統架構（docs/08-new-architecture.md）

> 本文描述**新系統**（.NET 9 重構）的多專案分層設計。
> 舊系統架構見 [docs/01-architecture.md](01-architecture.md)。
> 上次更新：2026-06-09（新增訂單編輯、圖片上傳機制）

---

## 1. 為何是多專案，而非單一專案

參考專案 `quotation.weypro.com/Api` 是**單一 csproj**，所有程式碼（Models、Services、Controllers、DTOs、Middleware、Router）都在同一個專案裡。

TFoodies 採**多專案分層**，原因：

| 考量 | 說明 |
|---|---|
| 規模 | 72 張資料表（vs 報價 ~20 張）、5 個整合（金流/發票/Blob/SMS/MongoDB）、複雜業務規則（FIFO 庫存、原子單號、VAT） |
| 可測試性 | Domain/Application 不依賴任何基礎設施，可跑純 unit test，不需要 DB 或 Azure 模擬 |
| 可替換性 | Infrastructure 可以換（例如：把 EF Core 換成純 Dapper），不影響 Application 層 |
| 強制相依方向 | csproj 的 `ProjectReference` 限制了誰能呼叫誰，防止 Controller 直接存取 DbContext |

> 報價系統夠小（13 張表、1 個整合），單專案的成本效益最高。  
> TFoodies 規模下，分層的效益才大於成本。

---

## 2. 專案一覽

```
src/
├─ TFoodies.Domain/          純業務規則（無外部相依）
├─ TFoodies.Application/     介面定義、Use Case 協調（只依賴 Domain）
├─ TFoodies.Infrastructure/  外部實作（DB、金流、Blob…）（依賴 Application）
├─ TFoodies.Contracts/       API 請求/回應 DTO（依賴 Domain）
└─ TFoodies.Api.Functions/   Azure Functions host（依賴所有層）

tests/
├─ TFoodies.Domain.Tests/         ← 17 tests（VAT parity, Money, Enum）
├─ TFoodies.Application.Tests/    ← 空（待補）
└─ TFoodies.Infrastructure.Tests/ ← 12 tests（codec 10 + ATM 2）
```

---

## 3. 各專案職責

### TFoodies.Domain
**「業務規則的真相」**。不依賴任何 NuGet 套件或外部服務（純 C# 類別庫）。

內容：
- `Common/Result.cs` — `Result<T>` / `Error`：業務操作回傳（取代吞例外的舊 `IResult`）
- `Common/Money.cs` — 金額值物件（不可變、精度安全）
- `Common/TaiwanVat.cs` — 台灣 5% VAT 計算（`price - round(price/1.05)`，單一真相）
- `Enums/Enums.cs` — 15 個狀態機 Enum（訂單、庫存、發票…），DB 數值不變

不應放在這裡：DB 存取、HTTP、外部 API 呼叫、DI 設定。

---

### TFoodies.Application
**「系統能做什麼的邊界定義」**。只依賴 `Domain`；不含任何實作。

內容（`Abstractions/`）：
- `IUnitOfWork` / `IUnitOfWorkTransaction` — 工作單元（`SaveChangesAsync`、`BeginTransactionAsync`）
- `IDbConnectionFactory` — Dapper 連線工廠（`CreateConnection()`）
- `ICodeNumberService` — 原子單號產生（9 種 CodeKind，MERGE HOLDLOCK）
- `IStockAllocator` / `AllocationResult` / `StockPick` — FIFO 庫存揀貨
- `IPaymentGateway` / `IInvoiceService` — 金流/電子發票 port
- `IAuthService` — 會員/管理員登入（回傳 JWT token pair）
- `IJwtTokenService` — HS256 token 產生/驗證/刷新
- `IDiscountService` / `DiscountResult` — 折扣碼驗證 + 折扣金額計算
- `IOrderService` — 前台下單、查單、會員訂單列表
- `IStoreQueryService` — 前台商品/CMS 查詢（Dapper 讀）
- `IAdminPermissionService` / `AdminModulePermission` — 後台 Lims/AdminLims RBAC

這層是「port」（六角架構術語），`Infrastructure` 提供「adapter」。

不應放在這裡：SQL、HttpClient、EF Core、任何 `new SqlConnection()`。

---

### TFoodies.Infrastructure
**「外部世界的橋樑」**。依賴 `Application`（實作其 port）和 `Domain`。

內容：
```
Persistence/
  TfoodiesContext.cs           ← EF Core 9 Scaffolded（72 表，Database-First）
  Scaffolded/                  ← 72 個 EF 實體（by scaffold-db.sh，唯讀，勿手改）
  SqlConnectionFactory.cs      ← Dapper 連線工廠（Singleton）
  EfUnitOfWork.cs              ← IUnitOfWork 實作（Scoped，包裝 TfoodiesContext）
Auth/
  JwtSettings.cs               ← HS256 key/issuer/audience/expiry
  JwtTokenService.cs           ← IJwtTokenService（Singleton，in-memory refresh）
  AuthService.cs               ← IAuthService（PBKDF2 hash-on-login，自動升級明文）
Payments/Fisc/
  FiscMessageCodec.cs          ← 智付通 HMAC-SHA256 + AES-GCM codec
  FiscOptions.cs
  FiscPaymentGateway.cs        ← IPaymentGateway（HttpClient）
Invoicing/EzPay/
  EzPayCodec.cs                ← 藍新 AES-256-CBC + SHA256 codec
  EzPayOptions.cs
  EzPayInvoiceService.cs       ← IInvoiceService（HttpClient）
Orders/
  OrderSettings.cs             ← 運費門檻/金額、ATM 前綴/效期（appsettings:Order）
  DiscountService.cs           ← IDiscountService（istype 0=%, 1=固定；isonetime）
  OrderService.cs              ← IOrderService（PlaceOrderAsync/GetOrderAsync/…）
                                  ⚠ BuildAtmCode() public static（ATM 國泰演算法）
CodeNumbers/
  SqlCodeNumberService.cs      ← ICodeNumberService（MERGE HOLDLOCK，9 種 CodeKind）
Inventory/
  SqlStockAllocator.cs         ← IStockAllocator（FIFO，UPDLOCK/ROWLOCK）
                                  ⚠ namespace=Inventory（避免 CS0118 'Stock' 衝突）
Permissions/
  SqlAdminPermissionService.cs ← IAdminPermissionService（Lims/AdminLims RBAC）
                                  ⚠ namespace=Permissions（避免 CS0118 'Admin' 衝突）
Store/
  StoreQueryService.cs         ← IStoreQueryService（Dapper 讀；12 前台查詢）
DependencyInjection.cs         ← AddInfrastructure()，對外唯一入口
```

**命名空間衝突規則**：Infrastructure 資料夾名若與 EF Scaffolded 實體名相同，會引發 CS0118。已知衝突：
- `Stock` 實體 → Infrastructure 資料夾改用 `Inventory`（namespace `TFoodies.Infrastructure.Inventory`）
- `Admin` 實體 → Infrastructure 資料夾改用 `Permissions`（namespace `TFoodies.Infrastructure.Permissions`）

不應放在這裡：HTTP 路由、Azure Functions 觸發器、Controller 邏輯。

---

### TFoodies.Contracts
**「API 的資料型別」**。依賴 `Domain`（可用 Domain Enum/Value Object）。

內容：
```
Common/
  ApiResponse.cs       ← ApiResponse<T>、ApiErrorResponse、ApiError
  PaginatedResponse.cs ← PaginatedResponse<T>、PaginationInfo
```

未來按模組新增子目錄：
```
Store/
  ProductListDto.cs
  ProductDetailDto.cs
  …
Auth/
  LoginRequest.cs
  LoginResponse.cs
Admin/
  …
```

對應報價系統的 `DTOs/` 目錄，但獨立成專案以便未來生成 OpenAPI → TypeScript client。

---

### TFoodies.Api.Functions
**「Azure Functions host + HTTP 請求處理」**。依賴所有層。

結構與報價系統相同（DIY Router 模式），詳見 [docs/07-api-pattern.md](07-api-pattern.md)：

```
Functions/ApiFunction.cs     ← Azure Function trigger（catch-all）
Router/
  RouteContext.cs            ← 每次請求的上下文（HttpContext 包裝）
  RouteTable.cs              ← 所有路由定義（Singleton，HandlerFactory 延遲解析）
  RouteHandler.cs            ← 預編譯 Regex，dispatch 到 HandlerFactory
Middleware/                  ← Cors/Correlation/Exception/JwtAuth（全 Singleton）
Helpers/
  JwtHelper.cs               ← Bearer token 解析
  AdminGuard.cs              ← 後台 RBAC 守衛（RequireAdmin / AuthorizeAsync）
Controllers/
  StoreController.cs         ← 前台商品/CMS（12 GET 端點，公開）
  AuthController.cs          ← POST /auth/login, /auth/refresh
  StoreOrderController.cs    ← POST /store/orders, GET /store/orders/{code}, POST /store/discount/apply
  MemberController.cs        ← GET /member/orders（JWT member 身份）
  PaymentNotifyController.cs ← POST /store/payment/notify（公開 webhook）
  Admin/
    OrderAdminController.cs      ← /admin/orders（OrderMs RBAC）
                                   GET /admin/orders, GET /admin/orders/{code}
                                   PUT /admin/orders/{code}（全欄位編輯，含 items diff）
                                   PATCH /admin/orders/{code}/pending|ship|cancel|pay
    ProductAdminController.cs    ← /admin/products + /admin/brands + /admin/producttypes（ProductMs）
    MemberAdminController.cs     ← /admin/members（MemberMs）
    InventoryAdminController.cs  ← /admin/warehouses + /admin/inventory + /admin/stocks（InventoryMs）
    PurchaseAdminController.cs   ← /admin/suppliers + /admin/purchases（PurchaseMs）
    AccountingAdminController.cs ← /admin/expenditures + /admin/outcomes + /admin/ar-invoices
                                    + /admin/incomes + /admin/refounds（AccountingMs）
    ReturnAdminController.cs     ← /admin/returns（ReturnMs）
    CmsAdminController.cs        ← /admin/cms/*（HomeMs：橫幅/新聞/食譜/FAQ）
    InvoiceAdminController.cs    ← /admin/invoices（invoiceStatus 篩選、void、allowance）
    AdminAccountController.cs    ← /admin/admin-accounts（管理員帳號 CRUD + 模組權限設定）
    DiscountAdminController.cs   ← /admin/discounts（DiscountMs；列表/新增/明細/更新/軟刪除）
    ReportAdminController.cs     ← /admin/reports/sales + /admin/reports/amounts（ReportMs）
Program.cs     ← DI 組合根（所有 Controller 以 AddScoped 註冊）
```

**AdminGuard 模式**：
```csharp
// JWT 驗證 + 模組權限（一行）
var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "OrderMs", AdminOperation.Read);
if (guard.Result is not null) return guard.Result;  // 401/403 立即回傳
// 使用 guard.AdminId ...

// 僅 JWT 驗證（不檢查 RBAC 模組）
var guard = AdminGuard.RequireAdmin(ctx);
if (guard.Result is not null) return guard.Result;
```

**Captive Dependency 防止方式**：`RouteTable`（Singleton）的 `HandlerFactory` lambda 接收 `IServiceProvider`，在請求時才 `sp.GetRequiredService<TController>()` 解析 Scoped Controller，確保 Singleton 不持有 Scoped 實例。

---

### 前台 Vue 管理介面（web/admin）

技術棧：**Vue 3 + TypeScript + Tailwind CSS v4 + Vite + Pinia**

```
web/admin/src/
├─ components/
│    AdminLayout.vue      ← 主框架：slate-900 左側 sidebar + sticky topbar + RouterView
├─ views/
│    LoginView.vue                    ← 登入頁（split panel；dark 左品牌 + 右表單）
│    DashboardView.vue                ← 儀表板（stat cards + 模組快速導覽）
│    orders/
│      OrdersView.vue                 ← 訂單列表（分頁、狀態篩選）
│      OrderCreateView.vue            ← 新增訂單（兩欄表單，商品搜尋含縮圖+編號）
│      OrderDetailView.vue            ← 訂單明細（含出貨/取消操作，「編輯訂單」按鈕）
│      OrderEditView.vue              ← 訂單編輯（兩欄表單，預填所有欄位含收件/付款/出貨/發票/items）
│    products/
│      ProductsView.vue               ← 商品列表
│      ProductFormView.vue            ← 新增/編輯商品表單
│    members/
│      MembersView.vue                ← 會員列表
│    inventory/
│      InventoryView.vue              ← 庫存總覽（倉庫/效期/批次）
│    purchases/
│      PurchasesView.vue              ← 採購單列表
│      PurchaseFormView.vue           ← 新增/編輯採購單
│    accounting/
│      AccountingView.vue             ← 財務管理（支出/收入/AR）
│    returns/
│      ReturnsView.vue                ← 退貨管理
│    cms/
│      CmsView.vue                    ← 內容管理（橫幅/新聞/食譜）
│    invoices/
│      InvoicesView.vue               ← 發票管理（狀態篩選、作廢、折讓）
│    accounts/
│      AdminAccountsView.vue          ← 管理員帳號與模組權限設定
│    discounts/
│      DiscountsView.vue              ← 折扣碼 CRUD（slide-in panel 編輯）
│    reports/
│      ReportsView.vue                ← 銷售報表 Tab（商品銷量 / 訂單金額）
├─ stores/
│    auth.ts              ← Pinia：accessToken(記憶體)、login/logout、can(module)
├─ lib/
│    apiClient.ts         ← apiFetch<T>()：注入 Bearer token，統一錯誤處理
├─ router/index.ts        ← Vue Router；beforeEach 未登入導 /login
└─ style.css              ← @import "tailwindcss"; IBM Plex Sans/Mono 字型
```

**Auth flow**：`POST /auth/admin/login` → `{ accessToken, refreshToken, username, permissions[] }` → Pinia 存 token（記憶體，XSS 安全）→ `auth.can(module)` 控制 sidebar 顯示。

---

## 4. 相依方向（只能往下，不能往上）

```
TFoodies.Domain
       ▲
TFoodies.Application        TFoodies.Contracts
       ▲                           ▲
TFoodies.Infrastructure            │
               ▲                   │
         TFoodies.Api.Functions ───┘
```

規則：
- `Domain` → 不依賴任何人
- `Application` → 只依賴 `Domain`
- `Infrastructure` → 依賴 `Application`（實作其 port）+ `Domain`
- `Contracts` → 依賴 `Domain`（可引用 Enum/Value Object）
- `Api.Functions` → 依賴所有人（組合根）

---

## 5. 對照報價系統（單專案 vs 多專案）

| 面向 | 報價系統（單一 csproj） | TFoodies（多 csproj） |
|---|---|---|
| Models（EF 實體） | `Models/` | `Infrastructure/Persistence/Scaffolded/` |
| DbContext | `Models/QuotationDbContext.cs` | `Infrastructure/Persistence/TfoodiesContext.cs` |
| Services（業務邏輯） | `Services/` | `Infrastructure/`（外部整合）+ `Application/`（介面） |
| DTOs | `DTOs/` | `TFoodies.Contracts/` |
| Controllers | `Controllers/` | `Api.Functions/Controllers/`（待建） |
| Router | `Router/` | `Api.Functions/Router/` |
| Middleware | `Middleware/` | `Api.Functions/Middleware/` |
| Helpers | `Helpers/` | `Api.Functions/Helpers/` |
| Program.cs | `Program.cs` | `Api.Functions/Program.cs` |
| 業務規則值物件 | 無 | `TFoodies.Domain/`（Result、Money、VAT、Enum） |

---

## 6. 新增功能時，程式碼放哪裡

| 任務 | 放在哪個專案 |
|---|---|
| 新增 API 路由（前台/後台/認證） | `Api.Functions/Controllers/` + `Router/RouteTable.cs` |
| 定義新的 DTO | `Contracts/` 按模組子目錄 |
| 定義新的業務介面（port） | `Application/Abstractions/` |
| 實作資料庫存取（Dapper query） | `Infrastructure/` 新建 Query service |
| 實作外部 API 整合 | `Infrastructure/` 新建 adapter |
| 業務規則（計算、驗證） | `Domain/` |
| 修改 DI 註冊 | `Infrastructure/DependencyInjection.cs` 或 `Application/DependencyInjection.cs` |
| 修改 EF 實體 | ⚠️ 不要手改 `Scaffolded/`；改 DB 後重跑 `scaffold-db.sh` |

---

## 7. 圖片上傳與呈現機制（Blob Storage）

> **規則：所有欄位的圖片上傳與顯示，必須使用本節描述的統一機制。**
> 與舊系統一致：DB 只存純檔名，URL 由 `BaseUrl + ContainerName + fileName` 組合而成。

### 7.1 環境變數

| 位置 | 變數 | 說明 | 範例（本地） |
|---|---|---|---|
| 後端（`local.settings.json` / Azure App Settings） | `AzureBlob__ConnectionString` | Storage 連線字串 | `UseDevelopmentStorage=true` |
| 後端 | `AzureBlob__ContainerName` | Container 名稱（對應舊系統 `azure.blob.container`） | `tfoodies` |
| 後端 | `AzureBlob__BaseUrl` | Storage account URL，**不含 container**（對應舊系統 `azure.blob.url`） | `http://127.0.0.1:10000/devstoreaccount1` |
| 前端（`.env` / `.env.production`） | `VITE_BLOB_URL` | 同後端 `AzureBlob__BaseUrl` | `http://127.0.0.1:10000/devstoreaccount1` |
| 前端 | `VITE_BLOB_CONTAINER` | 同後端 `AzureBlob__ContainerName` | `tfoodies` |

完整圖片 URL = `BaseUrl` + `/` + `ContainerName` + `/` + `fileName`  
等同舊系統：`azure.blob.url` + `"/"` + `azure.blob.container` + `"/"` + `entity.photo`

### 7.2 後端：上傳 API

**端點：** `POST /api/admin/upload`  
**權限：** 需有效後台 JWT（任何模組）  
**Content-Type：** `multipart/form-data`，field 名稱 `file`

```
Request:  multipart/form-data { file: <image> }
Response: { "fileName": "20260609193238xx.jpg" }
```

- 允許格式：`.jpg` `.jpeg` `.png` `.gif` `.webp`
- 檔名格式：`DateTime.Now.ToString("yyyyMMddHHmmssff") + 副檔名`（與舊系統一致）
- **回傳純檔名，不含 URL**
- 實作位置：`Api.Functions/Controllers/Admin/UploadAdminController.cs`
- Blob service 介面：`Application/Abstractions/IBlobService.cs`
- Blob service 實作：`Infrastructure/Blob/AzureBlobService.cs`（Singleton）
- 合併 URL 屬性：`AzureBlobOptions.BlobUrl`（`BaseUrl + "/" + ContainerName`）

### 7.3 後台表單：上傳流程

1. 使用者選擇圖片 → `<input type="file">`
2. 以 `FormData` 打 `POST /api/admin/upload`（透過 `apiFetch`）
3. 取得 `{ fileName }` → 存入表單欄位（純檔名）
4. 儲存表單時，`fileName` 隨其他欄位寫入 DB

```ts
const fd = new FormData()
fd.append('file', file)
const { fileName } = await apiFetch<{ fileName: string }>('/admin/upload', { method: 'POST', body: fd })
form.photo = fileName  // 存純檔名
```

> ⚠️ `apiFetch` 對 `FormData` body **不可設** `Content-Type`，否則會破壞 multipart boundary。
> 目前 `apiClient.ts` 已處理：`!(init.body instanceof FormData)` 才補 `application/json`。

### 7.4 前台/後台列表：顯示圖片

使用 `src/lib/blobUrl.ts` 提供的 `toBlobUrl(photo)` 轉換：

```ts
import { toBlobUrl } from '@/lib/blobUrl'
// <img :src="toBlobUrl(item.photo)" />
```

`toBlobUrl` 邏輯：
- `photo` 為空 → 回傳 `''`
- `photo` 已是完整 URL（`http://` 或 `https://`）→ 直接回傳（向後相容）
- `photo` 為純檔名 → 回傳 `VITE_BLOB_URL + "/" + VITE_BLOB_CONTAINER + "/" + photo`

### 7.5 已套用的欄位

| 模組 | 欄位 | 表單元件 | 列表元件 |
|---|---|---|---|
| 首頁輪播 Banners | `photo` | `BannerFormView.vue` | `BannersView.vue` |

> 新增有圖片的功能時，照上述 7.2–7.4 的步驟套用，並在此表補列。
