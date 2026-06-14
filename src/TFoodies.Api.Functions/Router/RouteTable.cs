using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using TFoodies.Api.Functions.Controllers;
using TFoodies.Api.Functions.Controllers.Admin;

namespace TFoodies.Api.Functions.Router;

/// <summary>
/// 一筆路由定義。HandlerFactory 接收 IServiceProvider 以延遲解析 Controller，
/// 確保 RouteTable（Singleton）不捕捉任何 Scoped 實例。
/// </summary>
public record RouteDefinition(
    string Method,
    string Pattern,
    Func<IServiceProvider, RouteContext, Task<IActionResult>> HandlerFactory);

/// <summary>
/// 集中管理所有路由規則（Singleton）。
///
/// ╔══════════════════════════════════════════════════════════════════════╗
/// ║  ⚠ 設計決策：此類別必須註冊為 Singleton                              ║
/// ║                                                                    ║
/// ║  路由定義使用 HandlerFactory（Func&lt;IServiceProvider, ...&gt;），          ║
/// ║  在請求時才從 DI 容器取出需要的 Controller，避免 Singleton 持有         ║
/// ║  Scoped 實例（Captive Dependency 反模式）。                           ║
/// ║                                                                    ║
/// ║  ❌ 錯誤：AddScoped&lt;RouteTable&gt;() — 每次請求解析全部 Controller      ║
/// ║  ✅ 正確：AddSingleton&lt;RouteTable&gt;() — 路由表只建一次               ║
/// ╚══════════════════════════════════════════════════════════════════════╝
/// </summary>
public class RouteTable
{
    private readonly List<RouteDefinition> _routes = new();

    public IReadOnlyList<RouteDefinition> Routes => _routes;

    public RouteTable()
    {
        RegisterRoutes();
    }

    private void RegisterRoutes()
    {
        // ── Store（前台，無需登入）——具體路由優先於同前綴的通用路由 ───────────────
        Register<StoreController>("GET", "store/home",             (c, ctx) => c.GetHome(ctx));
        Register<StoreController>("GET", "store/products/detail",  (c, ctx) => c.GetProductDetail(ctx));
        Register<StoreController>("GET", "store/products",         (c, ctx) => c.GetProducts(ctx));
        Register<StoreController>("GET", "store/brands/detail",    (c, ctx) => c.GetBrandDetail(ctx));
        Register<StoreController>("GET", "store/brands/products",  (c, ctx) => c.GetBrandProducts(ctx));
        Register<StoreController>("GET", "store/brands",           (c, ctx) => c.GetBrands(ctx));
        Register<StoreController>("GET", "store/news/detail",      (c, ctx) => c.GetNewsDetail(ctx));
        Register<StoreController>("GET", "store/news",             (c, ctx) => c.GetNews(ctx));
        Register<StoreController>("GET", "store/recipes/detail",   (c, ctx) => c.GetRecipeDetail(ctx));
        Register<StoreController>("GET", "store/recipes",          (c, ctx) => c.GetRecipes(ctx));
        Register<StoreController>("GET", "store/issues/detail",    (c, ctx) => c.GetIssueDetail(ctx));
        Register<StoreController>("GET", "store/issues",           (c, ctx) => c.GetIssues(ctx));
        Register<StoreController>("GET", "store/knowledges/detail", (c, ctx) => c.GetKnowledgeDetail(ctx));
        Register<StoreController>("GET", "store/knowledges",       (c, ctx) => c.GetKnowledges(ctx));
        Register<StoreController>("GET", "store/blogs",            (c, ctx) => c.GetBlogs(ctx));
        Register<StoreController>("GET", "store/events/detail",    (c, ctx) => c.GetEventDetail(ctx));
        Register<StoreController>("GET", "store/events",           (c, ctx) => c.GetEvents(ctx));
        Register<StoreController>("GET", "store/shopping-guide",   (c, ctx) => c.GetShoppingGuide(ctx));

        // ── Store 下單 ──────────────────────────────────────────────────────
        Register<StoreOrderController>("POST", "store/discount/apply", (c, ctx) => c.ApplyDiscount(ctx));
        Register<StoreOrderController>("POST", "store/orders",         (c, ctx) => c.PlaceOrder(ctx));
        Register<StoreOrderController>("GET",  @"store/orders/(?<code>[^/]+)", (c, ctx) => c.GetOrder(ctx));
        Register<StoreZipcodeController>("GET", "store/zipcodes/cities", (c, ctx) => c.Cities(ctx));
        Register<StoreZipcodeController>("GET", "store/zipcodes/areas",  (c, ctx) => c.Areas(ctx));

        // ── Auth ────────────────────────────────────────────────────────────
        Register<AuthController>("POST", "auth/login",   (c, ctx) => c.Login(ctx));
        Register<AuthController>("POST", "auth/refresh", (c, ctx) => c.Refresh(ctx));

        // ── Member（前台，需 JWT member 身份）──────────────────────────────
        Register<MemberController>("GET", @"member/orders/(?<code>[^/]+)", (c, ctx) => c.GetOrder(ctx));
        Register<MemberController>("GET", "member/orders",                 (c, ctx) => c.GetOrders(ctx));

        // ── Admin Orders（OrderMs） ─────────────────────────────────────────
        // ⚠ 字面路由（export、picking）必須排在 {code} 萬用路由之前。
        Register<OrderAdminController>("GET",   "admin/orders",                                           (c, ctx) => c.List(ctx));
        Register<OrderAdminController>("POST",  "admin/orders",                                           (c, ctx) => c.Create(ctx));
        Register<OrderAdminController>("GET",   "admin/orders/export",                                    (c, ctx) => c.ExportOrders(ctx));
        Register<OrderAdminController>("GET",   "admin/orders/picking",                                   (c, ctx) => c.ExportPicking(ctx));
        Register<OrderAdminController>("GET",   @"admin/orders/(?<code>[^/]+)/deliver$",                  (c, ctx) => c.ExportDeliver(ctx));
        Register<OrderAdminController>("PATCH", @"admin/orders/(?<code>[^/]+)/pending",                   (c, ctx) => c.ToPending(ctx));
        Register<OrderAdminController>("PATCH", @"admin/orders/(?<code>[^/]+)/ship",                      (c, ctx) => c.Ship(ctx));
        Register<OrderAdminController>("PATCH", @"admin/orders/(?<code>[^/]+)/cancel",                    (c, ctx) => c.Cancel(ctx));
        Register<OrderAdminController>("PATCH", @"admin/orders/(?<code>[^/]+)/pay",                       (c, ctx) => c.MarkPaid(ctx));
        Register<OrderAdminController>("POST",  @"admin/orders/(?<code>[^/]+)/charge",                    (c, ctx) => c.Charge(ctx));
        Register<OrderAdminController>("POST",  @"admin/orders/(?<code>[^/]+)/invoice",                   (c, ctx) => c.IssueInvoice(ctx));
        Register<OrderAdminController>("GET",   @"admin/orders/(?<code>[^/]+)$",                          (c, ctx) => c.Detail(ctx));
        Register<OrderAdminController>("PUT",   @"admin/orders/(?<code>[^/]+)$",                          (c, ctx) => c.Update(ctx));

        // ── Admin Logistics（OrderMs → 物流商） ──────────────────────────────
        Register<LogisticAdminController>("GET",  "admin/logistics",                                      (c, ctx) => c.List(ctx));
        Register<LogisticAdminController>("POST", "admin/logistics",                                      (c, ctx) => c.Create(ctx));
        Register<LogisticAdminController>("GET",  @"admin/logistics/(?<id>[^/]+)$",                       (c, ctx) => c.Detail(ctx));
        Register<LogisticAdminController>("PUT",  @"admin/logistics/(?<id>[^/]+)$",                       (c, ctx) => c.Update(ctx));

        // ── Admin Outofnotices（OrderMs → 缺貨通知） ─────────────────────────
        Register<OutofnoticeAdminController>("GET",    "admin/outofnotices",                              (c, ctx) => c.List(ctx));
        Register<OutofnoticeAdminController>("PATCH",  @"admin/outofnotices/(?<id>[^/]+)/notice",         (c, ctx) => c.MarkNotified(ctx));
        Register<OutofnoticeAdminController>("DELETE", @"admin/outofnotices/(?<id>[^/]+)$",               (c, ctx) => c.Delete(ctx));

        // ── Admin Declarations（OrderMs → 報關） ─────────────────────────────
        Register<DeclarationAdminController>("GET", "admin/declarations/declarable",                      (c, ctx) => c.Declarable(ctx));
        Register<DeclarationAdminController>("GET", "admin/declarations",                                 (c, ctx) => c.List(ctx));

        // ── Admin Products（ProductMs） ─────────────────────────────────────
        // ⚠ 字面路由（check-*、sort、photos/sort）必須排在對應的 {id}/{photoId} 萬用路由之前。
        // Brands
        Register<ProductAdminController>("GET",    "admin/brands",                                        (c, ctx) => c.Brands(ctx));
        Register<ProductAdminController>("POST",   "admin/brands",                                        (c, ctx) => c.CreateBrand(ctx));
        Register<ProductAdminController>("PUT",    "admin/brands/sort",                                   (c, ctx) => c.SortBrands(ctx));
        Register<ProductAdminController>("GET",    @"admin/brands/(?<id>[^/]+)/photos$",                  (c, ctx) => c.BrandPhotos(ctx));
        Register<ProductAdminController>("POST",   @"admin/brands/(?<id>[^/]+)/photos$",                  (c, ctx) => c.CreateBrandPhoto(ctx));
        Register<ProductAdminController>("PUT",    @"admin/brands/(?<id>[^/]+)/photos/sort$",             (c, ctx) => c.SortBrandPhotos(ctx));
        Register<ProductAdminController>("PUT",    @"admin/brands/(?<id>[^/]+)/photos/(?<photoId>[^/]+)$",(c, ctx) => c.UpdateBrandPhoto(ctx));
        Register<ProductAdminController>("DELETE", @"admin/brands/(?<id>[^/]+)/photos/(?<photoId>[^/]+)$",(c, ctx) => c.DeleteBrandPhoto(ctx));
        Register<ProductAdminController>("GET",    @"admin/brands/(?<id>[^/]+)$",                         (c, ctx) => c.BrandDetail(ctx));
        Register<ProductAdminController>("PUT",    @"admin/brands/(?<id>[^/]+)$",                         (c, ctx) => c.UpdateBrand(ctx));
        Register<ProductAdminController>("DELETE", @"admin/brands/(?<id>[^/]+)$",                         (c, ctx) => c.DeleteBrand(ctx));
        // Producttypes
        Register<ProductAdminController>("GET",    "admin/producttypes",                                  (c, ctx) => c.ProductTypes(ctx));
        Register<ProductAdminController>("POST",   "admin/producttypes",                                  (c, ctx) => c.CreateProductType(ctx));
        Register<ProductAdminController>("GET",    "admin/producttypes/check-name",                       (c, ctx) => c.CheckProductTypeName(ctx));
        Register<ProductAdminController>("PUT",    "admin/producttypes/sort",                             (c, ctx) => c.SortProductTypes(ctx));
        Register<ProductAdminController>("PUT",    @"admin/producttypes/(?<id>[^/]+)$",                   (c, ctx) => c.UpdateProductType(ctx));
        Register<ProductAdminController>("DELETE", @"admin/producttypes/(?<id>[^/]+)$",                   (c, ctx) => c.DeleteProductType(ctx));
        // Products
        Register<ProductAdminController>("GET",    "admin/products",                                      (c, ctx) => c.List(ctx));
        Register<ProductAdminController>("POST",   "admin/products",                                      (c, ctx) => c.Create(ctx));
        Register<ProductAdminController>("GET",    "admin/products/check-num",                            (c, ctx) => c.CheckProductNum(ctx));
        Register<ProductAdminController>("GET",    "admin/products/check-name",                           (c, ctx) => c.CheckProductName(ctx));
        Register<ProductAdminController>("PUT",    "admin/products/sort",                                 (c, ctx) => c.SortProducts(ctx));
        Register<ProductAdminController>("GET",    @"admin/products/(?<id>[^/]+)/photos$",                (c, ctx) => c.ProductPhotos(ctx));
        Register<ProductAdminController>("POST",   @"admin/products/(?<id>[^/]+)/photos$",                (c, ctx) => c.CreateProductPhoto(ctx));
        Register<ProductAdminController>("PUT",    @"admin/products/(?<id>[^/]+)/photos/sort$",           (c, ctx) => c.SortProductPhotos(ctx));
        Register<ProductAdminController>("PUT",    @"admin/products/(?<id>[^/]+)/photos/(?<photoId>[^/]+)$",   (c, ctx) => c.UpdateProductPhoto(ctx));
        Register<ProductAdminController>("DELETE", @"admin/products/(?<id>[^/]+)/photos/(?<photoId>[^/]+)$",   (c, ctx) => c.DeleteProductPhoto(ctx));
        Register<ProductAdminController>("GET",    @"admin/products/(?<id>[^/]+)$",                       (c, ctx) => c.Detail(ctx));
        Register<ProductAdminController>("PUT",    @"admin/products/(?<id>[^/]+)$",                       (c, ctx) => c.Update(ctx));
        Register<ProductAdminController>("DELETE", @"admin/products/(?<id>[^/]+)$",                       (c, ctx) => c.Delete(ctx));

        // ── Admin Members（MemberMs） ───────────────────────────────────────
        Register<MemberAdminController>("GET",    "admin/members",                                        (c, ctx) => c.List(ctx));
        Register<MemberAdminController>("GET",    "admin/members/check-mobile",                           (c, ctx) => c.CheckMobile(ctx));
        Register<MemberAdminController>("POST",   "admin/members",                                        (c, ctx) => c.Create(ctx));
        Register<MemberAdminController>("GET",    @"admin/members/(?<id>[^/]+)$",                         (c, ctx) => c.Detail(ctx));
        Register<MemberAdminController>("PUT",    @"admin/members/(?<id>[^/]+)$",                         (c, ctx) => c.Update(ctx));
        Register<MemberAdminController>("DELETE", @"admin/members/(?<id>[^/]+)$",                         (c, ctx) => c.Delete(ctx));

        // ── Zipcode 參照（縣市/區域連動）───────────────────────────────────
        Register<ZipcodeAdminController>("GET",   "admin/zipcodes/cities",                                (c, ctx) => c.Cities(ctx));
        Register<ZipcodeAdminController>("GET",   "admin/zipcodes/areas",                                 (c, ctx) => c.Areas(ctx));

        // ── Admin SMS 簡訊維護（隸屬 MemberMs）──────────────────────────────
        Register<SmsAdminController>("GET",    "admin/sms",                                               (c, ctx) => c.List(ctx));
        Register<SmsAdminController>("POST",   "admin/sms",                                               (c, ctx) => c.Create(ctx));
        Register<SmsAdminController>("DELETE", @"admin/sms/recipients/(?<detailId>[^/]+)$",               (c, ctx) => c.DeleteRecipient(ctx));
        Register<SmsAdminController>("GET",    @"admin/sms/(?<id>[^/]+)/recipients$",                     (c, ctx) => c.Recipients(ctx));
        Register<SmsAdminController>("POST",   @"admin/sms/(?<id>[^/]+)/recipients$",                     (c, ctx) => c.AddRecipients(ctx));
        Register<SmsAdminController>("GET",    @"admin/sms/(?<id>[^/]+)/available-members$",              (c, ctx) => c.AvailableMembers(ctx));
        Register<SmsAdminController>("POST",   @"admin/sms/(?<id>[^/]+)/send$",                           (c, ctx) => c.Send(ctx));
        Register<SmsAdminController>("PUT",    @"admin/sms/(?<id>[^/]+)$",                                (c, ctx) => c.Update(ctx));
        Register<SmsAdminController>("DELETE", @"admin/sms/(?<id>[^/]+)$",                                (c, ctx) => c.Delete(ctx));

        // ── Admin Inventory（InventoryMs） ──────────────────────────────────
        // 倉儲維護 Warehouses
        Register<InventoryAdminController>("GET",    "admin/warehouses",                                  (c, ctx) => c.ListWarehouses(ctx));
        Register<InventoryAdminController>("POST",   "admin/warehouses",                                  (c, ctx) => c.CreateWarehouse(ctx));
        Register<InventoryAdminController>("PUT",    @"admin/warehouses/(?<id>[^/]+)$",                   (c, ctx) => c.UpdateWarehouse(ctx));
        Register<InventoryAdminController>("DELETE", @"admin/warehouses/(?<id>[^/]+)$",                   (c, ctx) => c.DeleteWarehouse(ctx));
        Register<InventoryAdminController>("GET",    "admin/inventory",                                   (c, ctx) => c.ListInventory(ctx));
        Register<InventoryAdminController>("GET",    @"admin/inventory/(?<productId>[^/]+)$",             (c, ctx) => c.ProductInventory(ctx));
        // 入庫維護 Stocks（exact 路由須先於 admin/stocks/{id}）
        Register<InventoryAdminController>("GET",    "admin/stocks",                                      (c, ctx) => c.ListStocks(ctx));
        Register<InventoryAdminController>("GET",    "admin/stocks/purchasable",                          (c, ctx) => c.PurchasableList(ctx));
        Register<InventoryAdminController>("GET",    @"admin/stocks/purchasable/(?<id>[^/]+)/details$",   (c, ctx) => c.PurchaseDetailOptions(ctx));
        Register<InventoryAdminController>("GET",    "admin/stocks/check-notice",                         (c, ctx) => c.CheckNotice(ctx));
        Register<InventoryAdminController>("GET",    @"admin/stocks/(?<id>[^/]+)$",                       (c, ctx) => c.StockDetail(ctx));
        Register<InventoryAdminController>("POST",   "admin/stocks",                                      (c, ctx) => c.AddStock(ctx));
        Register<InventoryAdminController>("PUT",    @"admin/stocks/(?<id>[^/]+)$",                       (c, ctx) => c.UpdateStock(ctx));
        // 移庫維護 Warehousestocks
        Register<InventoryAdminController>("GET",    "admin/warehousestocks",                             (c, ctx) => c.ListWarehousestocks(ctx));
        Register<InventoryAdminController>("GET",    "admin/warehousestocks/source",                      (c, ctx) => c.SourceStocks(ctx));
        Register<InventoryAdminController>("POST",   "admin/warehousestocks/transfer",                    (c, ctx) => c.TransferStock(ctx));
        Register<InventoryAdminController>("PUT",    @"admin/warehousestocks/(?<id>[^/]+)$",              (c, ctx) => c.UpdateWarehousestock(ctx));

        // ── Admin Purchases（PurchaseMs） ────────────────────────────────────
        Register<PurchaseAdminController>("GET",    "admin/suppliers",                                    (c, ctx) => c.ListSuppliers(ctx));
        Register<PurchaseAdminController>("POST",   "admin/suppliers",                                    (c, ctx) => c.CreateSupplier(ctx));
        Register<PurchaseAdminController>("PUT",    @"admin/suppliers/(?<id>[^/]+)$",                     (c, ctx) => c.UpdateSupplier(ctx));
        Register<PurchaseAdminController>("DELETE", @"admin/suppliers/(?<id>[^/]+)$",                     (c, ctx) => c.DeleteSupplier(ctx));
        Register<PurchaseAdminController>("GET",    "admin/exchanges",                                    (c, ctx) => c.ListExchanges(ctx));
        Register<PurchaseAdminController>("GET",    "admin/purchases",                                    (c, ctx) => c.ListPurchases(ctx));
        Register<PurchaseAdminController>("POST",   "admin/purchases",                                    (c, ctx) => c.CreatePurchase(ctx));
        Register<PurchaseAdminController>("GET",    "admin/purchases/export",                             (c, ctx) => c.ExportPurchases(ctx));
        Register<PurchaseAdminController>("GET",    @"admin/purchases/(?<id>[^/]+)$",                     (c, ctx) => c.DetailPurchase(ctx));
        Register<PurchaseAdminController>("PUT",    @"admin/purchases/(?<id>[^/]+)$",                     (c, ctx) => c.UpdatePurchase(ctx));
        Register<PurchaseAdminController>("PATCH",  @"admin/purchases/(?<id>[^/]+)/expenditure",          (c, ctx) => c.ToExpenditure(ctx));

        // ── Admin Accounting（會計帳管理 AccountingMs） ───────────────────────
        // 匯率 Exchanges（GET /admin/exchanges 在 PurchaseAdminController，採購表單下拉共用）
        Register<AccountingAdminController>("POST",   "admin/exchanges",                                  (c, ctx) => c.CreateExchange(ctx));
        Register<AccountingAdminController>("PUT",    @"admin/exchanges/(?<id>[^/]+)$",                   (c, ctx) => c.UpdateExchange(ctx));
        Register<AccountingAdminController>("DELETE", @"admin/exchanges/(?<id>[^/]+)$",                   (c, ctx) => c.DeleteExchange(ctx));
        // 會計科目 Accountings
        Register<AccountingAdminController>("GET",    "admin/accountings",                                (c, ctx) => c.ListAccountings(ctx));
        Register<AccountingAdminController>("POST",   "admin/accountings",                                (c, ctx) => c.CreateAccounting(ctx));
        Register<AccountingAdminController>("PUT",    @"admin/accountings/(?<id>[^/]+)$",                 (c, ctx) => c.UpdateAccounting(ctx));
        Register<AccountingAdminController>("DELETE", @"admin/accountings/(?<id>[^/]+)$",                 (c, ctx) => c.DeleteAccounting(ctx));
        // 營業支出 Expenditures（payable 須在 {id} 之前註冊）
        Register<AccountingAdminController>("GET",    "admin/expenditures",                               (c, ctx) => c.ListExpenditures(ctx));
        Register<AccountingAdminController>("GET",    "admin/expenditures/payable",                       (c, ctx) => c.ListPayableExpenditures(ctx));
        Register<AccountingAdminController>("POST",   "admin/expenditures",                               (c, ctx) => c.CreateExpenditure(ctx));
        Register<AccountingAdminController>("GET",    @"admin/expenditures/(?<id>[^/]+)$",                (c, ctx) => c.DetailExpenditure(ctx));
        Register<AccountingAdminController>("PUT",    @"admin/expenditures/(?<id>[^/]+)$",                (c, ctx) => c.UpdateExpenditure(ctx));
        Register<AccountingAdminController>("DELETE", @"admin/expenditures/(?<id>[^/]+)$",                (c, ctx) => c.DeleteExpenditure(ctx));
        // 付款 Outcomes
        Register<AccountingAdminController>("GET",    "admin/outcomes",                                   (c, ctx) => c.ListOutcomes(ctx));
        Register<AccountingAdminController>("POST",   "admin/outcomes",                                   (c, ctx) => c.CreateOutcome(ctx));
        Register<AccountingAdminController>("PUT",    @"admin/outcomes/(?<id>[^/]+)$",                    (c, ctx) => c.UpdateOutcome(ctx));
        Register<AccountingAdminController>("DELETE", @"admin/outcomes/(?<id>[^/]+)$",                    (c, ctx) => c.DeleteOutcome(ctx));
        // 退款 Refounds（refundable-* 須在任何 {id} 之前）
        Register<AccountingAdminController>("GET",    "admin/refounds",                                   (c, ctx) => c.ListRefounds(ctx));
        Register<AccountingAdminController>("GET",    "admin/refounds/refundable-members",                (c, ctx) => c.ListRefundableMembers(ctx));
        Register<AccountingAdminController>("GET",    "admin/refounds/refundable-returns",                (c, ctx) => c.ListRefundableReturns(ctx));
        Register<AccountingAdminController>("POST",   "admin/refounds",                                   (c, ctx) => c.CreateRefound(ctx));
        Register<AccountingAdminController>("PUT",    @"admin/refounds/(?<id>[^/]+)$",                    (c, ctx) => c.UpdateRefound(ctx));
        Register<AccountingAdminController>("DELETE", @"admin/refounds/(?<id>[^/]+)$",                    (c, ctx) => c.DeleteRefound(ctx));
        // 請款 AR Invoices（billable-* 須在 {id} 之前）
        Register<AccountingAdminController>("GET",    "admin/ar-invoices",                                (c, ctx) => c.ListArInvoices(ctx));
        Register<AccountingAdminController>("GET",    "admin/ar-invoices/billable-members",               (c, ctx) => c.ListBillableMembers(ctx));
        Register<AccountingAdminController>("GET",    "admin/ar-invoices/billable-orders",                (c, ctx) => c.ListBillableOrders(ctx));
        Register<AccountingAdminController>("POST",   "admin/ar-invoices",                                (c, ctx) => c.CreateArInvoice(ctx));
        Register<AccountingAdminController>("GET",    @"admin/ar-invoices/(?<id>[^/]+)$",                 (c, ctx) => c.DetailArInvoice(ctx));
        Register<AccountingAdminController>("PUT",    @"admin/ar-invoices/(?<id>[^/]+)$",                 (c, ctx) => c.UpdateArInvoice(ctx));
        Register<AccountingAdminController>("DELETE", @"admin/ar-invoices/(?<id>[^/]+)$",                 (c, ctx) => c.DeleteArInvoice(ctx));
        // 入帳 Incomes（billable-* 須在 {id} 之前）
        Register<AccountingAdminController>("GET",    "admin/incomes",                                    (c, ctx) => c.ListIncomes(ctx));
        Register<AccountingAdminController>("GET",    "admin/incomes/billable-members",                   (c, ctx) => c.ListIncomeBillableMembers(ctx));
        Register<AccountingAdminController>("GET",    "admin/incomes/billable-invoices",                  (c, ctx) => c.ListBillableInvoices(ctx));
        Register<AccountingAdminController>("POST",   "admin/incomes",                                    (c, ctx) => c.CreateIncome(ctx));
        Register<AccountingAdminController>("GET",    @"admin/incomes/(?<id>[^/]+)$",                     (c, ctx) => c.DetailIncome(ctx));
        Register<AccountingAdminController>("PUT",    @"admin/incomes/(?<id>[^/]+)$",                     (c, ctx) => c.UpdateIncome(ctx));
        Register<AccountingAdminController>("DELETE", @"admin/incomes/(?<id>[^/]+)$",                     (c, ctx) => c.DeleteIncome(ctx));

        // ── Admin Statements（會計報表管理 StatementMs） ──────────────────────
        Register<StatementAdminController>("GET",    "admin/statements/income-statement",                (c, ctx) => c.IncomeStatement(ctx));
        Register<StatementAdminController>("GET",    "admin/statements/balance-sheet",                   (c, ctx) => c.BalanceSheet(ctx));

        // ── Admin Tags ──────────────────────────────────────────────────────────
        Register<ProductAdminController>("GET",    "admin/tags",                                          (c, ctx) => c.ListTags(ctx));
        Register<ProductAdminController>("POST",   "admin/tags",                                          (c, ctx) => c.CreateTag(ctx));
        Register<ProductAdminController>("PUT",    @"admin/tags/(?<id>[^/]+)$",                           (c, ctx) => c.UpdateTag(ctx));
        Register<ProductAdminController>("DELETE", @"admin/tags/(?<id>[^/]+)$",                           (c, ctx) => c.DeleteTag(ctx));

        // ── Payment（公開，財金 WEBPOS 信用卡金流）─────────────────────────────
        Register<PaymentController>("POST", "store/payment/create",                                       (c, ctx) => c.CreatePayment(ctx));
        Register<PaymentController>("POST", "store/payment/return",                                       (c, ctx) => c.Return(ctx));
        Register<PaymentController>("POST", "store/payment/return-admin",                                 (c, ctx) => c.ReturnAdmin(ctx));
        Register<PaymentController>("POST", "store/payment/notify",                                       (c, ctx) => c.Notify(ctx));

        // ── Admin Auth ──────────────────────────────────────────────────────────
        Register<AdminAuthController>("POST", "auth/admin/login",                                         (c, ctx) => c.Login(ctx));
        Register<AdminAuthController>("POST", "auth/admin/logout",                                        (c, ctx) => c.Logout(ctx));

        // ── Member Auth（公開，無需登入）────────────────────────────────────────
        Register<MemberAuthController>("POST", "auth/register",                                           (c, ctx) => c.Register(ctx));
        Register<MemberAuthController>("POST", "auth/forgot-password",                                    (c, ctx) => c.ForgotPassword(ctx));

        // ── Member Profile & Wishlist（需 JWT member）──────────────────────────
        Register<MemberProfileController>("GET",    "member/profile",                                    (c, ctx) => c.GetProfile(ctx));
        Register<MemberProfileController>("PATCH",  "member/profile",                                    (c, ctx) => c.UpdateProfile(ctx));
        Register<MemberProfileController>("POST",   "member/password",                                   (c, ctx) => c.ChangePassword(ctx));
        Register<MemberProfileController>("GET",    "member/wishlist",                                    (c, ctx) => c.GetWishlist(ctx));
        Register<MemberProfileController>("POST",   "member/wishlist",                                    (c, ctx) => c.AddWishlist(ctx));
        Register<MemberProfileController>("DELETE", @"member/wishlist/(?<productId>[^/]+)$",              (c, ctx) => c.RemoveWishlist(ctx));

        // ── Returns（前台）──────────────────────────────────────────────────────
        Register<ReturnController>("POST", "store/returns",                                               (c, ctx) => c.CreateReturn(ctx));
        Register<ReturnController>("GET",  "member/returns",                                              (c, ctx) => c.ListMemberReturns(ctx));
        Register<ReturnController>("GET",  @"member/returns/(?<returnCode>[^/]+)$",                      (c, ctx) => c.GetMemberReturn(ctx));

        // ── Admin Returns（OrderMs） ─────────────────────────────────────────
        Register<ReturnAdminController>("GET",   "admin/returns",                                         (c, ctx) => c.List(ctx));
        Register<ReturnAdminController>("POST",  "admin/returns",                                         (c, ctx) => c.Create(ctx));
        Register<ReturnAdminController>("GET",   @"admin/returns/(?<id>[^/]+)$",                          (c, ctx) => c.Detail(ctx));
        Register<ReturnAdminController>("PUT",   @"admin/returns/(?<id>[^/]+)$",                          (c, ctx) => c.Update(ctx));
        Register<ReturnAdminController>("PATCH", @"admin/returns/(?<id>[^/]+)/receive",                   (c, ctx) => c.Receive(ctx));
        Register<ReturnAdminController>("PATCH", @"admin/returns/(?<id>[^/]+)/refund",                    (c, ctx) => c.Refund(ctx));

        // ── Admin Dashboard（儀表板統計，唯讀，任一登入管理員可讀）─────────────
        Register<DashboardAdminController>("GET",  "admin/dashboard/stats",                                 (c, ctx) => c.Stats(ctx));

        // ── Admin Accounts ───────────────────────────────────────────────────
        // 左側選單（依權限由 Lims 樹動態產生）
        Register<AdminMenuController>("GET",       "admin/menu",                                            (c, ctx) => c.Get(ctx));
        // 當前登入管理員的模組權限（供前端頁面重整後刷新側欄）
        Register<AdminAccountController>("GET",    "admin/me/permissions",                                  (c, ctx) => c.MyPermissions(ctx));
        Register<AdminAccountController>("GET",    "admin/admin-accounts",                                  (c, ctx) => c.List(ctx));
        Register<AdminAccountController>("POST",   "admin/admin-accounts",                                  (c, ctx) => c.Create(ctx));
        // ⚠️ permissions 路由必須比 /{id}$ 更早註冊，避免被通用路由攔截
        Register<AdminAccountController>("GET",    @"admin/admin-accounts/(?<id>[^/]+)/permissions",        (c, ctx) => c.GetPermissions(ctx));
        Register<AdminAccountController>("PUT",    @"admin/admin-accounts/(?<id>[^/]+)/permissions",        (c, ctx) => c.SetPermissions(ctx));
        Register<AdminAccountController>("PUT",    @"admin/admin-accounts/(?<id>[^/]+)$",                   (c, ctx) => c.Update(ctx));
        Register<AdminAccountController>("DELETE", @"admin/admin-accounts/(?<id>[^/]+)$",                   (c, ctx) => c.Disable(ctx));

        // ── Admin CMS（HomeMs）── Banners ─────────────────────────────────────
        Register<CmsAdminController>("GET",    "admin/cms/banners",                                       (c, ctx) => c.BannerList(ctx));
        Register<CmsAdminController>("GET",    @"admin/cms/banners/(?<id>[^/]+)$",                        (c, ctx) => c.BannerDetail(ctx));
        Register<CmsAdminController>("POST",   "admin/cms/banners",                                       (c, ctx) => c.BannerCreate(ctx));
        Register<CmsAdminController>("PUT",    @"admin/cms/banners/(?<id>[^/]+)$",                        (c, ctx) => c.BannerUpdate(ctx));
        Register<CmsAdminController>("DELETE", @"admin/cms/banners/(?<id>[^/]+)$",                        (c, ctx) => c.BannerDelete(ctx));

        // ── Admin CMS── News ─────────────────────────────────────────────────
        Register<CmsAdminController>("GET",    "admin/cms/news",                                          (c, ctx) => c.NewsList(ctx));
        Register<CmsAdminController>("GET",    @"admin/cms/news/(?<id>[^/]+)$",                           (c, ctx) => c.NewsDetail(ctx));
        Register<CmsAdminController>("POST",   "admin/cms/news",                                          (c, ctx) => c.NewsCreate(ctx));
        Register<CmsAdminController>("PUT",    @"admin/cms/news/(?<id>[^/]+)$",                           (c, ctx) => c.NewsUpdate(ctx));
        Register<CmsAdminController>("DELETE", @"admin/cms/news/(?<id>[^/]+)$",                           (c, ctx) => c.NewsDelete(ctx));

        // ── Admin CMS── Recipes ──────────────────────────────────────────────
        Register<CmsAdminController>("GET",    "admin/cms/products/all",                                  (c, ctx) => c.RecipeProductsAll(ctx));
        Register<CmsAdminController>("GET",    "admin/cms/recipes",                                       (c, ctx) => c.RecipeList(ctx));
        Register<CmsAdminController>("GET",    @"admin/cms/recipes/(?<id>[^/]+)$",                        (c, ctx) => c.RecipeDetail(ctx));
        Register<CmsAdminController>("POST",   "admin/cms/recipes",                                       (c, ctx) => c.RecipeCreate(ctx));
        Register<CmsAdminController>("PUT",    @"admin/cms/recipes/(?<id>[^/]+)$",                        (c, ctx) => c.RecipeUpdate(ctx));
        Register<CmsAdminController>("DELETE", @"admin/cms/recipes/(?<id>[^/]+)$",                        (c, ctx) => c.RecipeDelete(ctx));

        // ── Admin CMS── Issues ───────────────────────────────────────────────
        Register<CmsAdminController>("GET",    "admin/cms/issues",                                        (c, ctx) => c.IssueList(ctx));
        Register<CmsAdminController>("GET",    @"admin/cms/issues/(?<id>[^/]+)$",                         (c, ctx) => c.IssueDetail(ctx));
        Register<CmsAdminController>("POST",   "admin/cms/issues",                                        (c, ctx) => c.IssueCreate(ctx));
        Register<CmsAdminController>("PUT",    @"admin/cms/issues/(?<id>[^/]+)$",                         (c, ctx) => c.IssueUpdate(ctx));
        Register<CmsAdminController>("DELETE", @"admin/cms/issues/(?<id>[^/]+)$",                         (c, ctx) => c.IssueDelete(ctx));

        // ── Admin CMS── Events ───────────────────────────────────────────────
        // ⚠️ Eventphoto 的巢狀路由必須比 events/{id} 更早註冊，避免 regex 被單層路由攔截
        Register<CmsAdminController>("GET",    @"admin/cms/events/(?<eventId>[^/]+)/photos$",              (c, ctx) => c.EventphotoList(ctx));
        Register<CmsAdminController>("POST",   @"admin/cms/events/(?<eventId>[^/]+)/photos$",              (c, ctx) => c.EventphotoCreate(ctx));
        Register<CmsAdminController>("PUT",    @"admin/cms/events/(?<eventId>[^/]+)/photos/(?<id>[^/]+)$", (c, ctx) => c.EventphotoUpdate(ctx));
        Register<CmsAdminController>("DELETE", @"admin/cms/events/(?<eventId>[^/]+)/photos/(?<id>[^/]+)$", (c, ctx) => c.EventphotoDelete(ctx));
        Register<CmsAdminController>("GET",    "admin/cms/events",                                         (c, ctx) => c.EventList(ctx));
        Register<CmsAdminController>("GET",    @"admin/cms/events/(?<id>[^/]+)$",                          (c, ctx) => c.EventDetail(ctx));
        Register<CmsAdminController>("POST",   "admin/cms/events",                                         (c, ctx) => c.EventCreate(ctx));
        Register<CmsAdminController>("PUT",    @"admin/cms/events/(?<id>[^/]+)$",                          (c, ctx) => c.EventUpdate(ctx));
        Register<CmsAdminController>("DELETE", @"admin/cms/events/(?<id>[^/]+)$",                          (c, ctx) => c.EventDelete(ctx));

        // ── Admin CMS── Knowledges / FAQ ─────────────────────────────────────
        Register<CmsAdminController>("GET",    "admin/cms/knowledges",                                    (c, ctx) => c.KnowledgeList(ctx));
        Register<CmsAdminController>("GET",    @"admin/cms/knowledges/(?<id>[^/]+)$",                     (c, ctx) => c.KnowledgeDetail(ctx));
        Register<CmsAdminController>("POST",   "admin/cms/knowledges",                                    (c, ctx) => c.KnowledgeCreate(ctx));
        Register<CmsAdminController>("PUT",    @"admin/cms/knowledges/(?<id>[^/]+)$",                     (c, ctx) => c.KnowledgeUpdate(ctx));
        Register<CmsAdminController>("DELETE", @"admin/cms/knowledges/(?<id>[^/]+)$",                     (c, ctx) => c.KnowledgeDelete(ctx));

        // ── Admin CMS── Blogs ────────────────────────────────────────────────
        Register<CmsAdminController>("GET",    "admin/cms/blogs",                                         (c, ctx) => c.BlogList(ctx));
        Register<CmsAdminController>("GET",    @"admin/cms/blogs/(?<id>[^/]+)$",                          (c, ctx) => c.BlogDetail(ctx));
        Register<CmsAdminController>("POST",   "admin/cms/blogs",                                         (c, ctx) => c.BlogCreate(ctx));
        Register<CmsAdminController>("PUT",    @"admin/cms/blogs/(?<id>[^/]+)$",                          (c, ctx) => c.BlogUpdate(ctx));
        Register<CmsAdminController>("DELETE", @"admin/cms/blogs/(?<id>[^/]+)$",                          (c, ctx) => c.BlogDelete(ctx));

        // ── Admin Invoices（InvoiceMs） ──────────────────────────────────────
        Register<InvoiceAdminController>("GET",   "admin/invoices",                                       (c, ctx) => c.ListInvoices(ctx));
        Register<InvoiceAdminController>("PATCH", @"admin/invoices/(?<id>[^/]+)/void",                    (c, ctx) => c.VoidInvoice(ctx));
        Register<InvoiceAdminController>("PATCH", @"admin/invoices/(?<id>[^/]+)/allowance",               (c, ctx) => c.AllowanceInvoice(ctx));

        // ── Admin Discounts（DiscountMs） ────────────────────────────────────
        Register<DiscountAdminController>("GET",    "admin/discounts",                                    (c, ctx) => c.List(ctx));
        Register<DiscountAdminController>("POST",   "admin/discounts",                                    (c, ctx) => c.Create(ctx));
        Register<DiscountAdminController>("GET",    @"admin/discounts/(?<id>[^/]+)$",                     (c, ctx) => c.Detail(ctx));
        Register<DiscountAdminController>("PUT",    @"admin/discounts/(?<id>[^/]+)$",                     (c, ctx) => c.Update(ctx));
        Register<DiscountAdminController>("DELETE", @"admin/discounts/(?<id>[^/]+)$",                     (c, ctx) => c.Delete(ctx));

        // ── Admin Reports（ReportMs） ─────────────────────────────────────────
        Register<ReportAdminController>("GET", "admin/reports/sales",                                     (c, ctx) => c.Sales(ctx));
        Register<ReportAdminController>("GET", "admin/reports/amounts",                                   (c, ctx) => c.Amounts(ctx));

        // ── Admin 購物說明（SettingMs → 購物說明分類 Questiontypes / 購物說明 Questions）──
        Register<ShoppingGuideAdminController>("GET",    "admin/questiontypes",                           (c, ctx) => c.TypeList(ctx));
        Register<ShoppingGuideAdminController>("POST",   "admin/questiontypes",                           (c, ctx) => c.TypeCreate(ctx));
        Register<ShoppingGuideAdminController>("GET",    @"admin/questiontypes/(?<id>[^/]+)$",            (c, ctx) => c.TypeDetail(ctx));
        Register<ShoppingGuideAdminController>("PUT",    @"admin/questiontypes/(?<id>[^/]+)$",            (c, ctx) => c.TypeUpdate(ctx));
        Register<ShoppingGuideAdminController>("DELETE", @"admin/questiontypes/(?<id>[^/]+)$",            (c, ctx) => c.TypeDelete(ctx));
        Register<ShoppingGuideAdminController>("GET",    "admin/questions",                               (c, ctx) => c.List(ctx));
        Register<ShoppingGuideAdminController>("POST",   "admin/questions",                               (c, ctx) => c.Create(ctx));
        Register<ShoppingGuideAdminController>("GET",    @"admin/questions/(?<id>[^/]+)$",                (c, ctx) => c.Detail(ctx));
        Register<ShoppingGuideAdminController>("PUT",    @"admin/questions/(?<id>[^/]+)$",                (c, ctx) => c.Update(ctx));
        Register<ShoppingGuideAdminController>("DELETE", @"admin/questions/(?<id>[^/]+)$",                (c, ctx) => c.Delete(ctx));

        // ── Admin Upload（共用圖片上傳，需登入，container = tfoodies）────────────
        Register<UploadAdminController>("POST", "admin/upload",                                           (c, ctx) => c.Upload(ctx));
    }

    /// <summary>
    /// 泛型路由註冊：Controller 實例在請求時才從 IServiceProvider 延遲解析，
    /// 避免 Singleton RouteTable 持有 Scoped Controller（Captive Dependency）。
    /// </summary>
    protected void Register<TController>(
        string method,
        string pattern,
        Func<TController, RouteContext, Task<IActionResult>> handler)
        where TController : notnull
    {
        _routes.Add(new RouteDefinition(
            method.ToUpperInvariant(),
            pattern,
            (sp, ctx) =>
            {
                var controller = (TController)sp.GetRequiredService(typeof(TController));
                return handler(controller, ctx);
            }));
    }

    // ── Path param 解析輔助 ────────────────────────────────────────────────

    protected static Guid ParseGuid(RouteContext ctx, string paramName)
    {
        if (ctx.PathParams.TryGetValue(paramName, out var v) && Guid.TryParse(v, out var g)) return g;
        return Guid.Empty;
    }

    protected static int ParseInt(RouteContext ctx, string paramName)
    {
        if (ctx.PathParams.TryGetValue(paramName, out var v) && int.TryParse(v, out var i)) return i;
        return 0;
    }
}
