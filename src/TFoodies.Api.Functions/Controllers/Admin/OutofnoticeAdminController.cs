using Dapper;
using Microsoft.AspNetCore.Mvc;
using TFoodies.Api.Functions.Helpers;
using TFoodies.Api.Functions.Router;
using TFoodies.Application.Abstractions;
using TFoodies.Contracts.Common;

namespace TFoodies.Api.Functions.Controllers.Admin;

/// <summary>
/// 後台缺貨通知（OrderMs → Outofnotices）。對應舊系統 OrderMsController.Outofnotices。
/// 顧客在商品缺貨時登記，到貨可通知。後台僅檢視 / 標記已通知 / 刪除。
///
///   GET    /admin/outofnotices        — 清單（分頁）
///   PATCH  /admin/outofnotices/{id}/notice — 標記已通知
///   DELETE /admin/outofnotices/{id}   — 刪除
/// </summary>
public sealed class OutofnoticeAdminController
{
    private readonly IAdminPermissionService _perms;
    private readonly IDbConnectionFactory _db;

    public OutofnoticeAdminController(IAdminPermissionService perms, IDbConnectionFactory db)
    {
        _perms = perms;
        _db = db;
    }

    // GET /admin/outofnotices?page=1&pageSize=20
    public async Task<IActionResult> List(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "OrderMs", AdminOperation.Read);
        if (guard.Result is not null) return guard.Result;

        var q = ctx.Request.Query;
        var page = Math.Max(1, int.TryParse(q["page"], out var p) ? p : 1);
        var pageSize = Math.Clamp(int.TryParse(q["pageSize"], out var sz) ? sz : 20, 1, 100);
        var offset = (page - 1) * pageSize;

        using var conn = await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);

        var total = await conn.ExecuteScalarAsync<int>("SELECT COUNT(1) FROM Outofnotices");

        var rows = await conn.QueryAsync(@"
SELECT o.outofnoticeid AS outofnoticeId, o.productid AS productId, p.title AS productTitle,
       p.productnum AS productNum, o.name, o.email, o.mobile, o.createdate, o.isnotice
FROM Outofnotices o
JOIN Products p ON p.productid = o.productid
ORDER BY o.createdate DESC
OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY",
            new { offset, pageSize });

        var list = rows.Cast<object>().ToList();
        return ctx.OkPaged(PaginatedResponse<object>.Create(list, total, page, pageSize));
    }

    // PATCH /admin/outofnotices/{id}/notice
    public async Task<IActionResult> MarkNotified(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "OrderMs", AdminOperation.Update);
        if (guard.Result is not null) return guard.Result;
        if (!Guid.TryParse(ctx.RequirePathParam("id"), out var id)) return ctx.BadRequest("無效的通知 ID。");

        using var conn = await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);
        var rows = await conn.ExecuteAsync(
            "UPDATE Outofnotices SET isnotice=1 WHERE outofnoticeid=@id", new { id });
        if (rows == 0) return ctx.NotFound("找不到缺貨通知。");
        return ctx.Ok(new { message = "已標記為已通知" });
    }

    // DELETE /admin/outofnotices/{id}
    public async Task<IActionResult> Delete(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "OrderMs", AdminOperation.Delete);
        if (guard.Result is not null) return guard.Result;
        if (!Guid.TryParse(ctx.RequirePathParam("id"), out var id)) return ctx.BadRequest("無效的通知 ID。");

        using var conn = await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);
        var rows = await conn.ExecuteAsync("DELETE FROM Outofnotices WHERE outofnoticeid=@id", new { id });
        if (rows == 0) return ctx.NotFound("找不到缺貨通知。");
        return ctx.Ok(new { message = "缺貨通知已刪除" });
    }
}
