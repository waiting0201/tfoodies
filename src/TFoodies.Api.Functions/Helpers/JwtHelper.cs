using System.Security.Claims;
using TFoodies.Application.Abstractions;

namespace TFoodies.Api.Functions.Helpers;

/// <summary>
/// JwtHelper 委派給 IJwtTokenService（Infrastructure 層）。
/// 保留此類別供 JwtAuthMiddleware 使用（不破壞現有 Singleton 組裝）。
/// </summary>
public class JwtHelper
{
    private readonly IJwtTokenService _jwt;

    public JwtHelper(IJwtTokenService jwt) => _jwt = jwt;

    public ClaimsPrincipal? ValidateToken(string token) => _jwt.ValidateAccessToken(token);
}
