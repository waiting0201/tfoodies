using System.Security.Cryptography;
using TFoodies.Infrastructure.Payments.Fisc;
using Xunit;

namespace TFoodies.Infrastructure.Tests.Payments;

public class FiscMessageCodecTests
{
    // 32-byte keys as hex.
    private const string VerKeyHex = "000102030405060708090A0B0C0D0E0F101112131415161718191A1B1C1D1E1F";
    private const string FieldKeyHex = "202122232425262728292A2B2C2D2E2F303132333435363738393A3B3C3D3E3F";

    private static FiscMessageCodec Codec() => FiscMessageCodec.FromHex(VerKeyHex, FieldKeyHex);

    [Fact]
    public void BuildVerifyCode_IsDeterministic_AndOrderIndependent()
    {
        var codec = Codec();
        var a = new Dictionary<string, string>
        {
            ["merchantId"] = "M001", ["amt"] = "000000010500", ["orderNumber"] = "O20260601001",
        };
        var b = new Dictionary<string, string>  // same data, different insertion order
        {
            ["orderNumber"] = "O20260601001", ["amt"] = "000000010500", ["merchantId"] = "M001",
        };

        var codeA = codec.BuildVerifyCode(a);
        var codeB = codec.BuildVerifyCode(b);

        Assert.Equal(codeA, codeB);
        Assert.Equal(64, codeA.Length); // SHA-256 hex
    }

    [Fact]
    public void BuildVerifyCode_ExcludesEmptyAndVerifyCodeItself()
    {
        var codec = Codec();
        var withExtras = new Dictionary<string, string>
        {
            ["merchantId"] = "M001", ["amt"] = "100", ["empty"] = "", ["verifyCode"] = "SHOULD_BE_IGNORED",
        };
        var without = new Dictionary<string, string> { ["merchantId"] = "M001", ["amt"] = "100" };

        Assert.Equal(codec.BuildVerifyCode(without), codec.BuildVerifyCode(withExtras));
    }

    [Fact]
    public void VerifyVerifyCode_AcceptsGood_RejectsTampered()
    {
        var codec = Codec();
        var fields = new Dictionary<string, string> { ["merchantId"] = "M001", ["amt"] = "100" };
        var good = codec.BuildVerifyCode(fields);

        Assert.True(codec.VerifyVerifyCode(fields, good));
        Assert.False(codec.VerifyVerifyCode(fields, good[..^1] + (good[^1] == 'A' ? 'B' : 'A')));
    }

    [Fact]
    public void EncryptField_RoundTrips()
    {
        var codec = Codec();
        var (cipher, nonce, tag) = codec.EncryptField("0512"); // e.g. card expiry YYMM

        Assert.NotEqual("0512", cipher);
        Assert.Equal(FiscMessageCodec.NonceSize * 2, nonce.Length); // hex
        Assert.Equal("0512", codec.DecryptField(cipher, nonce, tag));
    }

    [Fact]
    public void DecryptField_FailsOnTamperedTag()
    {
        var codec = Codec();
        var (cipher, nonce, tag) = codec.EncryptField("secret");
        var badTag = tag[..^1] + (tag[^1] == '0' ? '1' : '0');

        Assert.Throws<AuthenticationTagMismatchException>(() => codec.DecryptField(cipher, nonce, badTag));
    }
}
