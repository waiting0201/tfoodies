namespace TFoodies.Infrastructure.Orders;

public sealed class OrderSettings
{
    public const string SectionName = "Order";

    /// <summary>免運門檻（NTD）</summary>
    public int FreightLimit { get; init; } = 2000;

    /// <summary>運費金額（NTD）</summary>
    public int FreightAmount { get; init; } = 180;

    /// <summary>ATM 付款到期天數</summary>
    public int AtmExpiryDays { get; init; } = 3;

    /// <summary>國泰 ATM 前置碼（4 位）</summary>
    public string AtmPrefix { get; init; } = "1943";
}
