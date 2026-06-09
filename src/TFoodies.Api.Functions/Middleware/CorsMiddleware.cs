using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TFoodies.Api.Functions.Router;

namespace TFoodies.Api.Functions.Middleware;

/// <summary>
/// CORS 中介層（Singleton）：
/// - OPTIONS preflight 直接回 204 + CORS headers，不進 router
/// - 其他請求在 response 加上 CORS headers 後繼續
/// 必須是 pipeline 第一個中介層。
/// </summary>
public sealed class CorsMiddleware : IMiddleware
{
    public async Task InvokeAsync(RouteContext context, Func<Task> next)
    {
        var response = context.Request.HttpContext.Response;
        var origin   = context.Request.Headers.Origin.ToString();

        // credentials: 'include' 要求 Allow-Origin 必須是具體 origin（不能是 *）
        response.Headers["Access-Control-Allow-Origin"]      = string.IsNullOrEmpty(origin) ? "*" : origin;
        response.Headers["Access-Control-Allow-Credentials"] = "true";
        response.Headers["Access-Control-Allow-Methods"]     = "GET, POST, PUT, DELETE, PATCH, OPTIONS";
        response.Headers["Access-Control-Allow-Headers"]     = "Content-Type, Authorization, X-Correlation-Id";

        if (context.Request.Method.Equals("OPTIONS", StringComparison.OrdinalIgnoreCase))
        {
            response.Headers["Access-Control-Max-Age"] = "86400";
            context.Result = new StatusCodeResult(StatusCodes.Status204NoContent);
            return;
        }

        await next();
    }
}
