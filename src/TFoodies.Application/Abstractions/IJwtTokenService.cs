using System.Security.Claims;

namespace TFoodies.Application.Abstractions;

/// <summary>
/// JWT access token 產生與驗證 + refresh token 存取。Singleton。
/// </summary>
public interface IJwtTokenService
{
    /// <summary>驗證 access token，回傳 ClaimsPrincipal；無效回傳 null。</summary>
    ClaimsPrincipal? ValidateAccessToken(string token);

    /// <summary>產生 access token（有效期由設定決定）。</summary>
    string GenerateAccessToken(IEnumerable<Claim> claims);

    /// <summary>產生不透明 refresh token（cryptographically random）。</summary>
    string GenerateRefreshToken();

    /// <summary>儲存 refresh token → (subject, expiry) 對應。</summary>
    void StoreRefreshToken(string refreshToken, string subject, DateTime expiry);

    /// <summary>驗證並消費 refresh token（one-time use）。成功回傳 subject；失效回傳 null。</summary>
    string? ConsumeRefreshToken(string refreshToken);
}
