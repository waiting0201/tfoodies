using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TFoodies.Application.Abstractions;
using TFoodies.Infrastructure.Auth;
using TFoodies.Infrastructure.CodeNumbers;
using TFoodies.Infrastructure.Invoicing.EzPay;
using TFoodies.Infrastructure.Payments.Fisc;
using TFoodies.Infrastructure.Persistence;
using TFoodies.Infrastructure.Permissions;
using TFoodies.Infrastructure.Inventory;
using TFoodies.Infrastructure.Orders;
using TFoodies.Infrastructure.Sms;
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

        // ── Payment gateway（Singleton — HttpClient pool） ────────────────────────
        services.Configure<FiscOptions>(configuration.GetSection(FiscOptions.SectionName));
        services.AddSingleton<FiscMessageCodec>(sp =>
        {
            var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<FiscOptions>>().Value;
            return FiscMessageCodec.FromHex(opts.VerificationKeyHex, opts.FieldKeyHex);
        });
        services.AddHttpClient<IPaymentGateway, FiscPaymentGateway>();

        // ── Invoice service（Singleton — HttpClient pool） ────────────────────────
        services.Configure<EzPayOptions>(configuration.GetSection(EzPayOptions.SectionName));
        services.AddSingleton<EzPayCodec>(sp =>
        {
            var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<EzPayOptions>>().Value;
            return new EzPayCodec(opts.HashKey, opts.HashIV);
        });
        services.AddHttpClient<IInvoiceService, EzPayInvoiceService>();

        // ── SMS service（HttpClient pool） ────────────────────────────────────────
        services.Configure<MitakeSmsOptions>(configuration.GetSection(MitakeSmsOptions.SectionName));
        services.AddHttpClient<ISmsService, MitakeSmsService>();

        return services;
    }
}
