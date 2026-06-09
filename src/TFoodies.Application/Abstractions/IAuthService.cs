namespace TFoodies.Application.Abstractions;

/// <summary>
/// 會員 / 後台管理員登入與 refresh token 換發。
/// PBKDF2 hash-on-login：偵測到明文密碼時自動升級（不中斷登入流程）。
/// </summary>
public interface IAuthService
{
    /// <summary>
    /// 登入。role = "member"（用 mobile+password）或 "admin"（用 username+password）。
    /// 回傳 null 表示帳號不存在或密碼錯誤。
    /// </summary>
    Task<TokenPair?> LoginAsync(string role, string identifier, string password, CancellationToken ct = default);

    /// <summary>用 refresh token 換發新的 token pair（自動 rotate）。</summary>
    Task<TokenPair?> RefreshAsync(string refreshToken, CancellationToken ct = default);
}

public sealed record TokenPair(string AccessToken, string RefreshToken, DateTime ExpiresAt);
