# TFoodies API 架構模式（docs/07-api-pattern.md）

> 參考：`/Users/tim/webapps/quotation.weypro.com/Api`（下稱「報價系統」）
> 上次更新：2026-06-06

---

## 1. 整體架構（DIY Router 模式）

TFoodies API（`src/TFoodies.Api.Functions`）採 **Azure Functions Isolated Worker + DIY Router**，與報價系統相同的自訂路由模式：

```
HTTP Request
  └─► ApiRouter（Function trigger，catch-all "{*route}"）
        │  建立 RouteContext（per-request）
        │  取得 req.HttpContext.RequestServices（scoped provider）
        └─► MiddlewarePipeline（Singleton）
              ├─ CorsMiddleware        → 設定 CORS headers；OPTIONS 短路 204
              ├─ CorrelationMiddleware → 讀取/產生 X-Correlation-ID
              ├─ ExceptionHandlingMiddleware → 捕捉未處理例外，對應狀態碼
              ├─ JwtAuthMiddleware     → 驗證 Bearer token；公開路由略過
              └─► RouteHandler（Singleton，Regex dispatch）
                    └─► Controller（Scoped，per-request，透過 HandlerFactory 延遲解析）
```

所有請求從同一個 Function trigger 進入，無 function-per-endpoint 分散。

---

## 2. 檔案結構

```
src/TFoodies.Api.Functions/
├─ Program.cs                        # DI 組合根（Singleton/Scoped 宣告）
├─ Http/
│   ├─ ApiRouter.cs                  # Function trigger；建立 RouteContext，驅動 pipeline
│   ├─ RouteContext.cs               # Per-request 上下文（Request, PathParams, CurrentUser, Result）
│   └─ Router/
│       ├─ RouteDefinition.cs        # record（Method, Pattern, HandlerFactory）
│       ├─ RouteTable.cs             # Singleton，RegisterRoutes() 登記所有路由
│       └─ RouteHandler.cs           # Singleton，預編譯 Regex，dispatch → Controller
├─ Middleware/
│   ├─ IMiddleware.cs                # 自訂介面（非 IFunctionsWorkerMiddleware）
│   ├─ MiddlewarePipeline.cs         # 責任鏈建構（Singleton）
│   ├─ CorsMiddleware.cs             # CORS（Singleton）
│   ├─ CorrelationMiddleware.cs      # X-Correlation-ID（Singleton）
│   ├─ ExceptionHandlingMiddleware.cs # 例外 → ApiErrorResponse（Singleton）
│   └─ JwtAuthMiddleware.cs          # Bearer JWT 驗證（Singleton）
└─ Helpers/
    └─ JwtHelper.cs                  # JWT 驗證/產生（Singleton，TODO: 實作）

src/TFoodies.Contracts/
└─ Common/
    ├─ ApiResponse.cs                # ApiResponse<T>、ApiErrorResponse、ApiError
    └─ PaginatedResponse.cs          # PaginatedResponse<T>、PaginationInfo
```

---

## 3. DI 生命週期

| 類型 | 生命週期 | 理由 |
|---|---|---|
| `RouteTable` | **Singleton** | 路由定義不變；HandlerFactory 避免捕捉 Scoped |
| `RouteHandler` | **Singleton** | Regex 預編譯，只做一次 |
| `MiddlewarePipeline` | **Singleton** | 管線結構不變；per-request 狀態存於 RouteContext |
| `CorsMiddleware` 等 | **Singleton** | 無狀態，僅依賴 Singleton 服務（Logger）|
| `JwtHelper` | **Singleton** | JWT 設定不變 |
| `TfoodiesContext` | **Scoped** | 一次 Function 呼叫一個 DbContext；避免跨請求資料污染 |
| `IUnitOfWork` | **Scoped** | 包裝 DbContext，同 scope |
| `IDbConnectionFactory` | **Singleton** | 連線池工廠，本身無狀態 |
| Controllers | **Scoped** | 依賴 DbContext/Service；透過 HandlerFactory 延遲解析 |
| Services | **Scoped** | 依賴 DbContext |

> **⚠ Captive Dependency 陷阱**：Singleton 不可直接持有 Scoped 實例。  
> `RouteTable`（Singleton）透過 `HandlerFactory`（`Func<IServiceProvider, RouteContext, Task<IActionResult>>`）  
> 在每次請求時從 scoped `req.HttpContext.RequestServices` 延遲解析 Controller，  
> 而非在建構時注入，確保生命週期邊界安全。

---

## 4. 路由註冊方式（RouteTable）

新增 Controller 時，在 `RouteTable.RegisterRoutes()` 中登記：

```csharp
// ⚠ 具體路由必須排在萬用路由之前（如 store/news/detail 優先於 store/news/{id}）
Register<StoreController>("GET", "store/products",        (c, ctx) => c.GetProducts(ctx));
Register<StoreController>("GET", "store/products/{id}",   (c, ctx) => c.GetProductById(ctx, ParseGuid(ctx, "id")));
```

`Register<TController>` 的 lambda 在請求時才執行，`sp.GetRequiredService<TController>()` 從 scoped provider 取得實例。

---

## 5. Response 格式

```jsonc
// 成功（200）
{ "data": { ... } }

// 成功建立（201）
{ "data": { ... } }

// 分頁（200）
{ "data": [...], "pagination": { "page": 1, "pageSize": 20, "totalCount": 150, "totalPages": 8 } }

// 錯誤
{ "error": { "code": "NOT_FOUND", "message": "找不到資源", "details": null } }
```

HTTP 狀態碼映射：

| 情況 | 狀態碼 | code |
|---|---|---|
| 成功讀取 | 200 | — |
| 成功建立 | 201 | — |
| 成功刪除/更新（無回傳）| 204 | — |
| 輸入驗證失敗 | 400 | `BAD_REQUEST` |
| 未登入 | 401 | `UNAUTHORIZED` |
| 無權限 | 403 | `FORBIDDEN` |
| 找不到資源 | 404 | `NOT_FOUND` |
| 業務規則違反 | 422 | `UNPROCESSABLE_ENTITY` |
| 伺服器內部錯誤 | 500 | `INTERNAL_ERROR` |

---

## 6. Middleware 說明

```
CorsMiddleware（Singleton）
  - 允許 origin：localhost:3000（Nuxt 前台）、localhost:5173（Vite 後台）
  - OPTIONS preflight → 204 短路

CorrelationMiddleware（Singleton）
  - 讀取或產生 X-Correlation-ID
  - 寫入 HttpContext.Items 供後續 middleware 讀取
  - 寫入 Response.Headers[X-Correlation-ID]

ExceptionHandlingMiddleware（Singleton）
  - ArgumentException         → 400 BAD_REQUEST
  - KeyNotFoundException      → 404 NOT_FOUND
  - UnauthorizedAccessException → 403 FORBIDDEN
  - InvalidOperationException → 422 UNPROCESSABLE_ENTITY
  - 其他                      → 500 INTERNAL_ERROR（不洩露 stack trace）

JwtAuthMiddleware（Singleton）
  - 公開前綴（store、auth、health）→ 略過驗證，直接 next()
  - 其餘路由（admin/*）→ 驗證 Bearer token，設定 context.CurrentUser
```

---

## 7. 路由群組規劃

```
GET  /health                        → ApiRouter 直接回傳（不走 pipeline）

# 前台（store）— 公開，無需登入
GET  /store/home                    → 首頁資料（banners / hotProducts / news / recipes / issues）
GET  /store/products                → 商品列表（?producttypetitle=）
GET  /store/products/detail         → 商品詳情（?title=）
GET  /store/brands/detail           → 品牌詳情（?brandtitle=）
GET  /store/news                    → 消息列表（?p=）
GET  /store/news/detail             → 消息詳情（?newid=&p=）
GET  /store/recipes                 → 料理列表（?p=&k=）
GET  /store/recipes/detail          → 料理詳情（?recipeid=&p=）
GET  /store/issues                  → 綠誌列表（?p=&k=）
GET  /store/issues/detail           → 綠誌詳情（?issuetitle=&p=）
GET  /store/events                  → 活動列表（?p=）
GET  /store/events/detail           → 活動詳情（?eventid=）

# 認證 — 公開
POST /auth/login                    → 登入（會員 / 後台共用，role 欄位區分）
POST /auth/refresh                  → Refresh token

# 後台（admin）— 需 JWT
GET  /admin/...                     → 後台各模組（待實作）
```

---

## 8. Controller 寫法（POCO，無 MVC 屬性）

Controller 是純 POCO，不繼承 ControllerBase，透過 RouteContext 取得輸入並回傳 IActionResult：

```csharp
// Scoped，由 RouteTable HandlerFactory 延遲解析
public sealed class StoreController
{
    private readonly IProductQueryService _products;

    public StoreController(IProductQueryService products) => _products = products;

    public async Task<IActionResult> GetProducts(RouteContext ctx)
    {
        var typeTitle = ctx.Request.Query["producttypetitle"].ToString();
        var items = await _products.GetListAsync(typeTitle);
        return ctx.Ok(items);
    }
}
```

> 報價系統與 TFoodies 均採此模式：  
> 無 `[ApiController]`、無 model binding 屬性，一切從 `RouteContext` 手動取得。

---

## 9. DB 存取原則

- **讀取（列表 / 複雜 JOIN）→ Dapper**：效能佳，SQL 可讀性高  
- **寫入（新增 / 更新 / 刪除）→ EF Core**：型別安全，`SaveChangesAsync` 一次提交  
- **原子操作（單號 / 庫存 FIFO）→ Dapper + `UPDLOCK/HOLDLOCK`**：需要明確鎖定語意

```csharp
// DI 宣告（DependencyInjection.cs）
services.AddDbContext<TfoodiesContext>(o =>
    o.UseSqlServer(cs, sql => sql.EnableRetryOnFailure(3, TimeSpan.FromSeconds(5), null)));
services.AddScoped<IUnitOfWork, EfUnitOfWork>();
services.AddSingleton<IDbConnectionFactory>(_ => new SqlConnectionFactory(cs));
```

---

## 10. 本機開發設定

```jsonc
// src/TFoodies.Api.Functions/local.settings.json（不 commit）
{
    "IsEncrypted": false,
    "Values": {
        "AzureWebJobsStorage": "UseDevelopmentStorage=true",
        "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated"
    },
    "ConnectionStrings": {
        "Tfoodies": "Server=localhost;Database=tfoodies;User Id=sa;Password=...;TrustServerCertificate=True"
    }
}
```

啟動指令：
```bash
azurite --silent &
cd src/TFoodies.Api.Functions && func start
curl localhost:7071/api/health   # → {"status":"ok"}
```

---

## 11. 與報價系統差異對照

| 面向 | 報價系統 | TFoodies |
|---|---|---|
| 路由機制 | 自訂 RouteTable + RouteHandler + Regex | **相同** |
| 請求上下文 | 自訂 RouteContext | **相同** |
| Middleware | 自訂 IMiddleware + MiddlewarePipeline | **相同** |
| Controller | POCO（無 MVC 屬性） | **相同** |
| Captive Dependency 防護 | HandlerFactory 延遲解析 | **相同** |
| DI 生命週期原則 | Scoped Service，Singleton Factory | **相同** |
| DB 存取混用 | EF Core + Dapper | **相同** |
| Response 格式 | `ApiResponse<T>` / `ApiErrorResponse` | **相同** |
| 公開路由定義 | `PublicRoutes`（HashSet）| 前綴比對（store, auth, health）|
| CORS 允許 origin | localhost:4200（Angular） | localhost:3000（Nuxt）+ :5173（Vite）|
