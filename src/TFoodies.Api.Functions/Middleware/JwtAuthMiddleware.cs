using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using TFoodies.Api.Functions.Helpers;
using TFoodies.Api.Functions.Router;
using TFoodies.Contracts.Common;

namespace TFoodies.Api.Functions.Middleware;

/// <summary>
/// JWT 認證中介層（Singleton）：
/// - 從 Authorization header 提取 Bearer token 並驗證
/// - 公開路由（store/*、auth/*）略過驗證
/// - 驗證通過後設定 context.CurrentUser
///
/// 僅依賴 JwtHelper（Singleton）+ ILogger（thread-safe），安全作為 Singleton。
/// </summary>
public sealed class JwtAuthMiddleware : IMiddleware
{
    private readonly JwtHelper _jwtHelper;
    private readonly ILogger<JwtAuthMiddleware> _logger;

    /// <summary>
    /// 不需要 JWT 驗證的公開路由前綴（前台讀取 + 認證端點）
    /// </summary>
    private static readonly HashSet<string> PublicPrefixes = new(StringComparer.OrdinalIgnoreCase)
    {
        "store",
        "auth",
        "health",
    };

    public JwtAuthMiddleware(JwtHelper jwtHelper, ILogger<JwtAuthMiddleware> logger)
    {
        _jwtHelper = jwtHelper;
        _logger = logger;
    }

    public async Task InvokeAsync(RouteContext context, Func<Task> next)
    {
        var route = context.Route.Trim('/');

        if (IsPublicRoute(route))
        {
            await next();
            return;
        }

        var authHeader = context.Request.Headers.Authorization.ToString();
        if (string.IsNullOrWhiteSpace(authHeader) ||
            !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            context.Result = new UnauthorizedObjectResult(
                ApiErrorResponse.Create("UNAUTHORIZED", "缺少或格式不正確的 Authorization header。"));
            return;
        }

        var token = authHeader["Bearer ".Length..].Trim();
        var principal = _jwtHelper.ValidateToken(token);

        if (principal is null)
        {
            _logger.LogWarning("JWT 驗證失敗，路由：{Route}", route);
            context.Result = new UnauthorizedObjectResult(
                ApiErrorResponse.Create("UNAUTHORIZED", "Token 無效或已過期。"));
            return;
        }

        context.CurrentUser = principal;
        await next();
    }

    private static bool IsPublicRoute(string route)
    {
        foreach (var prefix in PublicPrefixes)
        {
            if (route.Equals(prefix, StringComparison.OrdinalIgnoreCase) ||
                route.StartsWith(prefix + "/", StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }
}
