namespace TFoodies.Infrastructure.Auth;

public sealed class JwtSettings
{
    public const string SectionName = "Jwt";

    /// <summary>HMAC-SHA256 signing secret (min 32 chars).</summary>
    public string Secret { get; set; } = "";

    /// <summary>Token issuer — used in iss claim.</summary>
    public string Issuer { get; set; } = "tfoodies-api";

    /// <summary>Token audience — used in aud claim.</summary>
    public string Audience { get; set; } = "tfoodies-client";

    /// <summary>Access token lifetime in minutes (default 60).</summary>
    public int ExpiryMinutes { get; set; } = 60;

    /// <summary>Refresh token lifetime in days (default 30).</summary>
    public int RefreshExpiryDays { get; set; } = 30;
}
