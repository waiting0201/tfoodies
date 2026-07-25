using TFoodies.Api.Functions.Router;

namespace TFoodies.Api.Functions.Helpers;

/// <summary>
/// 財金 WEBPOS 授權結果解析（手冊 v2.7 §3.1.2 / §3.4.3）。
///
/// 由訂單刷卡（PaymentController）與收款連結（PaymentLinkController）共用——授權成功的判定
/// 條件只能有一份，複製兩份遲早會走樣。
/// </summary>
public static class FiscWebposParser
{
    /// <summary>授權結果。<c>IsSuccess</c> 為 true 才可標記已付款。</summary>
    public sealed record WebposResult(bool IsSuccess, string Lidm, string? LastPan4, string TxnRef);

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
        return new WebposResult(success, lidm, lastPan4, txnRef);
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

    /// <summary>讀取 form；非 form content-type 或讀取失敗一律回空字典（回呼不該因此 500）。</summary>
    public static async Task<IReadOnlyDictionary<string, string>> ReadFormSafeAsync(RouteContext ctx, CancellationToken ct)
    {
        try
        {
            if (!ctx.Request.HasFormContentType) return new Dictionary<string, string>();
            var form = await ctx.Request.ReadFormAsync(ct);
            return form.ToDictionary(kv => kv.Key, kv => kv.Value.ToString());
        }
        catch
        {
            return new Dictionary<string, string>();
        }
    }

    public static async Task<string> ReadRawBodyAsync(RouteContext ctx, CancellationToken ct)
    {
        try
        {
            using var reader = new StreamReader(ctx.Request.Body);
            return await reader.ReadToEndAsync(ct);
        }
        catch
        {
            return "";
        }
    }
}
