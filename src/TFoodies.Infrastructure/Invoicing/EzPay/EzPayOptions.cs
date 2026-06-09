namespace TFoodies.Infrastructure.Invoicing.EzPay;

/// <summary>
/// ezPay/NewebPay e-invoice configuration. HashKey/HashIV come from Key Vault (the legacy
/// system hard-coded them — see plan §安全債).
/// </summary>
public sealed class EzPayOptions
{
    public const string SectionName = "EzPay";

    public string BaseUrl { get; set; } = "https://cinv.ezpay.com.tw/Api"; // test; prod = https://inv.ezpay.com.tw/Api
    public string MerchantId { get; set; } = "";

    /// <summary>32-character key (AES-256).</summary>
    public string HashKey { get; set; } = "";

    /// <summary>16-character IV.</summary>
    public string HashIV { get; set; } = "";
}
