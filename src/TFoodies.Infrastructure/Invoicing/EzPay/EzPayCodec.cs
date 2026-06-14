using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace TFoodies.Infrastructure.Invoicing.EzPay;

/// <summary>
/// Wire crypto for ezPay e-invoice (EZP_INVI 1.2.2): PostData_ is AES-256-CBC (HashKey/HashIV,
/// PKCS#7) then bin2hex (lower-case). Pure/deterministic for unit tests. The HTTP POST sends
/// MerchantID_ + PostData_ (application/x-www-form-urlencoded).
/// </summary>
public sealed class EzPayCodec
{
    private readonly string _hashKey;
    private readonly string _hashIv;

    // 金鑰驗證刻意延後到實際加解密時才做（而非建構子）：發票金鑰僅在「開立發票」路徑會用到，
    // 但 EzPayCodec 是 Singleton，會被付款完成服務間接相依。若在建構子就驗證，未設定 EzPay
    // 金鑰的環境（如本機開發）連「信用卡發起刷卡」這種不碰發票的路徑都會在 DI 解析時整個炸掉。
    public EzPayCodec(string hashKey, string hashIv)
    {
        _hashKey = hashKey;
        _hashIv = hashIv;
    }

    private (byte[] Key, byte[] Iv) ResolveKeys()
    {
        var key = Encoding.UTF8.GetBytes(_hashKey);
        var iv = Encoding.UTF8.GetBytes(_hashIv);
        if (key.Length != 32) throw new ArgumentException("HashKey must be 32 chars (AES-256).", nameof(_hashKey));
        if (iv.Length != 16) throw new ArgumentException("HashIV must be 16 chars.", nameof(_hashIv));
        return (key, iv);
    }

    /// <summary>AES-256-CBC + PKCS7, returned as lower-case hex (ezPay PostData_ format).</summary>
    public string EncryptToHex(string plaintext)
    {
        var (key, iv) = ResolveKeys();
        using var aes = Aes.Create();
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        aes.Key = key;
        aes.IV = iv;

        using var enc = aes.CreateEncryptor();
        var input = Encoding.UTF8.GetBytes(plaintext);
        var cipher = enc.TransformFinalBlock(input, 0, input.Length);
        return Convert.ToHexStringLower(cipher);
    }

    /// <summary>Reverse of <see cref="EncryptToHex"/> (used to decode ezPay responses if hex-encoded).</summary>
    public string DecryptFromHex(string hex)
    {
        var (key, iv) = ResolveKeys();
        using var aes = Aes.Create();
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        aes.Key = key;
        aes.IV = iv;

        using var dec = aes.CreateDecryptor();
        var cipher = Convert.FromHexString(hex);
        var plain = dec.TransformFinalBlock(cipher, 0, cipher.Length);
        return Encoding.UTF8.GetString(plain);
    }

    /// <summary>Build the encrypted PostData_ from inner params (url-encoded, then AES-CBC, then hex).</summary>
    public string BuildPostData(IEnumerable<KeyValuePair<string, string>> innerParams)
    {
        var query = string.Join("&", innerParams
            .Where(p => p.Value is not null)
            .Select(p => $"{WebUtility.UrlEncode(p.Key)}={WebUtility.UrlEncode(p.Value)}"));
        return EncryptToHex(query);
    }

    /// <summary>SHA-256 (upper-case hex) helper for the CheckCode used to verify ezPay responses.</summary>
    public static string Sha256Upper(string input)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input)));
}
