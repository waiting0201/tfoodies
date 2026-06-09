using Microsoft.Extensions.Options;
using TFoodies.Application.Abstractions;

namespace TFoodies.Infrastructure.Sms;

/// <summary>
/// 三竹（Mitake）簡訊服務實作。
/// </summary>
public sealed class MitakeSmsService : ISmsService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly MitakeSmsOptions _options;

    public MitakeSmsService(IHttpClientFactory httpClientFactory, IOptions<MitakeSmsOptions> options)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
    }

    public async Task<bool> SendAsync(string mobile, string message, CancellationToken ct = default)
    {
        var client = _httpClientFactory.CreateClient(nameof(MitakeSmsService));

        var formData = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["username"] = _options.Username,
            ["password"] = _options.Password,
            ["dstaddr"]  = mobile,
            ["smbody"]   = message,
            ["Charset"]  = "BIG5",
        });

        HttpResponseMessage response;
        try
        {
            response = await client.PostAsync(_options.ApiUrl, formData, ct);
        }
        catch
        {
            return false;
        }

        if (!response.IsSuccessStatusCode) return false;

        var body = await response.Content.ReadAsStringAsync(ct);
        return body.Contains("statuscode=0");
    }
}

/// <summary>
/// 三竹簡訊服務組態。對應 appsettings.json 中的 "Sms" 節。
/// </summary>
public sealed class MitakeSmsOptions
{
    public const string SectionName = "Sms";

    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string ApiUrl   { get; set; } = "https://sms.mitake.com.tw/b2c/mtk/SmSend";
}
