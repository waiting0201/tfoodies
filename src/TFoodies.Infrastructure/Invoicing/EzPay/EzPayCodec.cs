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
    private readonly byte[] _key; // 32 bytes (AES-256)
    private readonly byte[] _iv;  // 16 bytes

    public EzPayCodec(string hashKey, string hashIv)
    {
        _key = Encoding.UTF8.GetBytes(hashKey);
        _iv = Encoding.UTF8.GetBytes(hashIv);
        if (_key.Length != 32) throw new ArgumentException("HashKey must be 32 chars (AES-256).", nameof(hashKey));
        if (_iv.Length != 16) throw new ArgumentException("HashIV must be 16 chars.", nameof(hashIv));
    }

    /// <summary>AES-256-CBC + PKCS7, returned as lower-case hex (ezPay PostData_ format).</summary>
    public string EncryptToHex(string plaintext)
    {
        using var aes = Aes.Create();
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        aes.Key = _key;
        aes.IV = _iv;

        using var enc = aes.CreateEncryptor();
        var input = Encoding.UTF8.GetBytes(plaintext);
        var cipher = enc.TransformFinalBlock(input, 0, input.Length);
        return Convert.ToHexStringLower(cipher);
    }

    /// <summary>Reverse of <see cref="EncryptToHex"/> (used to decode ezPay responses if hex-encoded).</summary>
    public string DecryptFromHex(string hex)
    {
        using var aes = Aes.Create();
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        aes.Key = _key;
        aes.IV = _iv;

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
