using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TFoodies.Application.Abstractions;
using TFoodies.Infrastructure.Auth;
using TFoodies.Infrastructure.CodeNumbers;
using TFoodies.Infrastructure.Invoicing.EzPay;
using TFoodies.Infrastructure.Payments;
using TFoodies.Infrastructure.Payments.Fisc;
using TFoodies.Infrastructure.Persistence;
using TFoodies.Infrastructure.Permissions;
using TFoodies.Infrastructure.Inventory;
using TFoodies.Infrastructure.Orders;
using TFoodies.Infrastructure.Sms;
using TFoodies.Infrastructure.Email;
using TFoodies.Infrastructure.Blob;
using TFoodies.Infrastructure.Store;

namespace TFoodies.Infrastructure;

/// <summary>
/// Composition root for the Infrastructure layer.
/// Secrets come from Key Vault / App Configuration via <paramref name="configuration"/>.
/// </summary>
public static class DependencyInjection
{
    public const string ConnectionStringName = "Tfoodies";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Dapper 全域：讓 DateOnly / DateOnly? 可作為 SQL 參數（此 runtime 無內建支援）。
        // 影響所有 Dapper 寫入（採購單、訂單、會計帳等的 date 欄位）。
        Dapper.SqlMapper.AddTypeHandler(new Persistence.DateOnlyTypeHandler());

        var connectionString = configuration.GetConnectionString(ConnectionStringName);

        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            // EF Core：Scoped（一次 Function 呼叫一個 DbContext），啟用重試
            services.AddDbContext<TfoodiesContext>(o =>
                o.UseSqlServer(connectionString, sql =>
                    sql.EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(5), errorNumbersToAdd: null)));

            // UoW 包裝 DbContext（Scoped）
            services.AddScoped<IUnitOfWork, EfUnitOfWork>();

            // Dapper：hot-read / 原子單號 SQL，工廠為 Singleton（連線池）
            services.AddSingleton<IDbConnectionFactory>(_ => new SqlConnectionFactory(connectionString));
        }

        // ── Store query service（Dapper，Scoped） ─────────────────────────────────
        services.AddScoped<IStoreQueryService, StoreQueryService>();

        // ── Auth ──────────────────────────────────────────────────────────────────
        services.Configure<JwtSettings>(configuration.GetSection(JwtSettings.SectionName));
        services.AddSingleton<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IAuthService, AuthService>();

        // ── Code numbers（Singleton — stateless，每次 tx 傳入） ───────────────────
        services.AddSingleton<ICodeNumberService, SqlCodeNumberService>();

        // ── Stock allocator（Singleton — stateless） ──────────────────────────────
        services.AddSingleton<IStockAllocator, SqlStockAllocator>();

        // ── Admin RBAC（Scoped） ──────────────────────────────────────────────────
        services.AddScoped<IAdminPermissionService, SqlAdminPermissionService>();

        // ── Order + Discount services（Scoped） ───────────────────────────────────
        services.Configure<OrderSettings>(configuration.GetSection(OrderSettings.SectionName));
        services.AddScoped<IDiscountService, DiscountService>();
        services.AddScoped<IOrderService, OrderService>();

        // ── Payment：財金 WEBPOS 設定 + 付款完成共用服務（Scoped）───────────────────
        // WEBPOS 為前端 form POST 導向刷卡，後端不需 HttpClient/加密；付款成功處理
        // （標記已付款/建 Income/寄信/開發票）由 /return 與 /notify 共用此服務。
        services.Configure<FiscOptions>(configuration.GetSection(FiscOptions.SectionName));
        services.AddScoped<IPaymentCompletionService, PaymentCompletionService>();

        // ── Invoice service（Singleton — HttpClient pool） ────────────────────────
        services.Configure<EzPayOptions>(configuration.GetSection(EzPayOptions.SectionName));
        services.AddSingleton<EzPayCodec>(sp =>
        {
            var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<EzPayOptions>>().Value;
            return new EzPayCodec(opts.HashKey, opts.HashIV);
        });
        services.AddHttpClient<IInvoiceService, EzPayInvoiceService>();

        // ── SMS service（HttpClient pool） ────────────────────────────────────────
        // MitakeSmsService 以 IHttpClientFactory.CreateClient(nameof(MitakeSmsService)) 取得 client，
        // 不是 typed client（建構子無 HttpClient 參數），故註冊「具名 client + 一般服務」而非 AddHttpClient<I,T>。
        services.Configure<MitakeSmsOptions>(configuration.GetSection(MitakeSmsOptions.SectionName));
        services.AddHttpClient(nameof(MitakeSmsService));
        services.AddSingleton<ISmsService, MitakeSmsService>();

        // ── Email service（SMTP，Singleton — 無狀態） ─────────────────────────────
        services.Configure<SmtpOptions>(configuration.GetSection(SmtpOptions.SectionName));
        services.AddSingleton<IEmailService, SmtpEmailService>();

        // ── Blob Storage（Singleton — BlobContainerClient 是 thread-safe）────────
        services.Configure<AzureBlobOptions>(configuration.GetSection(AzureBlobOptions.SectionName));
        services.AddSingleton<IBlobService, AzureBlobService>();

        return services;
    }
}
