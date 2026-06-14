using System.Net;
using TFoodies.Infrastructure.Invoicing.EzPay;
using Xunit;

namespace TFoodies.Infrastructure.Tests.Invoicing;

public class EzPayCodecTests
{
    private const string HashKey = "12345678901234567890123456789012"; // 32 chars
    private const string HashIv = "1234567890123456";                   // 16 chars

    private static EzPayCodec Codec() => new(HashKey, HashIv);

    [Fact]
    public void Encrypt_IsLowerHex_Deterministic_AndRoundTrips()
    {
        var codec = Codec();
        const string plain = "RespondType=JSON&Version=1.5&MerchantOrderNo=O20260601001&TotalAmt=105";

        var hex1 = codec.EncryptToHex(plain);
        var hex2 = codec.EncryptToHex(plain);

        Assert.Equal(hex1, hex2);                                   // CBC with fixed IV → deterministic
        Assert.Matches("^[0-9a-f]+$", hex1);                        // lower-case hex
        Assert.Equal(0, hex1.Length % 2);                           // whole bytes
        Assert.Equal(plain, codec.DecryptFromHex(hex1));            // round-trip
    }

    [Fact]
    public void BuildPostData_ProducesDecryptableUrlEncodedPayload()
    {
        var codec = Codec();
        var inner = new[]
        {
            new KeyValuePair<string, string>("RespondType", "JSON"),
            new KeyValuePair<string, string>("Version", "1.5"),
            new KeyValuePair<string, string>("MerchantOrderNo", "O20260601001"),
            new KeyValuePair<string, string>("ItemName", "橄欖油|巴薩米克醋"),
        };

        var postData = codec.BuildPostData(inner);
        var decoded = codec.DecryptFromHex(postData);

        Assert.Contains("RespondType=JSON", decoded);
        Assert.Contains("Version=1.5", decoded);
        // value should be url-encoded inside the payload
        Assert.Contains($"ItemName={WebUtility.UrlEncode("橄欖油|巴薩米克醋")}", decoded);
    }

    // 金鑰驗證刻意延後到實際加解密時：建構不會丟例外（避免未設定 EzPay 的環境在 DI 解析時整個炸掉），
    // 但一旦真正用到加解密就必須驗證金鑰長度。
    [Theory]
    [InlineData("tooShortKey", HashIv)]
    [InlineData(HashKey, "shortIV")]
    public void Constructor_DoesNotValidate_ButEncryptRejectsWrongKeyOrIvLength(string key, string iv)
    {
        var codec = new EzPayCodec(key, iv); // 不丟例外
        Assert.Throws<ArgumentException>(() => codec.EncryptToHex("x=1"));
    }

    [Fact]
    public void Sha256Upper_Is64HexChars()
    {
        var hash = EzPayCodec.Sha256Upper("HashIV=" + HashIv + "&x=1&HashKey=" + HashKey);
        Assert.Equal(64, hash.Length);
        Assert.Matches("^[0-9A-F]+$", hash);
    }
}
