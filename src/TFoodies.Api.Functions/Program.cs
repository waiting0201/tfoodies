using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TFoodies.Api.Functions.Controllers;
using TFoodies.Api.Functions.Controllers.Admin;
using TFoodies.Api.Functions.Helpers;
using TFoodies.Api.Functions.Middleware;
using TFoodies.Api.Functions.Router;
using TFoodies.Application;
using TFoodies.Infrastructure;

var builder = FunctionsApplication.CreateBuilder(args);

// Azure Functions Isolated Worker + ASP.NET Core 整合
// （保留 ConfigureFunctionsWebApplication 以取得 HttpContext、IActionResult 支援）
builder.ConfigureFunctionsWebApplication();

// ── Observability ───────────────────────────────────────────────────────────
builder.Services
    .AddApplicationInsightsTelemetryWorkerService()
    .ConfigureFunctionsApplicationInsights();

// ── DIY Router（全部 Singleton，不持有任何 Scoped 依賴）─────────────────────
//
//   RouteTable   → 儲存路由定義，HandlerFactory 延遲解析 Controller
//   RouteHandler → 預編譯 Regex，請求時 dispatch 到正確 handler
//   Middleware   → 全部 Singleton；運算結果寫入 per-request RouteContext
//   MiddlewarePipeline → 責任鏈，Singleton，接受 RouteContext 與 terminal lambda

builder.Services.AddSingleton<RouteTable>();
builder.Services.AddSingleton<RouteHandler>();

builder.Services.AddSingleton<JwtHelper>();
builder.Services.AddSingleton<CorsMiddleware>();
builder.Services.AddSingleton<CorrelationMiddleware>();
builder.Services.AddSingleton<ExceptionHandlingMiddleware>();
builder.Services.AddSingleton<JwtAuthMiddleware>();

// Pipeline 順序：Cors → Correlation → ExceptionHandling → JwtAuth → Router
builder.Services.AddSingleton(sp =>
{
    var pipeline = new MiddlewarePipeline();
    pipeline.Use(sp.GetRequiredService<CorsMiddleware>());
    pipeline.Use(sp.GetRequiredService<CorrelationMiddleware>());
    pipeline.Use(sp.GetRequiredService<ExceptionHandlingMiddleware>());
    pipeline.Use(sp.GetRequiredService<JwtAuthMiddleware>());
    return pipeline;
});

// OkObjectResult / JsonResult 統一使用 camelCase（前端 Vue 介面一致）
builder.Services.Configure<Microsoft.AspNetCore.Mvc.JsonOptions>(opt =>
    opt.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase);

// ── Application & Infrastructure 層 ────────────────────────────────────────
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// ── Controllers（Scoped，由 RouteTable HandlerFactory 延遲解析）──────────────
builder.Services.AddScoped<StoreController>();
builder.Services.AddScoped<AuthController>();
builder.Services.AddScoped<StoreOrderController>();
builder.Services.AddScoped<MemberController>();
builder.Services.AddScoped<OrderAdminController>();
builder.Services.AddScoped<ProductAdminController>();
builder.Services.AddScoped<MemberAdminController>();
builder.Services.AddScoped<ZipcodeAdminController>();
builder.Services.AddScoped<SmsAdminController>();
builder.Services.AddScoped<InventoryAdminController>();
builder.Services.AddScoped<PurchaseAdminController>();
builder.Services.AddScoped<AccountingAdminController>();
builder.Services.AddScoped<StatementAdminController>();
builder.Services.AddScoped<PaymentNotifyController>();
builder.Services.AddScoped<AdminAuthController>();
builder.Services.AddScoped<MemberAuthController>();
builder.Services.AddScoped<MemberProfileController>();
builder.Services.AddScoped<ReturnController>();
builder.Services.AddScoped<ReturnAdminController>();
builder.Services.AddScoped<LogisticAdminController>();
builder.Services.AddScoped<OutofnoticeAdminController>();
builder.Services.AddScoped<DeclarationAdminController>();
builder.Services.AddScoped<AdminAccountController>();
builder.Services.AddScoped<AdminMenuController>();
builder.Services.AddScoped<CmsAdminController>();
builder.Services.AddScoped<UploadAdminController>();
builder.Services.AddScoped<InvoiceAdminController>();
builder.Services.AddScoped<DiscountAdminController>();
builder.Services.AddScoped<ReportAdminController>();
builder.Services.AddScoped<ShoppingGuideAdminController>();
builder.Services.AddScoped<DashboardAdminController>();

builder.Build().Run();
