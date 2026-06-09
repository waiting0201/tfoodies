using System.Security.Cryptography;
using System.Text;

namespace TFoodies.Infrastructure.Payments.Fisc;

/// <summary>
/// Wire crypto for 財金 API 2.0 (v1.2.8): the HMAC-SHA256 verifyCode and AES-GCM field
/// encryption (new mode, since v1.2). Pure/deterministic so it can be unit-tested against the
/// spec's sample vectors before any live call (plan §5: "用規格附樣本向量做單元測試").
///
/// verifyCode canonicalisation: take all fields except verifyCode and empty values, sort by
/// field name (ordinal), join as key=value with '&', HMAC-SHA256 with the verification key,
/// output upper-case hex (64 chars). The exact concatenation is confirmed against spec vectors.
/// </summary>
public sealed class FiscMessageCodec
{
    private readonly byte[] _verificationKey;
    private readonly byte[] _fieldKey;

    public const int NonceSize = 12;   // AES-GCM IV = 12 bytes (tag50 = 24 hex)
    public const int TagSize = 16;     // AES-GCM auth tag = 16 bytes (tag10)

    public FiscMessageCodec(byte[] verificationKey, byte[] fieldKey)
    {
        _verificationKey = verificationKey ?? throw new ArgumentNullException(nameof(verificationKey));
        _fieldKey = fieldKey ?? throw new ArgumentNullException(nameof(fieldKey));
    }

    /// <summary>Build the verifyCode over a field set (excludes "verifyCode" and empty values).</summary>
    public string BuildVerifyCode(IReadOnlyDictionary<string, string> fields)
    {
        var canonical = string.Join("&", fields
            .Where(kv => !string.Equals(kv.Key, "verifyCode", StringComparison.OrdinalIgnoreCase)
                         && !string.IsNullOrEmpty(kv.Value))
            .OrderBy(kv => kv.Key, StringComparer.Ordinal)
            .Select(kv => $"{kv.Key}={kv.Value}"));

        using var hmac = new HMACSHA256(_verificationKey);
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(canonical));
        return Convert.ToHexString(hash); // upper-case, 64 chars
    }

    /// <summary>Constant-time verifyCode check.</summary>
    public bool VerifyVerifyCode(IReadOnlyDictionary<string, string> fields, string received)
    {
        var expected = BuildVerifyCode(fields);
        return CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(expected),
            Encoding.ASCII.GetBytes(received?.ToUpperInvariant() ?? string.Empty));
    }

    /// <summary>
    /// AES-GCM encrypt a field. Returns hex(nonce) + hex(ciphertext) + hex(tag) — the
    /// caller places these into tag50 (IV) and tag10 (auth tag) per spec.
    /// </summary>
    public (string CipherHex, string NonceHex, string TagHex) EncryptField(string plaintext, byte[]? nonce = null)
    {
        nonce ??= RandomNumberGenerator.GetBytes(NonceSize);
        if (nonce.Length != NonceSize) throw new ArgumentException($"nonce must be {NonceSize} bytes", nameof(nonce));

        var plainBytes = Encoding.UTF8.GetBytes(plaintext);
        var cipher = new byte[plainBytes.Length];
        var tag = new byte[TagSize];

        using var aes = new AesGcm(_fieldKey, TagSize);
        aes.Encrypt(nonce, plainBytes, cipher, tag);

        return (Convert.ToHexString(cipher), Convert.ToHexString(nonce), Convert.ToHexString(tag));
    }

    /// <summary>AES-GCM decrypt a field; throws CryptographicException if the tag fails.</summary>
    public string DecryptField(string cipherHex, string nonceHex, string tagHex)
    {
        var cipher = Convert.FromHexString(cipherHex);
        var nonce = Convert.FromHexString(nonceHex);
        var tag = Convert.FromHexString(tagHex);
        var plain = new byte[cipher.Length];

        using var aes = new AesGcm(_fieldKey, TagSize);
        aes.Decrypt(nonce, cipher, tag, plain);

        return Encoding.UTF8.GetString(plain);
    }

    /// <summary>Factory from configured hex keys.</summary>
    public static FiscMessageCodec FromHex(string verificationKeyHex, string fieldKeyHex)
        => new(Convert.FromHexString(verificationKeyHex), Convert.FromHexString(fieldKeyHex));
}
