using Dapper;
using Microsoft.AspNetCore.Mvc;
using TFoodies.Api.Functions.Helpers;
using TFoodies.Api.Functions.Router;
using TFoodies.Application.Abstractions;

namespace TFoodies.Api.Functions.Controllers.Admin;

/// <summary>
/// 後台報關（OrderMs → Declarations）。舊系統 OrderMsController.Declarations 僅為 TODO stub
/// （「需要判斷 stocks 的 stocktype 是否需要申報」），未實作任何畫面邏輯。
///
/// 此處提供唯讀檢視：已建立的報關單，以及尚未歸入報關單、且標記需報關（isdeclaration=1）的已出貨訂單。
///
///   GET /admin/declarations              — 已建立的報關單清單
///   GET /admin/declarations/declarable   — 待報關訂單（isdeclaration=1 且未歸入報關單）
/// </summary>
public sealed class DeclarationAdminController
{
    private readonly IAdminPermissionService _perms;
    private readonly IDbConnectionFactory _db;

    public DeclarationAdminController(IAdminPermissionService perms, IDbConnectionFactory db)
    {
        _perms = perms;
        _db = db;
    }

    // GET /admin/declarations
    public async Task<IActionResult> List(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "OrderMs", AdminOperation.Read);
        if (guard.Result is not null) return guard.Result;

        using var conn = await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);
        var rows = await conn.QueryAsync(@"
SELECT d.declarationid AS declarationId, d.declarationtype AS declarationType,
       d.declarationdate AS declarationDate, d.soldtarget AS soldTarget, d.createdate AS createDate,
       (SELECT COUNT(1) FROM Declarationdetails dd WHERE dd.declarationid = d.declarationid) AS orderCount
FROM Declarations d
ORDER BY d.declarationdate DESC, d.createdate DESC");
        return ctx.Ok(rows);
    }

    // GET /admin/declarations/declarable
    public async Task<IActionResult> Declarable(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "OrderMs", AdminOperation.Read);
        if (guard.Result is not null) return guard.Result;

        using var conn = await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);
        var rows = await conn.QueryAsync(@"
SELECT o.orderid AS orderId, o.ordercode AS orderCode, o.orderdate AS orderDate,
       m.name AS memberName, o.recivername AS receiverName, o.total
FROM Orders o
JOIN Members m ON m.memberid = o.memberid
WHERE o.isdeclaration = 1
  AND NOT EXISTS (SELECT 1 FROM Declarationdetails dd WHERE dd.orderid = o.orderid)
ORDER BY o.orderdate DESC, o.ordercode DESC");
        return ctx.Ok(rows);
    }
}
