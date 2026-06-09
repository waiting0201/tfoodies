using System.Security.Claims;
using System.Text;
using Dapper;
using Microsoft.AspNetCore.Mvc;
using TFoodies.Api.Functions.Router;
using TFoodies.Application.Abstractions;

namespace TFoodies.Api.Functions.Controllers;

/// <summary>
/// 會員個人資料與收藏清單端點（需要 JWT，role = member）。
///   PATCH  /member/profile              — 更新個人資料
///   GET    /member/wishlist             — 取得收藏清單
///   POST   /member/wishlist             — 新增收藏
///   DELETE /member/wishlist/{productId} — 移除收藏
/// </summary>
public sealed class MemberProfileController
{
    private readonly IDbConnectionFactory _db;

    public MemberProfileController(IDbConnectionFactory db) => _db = db;

    // PATCH /member/profile
    public async Task<IActionResult> UpdateProfile(RouteContext ctx)
    {
        var memberId = RequireMemberId(ctx);
        if (memberId is null) return ctx.Unauthorized("請先登入會員帳號。");

        var ct   = ctx.Request.HttpContext.RequestAborted;
        var body = await ctx.TryReadBodyAsync<UpdateProfileRequest>(ct);
        if (body is null) return ctx.BadRequest("Request body 格式不正確。");

        // 動態建構 UPDATE，只更新有提供值的欄位
        var sets   = new List<string>();
        var param  = new DynamicParameters();
        param.Add("memberId", memberId.Value);

        if (body.Name is not null)
        {
            sets.Add("name = @name");
            param.Add("name", body.Name);
        }
        if (body.Email is not null)
        {
            sets.Add("email = @email");
            param.Add("email", body.Email);
        }
        if (body.Address is not null)
        {
            sets.Add("address = @address");
            param.Add("address", body.Address);
        }
        if (body.ZipcodeId is not null)
        {
            sets.Add("zipcodeid = @zipcodeid");
            param.Add("zipcodeid", body.ZipcodeId.Value);
        }
        if (body.Birthday is not null)
        {
            sets.Add("birthday = @birthday");
            param.Add("birthday", body.Birthday.Value);
        }
        if (body.Gender is not null)
        {
            sets.Add("gender = @gender");
            param.Add("gender", body.Gender.Value);
        }

        if (sets.Count == 0) return ctx.Ok(new { message = "已更新" });

        using var conn = await _db.CreateOpenConnectionAsync(ct);

        var sql = $"UPDATE Members SET {string.Join(", ", sets)} WHERE memberid = @memberId";
        await conn.ExecuteAsync(sql, param);

        return ctx.Ok(new { message = "已更新" });
    }

    // GET /member/wishlist
    public async Task<IActionResult> GetWishlist(RouteContext ctx)
    {
        var memberId = RequireMemberId(ctx);
        if (memberId is null) return ctx.Unauthorized("請先登入會員帳號。");

        var ct = ctx.Request.HttpContext.RequestAborted;
        using var conn = await _db.CreateOpenConnectionAsync(ct);

        var items = await conn.QueryAsync<WishlistItem>(@"
SELECT p.productid, p.title, p.price, p.fixprice, p.shortener, pp.filename
FROM   Memberproducts mp
JOIN   Products       p  ON  p.productid  = mp.productid
LEFT JOIN Productphotos pp ON pp.productid = p.productid  AND pp.sort = 1
WHERE  mp.memberid = @id
ORDER  BY mp.createdate DESC",
            new { id = memberId.Value });

        return ctx.Ok(new { items });
    }

    // POST /member/wishlist
    public async Task<IActionResult> AddWishlist(RouteContext ctx)
    {
        var memberId = RequireMemberId(ctx);
        if (memberId is null) return ctx.Unauthorized("請先登入會員帳號。");

        var ct   = ctx.Request.HttpContext.RequestAborted;
        var body = await ctx.TryReadBodyAsync<WishlistAddRequest>(ct);
        if (body is null || body.ProductId is null)
            return ctx.BadRequest("缺少 productId 欄位。");

        using var conn = await _db.CreateOpenConnectionAsync(ct);

        await conn.ExecuteAsync(@"
IF NOT EXISTS (
    SELECT 1 FROM Memberproducts
    WHERE memberid = @memberid AND productid = @productid
)
INSERT INTO Memberproducts (memberproductid, productid, memberid, createdate)
VALUES (NEWID(), @productid, @memberid, @createdate)",
            new
            {
                memberid  = memberId.Value,
                productid = body.ProductId.Value,
                createdate = DateTime.UtcNow.AddHours(8),
            });

        return ctx.Ok(new { message = "已加入收藏" });
    }

    // DELETE /member/wishlist/{productId}
    public async Task<IActionResult> RemoveWishlist(RouteContext ctx)
    {
        var memberId = RequireMemberId(ctx);
        if (memberId is null) return ctx.Unauthorized("請先登入會員帳號。");

        var productIdStr = ctx.RequirePathParam("productId");
        if (!Guid.TryParse(productIdStr, out var productId))
            return ctx.BadRequest("productId 格式不正確。");

        var ct = ctx.Request.HttpContext.RequestAborted;
        using var conn = await _db.CreateOpenConnectionAsync(ct);

        await conn.ExecuteAsync(
            "DELETE FROM Memberproducts WHERE memberid = @memberid AND productid = @productid",
            new { memberid = memberId.Value, productid = productId });

        return ctx.Ok(new { message = "已移除" });
    }

    // ── Helpers ───────────────────────────────────────────────────────────────────

    private static Guid? RequireMemberId(RouteContext ctx)
    {
        var user = ctx.CurrentUser;
        if (user is null) return null;
        var role = user.FindFirstValue(ClaimTypes.Role);
        if (!string.Equals(role, "member", StringComparison.OrdinalIgnoreCase)) return null;
        var id = user.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(id, out var guid) ? guid : null;
    }

    // ── Request / Response records ────────────────────────────────────────────────

    private sealed record UpdateProfileRequest(
        string? Name,
        string? Email,
        string? Address,
        int? ZipcodeId,
        DateOnly? Birthday,
        int? Gender);

    private sealed record WishlistAddRequest(Guid? ProductId);

    private sealed record WishlistItem(
        Guid   productid,
        string title,
        int    price,
        int?   fixprice,
        string shortener,
        string? filename);
}
