using TFoodies.Application.Abstractions;

namespace TFoodies.Infrastructure.Payments.Fisc;

/// <summary>
/// 財金 FISC FOCAS_WEBPOS 刷卡 form 隱藏欄位產生器（手冊 3.1.1）。
/// 由 store 結帳（/store/payment/create）與後台線上刷卡（/admin/orders/{code}/charge）共用，
/// 差別僅在 AuthResURL（授權結果導回前台 or 後台）。purchAmt 由後端權威計算，避免前端竄改。
/// </summary>
public static class FiscWebpos
{
    public static Dictionary<string, string> BuildFields(OrderSummary summary, FiscOptions fisc, string authResUrl)
    {
        var payable = summary.Total + summary.Freight - summary.Discount;

        return new Dictionary<string, string>
        {
            ["MerchantID"]   = fisc.MerchantID,
            ["TerminalID"]   = fisc.TerminalID,
            ["merID"]        = fisc.MerID,
            ["MerchantName"] = fisc.MerchantName,
            ["lidm"]         = summary.OrderCode,
            ["purchAmt"]     = payable.ToString(),
            ["AutoCap"]      = "1",            // 自動轉入請款檔
            ["AuthResURL"]   = authResUrl,
            ["PayType"]      = "0",            // 一般交易
            ["enCodeType"]   = "UTF-8",
        };
    }
}
