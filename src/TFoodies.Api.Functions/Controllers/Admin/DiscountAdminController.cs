using Dapper;
using Microsoft.AspNetCore.Mvc;
using TFoodies.Api.Functions.Helpers;
using TFoodies.Api.Functions.Router;
using TFoodies.Application.Abstractions;
using TFoodies.Contracts.Common;

namespace TFoodies.Api.Functions.Controllers.Admin;

/// <summary>
/// 後台折扣碼管理（DiscountMs 模組）。
///   GET    /admin/discounts               — 列表（分頁，可篩 isdisable）
///   POST   /admin/discounts               — 新增折扣碼
///   GET    /admin/discounts/{id}          — 明細
///   PUT    /admin/discounts/{id}          — 更新折扣碼
///   DELETE /admin/discounts/{id}          — 軟刪除（isdisable=1）
/// </summary>
public sealed class DiscountAdminController
{
    private readonly IAdminPermissionService _perms;
    private readonly IDbConnectionFactory _db;

    public DiscountAdminController(IAdminPermissionService perms, IDbConnectionFactory db)
    {
        _perms = perms;
        _db    = db;
    }

    // ══════════════════════════════════════════════════════════════════
    // LIST — GET /admin/discounts?page=&pageSize=20&isdisable=
    // ══════════════════════════════════════════════════════════════════

    public async Task<IActionResult> List(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "DiscountMs", AdminOperation.Read);
        if (guard.Result is not null) return guard.Result;

        var q        = ctx.Request.Query;
        var page     = Math.Max(1, int.TryParse(q["page"],     out var pg) ? pg : 1);
        var pageSize = Math.Clamp(int.TryParse(q["pageSize"],  out var sz) ? sz : 20, 1, 100);
        var offset   = (page - 1) * pageSize;

        // isdisable 篩選：未傳入時回傳全部（含停用）
        int? isdisableFilter = int.TryParse(q["isdisable"], out var isd) ? isd : null;

        var whereClause = isdisableFilter.HasValue ? "WHERE isdisable = @isdisable" : "";

        using var conn = await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);

        var total = await conn.ExecuteScalarAsync<int>(
            $"SELECT COUNT(1) FROM Discounts {whereClause}",
            isdisableFilter.HasValue ? new { isdisable = isdisableFilter.Value } : null);

        var items = await conn.QueryAsync(
            $@"SELECT discountid, discountcode, istype, startdate, expiredate,
                      isonetime, v, memo, isdisable
               FROM Discounts
               {whereClause}
               ORDER BY discountcode
               OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY",
            isdisableFilter.HasValue
                ? (object)new { isdisable = isdisableFilter.Value, offset, pageSize }
                : new { offset, pageSize });

        var list = items.Select(r => (object)new {
            discountId    = r.discountid,
            discountcode  = r.discountcode,
            istype        = r.istype,
            startdate     = r.startdate,
            expiredate    = r.expiredate,
            isonetime     = r.isonetime,
            v             = r.v,
            memo          = r.memo,
            isdisable     = r.isdisable,
        }).ToList();
        return ctx.OkPaged(PaginatedResponse<object>.Create(list, total, page, pageSize));
    }

    // ══════════════════════════════════════════════════════════════════
    // CREATE — POST /admin/discounts
    // ══════════════════════════════════════════════════════════════════

    public async Task<IActionResult> Create(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "DiscountMs", AdminOperation.Add);
        if (guard.Result is not null) return guard.Result;

        var body = await ctx.TryReadBodyAsync<UpsertDiscountRequest>();
        if (body is null) return ctx.BadRequest("Request body 格式不正確。");

        var validation = ValidateDiscount(body);
        if (validation is not null) return ctx.BadRequest(validation);

        var newId = Guid.NewGuid();

        using var conn = await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);
        await conn.ExecuteAsync(@"
INSERT INTO Discounts
    (discountid, discountcode, istype, startdate, expiredate, isonetime, v, memo, isdisable)
VALUES
    (@discountid, @discountcode, @istype, @startdate, @expiredate, @isonetime, @v, @memo, @isdisable)",
            new
            {
                discountid   = newId,
                discountcode = body.Discountcode,
                istype       = body.Istype,
                startdate    = body.Startdate,
                expiredate   = body.Expiredate,
                isonetime    = body.Isonetime,
                v            = body.V,
                memo         = body.Memo ?? string.Empty,
                isdisable    = body.Isdisable,
            });

        return ctx.Created(new { discountId = newId });
    }

    // ══════════════════════════════════════════════════════════════════
    // DETAIL — GET /admin/discounts/{id}
    // ══════════════════════════════════════════════════════════════════

    public async Task<IActionResult> Detail(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "DiscountMs", AdminOperation.Read);
        if (guard.Result is not null) return guard.Result;

        if (!Guid.TryParse(ctx.RequirePathParam("id"), out var discountId))
            return ctx.BadRequest("無效的折扣碼 ID。");

        using var conn = await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);
        var row = await conn.QuerySingleOrDefaultAsync(
            @"SELECT discountid, discountcode, istype, startdate, expiredate,
                     isonetime, v, memo, isdisable
              FROM Discounts
              WHERE discountid = @discountId",
            new { discountId });

        if (row is null) return ctx.NotFound("找不到折扣碼。");
        return ctx.Ok(row);
    }

    // ══════════════════════════════════════════════════════════════════
    // UPDATE — PUT /admin/discounts/{id}
    // ══════════════════════════════════════════════════════════════════

    public async Task<IActionResult> Update(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "DiscountMs", AdminOperation.Update);
        if (guard.Result is not null) return guard.Result;

        if (!Guid.TryParse(ctx.RequirePathParam("id"), out var discountId))
            return ctx.BadRequest("無效的折扣碼 ID。");

        var body = await ctx.TryReadBodyAsync<UpsertDiscountRequest>();
        if (body is null) return ctx.BadRequest("Request body 格式不正確。");

        var validation = ValidateDiscount(body);
        if (validation is not null) return ctx.BadRequest(validation);

        using var conn = await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);
        var rows = await conn.ExecuteAsync(@"
UPDATE Discounts
SET discountcode = @discountcode,
    istype       = @istype,
    startdate    = @startdate,
    expiredate   = @expiredate,
    isonetime    = @isonetime,
    v            = @v,
    memo         = @memo,
    isdisable    = @isdisable
WHERE discountid = @discountid",
            new
            {
                discountid   = discountId,
                discountcode = body.Discountcode,
                istype       = body.Istype,
                startdate    = body.Startdate,
                expiredate   = body.Expiredate,
                isonetime    = body.Isonetime,
                v            = body.V,
                memo         = body.Memo ?? string.Empty,
                isdisable    = body.Isdisable,
            });

        if (rows == 0) return ctx.NotFound("找不到折扣碼。");
        return ctx.Ok(new { message = "折扣碼已更新" });
    }

    // ══════════════════════════════════════════════════════════════════
    // SOFT DELETE — DELETE /admin/discounts/{id}
    // ══════════════════════════════════════════════════════════════════

    public async Task<IActionResult> Delete(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "DiscountMs", AdminOperation.Delete);
        if (guard.Result is not null) return guard.Result;

        if (!Guid.TryParse(ctx.RequirePathParam("id"), out var discountId))
            return ctx.BadRequest("無效的折扣碼 ID。");

        using var conn = await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);
        var rows = await conn.ExecuteAsync(
            "UPDATE Discounts SET isdisable = 1 WHERE discountid = @discountId AND isdisable = 0",
            new { discountId });

        if (rows == 0) return ctx.NotFound("找不到折扣碼或折扣碼已停用。");
        return ctx.Ok(new { message = "折扣碼已停用" });
    }

    // ══════════════════════════════════════════════════════════════════
    // Validation & DTOs
    // ══════════════════════════════════════════════════════════════════

    private static string? ValidateDiscount(UpsertDiscountRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.Discountcode))       return "discountcode 為必填欄位。";
        if (r.Discountcode.Length > 8)                        return "discountcode 長度不可超過 8 個字元。";
        // istype 0=折扣比例（v=折扣後比例，如 0.85=85折）、1=折抵金額（v=固定金額）。
        // 對齊 Domain.DiscountType 與 DiscountService 計算邏輯。
        if (r.Istype != 0 && r.Istype != 1)                  return "istype 須為 0（折扣比例）或 1（折抵金額）。";
        // isonetime 0=不限、1=全站限用一次、2=每位會員限用一次（見 DiscountService）。
        if (r.Isonetime < 0 || r.Isonetime > 2)              return "isonetime 須為 0（不限）、1（全站限一次）或 2（每位會員限一次）。";
        if (r.Isdisable != 0 && r.Isdisable != 1)            return "isdisable 須為 0（啟用）或 1（停用）。";
        if (r.V <= 0)                                         return "v（折扣值）必須大於 0。";
        return null;
    }

    private sealed record UpsertDiscountRequest(
        string       Discountcode,
        int          Istype,
        DateTime?    Startdate,
        DateTime?    Expiredate,
        int          Isonetime,
        decimal      V,
        string?      Memo,
        int          Isdisable);
}
