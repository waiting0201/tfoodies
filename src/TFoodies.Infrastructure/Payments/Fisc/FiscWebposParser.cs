namespace TFoodies.Infrastructure.Payments.Fisc;

/// <summary>
/// 財金 WEBPOS 授權結果解析（手冊 v2.7 §3.1.2 / §3.4.3）。
///
/// 由訂單刷卡（PaymentController）與收款連結（PaymentLinkController）共用——授權成功的判定
/// 條件只能有一份，複製兩份遲早會走樣。
///
/// ⚠️ 除了成敗，還必須把 <c>errcode</c>/<c>errDesc</c> 一起解出來：顧客回報「刷卡沒成功」時，
/// 這兩欄是唯一能回答「為什麼」的資料（財金的 errDesc 本身就是中文說明）。解析結果由
/// <c>IPaymentAttemptLog</c> 寫進 Paymentattempts 表，後台訂單詳情才查得到。
/// ⚠️ 財金另回傳遮罩卡號 <c>pan</c>（480254******9104），刻意**不解析**：只留 lastPan4。
/// </summary>
public static class FiscWebposParser
{
    /// <summary>授權結果。<c>IsSuccess</c> 為 true 才可標記已付款。</summary>
    public sealed record WebposResult(
        bool IsSuccess,
        string Lidm,
        string? LastPan4,
        string TxnRef,
        string? Status = null,
        string? ErrCode = null,
        string? ErrDesc = null,
        string? AuthCode = null,
        string? Xid = null,
        string? CardBrand = null,
        int? AuthAmt = null);

    /// <summary>status=="0" 且 authCode 非空 = 授權成功。</summary>
    public static WebposResult ParseForm(IReadOnlyDictionary<string, string> f)
    {
        var status   = f.GetValueOrDefault("status");
        var authCode = f.GetValueOrDefault("authCode");
        var lidm     = f.GetValueOrDefault("lidm") ?? "";
        var lastPan4 = f.GetValueOrDefault("lastPan4");
        var xid      = f.GetValueOrDefault("xid");

        var success = status == "0" && !string.IsNullOrWhiteSpace(authCode);
        var txnRef  = $"FISC authCode:{authCode} xid:{xid}";

        // 失敗診斷欄位（手冊 §3.1.2）：errcode 固定 2 位、errDesc 為中文失敗原因說明。
        // authAmt 可能是空字串或 "null"（見手冊主動通知範例 authCode=null），解不出來就留 null。
        return new WebposResult(
            success, lidm, NullIfLiteralNull(lastPan4), txnRef,
            Status:    NullIfLiteralNull(status),
            ErrCode:   NullIfLiteralNull(f.GetValueOrDefault("errcode")),
            ErrDesc:   NullIfLiteralNull(f.GetValueOrDefault("errDesc")),
            AuthCode:  NullIfLiteralNull(authCode),
            Xid:       NullIfLiteralNull(xid),
            CardBrand: NullIfLiteralNull(f.GetValueOrDefault("cardBrand")),
            AuthAmt:   int.TryParse(f.GetValueOrDefault("authAmt"), out var amt) ? amt : null);
    }

    /// <summary>主動通知字串：AuthResp={status=0, authCode=123456, lidm=..., lastPan4=9104, ...}</summary>
    public static WebposResult ParseAuthResp(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return new WebposResult(false, "", null, "");

        var s = raw.Trim();
        var eq = s.IndexOf('=');
        if (s.StartsWith("AuthResp", StringComparison.OrdinalIgnoreCase) && eq >= 0)
            s = s[(eq + 1)..];
        s = s.Trim().Trim('{', '}');

        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in s.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var i = pair.IndexOf('=');
            if (i <= 0) continue;
            dict[pair[..i].Trim()] = pair[(i + 1)..].Trim();
        }
        return ParseForm(dict);
    }

    // 財金在無值欄位會送字面上的 "null"（見手冊 authCode=null 範例），別把它當成有效值存進 DB。
    private static string? NullIfLiteralNull(string? v)
        => string.IsNullOrWhiteSpace(v) || v.Equals("null", StringComparison.OrdinalIgnoreCase) ? null : v;
}
