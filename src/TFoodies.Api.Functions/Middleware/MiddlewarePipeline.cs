using TFoodies.Api.Functions.Router;

namespace TFoodies.Api.Functions.Middleware;

/// <summary>
/// 責任鏈式 middleware 管線（Singleton）。
/// Use() 在應用程式啟動時按順序呼叫，執行時從後往前包裝以保持插入順序。
/// </summary>
public class MiddlewarePipeline
{
    private readonly List<IMiddleware> _middlewares = new();

    public MiddlewarePipeline Use(IMiddleware middleware)
    {
        _middlewares.Add(middleware);
        return this;
    }

    /// <summary>
    /// 依插入順序執行所有 middleware，terminal 是最終的處理動作（Router dispatch）。
    /// </summary>
    public async Task ExecuteAsync(RouteContext context, Func<Task> terminal)
    {
        Func<Task> next = terminal;

        for (int i = _middlewares.Count - 1; i >= 0; i--)
        {
            var middleware = _middlewares[i];
            var capturedNext = next;
            next = () => middleware.InvokeAsync(context, capturedNext);
        }

        await next();
    }
}
