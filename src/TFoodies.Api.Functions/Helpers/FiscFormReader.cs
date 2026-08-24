using TFoodies.Api.Functions.Router;

namespace TFoodies.Api.Functions.Helpers;

/// <summary>
/// 財金 WEBPOS 回呼的 HTTP 讀取（授權結果導回 / 主動通知）。
/// 只負責把 request 變成欄位字典或原始字串；成敗判定與欄位解析在
/// <see cref="TFoodies.Infrastructure.Payments.Fisc.FiscWebposParser"/>（純函式，有測試鎖行為）。
/// </summary>
public static class FiscFormReader
{
    /// <summary>
    /// 讀取 form；非 form content-type 或讀取失敗一律回空字典（回呼不該因此 500）。
    /// 字典為**大小寫不敏感**：財金欄位名雖有大小寫之別（errDesc/lastPan4），但一個欄位名
    /// 大小寫飄移就整筆診斷資料落空的代價太高，與 ParseAuthResp 內部的字典一致。
    /// </summary>
    public static async Task<IReadOnlyDictionary<string, string>> ReadFormSafeAsync(RouteContext ctx, CancellationToken ct)
    {
        try
        {
            if (!ctx.Request.HasFormContentType) return Empty();
            var form = await ctx.Request.ReadFormAsync(ct);
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in form) dict[kv.Key] = kv.Value.ToString();
            return dict;
        }
        catch
        {
            return Empty();
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

    private static Dictionary<string, string> Empty() => new(StringComparer.OrdinalIgnoreCase);
}
