using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Dapper;
using Microsoft.Extensions.Options;
using TFoodies.Application.Abstractions;

namespace TFoodies.Infrastructure.Auth;

/// <summary>
/// 登入驗證 + refresh token 換發。Scoped（依賴 IDbConnectionFactory Singleton）。
///
/// 密碼策略（hash-on-login）：
///   1. 先嘗試以 PBKDF2 驗證（新格式 "pbkdf2:迭代:salt:hash"）。
///   2. 若格式不符，判斷為明文舊密碼，直接比對。
///   3. 成功後自動升級：以 PBKDF2 替換明文密碼，完成向前遷移。
///
/// JWT 主體格式："role:id"，例如 "member:3fa85f64-..."  /  "admin:888"
/// </summary>
public sealed class AuthService : IAuthService
{
    private readonly IDbConnectionFactory _db;
    private readonly IJwtTokenService _jwt;
    private readonly JwtSettings _settings;

    public AuthService(IDbConnectionFactory db, IJwtTokenService jwt, IOptions<JwtSettings> options)
    {
        _db = db;
        _jwt = jwt;
        _settings = options.Value;
    }

    public async Task<TokenPair?> LoginAsync(
        string role, string identifier, string password, CancellationToken ct = default)
    {
        return role.ToLowerInvariant() switch
        {
            "member" => await LoginMemberAsync(identifier, password, ct),
            "admin" => await LoginAdminAsync(identifier, password, ct),
            _ => null
        };
    }

    public Task<TokenPair?> RefreshAsync(string refreshToken, CancellationToken ct = default)
    {
        var subject = _jwt.ValidateRefreshToken(refreshToken);
        if (subject is null) return Task.FromResult<TokenPair?>(null);

        var parts = subject.Split(':', 2);
        if (parts.Length != 2) return Task.FromResult<TokenPair?>(null);

        var claims = BuildClaims(parts[0], parts[1], parts[1]);
        return Task.FromResult<TokenPair?>(IssueTokenPair(subject, claims));
    }

    // ── Member ────────────────────────────────────────────────────────────────────

    private async Task<TokenPair?> LoginMemberAsync(string mobile, string password, CancellationToken ct)
    {
        using var conn = await _db.CreateOpenConnectionAsync(ct);

        var row = await conn.QuerySingleOrDefaultAsync<MemberRow>(
            "SELECT memberid, name, password FROM Members WHERE mobile = @mobile AND ismember = 1 AND isenable = 1",
            new { mobile });

        if (row is null) return null;

        if (!VerifyPassword(password, row.password)) return null;

        await UpgradePasswordIfNeeded(conn, "UPDATE Members SET password=@h WHERE memberid=@id",
            row.password, password, row.memberid.ToString());

        var subject = $"member:{row.memberid}";
        var claims = BuildClaims("member", row.memberid.ToString(), row.name);
        return IssueTokenPair(subject, claims);
    }

    // ── Admin ─────────────────────────────────────────────────────────────────────

    private async Task<TokenPair?> LoginAdminAsync(string username, string password, CancellationToken ct)
    {
        using var conn = await _db.CreateOpenConnectionAsync(ct);

        var row = await conn.QuerySingleOrDefaultAsync<AdminRow>(
            "SELECT AdminID, Username, Password FROM Admins WHERE Username = @username AND Isenable = 1",
            new { username });

        if (row is null) return null;

        if (!VerifyPassword(password, row.Password)) return null;

        await UpgradePasswordIfNeeded(conn, "UPDATE Admins SET Password=@h WHERE AdminID=@id",
            row.Password, password, row.AdminID.ToString());

        var subject = $"admin:{row.AdminID}";
        var claims = BuildClaims("admin", row.AdminID.ToString(), row.Username);
        return IssueTokenPair(subject, claims);
    }

    // ── Token helpers ──────────────────────────────────────────────────────────────

    private static IEnumerable<Claim> BuildClaims(string role, string id, string name) =>
    [
        new(ClaimTypes.NameIdentifier, id),
        new(ClaimTypes.Name, name),
        new(ClaimTypes.Role, role),
    ];

    private TokenPair IssueTokenPair(string subject, IEnumerable<Claim> claims)
    {
        var accessToken = _jwt.GenerateAccessToken(claims);
        var expiry = DateTime.UtcNow.AddDays(_settings.RefreshExpiryDays);
        var refreshToken = _jwt.GenerateRefreshToken(subject, expiry);
        return new TokenPair(accessToken, refreshToken, DateTime.UtcNow.AddMinutes(_settings.ExpiryMinutes));
    }

    // ── Password helpers ───────────────────────────────────────────────────────────

    private static bool VerifyPassword(string input, string stored)
    {
        if (stored.StartsWith("pbkdf2:", StringComparison.Ordinal))
            return VerifyPbkdf2(input, stored);
        // 明文比對
        return string.Equals(input, stored, StringComparison.Ordinal);
    }

    private static bool VerifyPbkdf2(string input, string stored)
    {
        // Format: "pbkdf2:{iterations}:{saltBase64}:{hashBase64}"
        var parts = stored.Split(':');
        if (parts.Length != 4) return false;
        if (!int.TryParse(parts[1], out var iter)) return false;
        var salt = Convert.FromBase64String(parts[2]);
        var expected = Convert.FromBase64String(parts[3]);
        var actual = Pbkdf2Hash(input, salt, iter);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    private static string HashPassword(string password)
    {
        const int Iterations = 260_000;
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Pbkdf2Hash(password, salt, Iterations);
        return $"pbkdf2:{Iterations}:{Convert.ToBase64String(salt)}:{Convert.ToBase64String(hash)}";
    }

    private static byte[] Pbkdf2Hash(string password, byte[] salt, int iterations)
        => Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            salt,
            iterations,
            HashAlgorithmName.SHA256,
            32);

    private static async Task UpgradePasswordIfNeeded(
        System.Data.IDbConnection conn, string updateSql,
        string currentStored, string plaintext, string id)
    {
        if (currentStored.StartsWith("pbkdf2:", StringComparison.Ordinal)) return;
        var hashed = HashPassword(plaintext);
        await conn.ExecuteAsync(updateSql, new { h = hashed, id });
    }

    // ── Row types ──────────────────────────────────────────────────────────────────

    private sealed record MemberRow(Guid memberid, string name, string password);
    private sealed record AdminRow(int AdminID, string Username, string Password);
}
