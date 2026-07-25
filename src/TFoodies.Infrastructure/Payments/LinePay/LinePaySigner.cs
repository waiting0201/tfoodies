using System.Security.Cryptography;
using System.Text;

namespace TFoodies.Infrastructure.Payments.LinePay;

/// <summary>
/// LINE Pay Online API 的請求簽章（純函式，可完全離線測試；v3 與 v4 的簽章方式相同）。
///
/// 規格：<c>X-LINE-Authorization = Base64( HMAC-SHA256( ChannelSecret,
///        ChannelSecret + requestUri + requestBody + nonce ) )</c>
/// ChannelSecret 同時是 HMAC 金鑰與被簽訊息的前綴（LINE Pay 的規定，非筆誤）。
/// GET 請求則以 query string 取代 requestBody（本系統只用 POST）。
///
/// requestUri 為**不含網域的路徑**（例：/v4/payments/request）；requestBody 必須是
/// 實際送出的 JSON 字串本身 — 重新序列化一次可能產生不同字串而導致驗簽失敗，
/// 因此呼叫端一律「先序列化成字串 → 簽章 → 送出同一字串」。
/// </summary>
public static class LinePaySigner
{
    /// <summary>產生一次性 nonce（LINE Pay 要求每次請求唯一）。</summary>
    public static string NewNonce() => Guid.NewGuid().ToString();

    /// <summary>計算 X-LINE-Authorization 標頭值。</summary>
    public static string Sign(string channelSecret, string requestUri, string requestBody, string nonce)
    {
        var message = channelSecret + requestUri + requestBody + nonce;
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(channelSecret));
        return Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(message)));
    }
}
