using System.Security.Cryptography;
using System.Text;
using TFoodies.Infrastructure.Payments.LinePay;

namespace TFoodies.Infrastructure.Tests.Payments;

// LINE Pay 的驗簽核心（v3/v4 相同）。簽錯的症狀是所有請求一律 401/1106，且從錯誤訊息看不出哪裡錯，
// 故用固定向量鎖住「ChannelSecret 同時是金鑰與訊息前綴」這個容易被寫掉的規定。
public class LinePaySignerTests
{
    private const string Secret = "test-channel-secret";
    private const string Path = "/v4/payments/request";
    private const string Body = """{"amount":1200,"currency":"TWD","orderId":"O20260725001"}""";
    private const string Nonce = "0b7b5f3a-1c2d-4e5f-8a9b-0c1d2e3f4a5b";

    [Fact]
    public void Sign_matches_channel_secret_prefixed_hmac()
    {
        // 規格：Base64( HMAC-SHA256( ChannelSecret, ChannelSecret + requestUri + body + nonce ) )
        var expected = Convert.ToBase64String(
            new HMACSHA256(Encoding.UTF8.GetBytes(Secret))
                .ComputeHash(Encoding.UTF8.GetBytes(Secret + Path + Body + Nonce)));

        Assert.Equal(expected, LinePaySigner.Sign(Secret, Path, Body, Nonce));
    }

    [Fact]
    public void Sign_is_deterministic_for_same_inputs()
        => Assert.Equal(
            LinePaySigner.Sign(Secret, Path, Body, Nonce),
            LinePaySigner.Sign(Secret, Path, Body, Nonce));

    [Theory]
    [InlineData("other-secret", Path, Body, Nonce)]
    [InlineData(Secret, "/v4/payments/999/confirm", Body, Nonce)]
    [InlineData(Secret, Path, """{"amount":1201,"currency":"TWD","orderId":"O20260725001"}""", Nonce)]
    [InlineData(Secret, Path, Body, "11111111-2222-3333-4444-555555555555")]
    public void Sign_changes_when_any_component_changes(string secret, string path, string body, string nonce)
        => Assert.NotEqual(
            LinePaySigner.Sign(Secret, Path, Body, Nonce),
            LinePaySigner.Sign(secret, path, body, nonce));

    [Fact]
    public void NewNonce_is_unique_per_call()
        => Assert.NotEqual(LinePaySigner.NewNonce(), LinePaySigner.NewNonce());

    // 未設定完成前不得對外發起交易（避免帶著空 ChannelId 打 API，回錯而難以診斷）。
    [Theory]
    [InlineData(true, "cid", "secret", "https://sandbox-api-pay.line.me", true)]
    [InlineData(false, "cid", "secret", "https://sandbox-api-pay.line.me", false)]  // 總開關關閉
    [InlineData(true, "", "secret", "https://sandbox-api-pay.line.me", false)]      // 缺 ChannelId
    [InlineData(true, "cid", "", "https://sandbox-api-pay.line.me", false)]         // 缺 ChannelSecret
    [InlineData(true, "cid", "secret", "", false)]                                  // 缺 BaseUrl
    public void IsUsable_requires_enabled_and_complete_config(
        bool enabled, string channelId, string secret, string baseUrl, bool expected)
    {
        var opts = new LinePayOptions
        {
            Enabled = enabled, ChannelId = channelId, ChannelSecret = secret, BaseUrl = baseUrl,
        };

        Assert.Equal(expected, opts.IsUsable);
    }
}
