namespace TFoodies.Infrastructure.Payments.Fisc;

/// <summary>
/// 財金 API 2.0 merchant configuration. Secrets (keys) come from Key Vault; never hard-coded
/// (the legacy system hard-coded HashKey/IV — see plan §安全債).
/// </summary>
public sealed class FiscOptions
{
    public const string SectionName = "Fisc";

    public string BaseUrl { get; set; } = "https://www.focas-test.fisc.com.tw/FOCAS_WS/API20/V1/FISCII";
    public string MerchantId { get; set; } = "";
    public string TerminalId { get; set; } = "";
    public string AcqBank { get; set; } = "";

    /// <summary>HMAC-SHA256 verification key as a hex string (recommended 24/32 bytes → 48/64 hex).</summary>
    public string VerificationKeyHex { get; set; } = "";

    /// <summary>AES-GCM field-encryption key as a hex string (16 or 32 bytes → 32/64 hex).</summary>
    public string FieldKeyHex { get; set; } = "";
}
