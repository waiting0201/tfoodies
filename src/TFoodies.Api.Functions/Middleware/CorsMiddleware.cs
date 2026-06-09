using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TFoodies.Api.Functions.Router;

namespace TFoodies.Api.Functions.Middleware;

/// <summary>
/// CORS 中介層（Singleton）：
/// - 允許前台（Nuxt, :3000）和後台（Vite, :5173）的跨域請求
/// - OPTIONS 請求短路回傳 204，不進入後續 middleware
/// </summary>
public sealed class CorsMiddleware : IMiddleware
{
    private static readonly HashSet<string> AllowedOrigins = new(StringComparer.OrdinalIgnoreCase)
    {
        "http://localhost:3000",
        "https://localhost:3000",
        "http://localhost:5173",
        "https://localhost:5173",
    };

    private const string AllowedMethods = "GET, POST, PUT, DELETE, PATCH, OPTIONS";
    private const string AllowedHeaders = "Content-Type, Authorization, X-Correlation-ID";

    public async Task InvokeAsync(RouteContext context, Func<Task> next)
    {
        var origin = context.Request.Headers.Origin.ToString();

        if (!string.IsNullOrWhiteSpace(origin) && AllowedOrigins.Contains(origin))
        {
            context.Request.HttpContext.Response.Headers["Access-Control-Allow-Origin"] = origin;
            context.Request.HttpContext.Response.Headers["Access-Control-Allow-Methods"] = AllowedMethods;
            context.Request.HttpContext.Response.Headers["Access-Control-Allow-Headers"] = AllowedHeaders;
            context.Request.HttpContext.Response.Headers["Access-Control-Allow-Credentials"] = "true";
        }

        // OPTIONS preflight 短路
        if (context.Request.Method.Equals("OPTIONS", StringComparison.OrdinalIgnoreCase))
        {
            context.Result = new StatusCodeResult(StatusCodes.Status204NoContent);
            return;
        }

        await next();
    }
}
