using TFoodies.Domain.Common;

namespace TFoodies.Application.Abstractions;

public interface IDiscountService
{
    /// <summary>
    /// 驗證折扣碼並計算折扣金額（NTD）。
    /// orderSubtotal 不含運費，用於計算百分比折扣。
    /// 失敗回傳 Error，不拋出例外。
    /// </summary>
    Task<Result<DiscountResult>> ValidateAsync(
        string discountCode,
        int orderSubtotal,
        Guid? memberId,
        CancellationToken ct = default);
}

public sealed record DiscountResult(
    Guid DiscountId,
    string DiscountCode,
    int DiscountAmount);     // 折扣金額（NTD），已計算好
