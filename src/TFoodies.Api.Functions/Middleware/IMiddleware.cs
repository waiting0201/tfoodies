using TFoodies.Api.Functions.Router;

namespace TFoodies.Api.Functions.Middleware;

/// <summary>
/// HTTP 請求中介層介面。
/// 實作類別必須是 Singleton：它們不能持有任何 Scoped 依賴，
/// 且 next() delegate 的呼叫使每個請求都能向下傳遞。
/// </summary>
public interface IMiddleware
{
    Task InvokeAsync(RouteContext context, Func<Task> next);
}
