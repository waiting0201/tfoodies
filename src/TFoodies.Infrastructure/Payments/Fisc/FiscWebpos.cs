using TFoodies.Application.Abstractions;

namespace TFoodies.Infrastructure.Payments.Fisc;

/// <summary>
/// 財金 FISC FOCAS_WEBPOS 刷卡 form 隱藏欄位產生器（手冊 3.1.1）。
/// 由 store 結帳（/store/payment/create）、後台線上刷卡（/admin/orders/{code}/charge）與
/// 收款連結（/store/paylinks/{token}/checkout）共用，差別僅在 lidm/金額來源與
/// AuthResURL（授權結果導回前台 / 後台 / 收款連結結果頁）。
/// purchAmt 一律由後端權威計算，避免前端竄改。
/// </summary>
public static class FiscWebpos
{
    /// <summary>訂單刷卡：實付金額 = 商品小計 + 運費 - 折扣。</summary>
    public static Dictionary<string, string> BuildFields(OrderSummary summary, FiscOptions fisc, string authResUrl)
        => BuildFields(summary.OrderCode, summary.Total + summary.Freight - summary.Discount, fisc, authResUrl);

    /// <summary>
    /// 通用版：呼叫端自行決定單號與實付金額。收款連結用此多載（金額即管理員填入的數字，
    /// 不加運費、不套折扣）。
    /// </summary>
    public static Dictionary<string, string> BuildFields(string lidm, int purchAmt, FiscOptions fisc, string authResUrl)
    {
        // 欄位集合與舊系統正式可運作表單一致（ShoppingProfile.cshtml:344-353，共 9 欄）：
        // merID/MerchantID/TerminalID/lidm/purchAmt/AuthResURL/enCodeType/PayType/AutoCap（實測成功，不需 customize）。
        return new Dictionary<string, string>
        {
            ["merID"]        = fisc.MerID,
            ["MerchantID"]   = fisc.MerchantID,
            ["TerminalID"]   = fisc.TerminalID,
            ["lidm"]         = lidm,
            ["purchAmt"]     = purchAmt.ToString(),
            ["AuthResURL"]   = authResUrl,
            ["enCodeType"]   = "UTF-8",
            ["PayType"]      = "0",            // 一般交易
            ["AutoCap"]      = "1",            // 自動轉入請款檔
        };
    }
}
