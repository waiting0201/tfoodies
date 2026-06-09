using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using TFoodies.Application.Abstractions;
using TFoodies.Api.Functions.Router;

namespace TFoodies.Api.Functions.Helpers;

/// <summary>
/// 後台 Controller 共用守衛。
///
/// 用法（每個 admin action 第一步）：
///   var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "OrderMs", AdminOperation.Read);
///   if (guard.Result is not null) return guard.Result;
///   int adminId = guard.AdminId;
/// </summary>
public sealed class AdminGuard
{
    public int AdminId { get; }
    public IActionResult? Result { get; }

    private AdminGuard(int adminId, IActionResult? result)
    {
        AdminId = adminId;
        Result = result;
    }

    /// <summary>
    /// 驗證 JWT role=admin + Lims/AdminLims 模組授權。
    /// 回傳的 Result 非 null 時，直接 return 給 Functions host。
    /// </summary>
    public static async Task<AdminGuard> AuthorizeAsync(
        RouteContext ctx,
        IAdminPermissionService perms,
        string module,
        AdminOperation operation,
        CancellationToken ct = default)
    {
        var (adminId, authError) = ExtractAdminId(ctx.CurrentUser);
        if (authError is not null) return new AdminGuard(0, authError);

        var allowed = await perms.HasPermissionAsync(adminId, module, operation, ct);
        if (!allowed) return new AdminGuard(adminId, ctx.Forbidden($"無 {module} 模組的 {operation} 權限。"));

        return new AdminGuard(adminId, null);
    }

    /// <summary>僅驗證已登入（role=admin），不檢查模組授權。適用於唯讀的 dashboard。</summary>
    public static AdminGuard RequireAdmin(RouteContext ctx)
    {
        var (adminId, authError) = ExtractAdminId(ctx.CurrentUser);
        return new AdminGuard(adminId, authError);
    }

    // ── Private ───────────────────────────────────────────────────────────────────

    private static (int adminId, IActionResult? error) ExtractAdminId(ClaimsPrincipal? user)
    {
        if (user is null)
            return (0, UnauthorizedResult());

        var role = user.FindFirstValue(ClaimTypes.Role);
        if (!string.Equals(role, "admin", StringComparison.OrdinalIgnoreCase))
            return (0, UnauthorizedResult());

        var idStr = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(idStr, out var adminId))
            return (0, UnauthorizedResult());

        return (adminId, null);
    }

    private static IActionResult UnauthorizedResult() =>
        new Microsoft.AspNetCore.Mvc.UnauthorizedObjectResult(
            TFoodies.Contracts.Common.ApiErrorResponse.Create("UNAUTHORIZED", "請先登入後台帳號。"));
}
