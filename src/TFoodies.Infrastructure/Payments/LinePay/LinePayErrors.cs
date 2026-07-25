namespace TFoodies.Infrastructure.Payments.LinePay;

/// <summary>
/// LINE Pay returnCode → 可顯示給顧客的中文訊息。
/// 未列舉的代碼退回 LINE Pay 原始 returnMessage（英文），至少不會吞掉線索。
/// </summary>
public static class LinePayErrors
{
    private static readonly Dictionary<string, string> Messages = new()
    {
        ["1104"] = "商店設定有誤，請聯繫客服。",
        ["1105"] = "此商店目前無法使用 LINE Pay，請改用其他付款方式。",
        ["1106"] = "標頭資訊有誤，請重新操作。",
        ["1124"] = "付款金額有誤，請重新下單。",
        ["1133"] = "查無此交易，請重新付款。",
        ["1141"] = "LINE Pay 帳號狀態異常，請確認帳號後再試。",
        ["1142"] = "LINE Pay 餘額不足。",
        ["1145"] = "此交易正在處理中，請稍候再試。",
        ["1150"] = "查無此交易，請重新付款。",
        ["1152"] = "此交易已完成付款。",
        ["1155"] = "交易單號有誤，請重新下單。",
        ["1159"] = "付款資訊有誤，請重新操作。",
        ["1169"] = "需要於 LINE Pay 完成本人認證後再付款。",
        ["1170"] = "LINE Pay 餘額不足。",
        ["1172"] = "此交易已完成付款。",
        ["1177"] = "已超過付款可用時間，請重新下單。",
        ["1183"] = "付款金額必須大於 0。",
        ["1198"] = "系統忙碌中，請稍後再試。",
        ["2101"] = "付款參數有誤，請重新操作。",
        ["2102"] = "付款參數有誤，請重新操作。",
        ["9000"] = "LINE Pay 內部錯誤，請稍後再試。",
    };

    public static string Describe(string returnCode, string returnMessage)
        => Messages.TryGetValue(returnCode, out var msg)
            ? msg
            : string.IsNullOrWhiteSpace(returnMessage)
                ? $"LINE Pay 交易失敗（{returnCode}）。"
                : $"LINE Pay 交易失敗（{returnCode}）：{returnMessage}";
}
