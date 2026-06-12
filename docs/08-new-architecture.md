# 08 · 新系統架構（docs/08-new-architecture.md）

> 本文描述**新系統**（.NET 9 重構）的多專案分層設計。
> 舊系統架構見 [docs/01-architecture.md](01-architecture.md)。
> 上次更新：2026-06-10（SettingMs 購物說明移轉：新增 ShoppingGuideAdminController + 前端 shopping-guide/ 四個 view，
> 對應 DB Lims 既有 Questiontypes/Questions 兩選單；分類+明細兩層 CRUD，皆硬刪（FK CASCADE），answer 富文本由 HtmlEditor 處理。
> 先前：InventoryMs 完整移轉：拆三獨立選單頁對齊 DB Lims——倉儲維護/入庫維護/移庫維護；
> 入庫維護全套（採購連動、需申報/不需申報、通知號查重、CheckPurchaseStatus 推進採購狀態），
> 移庫維護批次 FIFO 調撥＋在庫帳編輯；倉別標籤修正為線上/線下/瑕疵品倉；
> 並修復舊有 AddStock/TransferStock 寫入錯欄位的缺陷。UI 全面套用 docs/10 規範）

---

## 1. 為何是多專案，而非單一專案

參考專案 `quotation.weypro.com/Api` 是**單一 csproj**，所有程式碼（Models、Services、Controllers、DTOs、Middleware、Router）都在同一個專案裡。

TFoodies 採**多專案分層**，原因：

| 考量 | 說明 |
|---|---|
| 規模 | 72 張資料表（vs 報價 ~20 張）、6 個整合（金流/發票/Blob/SMS/Email/MongoDB）、複雜業務規則（FIFO 庫存、原子單號、VAT） |
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
- `IPaymentCompletionService` / `IInvoiceService` — 付款完成處理（標記已付款+Income+寄信+發票）/電子發票 port
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
  JwtTokenService.cs           ← IJwtTokenService（Singleton，無狀態 JWT refresh token，重啟/多副本可驗證）
  AuthService.cs               ← IAuthService（PBKDF2 hash-on-login，自動升級明文）
Payments/
  PaymentCompletionService.cs  ← IPaymentCompletionService（標記已付款+Income+寄信+發票，WEBPOS 兩路徑共用）
  Fisc/FiscOptions.cs          ← 財金 WEBPOS 設定（ActionUrl/商店代號/AuthResUrl/StoreSuccessUrl）
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
Sms/
  MitakeSmsService.cs          ← ISmsService（三竹簡訊，具名 HttpClient）
Email/
  SmtpEmailService.cs          ← IEmailService（SMTP，Singleton；對齊舊 Libs.SendMail/Sendinblue，
                                  但失敗回 false 不無限遞迴；BCC 可設定，appsettings:Smtp）
Store/
  StoreQueryService.cs         ← IStoreQueryService（Dapper 讀；12 前台查詢）
DependencyInjection.cs         ← AddInfrastructure()，對外唯一入口
```

**命名空間衝突規則**：Infrastructure 資料夾名若與 EF Scaffolded 實體名相同，會引發 CS0118。已知衝突：
- `Stock` 實體 → Infrastructure 資料夾改用 `Inventory`（namespace `TFoodies.Infrastructure.Inventory`）
- `Admin` 實體 → Infrastructure 資料夾改用 `Permissions`（namespace `TFoodies.Infrastructure.Permissions`）

**⚠️ DB Schema 規範（資料庫結構唯讀，禁止任何 DDL 變更）**：
新系統沿用既有 `tfoodies` SQL Server，**整個資料庫結構不可異動**——禁止執行 `ALTER TABLE`、`ADD COLUMN`、`DROP COLUMN`、`CREATE TABLE`、`DROP TABLE` 等 DDL。所有功能設計必須以 **Scaffolded 實體（`src/TFoodies.Infrastructure/Persistence/Scaffolded/`）所反映的現有欄位**為準。

- 若需要某欄位卻不存在，應調整功能設計或 UI，而非修改資料庫。
- Scaffolded 實體是現有 DB 的忠實映射，實作前先確認實體欄位，不可憑假設撰寫 SQL。

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
  MemberAuthController.cs    ← 會員認證延伸（公開）：POST /auth/register（註冊）、
                                 POST /auth/forgot-password（忘記密碼，對齊舊 Ajax/PasswordSend：
                                 比對 mobile+email → 產生 6 碼亂數新密碼 → PBKDF2 雜湊存檔 →
                                 IEmailService 寄送新密碼至信箱）
  StoreOrderController.cs    ← POST /store/orders, GET /store/orders/{code}, POST /store/discount/apply
  MemberController.cs        ← GET /member/orders, /member/orders/{code}（JWT member 身份）
  MemberProfileController.cs ← 會員中心（JWT member）：GET/PATCH /member/profile（對齊舊 MemberMs/EditProfile）、
                                 POST /member/password（修改密碼，對齊舊 EditPassword：新密碼+確認相符即更新，明文存，限 20 字）、
                                 GET/POST/DELETE /member/wishlist（我的收藏，對齊舊 Mylists；Memberproducts 僅 memberid+productid 複合鍵、圖取 Products.photo）
  PaymentController.cs       ← 財金 WEBPOS 信用卡金流（公開）：POST /store/payment/create（前端 auto-submit 刷卡 form）、
                                 POST /store/payment/return（AuthResURL 導回，處理後 302 回前台 Success）、
                                 POST /store/payment/notify（主動通知補償，冪等）
  Admin/
    OrderAdminController.cs      ← /admin/orders（OrderMs RBAC）
                                   GET /admin/orders, GET /admin/orders/{code}
                                   POST /admin/orders（手動建單；未提供 zipcode 時回退會員地區）
                                   PUT /admin/orders/{code}（全欄位編輯，含 items diff）
                                   PATCH /admin/orders/{code}/pending|ship|cancel|pay
                                   ship 出貨時對「尚未配貨」明細以 IStockAllocator FIFO 扣庫存
                                   GET /admin/orders/export（category=tfoodies|shopcom Excel）
                                   GET /admin/orders/picking（揀貨單，orderIds 逗號分隔，FIFO 解析批號）
                                   GET /admin/orders/{code}/deliver（出貨單，deliver.xlsx 範本）
    LogisticAdminController.cs   ← /admin/logistics（OrderMs 物流商 CRUD，無刪除）
    OutofnoticeAdminController.cs← /admin/outofnotices（缺貨通知；標記已通知/刪除）
    DeclarationAdminController.cs← /admin/declarations(+/declarable)（報關唯讀，舊系統為 stub）
                                   ReturnAdminController 另補 POST/PUT（後台手動建立/編輯退貨單）
                                   Excel 匯出走 ClosedXML（Helpers/OrderExcelReport.cs + Templates/deliver.xlsx）
    ProductAdminController.cs    ← ProductMs 完整對等：商品（含標籤 M:N、套裝 Setproducts、
                                    代表圖、SEO、唯一性檢查、排序、Productphotos 圖庫）、
                                    品牌（全欄位含 story/people/pattern + Brandphotos 圖庫）、
                                    品類（含 memo/SEO）、標籤；圖檔走 /admin/upload + Blob 清理
    MemberAdminController.cs     ← /admin/members（MemberMs）欄位對齊舊系統（型態/縣市/性別/開通）；
                                    含新增會員、編輯（手機/型態/開通白名單，保留 isagent）、手機查重 check-mobile
    ZipcodeAdminController.cs    ← /admin/zipcodes/cities + /admin/zipcodes/areas（縣市→區域連動參照，僅需登入）
    SmsAdminController.cs        ← /admin/sms（簡訊維護，隸屬 MemberMs）：簡訊 CRUD + 收訊人(Smsdetails)管理
                                    + 開始發送（逐筆走 ISmsService 三竹閘道，issend/statuscode 回寫）
    InventoryAdminController.cs  ← InventoryMs 完整對等（對照舊系統 InventoryMsController）三子模組：
                                    倉儲維護 /admin/warehouses（CRUD，有庫存不可刪）+ /admin/inventory（在庫彙總/批次明細）；
                                    入庫維護 /admin/stocks（list 依 stocktype 分流、purchasable 採購單+明細連動下拉、
                                    check-notice 通知號查重、新增/編輯；建 Stock+Warehousestock 並 CheckPurchaseStatus 推進採購狀態）；
                                    移庫維護 /admin/warehousestocks（在庫帳 list、source 可調撥批次、
                                    transfer 批次 FIFO 調撥（UPDLOCK 遞減來源、建目的批）、編輯數量/剩餘量/備註）
    PurchaseAdminController.cs   ← PurchaseMs 完整對等（對照舊系統 PurchaseMsController）：
                                    供應商 CRUD（title/contactor/phone/address，仍有採購單則不可刪）；
                                    /admin/exchanges（幣別/匯率下拉，維護歸 AccountingMs）；
                                    採購單列表（狀態/供應商篩選、含金額合計與幣別、排序採購日期 DESC→採購編號 DESC）、明細、
                                    新增（產生 purchasecode、status=1、含 etd/deliverterm/付款條件/明細）、
                                    編輯（明細差異比對；已轉應付不可編輯）、PATCH 轉應付憑單、
                                    GET /admin/purchases/export（勾選 purchaseIds 或依篩選匯出 .xlsx，ClosedXML/PurchaseExcelReport）
    AccountingAdminController.cs ← /admin/expenditures + /admin/outcomes + /admin/ar-invoices
                                    + /admin/incomes + /admin/refounds（AccountingMs）
    ReturnAdminController.cs     ← /admin/returns（ReturnMs）
    CmsAdminController.cs        ← /admin/cms/*（HomeMs：橫幅/新聞/食譜/FAQ）
    InvoiceAdminController.cs    ← /admin/invoices（invoiceStatus 篩選、void、allowance）
    AdminAccountController.cs    ← /admin/admin-accounts（管理員帳號 CRUD + 模組權限設定）
    DiscountAdminController.cs   ← /admin/discounts（DiscountMs；列表/新增/明細/更新/軟刪除）
    ReportAdminController.cs     ← /admin/reports/sales + /admin/reports/amounts（ReportMs）
    DashboardAdminController.cs  ← /admin/dashboard/stats（儀表板統計；RequireAdmin 僅驗 JWT、不綁模組；
                                    今日訂單/待出貨/未付款/本月訂單/本月營收/上架商品/活躍會員/低庫存，單一查詢 8 子查詢，TW 時區）
    ShoppingGuideAdminController.cs ← /admin/questiontypes + /admin/questions（SettingMs；購物說明分類/購物說明；
                                       無啟用停用欄位→硬刪，FK CASCADE 連帶刪明細/媒體；answer ntext 需 CAST）
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
│    DashboardView.vue                ← 儀表板（串 /admin/dashboard/stats：今日待辦＋本月概覽兩區 stat cards、卡片可點進對應模組；＋模組快速導覽）
│    orders/
│      OrdersView.vue                 ← 訂單列表（分頁、狀態篩選）
│      OrderCreateView.vue            ← 新增訂單（兩欄表單，商品搜尋含縮圖+編號）
│      OrderDetailView.vue            ← 訂單明細（含出貨/取消操作、「出貨單下載」、「編輯訂單」按鈕）
│      OrderEditView.vue              ← 訂單編輯（兩欄表單，預填所有欄位含收件/付款/出貨/發票/items）
│      LogisticsView.vue              ← 物流商管理（清單 + 新增/編輯，無刪除）
│      OutofnoticesView.vue           ← 缺貨通知（分頁，標記已通知/刪除）
│      DeclarationsView.vue           ← 報關（唯讀：報關單 + 待報關訂單）
│      （OrdersView 另含 Excel 匯出 / 美安報表 / 待出貨揀貨單按鈕；下載走 apiClient.apiDownload）
│    products/
│      ProductsView.vue               ← 商品列表（品牌/分類/狀態篩選、圖庫/編輯/停用）
│      ProductFormView.vue            ← 新增/編輯商品（全欄位 + 標籤 + 套裝 + 代表圖 + HtmlEditor + 庫存檢視）
│      ProductPhotosView.vue          ← 商品圖庫管理（上傳/排序/刪除）
│      BrandsView.vue                 ← 品牌列表（導向表單頁 + 圖庫）
│      BrandFormView.vue              ← 新增/編輯品牌（基本/圖片/Pattern/Story/People/SEO）
│      BrandPhotosView.vue            ← 品牌圖庫管理（上傳/排序/刪除）
│      ProductTypesView.vue           ← 商品分類（側滑面板，含 memo/SEO）
│      TagsView.vue                   ← 標籤管理
│    members/
│      MembersView.vue                ← 會員列表
│    inventory/  ← 庫存管理（InventoryMs 下三獨立選單項，對齊 DB Lims：倉儲維護/入庫維護/移庫維護）
│      WarehousesView.vue             ← 倉儲維護（清單 + 右側面板 CRUD + 刪除 Modal；倉別=線上/線下/瑕疵品倉）
│      StocksView.vue                 ← 入庫維護（單一清單，需申報/不需申報以「分類」欄區分；關鍵字篩選 + 卡片表格 + 分頁，比照舊系統無 Tab）
│      StockFormView.vue              ← 新增/編輯入庫（採購單→明細連動、入庫倉/狀態、申報欄位、通知號查重；兩種類型）
│      WarehousestocksView.vue        ← 移庫維護（在庫/移倉帳清單 + 倉庫/關鍵字篩選 + 移倉調撥面板（三層連動）+ 編輯面板 + 逐列列印移倉單 window.print）
│    purchases/  ← 採購管理（PurchaseMs 下兩獨立選單項：供應商維護 Suppliers / 採購單維護 Purchases，非頁籤）
│      SuppliersView.vue              ← 供應商維護（卡片表格 + 右側滑出面板 CRUD + 刪除 Modal）
│      PurchasesView.vue              ← 採購單維護（狀態/供應商篩選 + 勾選列 + 卡片表格（金額/狀態 Badge）+ 分頁 + 編輯/轉應付 + 匯出 Excel）
│      PurchaseFormView.vue           ← 新增/編輯採購單（兩欄表單：供應商/幣別/採購日期/ETD/付款條件/交貨期限/備註
│                                        + 產品搜尋挑選器 + 明細表 + 合計）
│    accounting/
│      AccountingView.vue             ← 財務管理（支出/收入/AR）
│    returns/
│      ReturnsView.vue                ← 退貨管理（清單 + 收貨/退款 + 新增/編輯連結）
│      ReturnFormView.vue             ← 後台手動建立/編輯退貨單（選訂單→載明細→填退貨數量）
│    cms/
│      CmsView.vue                    ← 內容管理（橫幅/新聞/食譜）
│    invoices/
│      InvoicesView.vue               ← 發票管理（狀態篩選、作廢、折讓）
│    accounts/
│      AdminAccountsView.vue          ← 管理員帳號與模組權限設定
│    discounts/
│      DiscountsView.vue              ← 折扣碼 CRUD（slide-in panel 編輯）
│    reports/
│      SalesQtyReportView.vue         ← 銷售量報表（Salereports，/admin/reports）
│      SalesAmountReportView.vue      ← 銷售額報表（Amountreports，/admin/reports/amounts）
│    shopping-guide/                  ← 購物說明（SettingMs；前台呈現為「購物說明 / 會員常見問題」）
│      QuestiontypesView.vue          ← 購物說明分類清單＋右側 slide-in panel 內嵌新增/編輯（同 Discounts）；含問題數、刪除前提示連帶刪除
│      QuestionsView.vue              ← 購物說明清單（Questions；分頁＋分類篩選）
│      QuestionFormView.vue           ← 購物說明新增/編輯獨立頁（分類下拉＋title＋HtmlEditor 富文本 answer）
├─ stores/
│    auth.ts              ← Pinia：accessToken(記憶體)、login/logout、can(module)
├─ lib/
│    apiClient.ts         ← apiFetch<T>()：注入 Bearer token，統一錯誤處理
├─ router/index.ts        ← Vue Router；beforeEach 未登入導 /login
└─ style.css              ← @import "tailwindcss"; IBM Plex Sans/Mono 字型
```

**Auth flow**：`POST /auth/admin/login` → `{ accessToken, refreshToken, username, permissions[] }` → Pinia 存 token（記憶體，XSS 安全）。`permissions[]` 為頂層模組 key（供 action 層級檢查）。

**左側選單（server 驅動）**：`AdminLayout.vue` 於 mount 時呼叫 `GET /admin/menu`，後端 `AdminMenuController` 直接由 `Lims`/`AdminLims` 樹依權限過濾產生（比照舊系統 `buildMenuItems`：itadmin=888 全顯示；一般管理員的子項需在 `AdminLims` 有授予，頂層只要有任一可見子項即顯示）。前端僅維護 `Lim.Key → SPA 路由` 對照表，未對應路由者顯示為「開發中」停用項。選單順序/名稱/層級皆以 DB `Lims` 為準，改 DB 即反映，無需改前端。

---

### 前台顧客商城（web/store）

技術棧：**Nuxt 4 + Vue 3 + TypeScript + Pinia**，**完整 SSR**（`ssr: true`，Nitro `node-server` preset）。URL 結構與舊站 `RouteConfig.cs` 1:1，沿用舊 `main.css` + jQuery 視覺外掛（verbatim 服務於 `/public`，body-close 載入、hydration 後才執行）。

**為何全 SSR（SEO）**：舊站 `www.tfoodies.com` 已被 Google 索引；社群爬蟲（FB / LINE / Twitter）不執行 JS，SPA 空殼會讓分享預覽全壞。改 SSR 後伺服器即時輸出含完整 meta 的真 HTML。

```
web/store/app/
├─ pages/                  ← 檔案式路由（/Product/{slug}、/NewsDetail/{id}/{p?} …，對齊舊 URL）
├─ composables/
│    useSeo.ts             ← useSeo()：包 useSeoMeta，補齊 description/og:*/twitter/canonical（無圖 fallback favicon）
│                             + useJsonLd()：注入 application/ld+json
│    useXxxData.ts         ← useFetch 包裝，打 Store API（/store/...）
├─ utils/
│    seo.ts               ← stripHtml/truncate/metaDescription/absoluteUrl
│    jsonLd.ts            ← productJsonLd / articleJsonLd / breadcrumbJsonLd（schema.org）
│    slug.ts             ← title(斜線) ↔ URL(連字號) 轉換
└─ server/api/
     __sitemap-urls.ts    ← @nuxtjs/sitemap 動態來源：runtime 查 Store list API 彙整內容 URL
```

**全站導覽（品牌系列）**：`layouts/default.vue` 於 SSR 階段 `await useBrandsMenu()`（打 `GET /store/brands`，回 isdisplay=1 依 sort 的品牌），把清單同時餵給 `SiteHeader`（桌面 mega-nav `.navContent`）與 `MobileMenu`（行動側欄 `.slide-brand-series`）。品牌 `<li>` 因此於首屏 HTML 內，hydration 後載入的 legacy `main.js` 才綁得到展開/hover 行為（對齊舊 `BaseController.OnActionExecuted` 填 `ViewBag.Brands`）。品牌頁系列商品「More」改以 Vue 處理：`Brand/[brandtitle].vue` 初始 4 筆，點 More 以 `$fetch('/store/brands/products?skip=&take=4')` append 並更新 `hasMore`（取代舊 `Ajax/GetBrandMoreProducts` 回傳 partial 字串）。

**靜態頁**：`Reports.vue`（檢驗報告，路由 `/Reports`）為純靜態圖牆（舊 `MainMs/Reports`，無 model），1:1 移植 markup + `useSeo()`，圖檔在 `public/content/images/section/`。

**捲動進場（`.page` 區塊）**：`.page` 預設 `opacity:0`（main.css），靠 legacy onScreen 外掛捲到視窗內加 `.onScreen` 淡入。`main.js` 只在首次載入綁一次，後續經由 SPA 換頁渲染的 `.page` 不會被綁、永久隱形。`plugins/legacy-effects.client.ts` 的 `reinitSliders()` 於每次 `page:finish` 重綁（先 `onScreen('remove')` 卸載具名 `scroll.onScreen` handler 避免累積），並 `trigger('scroll')` 讓首屏區塊立即顯示。影響所有用 `.page` 的頁（Reports、News/Recipes/Issues/Events 列表、活動詳情）。

**SEO 機制**：
- **meta**：每頁呼叫 `useSeo()`（詳情頁帶 og:image=blob 圖、canonical=`shortener` 或站台 URL）。
- **JSON-LD**：商品=`Product`(offers/price/TWD/availability)、News/Recipe/Issue/Event=`Article`、詳情頁附 `BreadcrumbList`。
- **sitemap.xml**：`@nuxtjs/sitemap`，靜態路由自動收錄，動態內容由 `server/api/__sitemap-urls.ts` 於 runtime 查 API（含商品/型錄/消息/料理/綠誌/活動，以及 `GET /store/brands` 列出的上線品牌頁）。
- **robots.txt**：`@nuxtjs/robots` 產生（不再有靜態檔），`Disallow: /Member/ /Cart /Checkout /Order/` 並自動附 `Sitemap:`；被 disallow 的頁面自動補 `noindex` meta。

**部署**：Docker 化（`web/store/Dockerfile`，多階段 → `node .output/server/index.mjs`，port 3000）→ **Azure Container Apps**（scale-to-zero 省成本，冷啟動如影響 SEO 改 minReplicas=1）。CI `.github/workflows/store.yml`：`az acr build` → `az containerapp update`。API base 由 Container App env `NUXT_PUBLIC_API_BASE` 注入。

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
| Admin 前端（`.env` / `.env.production`） | `VITE_BLOB_URL` | 同後端 `AzureBlob__BaseUrl` | `http://127.0.0.1:10000/devstoreaccount1` |
| Admin 前端 | `VITE_BLOB_CONTAINER` | 同後端 `AzureBlob__ContainerName` | `tfoodies` |
| Store 前端（Container App env / `nuxt.config`） | `NUXT_PUBLIC_BLOB_URL` | **合併且結尾帶 `/`**：`BaseUrl/Container/`（Nuxt 直接 blobUrl+檔名） | `http://127.0.0.1:10000/devstoreaccount1/tfoodies/` |

完整圖片 URL = `BaseUrl` + `/` + `ContainerName` + `/` + `fileName`  
等同舊系統：`azure.blob.url` + `"/"` + `azure.blob.container` + `"/"` + `entity.photo`

#### 部署來源（共通 GitHub 變數，單一真實來源）

production 三邊全部由**同兩個 GitHub Actions 變數**驅動，避免各自寫死漂移：

| GitHub 變數 | 流向後端 | 流向 Admin | 流向 Store |
|---|---|---|---|
| `BLOB_BASE_URL` | `infra.yml` → bicep `blobBaseUrl` → `AzureBlob__BaseUrl` | `admin.yml` build env → `VITE_BLOB_URL` | bicep 組 `NUXT_PUBLIC_BLOB_URL = base/container/` |
| `BLOB_CONTAINER` | `infra.yml` → bicep `blobContainerName` → `AzureBlob__ContainerName`（未設則 `tfoodies`） | `admin.yml` → `VITE_BLOB_CONTAINER`（未設則 `tfoodies`） | 同上組合 |

> framework 層的 env 名（`AzureBlob__*` / `VITE_*` / `NUXT_PUBLIC_*`）因各框架前綴要求無法統一，但共通的「真實來源」是 GitHub 變數層的 `BLOB_BASE_URL` / `BLOB_CONTAINER`。本地開發各自走 `local.settings.json` / `.env` 預設值。

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
