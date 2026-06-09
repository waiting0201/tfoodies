using TFoodies.Api.Functions.Router;

namespace TFoodies.Api.Functions.Middleware;

/// <summary>
/// 關聯 ID 中介層（Singleton）：
/// 從請求 header 讀取或產生 X-Correlation-ID，並寫回 response header，
/// 讓同一請求在 App Insights 中可以端到端追蹤。
/// </summary>
public sealed class CorrelationMiddleware : IMiddleware
{
    public const string HeaderName = "X-Correlation-ID";

    public async Task InvokeAsync(RouteContext context, Func<Task> next)
    {
        var correlationId = context.Request.Headers.TryGetValue(HeaderName, out var existing)
            && !string.IsNullOrWhiteSpace(existing)
            ? existing.ToString()
            : Guid.NewGuid().ToString("N");

        context.Request.HttpContext.Items[HeaderName] = correlationId;
        context.Request.HttpContext.Response.Headers[HeaderName] = correlationId;

        await next();
    }
}
