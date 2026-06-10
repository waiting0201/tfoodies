using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using TFoodies.Application.Abstractions;

namespace TFoodies.Infrastructure.Auth;

/// <summary>
/// HS256 JWT access token + 無狀態簽章 refresh token。Singleton。
/// Refresh token 為 JWT（內含 subject、token_use=refresh、到期），以同一把 HMAC 金鑰簽章；
/// 不需伺服器端儲存，故重啟、多副本部署皆可正常換發（解決 reload 跳回登入頁問題）。
/// 代價：無法於到期前主動撤銷單一 token（管理後台可接受）。
/// </summary>
public sealed class JwtTokenService : IJwtTokenService
{
    private const string TokenUseClaim = "token_use";
    private const string RefreshTokenUse = "refresh";

    private readonly JwtSettings _settings;
    private readonly SymmetricSecurityKey _signingKey;
    private readonly TokenValidationParameters _validationParams;
    private readonly JwtSecurityTokenHandler _handler = new();

    public JwtTokenService(IOptions<JwtSettings> options)
    {
        _settings = options.Value;
        _signingKey = new SymmetricSecurityKey(
            System.Text.Encoding.UTF8.GetBytes(_settings.Secret));

        _validationParams = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = _settings.Issuer,
            ValidateAudience = true,
            ValidAudience = _settings.Audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = _signingKey,
            ClockSkew = TimeSpan.FromSeconds(30),
        };
    }

    public ClaimsPrincipal? ValidateAccessToken(string token)
    {
        try
        {
            var principal = _handler.ValidateToken(token, _validationParams, out _);
            // 拒絕把 refresh token 當 access token 使用（同一把金鑰簽章）。
            if (principal.FindFirst(TokenUseClaim)?.Value == RefreshTokenUse) return null;
            return principal;
        }
        catch
        {
            return null;
        }
    }

    public string GenerateAccessToken(IEnumerable<Claim> claims)
    {
        var now = DateTime.UtcNow;
        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Issuer = _settings.Issuer,
            Audience = _settings.Audience,
            IssuedAt = now,
            NotBefore = now,
            Expires = now.AddMinutes(_settings.ExpiryMinutes),
            SigningCredentials = new SigningCredentials(_signingKey, SecurityAlgorithms.HmacSha256),
        };

        return _handler.WriteToken(_handler.CreateToken(descriptor));
    }

    public string GenerateRefreshToken(string subject, DateTime expiry)
    {
        var now = DateTime.UtcNow;
        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, subject),
                new Claim(TokenUseClaim, RefreshTokenUse),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            ]),
            Issuer = _settings.Issuer,
            Audience = _settings.Audience,
            IssuedAt = now,
            NotBefore = now,
            Expires = expiry,
            SigningCredentials = new SigningCredentials(_signingKey, SecurityAlgorithms.HmacSha256),
        };

        return _handler.WriteToken(_handler.CreateToken(descriptor));
    }

    public string? ValidateRefreshToken(string refreshToken)
    {
        try
        {
            var principal = _handler.ValidateToken(refreshToken, _validationParams, out _);
            if (principal.FindFirst(TokenUseClaim)?.Value != RefreshTokenUse) return null;
            // 預設 inbound claim mapping 會把 "sub" 對應到 NameIdentifier；兩者皆讀以策安全。
            return principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        }
        catch
        {
            return null;
        }
    }
}
