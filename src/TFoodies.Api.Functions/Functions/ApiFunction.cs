using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using TFoodies.Api.Functions.Middleware;
using TFoodies.Api.Functions.Router;

namespace TFoodies.Api.Functions.Functions;

/// <summary>
/// 唯一的 HTTP 入口點（catch-all trigger）。
///
/// 所有請求流程：
///   HttpTrigger → RouteContext → MiddlewarePipeline（Cors→Correlation→Exception→Jwt）
///                                              → RouteHandler（Regex dispatch → Controller）
///
/// ApiFunction 本身是 Scoped（Azure Functions 每次呼叫注入一次），
/// 而 MiddlewarePipeline / RouteHandler / RouteTable 都是 Singleton，
/// 在 DI 中以建構子注入，不持有任何 Scoped 依賴。
/// </summary>
public sealed class ApiFunction
{
    private readonly MiddlewarePipeline _pipeline;
    private readonly RouteHandler _routeHandler;

    public ApiFunction(MiddlewarePipeline pipeline, RouteHandler routeHandler)
    {
        _pipeline = pipeline;
        _routeHandler = routeHandler;
    }

    [Function("Api")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous,
            "get", "post", "put", "delete", "patch", "options",
            Route = "{*route}")]
        HttpRequest req,
        string? route)
    {
        route = (route ?? string.Empty).Trim('/');

        // health check：不經過 middleware pipeline，直接回傳
        if (route is "health" or "")
            return new OkObjectResult(new { status = "ok", service = "TFoodies.Api.Functions" });

        var context = new RouteContext(req, route);
        var scopedProvider = req.HttpContext.RequestServices;

        await _pipeline.ExecuteAsync(context, async () =>
        {
            context.Result = await _routeHandler.HandleAsync(context, scopedProvider);
        });

        return context.Result
            ?? new ObjectResult(null) { StatusCode = StatusCodes.Status204NoContent };
    }
}
