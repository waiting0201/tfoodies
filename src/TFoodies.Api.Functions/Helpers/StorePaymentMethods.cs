using TFoodies.Domain.Enums;

namespace TFoodies.Api.Functions.Helpers;

/// <summary>
/// 前台結帳可用的付款方式（單一真相）。
///
/// 舊做法是把選項寫死在結帳頁的 radio、後端完全不驗，任何 int 都能寫進 Orders.paytype。
/// 改由本清單同時驅動「GET /store/payment/methods 回傳的選項」與「下單時的白名單驗證」，
/// 兩邊不會再各說各話。LINE Pay 未啟用時整個消失（前台看不到、後端也拒收）。
/// </summary>
public static class StorePaymentMethods
{
    public static IReadOnlyList<StorePaymentMethod> Available(bool linePayEnabled)
    {
        var methods = new List<StorePaymentMethod>
        {
            new((int)PayType.CreditCard, "credit", "信用卡線上刷卡", "結帳時將自動跳轉至銀行刷卡頁面"),
        };

        if (linePayEnabled)
            methods.Add(new((int)PayType.LinePay, "linepay", "LINE Pay", "結帳時將跳轉至 LINE Pay 完成付款"));

        methods.Add(new((int)PayType.CashOnDelivery, "delivery", "宅配貨到付款", "貨品寄達時向貨運司機支付款項"));
        return methods;
    }

    /// <summary>下單時的白名單驗證。</summary>
    public static bool IsAllowed(int payType, bool linePayEnabled)
        => Available(linePayEnabled).Any(m => m.Value == payType);
}

/// <summary>前台付款方式選項。<paramref name="Value"/> 即 Orders.paytype 寫入值。</summary>
public sealed record StorePaymentMethod(int Value, string Key, string Label, string Note);
