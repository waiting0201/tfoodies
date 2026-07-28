using TFoodies.Domain.Common;

namespace TFoodies.Application.Abstractions;

public interface IDiscountService
{
    /// <summary>
    /// 驗證折扣碼並計算折扣金額（NTD）。
    /// orderSubtotal 不含運費，用於計算百分比折扣。
    /// 失敗回傳 Error，不拋出例外。
    ///
    /// enforcePerMemberLimit：是否套用 isonetime=2（每會員限用一次）的檢查。
    /// 前台結帳的「套用」預覽階段拿不到 memberId（store/* 為公開路由，CurrentUser 恆為 null），
    /// 而下單時才以手機號解析出會員 → 若兩階段規則不同，顧客會遇到「套用顯示成功、送出卻被拒」
    /// 且無從補救。因此前台下單階段以 false 呼叫：已通過預覽的碼一律放行。
    /// 後台/其他呼叫端維持 true。
    /// </summary>
    Task<Result<DiscountResult>> ValidateAsync(
        string discountCode,
        int orderSubtotal,
        Guid? memberId,
        bool enforcePerMemberLimit = true,
        CancellationToken ct = default);
}

public sealed record DiscountResult(
    Guid DiscountId,
    string DiscountCode,
    int DiscountAmount);     // 折扣金額（NTD），已計算好
