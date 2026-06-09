using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using TFoodies.Application.Abstractions;

namespace TFoodies.Infrastructure.Auth;

/// <summary>
/// HS256 JWT access token + opaque refresh token。Singleton。
/// Refresh token 以 in-memory ConcurrentDictionary 儲存（single instance 足夠）；
/// 多副本部署或重啟後需換成 Redis / Azure Table Storage（TODO）。
/// </summary>
public sealed class JwtTokenService : IJwtTokenService
{
    private readonly JwtSettings _settings;
    private readonly SymmetricSecurityKey _signingKey;
    private readonly TokenValidationParameters _validationParams;
    private readonly JwtSecurityTokenHandler _handler = new();

    // refreshToken → (subject, expiresAt)
    private readonly ConcurrentDictionary<string, (string Subject, DateTime Expiry)> _refreshTokens = new();

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
            return _handler.ValidateToken(token, _validationParams, out _);
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

    public string GenerateRefreshToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

    public void StoreRefreshToken(string refreshToken, string subject, DateTime expiry)
        => _refreshTokens[refreshToken] = (subject, expiry);

    public string? ConsumeRefreshToken(string refreshToken)
    {
        if (!_refreshTokens.TryRemove(refreshToken, out var entry)) return null;
        if (entry.Expiry < DateTime.UtcNow) return null;
        return entry.Subject;
    }
}
