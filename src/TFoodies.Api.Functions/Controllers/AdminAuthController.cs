using Dapper;
using Microsoft.AspNetCore.Mvc;
using TFoodies.Api.Functions.Router;
using TFoodies.Application.Abstractions;

namespace TFoodies.Api.Functions.Controllers;

/// <summary>
/// 後台管理員認證端點（公開，無需 JWT）。Scoped，由 HandlerFactory 延遲解析。
/// </summary>
public sealed class AdminAuthController
{
    private readonly IAuthService _auth;
    private readonly IAdminPermissionService _perms;
    private readonly IDbConnectionFactory _db;

    public AdminAuthController(IAuthService auth, IAdminPermissionService perms, IDbConnectionFactory db)
    {
        _auth = auth;
        _perms = perms;
        _db = db;
    }

    // POST /auth/admin/login
    // Body: { "username": "...", "password": "..." }
    public async Task<IActionResult> Login(RouteContext ctx)
    {
        var ct = ctx.Request.HttpContext.RequestAborted;

        var body = await ctx.TryReadBodyAsync<AdminLoginRequest>(ct);
        if (body is null) return ctx.BadRequest("Request body 格式不正確。");

        var pair = await _auth.LoginAsync("admin", body.Username, body.Password, ct);
        if (pair is null)
            return ctx.Unauthorized("帳號或密碼錯誤");

        using var conn = await _db.CreateOpenConnectionAsync(ct);
        var adminRow = await conn.QuerySingleOrDefaultAsync<AdminRow>(
            "SELECT adminid, username FROM Admins WHERE LOWER(username) = LOWER(@u)",
            new { u = body.Username });

        if (adminRow is null)
            return ctx.Unauthorized("帳號或密碼錯誤");

        var perms = await _perms.GetPermissionsAsync(adminRow.adminid, ct);

        return ctx.Ok(new
        {
            accessToken  = pair.AccessToken,
            refreshToken = pair.RefreshToken,
            expiresAt    = pair.ExpiresAt,
            username     = adminRow.username,
            permissions  = perms.Select(p => p.Module).ToList()
        });
    }

    // POST /auth/admin/logout
    // No auth required. Body ignored.
    public Task<IActionResult> Logout(RouteContext ctx)
        => Task.FromResult(ctx.Ok(new { message = "logged out" }));

    // ── Private types ────────────────────────────────────────────────────────

    private sealed record AdminLoginRequest(string? Username, string? Password);

    private sealed record AdminRow(int adminid, string username);
}
