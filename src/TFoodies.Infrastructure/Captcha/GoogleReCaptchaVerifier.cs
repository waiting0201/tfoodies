using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using TFoodies.Application.Abstractions;

namespace TFoodies.Infrastructure.Captcha;

/// <summary>
/// Google reCAPTCHA v3 驗證實作。呼叫 siteverify API 比對 token，並檢查分數門檻。
///
/// 設計：若未設定 SecretKey（本地開發 / 尚未佈署金鑰），<see cref="VerifyAsync"/> 直接放行，
/// 讓功能在金鑰備妥前仍可端到端運作；一旦設定金鑰即強制驗證。
/// 錯誤處理對齊 <c>MitakeSmsService</c>：外部呼叫失敗時 catch 後回傳判定結果，不丟例外。
/// </summary>
public sealed class GoogleReCaptchaVerifier : ICaptchaVerifier
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ReCaptchaOptions _options;

    public GoogleReCaptchaVerifier(IHttpClientFactory httpClientFactory, IOptions<ReCaptchaOptions> options)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
    }

    public async Task<bool> VerifyAsync(string? token, string? action = null, CancellationToken ct = default)
    {
        // 未設定金鑰 → 略過驗證（本地 / 尚未佈署）。
        if (string.IsNullOrWhiteSpace(_options.SecretKey)) return true;

        if (string.IsNullOrWhiteSpace(token)) return false;

        var client = _httpClientFactory.CreateClient(nameof(GoogleReCaptchaVerifier));

        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["secret"]   = _options.SecretKey,
            ["response"] = token,
        });

        ReCaptchaSiteVerifyResponse? result;
        try
        {
            var response = await client.PostAsync(_options.VerifyUrl, form, ct);
            if (!response.IsSuccessStatusCode) return false;
            var json = await response.Content.ReadAsStringAsync(ct);
            result = JsonSerializer.Deserialize<ReCaptchaSiteVerifyResponse>(json);
        }
        catch
        {
            // 驗證服務不可用：回 false（不放行），由呼叫端回應錯誤。
            return false;
        }

        if (result is null || !result.Success) return false;

        // v3：分數門檻（0.0 ~ 1.0，越高越像真人）。
        if (result.Score < _options.MinScore) return false;

        // action 比對（若呼叫端有指定且回應有帶 action）。
        if (!string.IsNullOrWhiteSpace(action) &&
            !string.IsNullOrWhiteSpace(result.Action) &&
            !string.Equals(action, result.Action, StringComparison.Ordinal))
        {
            return false;
        }

        return true;
    }

    private sealed class ReCaptchaSiteVerifyResponse
    {
        [JsonPropertyName("success")] public bool Success { get; set; }
        [JsonPropertyName("score")] public double Score { get; set; }
        [JsonPropertyName("action")] public string? Action { get; set; }
        [JsonPropertyName("hostname")] public string? Hostname { get; set; }
        [JsonPropertyName("error-codes")] public string[]? ErrorCodes { get; set; }
    }
}

/// <summary>
/// reCAPTCHA 組態。對應 appsettings.json 中的 "ReCaptcha" 節。
/// SecretKey 為機密（後端用）；網站金鑰（site key）為前端公開值，不在此處。
/// </summary>
public sealed class ReCaptchaOptions
{
    public const string SectionName = "ReCaptcha";

    /// <summary>後端機密金鑰。留空則略過驗證（本地 / 尚未佈署）。</summary>
    public string SecretKey { get; set; } = string.Empty;

    /// <summary>v3 分數門檻，低於此值視為機器人。預設 0.5。</summary>
    public double MinScore { get; set; } = 0.5;

    public string VerifyUrl { get; set; } = "https://www.google.com/recaptcha/api/siteverify";
}
