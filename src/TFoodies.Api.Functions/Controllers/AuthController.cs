using Microsoft.AspNetCore.Mvc;
using TFoodies.Api.Functions.Router;
using TFoodies.Application.Abstractions;

namespace TFoodies.Api.Functions.Controllers;

/// <summary>
/// 認證端點（公開，無需 JWT）。Scoped，由 HandlerFactory 延遲解析。
/// </summary>
public sealed class AuthController
{
    private readonly IAuthService _auth;

    public AuthController(IAuthService auth) => _auth = auth;

    // POST /auth/login
    // Body: { "role": "member"|"admin", "identifier": "...", "password": "..." }
    public async Task<IActionResult> Login(RouteContext ctx)
    {
        var body = await ctx.TryReadBodyAsync<LoginRequest>();
        if (body is null) return ctx.BadRequest("Request body 格式不正確。");
        if (string.IsNullOrWhiteSpace(body.Role)) return ctx.BadRequest("缺少 role 欄位。");
        if (string.IsNullOrWhiteSpace(body.Identifier)) return ctx.BadRequest("缺少 identifier 欄位。");
        if (string.IsNullOrWhiteSpace(body.Password)) return ctx.BadRequest("缺少 password 欄位。");

        var pair = await _auth.LoginAsync(body.Role, body.Identifier, body.Password);
        if (pair is null)
            return ctx.Unauthorized("帳號或密碼錯誤。");

        return ctx.Ok(new TokenResponse(pair.AccessToken, pair.RefreshToken, pair.ExpiresAt));
    }

    // POST /auth/refresh
    // Body: { "refreshToken": "..." }
    public async Task<IActionResult> Refresh(RouteContext ctx)
    {
        var body = await ctx.TryReadBodyAsync<RefreshRequest>();
        if (body is null || string.IsNullOrWhiteSpace(body.RefreshToken))
            return ctx.BadRequest("缺少 refreshToken 欄位。");

        var pair = await _auth.RefreshAsync(body.RefreshToken);
        if (pair is null)
            return ctx.Unauthorized("Refresh token 無效或已過期。");

        return ctx.Ok(new TokenResponse(pair.AccessToken, pair.RefreshToken, pair.ExpiresAt));
    }

    private sealed record LoginRequest(string? Role, string? Identifier, string? Password);
    private sealed record RefreshRequest(string? RefreshToken);
    private sealed record TokenResponse(string AccessToken, string RefreshToken, DateTime ExpiresAt);
}
