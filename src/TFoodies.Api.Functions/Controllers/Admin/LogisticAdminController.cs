using Dapper;
using Microsoft.AspNetCore.Mvc;
using TFoodies.Api.Functions.Helpers;
using TFoodies.Api.Functions.Router;
using TFoodies.Application.Abstractions;

namespace TFoodies.Api.Functions.Controllers.Admin;

/// <summary>
/// 後台物流商管理（OrderMs → Logistics）。對應舊系統 OrderMsController 的
/// Logistics / AddLogistics / EditLogistics。物流商無刪除（舊系統亦無），以 isenable 控制啟用。
///
///   GET    /admin/logistics        — 清單
///   GET    /admin/logistics/{id}   — 明細
///   POST   /admin/logistics        — 新增
///   PUT    /admin/logistics/{id}   — 更新
/// </summary>
public sealed class LogisticAdminController
{
    private readonly IAdminPermissionService _perms;
    private readonly IDbConnectionFactory _db;

    public LogisticAdminController(IAdminPermissionService perms, IDbConnectionFactory db)
    {
        _perms = perms;
        _db = db;
    }

    // GET /admin/logistics
    public async Task<IActionResult> List(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "OrderMs", AdminOperation.Read);
        if (guard.Result is not null) return guard.Result;

        using var conn = await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);
        var rows = await conn.QueryAsync(
            "SELECT logisticid, logisticcode, title, address, phone, isenable FROM Logistics ORDER BY title");
        return ctx.Ok(rows);
    }

    // GET /admin/logistics/{id}
    public async Task<IActionResult> Detail(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "OrderMs", AdminOperation.Read);
        if (guard.Result is not null) return guard.Result;
        if (!Guid.TryParse(ctx.RequirePathParam("id"), out var id)) return ctx.BadRequest("無效的物流商 ID。");

        using var conn = await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);
        var row = await conn.QuerySingleOrDefaultAsync(
            "SELECT logisticid, logisticcode, title, address, phone, isenable FROM Logistics WHERE logisticid=@id",
            new { id });
        if (row is null) return ctx.NotFound("找不到物流商。");
        return ctx.Ok(row);
    }

    // POST /admin/logistics
    public async Task<IActionResult> Create(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "OrderMs", AdminOperation.Add);
        if (guard.Result is not null) return guard.Result;

        var body = await ctx.TryReadBodyAsync<UpsertLogisticRequest>();
        if (body is null || string.IsNullOrWhiteSpace(body.Title)) return ctx.BadRequest("缺少 title。");

        var id = Guid.NewGuid();
        using var conn = await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);
        await conn.ExecuteAsync(@"
INSERT INTO Logistics (logisticid, logisticcode, title, address, phone, isenable)
VALUES (@id, @logisticcode, @title, @address, @phone, @isenable)",
            new
            {
                id,
                logisticcode = body.LogisticCode ?? string.Empty,
                title = body.Title.Trim(),
                address = body.Address,
                phone = body.Phone,
                isenable = body.IsEnable,
            });
        return ctx.Created(new { logisticId = id });
    }

    // PUT /admin/logistics/{id}
    public async Task<IActionResult> Update(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "OrderMs", AdminOperation.Update);
        if (guard.Result is not null) return guard.Result;
        if (!Guid.TryParse(ctx.RequirePathParam("id"), out var id)) return ctx.BadRequest("無效的物流商 ID。");

        var body = await ctx.TryReadBodyAsync<UpsertLogisticRequest>();
        if (body is null || string.IsNullOrWhiteSpace(body.Title)) return ctx.BadRequest("缺少 title。");

        using var conn = await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);
        var rows = await conn.ExecuteAsync(@"
UPDATE Logistics SET logisticcode=@logisticcode, title=@title, address=@address, phone=@phone, isenable=@isenable
WHERE logisticid=@id",
            new
            {
                id,
                logisticcode = body.LogisticCode ?? string.Empty,
                title = body.Title.Trim(),
                address = body.Address,
                phone = body.Phone,
                isenable = body.IsEnable,
            });
        if (rows == 0) return ctx.NotFound("找不到物流商。");
        return ctx.Ok(new { message = "物流商已更新" });
    }

    private sealed record UpsertLogisticRequest(
        string? LogisticCode, string Title, string? Address, string? Phone, bool IsEnable);
}
