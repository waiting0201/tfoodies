using Dapper;
using TFoodies.Application.Abstractions;
using TFoodies.Domain.Common;

namespace TFoodies.Infrastructure.Orders;

/// <summary>
/// 折扣碼驗證服務。移植舊系統 General.GetDiscount 的邏輯：
///   isdisable=0、未過期、startdate 已到、isonetime 檢查。
///   istype=0（百分比）：discountAmount = round(subtotal * (1 - v))
///   istype=1（固定金額）：discountAmount = (int)v
/// </summary>
public sealed class DiscountService : IDiscountService
{
    private readonly IDbConnectionFactory _db;

    public DiscountService(IDbConnectionFactory db) => _db = db;

    public async Task<Result<DiscountResult>> ValidateAsync(
        string discountCode, int orderSubtotal, Guid? memberId, CancellationToken ct = default)
    {
        using var conn = await _db.CreateOpenConnectionAsync(ct);

        var row = await conn.QuerySingleOrDefaultAsync<DiscountRow>(
            @"SELECT discountid, discountcode, istype, startdate, expiredate, isonetime, v, isdisable
              FROM Discounts
              WHERE discountcode = @discountCode",
            new { discountCode });

        if (row is null)
            return new Error("discount.not_found", "查無此折扣碼");

        if (row.isdisable == 1)
            return new Error("discount.disabled", "折扣碼無法使用");

        var now = DateTime.UtcNow.AddHours(8); // 台灣時間

        if (row.startdate.HasValue && now < row.startdate.Value)
            return new Error("discount.not_started", "折扣碼尚無法使用");

        if (row.expiredate.HasValue && now > row.expiredate.Value)
            return new Error("discount.expired", "折扣碼使用過期");

        // isonetime = 0：無限制；1：全域一次性；2：每會員限用一次
        if (row.isonetime == 1)
        {
            var used = await conn.ExecuteScalarAsync<int>(
                "SELECT COUNT(1) FROM Orders WHERE discountid = @id", new { id = row.discountid });
            if (used > 0)
                return new Error("discount.used", "折扣碼已使用");
        }
        else if (row.isonetime == 2 && memberId.HasValue)
        {
            var used = await conn.ExecuteScalarAsync<int>(
                "SELECT COUNT(1) FROM Orders WHERE discountid = @id AND memberid = @memberId",
                new { id = row.discountid, memberId = memberId.Value });
            if (used > 0)
                return new Error("discount.used_by_member", "此折扣碼已使用過");
        }

        int discountAmount;
        if (row.istype == 0) // 百分比折扣：v = 折扣後比例（如 0.9 = 9折）
        {
            discountAmount = (int)Math.Round(orderSubtotal * (1m - row.v), MidpointRounding.AwayFromZero);
        }
        else // 固定金額
        {
            discountAmount = (int)row.v;
        }

        discountAmount = Math.Max(0, Math.Min(discountAmount, orderSubtotal));

        return new DiscountResult(row.discountid, row.discountcode, discountAmount);
    }

    private sealed record DiscountRow(
        Guid discountid, string discountcode,
        int istype, DateTime? startdate, DateTime? expiredate,
        int isonetime, decimal v, int isdisable);
}
