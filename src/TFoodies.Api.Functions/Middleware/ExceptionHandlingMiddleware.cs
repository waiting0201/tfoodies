using Microsoft.Extensions.Logging;
using TFoodies.Api.Functions.Router;

namespace TFoodies.Api.Functions.Middleware;

/// <summary>
/// 例外處理中介層（Singleton）：
/// 捕捉 pipeline 中所有未處理例外，依型別對應 HTTP 狀態碼，
/// 並以 ApiErrorResponse 格式回傳（不洩露 stack trace）。
///
/// 例外對應：
///   ArgumentException         → 400 BAD_REQUEST
///   KeyNotFoundException      → 404 NOT_FOUND
///   UnauthorizedAccessException → 403 FORBIDDEN
///   InvalidOperationException → 422 UNPROCESSABLE_ENTITY
///   其他                      → 500 INTERNAL_ERROR
/// </summary>
public sealed class ExceptionHandlingMiddleware : IMiddleware
{
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(ILogger<ExceptionHandlingMiddleware> logger)
        => _logger = logger;

    public async Task InvokeAsync(RouteContext context, Func<Task> next)
    {
        try
        {
            await next();
        }
        catch (Exception ex)
        {
            var correlationId = context.Request.HttpContext.Items
                .TryGetValue(CorrelationMiddleware.HeaderName, out var cid)
                ? cid?.ToString()
                : null;

            _logger.LogError(ex, "未處理例外 (correlationId={CorrelationId})", correlationId);

            context.Result = ex switch
            {
                ArgumentException e        => context.BadRequest(e.Message),
                KeyNotFoundException e     => context.NotFound(e.Message),
                UnauthorizedAccessException => context.Forbidden(),
                InvalidOperationException e => context.UnprocessableEntity(e.Message),
                _                          => context.InternalServerError(correlationId)
            };
        }
    }
}
