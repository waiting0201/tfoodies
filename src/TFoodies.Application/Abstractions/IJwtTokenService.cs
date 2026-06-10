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

    /// <summary>
    /// 產生 refresh token。為「無狀態」簽章 JWT（內含 subject 與到期時間），
    /// 不需伺服器端儲存，重啟／多副本部署皆可驗證。
    /// </summary>
    string GenerateRefreshToken(string subject, DateTime expiry);

    /// <summary>驗證 refresh token（簽章 + 到期 + token_use）。成功回傳 subject；失效回傳 null。</summary>
    string? ValidateRefreshToken(string refreshToken);
}
