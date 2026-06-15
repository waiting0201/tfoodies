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

        // 欄位集合與舊系統正式可運作表單一致（ShoppingProfile.cshtml:344-353，共 9 欄）：
        // merID/MerchantID/TerminalID/lidm/purchAmt/AuthResURL/enCodeType/PayType/AutoCap（實測成功，不需 customize）。
        return new Dictionary<string, string>
        {
            ["merID"]        = fisc.MerID,
            ["MerchantID"]   = fisc.MerchantID,
            ["TerminalID"]   = fisc.TerminalID,
            ["lidm"]         = summary.OrderCode,
            ["purchAmt"]     = payable.ToString(),
            ["AuthResURL"]   = authResUrl,
            ["enCodeType"]   = "UTF-8",
            ["PayType"]      = "0",            // 一般交易
            ["AutoCap"]      = "1",            // 自動轉入請款檔
        };
    }
}
