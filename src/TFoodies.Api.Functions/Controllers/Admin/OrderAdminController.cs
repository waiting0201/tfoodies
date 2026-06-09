using Dapper;
using Microsoft.AspNetCore.Mvc;
using TFoodies.Api.Functions.Helpers;
using TFoodies.Api.Functions.Router;
using TFoodies.Application.Abstractions;
using TFoodies.Contracts.Common;
using TFoodies.Domain.Enums;

namespace TFoodies.Api.Functions.Controllers.Admin;

/// <summary>
/// 後台訂單管理。
///   GET  /admin/orders                 — 訂單列表（篩選/分頁）
///   GET  /admin/orders/{code}          — 訂單明細
///   PATCH /admin/orders/{code}/pending — 未出貨 → 待出貨
///   PATCH /admin/orders/{code}/ship    — 待出貨 → 已出貨
///   PATCH /admin/orders/{code}/cancel  — 取消訂單
///   PATCH /admin/orders/{code}/pay     — 標記已付款（後台人工確認）
/// </summary>
public sealed class OrderAdminController
{
    private readonly IAdminPermissionService _perms;
    private readonly IDbConnectionFactory _db;

    public OrderAdminController(IAdminPermissionService perms, IDbConnectionFactory db)
    {
        _perms = perms;
        _db = db;
    }

    // GET /admin/orders?deliverStatus=0&payStatus=&keyword=&dateFrom=&dateTo=&page=1&pageSize=20
    // deliverStatus/payStatus < 0 → 不篩選（前端「全部」選項傳 -1）
    public async Task<IActionResult> List(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "OrderMs", AdminOperation.Read);
        if (guard.Result is not null) return guard.Result;

        var q = ctx.Request.Query;
        int? deliverStatus = int.TryParse(q["deliverStatus"], out var ds) && ds >= 0 ? ds : null;
        int? payStatus     = int.TryParse(q["payStatus"],     out var ps) && ps >= 0 ? ps : null;
        var keyword  = q["keyword"].ToString().Trim();
        DateTime? dateFrom = DateTime.TryParse(q["dateFrom"], out var df) ? df.Date : null;
        DateTime? dateTo   = DateTime.TryParse(q["dateTo"],   out var dt) ? dt.Date.AddDays(1).AddSeconds(-1) : null;
        var page     = Math.Max(1, int.TryParse(q["page"],     out var p)  ? p  : 1);
        var pageSize = Math.Clamp(int.TryParse(q["pageSize"],  out var sz) ? sz : 20, 1, 100);
        var offset   = (page - 1) * pageSize;

        using var conn = await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);

        var where = BuildOrderWhere(deliverStatus, payStatus, keyword, dateFrom, dateTo);

        var totalCount = await conn.ExecuteScalarAsync<int>(
            $"SELECT COUNT(1) FROM Orders o JOIN Members m ON m.memberid=o.memberid WHERE {where.Sql}",
            where.Params);

        var dp = new DynamicParameters(where.Params);
        dp.Add("offset", offset);
        dp.Add("pageSize", pageSize);

        var items = await conn.QueryAsync<AdminOrderRow>($@"
SELECT o.orderid, o.ordercode, o.orderdate,
       m.name AS memberName, m.mobile AS memberMobile,
       o.total, o.freight, ISNULL(o.discount,0) AS discount,
       o.paytype, o.paystatus, o.deliverstatus,
       o.codeatm, o.expirepaydate,
       o.recivername, o.reciveraddress, o.createdate
FROM Orders o
JOIN Members m ON m.memberid = o.memberid
WHERE {where.Sql}
ORDER BY o.createdate DESC
OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY", dp);

        var list = items.Select(r => new AdminOrderListItem(
            r.orderid, r.ordercode, r.orderdate,
            r.memberName, r.memberMobile,
            r.total, r.freight, r.discount,
            (PayType)r.paytype, (PayStatus)r.paystatus, (DeliverStatus)r.deliverstatus,
            r.codeatm, r.expirepaydate,
            r.recivername, r.reciveraddress,
            r.createdate)).ToList();

        return ctx.OkPaged(PaginatedResponse<AdminOrderListItem>.Create(list, totalCount, page, pageSize));
    }

    // GET /admin/orders/{code}
    public async Task<IActionResult> Detail(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "OrderMs", AdminOperation.Read);
        if (guard.Result is not null) return guard.Result;

        var code = ctx.RequirePathParam("code");
        using var conn = await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);

        using var multi = await conn.QueryMultipleAsync(@"
SELECT o.orderid, o.ordercode, o.orderdate,
       m.memberid, m.name AS memberName, m.mobile AS memberMobile,
       o.total, o.freight, ISNULL(o.discount,0) AS discount,
       o.paytype, o.paystatus, o.deliverstatus, o.paydate, o.deliverdate,
       o.invoicetype, o.invoicestatus, o.invoicecode,
       o.companytitle, o.companynumber, o.lovecode,
       o.codeatm, o.expirepaydate,
       o.recivername, o.recivermobile, o.reciverzipcodeid, o.reciveraddress, o.recivertime,
       o.remark, o.note, o.trackingnumber, o.createdate
FROM Orders o JOIN Members m ON m.memberid=o.memberid
WHERE o.ordercode = @code;

SELECT od.orderdetailid, od.productid, p.title AS productTitle,
       p.productnum, p.photo,
       od.qty, od.price, od.subtotal, od.isgift
FROM Orderdetails od
JOIN Products p ON p.productid = od.productid
JOIN Orders o2 ON o2.orderid = od.orderid
WHERE o2.ordercode = @code;",
            new { code });

        var header = await multi.ReadSingleOrDefaultAsync<AdminOrderDetailRow>();
        if (header is null) return ctx.NotFound("找不到訂單");

        var lines = (await multi.ReadAsync<AdminOrderLineRow>()).ToList();

        return ctx.Ok(new
        {
            code           = header.ordercode,
            orderDate      = header.orderdate,
            createdAt      = header.createdate,
            memberId       = header.memberid,
            memberName     = header.memberName,
            memberMobile   = header.memberMobile,
            total          = header.total,
            shippingFee    = header.freight,
            discount       = header.discount,
            payType        = (int)header.paytype,
            payStatus      = (int)header.paystatus,
            deliverStatus  = (int)header.deliverstatus,
            payDate        = header.paydate,
            deliverDate    = header.deliverdate,
            invoiceType    = (int)header.invoicetype,
            invoiceStatus  = (int)header.invoicestatus,
            invoiceCode    = header.invoicecode,
            companyTitle   = header.companytitle,
            companyNumber  = header.companynumber,
            loveCode       = header.lovecode,
            atmCode        = header.codeatm,
            atmExpiry      = header.expirepaydate,
            receiverName   = header.recivername,
            receiverMobile = header.recivermobile,
            receiverAddress= header.reciveraddress,
            receiverTime   = header.recivertime,
            remark         = header.remark,
            note           = header.note,
            trackingNumber = header.trackingnumber,
            items = lines.Select(l => new
            {
                id          = l.orderdetailid,
                productId   = l.productid,
                productName = l.productTitle,
                productNum  = l.productnum,
                photo       = l.photo,
                qty         = l.qty,
                unitPrice   = l.price,
                subtotal    = l.subtotal,
                isGift      = l.isgift == 1,
            })
        });
    }

    // PUT /admin/orders/{code}
    public async Task<IActionResult> Update(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "OrderMs", AdminOperation.Update);
        if (guard.Result is not null) return guard.Result;

        var code = ctx.RequirePathParam("code");
        var body = await ctx.TryReadBodyAsync<UpdateOrderRequest>();
        if (body is null) return ctx.BadRequest("請求內容無效");

        using var conn = await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);

        var orderId = await conn.ExecuteScalarAsync<Guid?>(
            "SELECT orderid FROM Orders WHERE ordercode = @code", new { code });
        if (orderId is null) return ctx.NotFound("找不到訂單");

        // Update order header
        await conn.ExecuteAsync(@"
UPDATE Orders SET
    recivername      = @ReceiverName,
    recivermobile    = @ReceiverMobile,
    reciveraddress   = @ReceiverAddress,
    recivertime      = @ReceiverTime,
    paytype          = @PayType,
    paystatus        = @PayStatus,
    paydate          = @PayDate,
    deliverstatus    = @DeliverStatus,
    deliverdate      = @DeliverDate,
    trackingnumber   = @TrackingNumber,
    invoicetype      = @InvoiceType,
    invoicecode      = @InvoiceCode,
    companytitle     = @CompanyTitle,
    companynumber    = @CompanyNumber,
    lovecode         = @LoveCode,
    freight          = @Freight,
    discount         = @Discount,
    total            = @Total,
    note             = @Note,
    remark           = @Remark
WHERE orderid = @OrderId",
            new
            {
                ReceiverName    = body.ReceiverName,
                ReceiverMobile  = body.ReceiverMobile,
                ReceiverAddress = body.ReceiverAddress,
                ReceiverTime    = body.ReceiverTime,
                PayType         = body.PayType,
                PayStatus       = body.PayStatus,
                PayDate         = body.PayDate,
                DeliverStatus   = body.DeliverStatus,
                DeliverDate     = body.DeliverDate,
                TrackingNumber  = body.TrackingNumber,
                InvoiceType     = body.InvoiceType,
                InvoiceCode     = body.InvoiceCode,
                CompanyTitle    = body.CompanyTitle,
                CompanyNumber   = body.CompanyNumber,
                LoveCode        = body.LoveCode,
                Freight         = body.Freight,
                Discount        = body.Discount,
                Total           = body.Total,
                Note            = body.Note,
                Remark          = body.Remark,
                OrderId         = orderId.Value,
            });

        // Items diff: load current items
        var existing = (await conn.QueryAsync<(Guid Id, Guid ProductId)>(
            "SELECT orderdetailid AS Id, productid AS ProductId FROM Orderdetails WHERE orderid = @orderId",
            new { orderId = orderId.Value })).ToList();

        var requestedIds = body.Items
            .Where(i => i.OrderDetailId.HasValue)
            .Select(i => i.OrderDetailId!.Value)
            .ToHashSet();

        // Delete items removed from the list
        var toDelete = existing
            .Where(e => !requestedIds.Contains(e.Id))
            .Select(e => e.Id)
            .ToList();

        foreach (var delId in toDelete)
            await conn.ExecuteAsync(
                "DELETE FROM Orderdetails WHERE orderdetailid = @Id", new { Id = delId });

        // Update or insert each submitted item
        foreach (var item in body.Items)
        {
            if (item.OrderDetailId.HasValue)
            {
                await conn.ExecuteAsync(@"
UPDATE Orderdetails SET qty=@Qty, price=@Price, subtotal=@Subtotal, isgift=@IsGift
WHERE orderdetailid=@Id",
                    new { Qty = item.Qty, Price = item.Price, Subtotal = item.Subtotal,
                          IsGift = item.IsGift ? 1 : 0, Id = item.OrderDetailId.Value });
            }
            else
            {
                await conn.ExecuteAsync(@"
INSERT INTO Orderdetails (orderdetailid, orderid, productid, qty, price, subtotal, isgift)
VALUES (@Id, @OrderId, @ProductId, @Qty, @Price, @Subtotal, @IsGift)",
                    new { Id = Guid.NewGuid(), OrderId = orderId.Value,
                          ProductId = item.ProductId, Qty = item.Qty,
                          Price = item.Price, Subtotal = item.Subtotal,
                          IsGift = item.IsGift ? 1 : 0 });
            }
        }

        return ctx.Ok(new { message = "訂單已更新" });
    }

    // PATCH /admin/orders/{code}/pending — 未出貨(0) → 待出貨(4)
    public async Task<IActionResult> ToPending(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "OrderMs", AdminOperation.Update);
        if (guard.Result is not null) return guard.Result;

        return await ChangeDeliverStatus(ctx, DeliverStatus.NotShipped, DeliverStatus.PendingShipment);
    }

    // PATCH /admin/orders/{code}/ship — 待出貨(4) → 已出貨(1)
    public async Task<IActionResult> Ship(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "OrderMs", AdminOperation.Update);
        if (guard.Result is not null) return guard.Result;

        var body = await ctx.TryReadBodyAsync<ShipRequest>();
        var code = ctx.RequirePathParam("code");
        using var conn = await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);

        var order = await conn.QuerySingleOrDefaultAsync<StatusRow>(
            "SELECT orderid, deliverstatus FROM Orders WHERE ordercode = @code", new { code });
        if (order is null) return ctx.NotFound("找不到訂單");
        if (order.deliverstatus != (int)DeliverStatus.PendingShipment)
            return ctx.UnprocessableEntity("訂單目前不是「待出貨」狀態。");

        var deliverDate = body?.DeliverDate ?? DateTime.UtcNow.AddHours(8).Date;
        await conn.ExecuteAsync(
            @"UPDATE Orders SET deliverstatus=@status, deliverdate=@deliverDate, trackingnumber=@tracking
              WHERE orderid=@id",
            new
            {
                status = (int)DeliverStatus.Shipped,
                deliverDate,
                tracking = body?.TrackingNumber,
                id = order.orderid,
            });

        return ctx.Ok(new { message = "已標記為已出貨" });
    }

    // PATCH /admin/orders/{code}/cancel
    public async Task<IActionResult> Cancel(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "OrderMs", AdminOperation.Update);
        if (guard.Result is not null) return guard.Result;

        var code = ctx.RequirePathParam("code");
        using var conn = await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);

        var order = await conn.QuerySingleOrDefaultAsync<StatusRow>(
            "SELECT orderid, deliverstatus, paystatus FROM Orders WHERE ordercode = @code", new { code });
        if (order is null) return ctx.NotFound("找不到訂單");
        if (order.deliverstatus == (int)DeliverStatus.Shipped)
            return ctx.UnprocessableEntity("已出貨訂單無法直接取消，請走退貨流程。");

        await conn.ExecuteAsync(
            "UPDATE Orders SET deliverstatus=@ds, paystatus=@ps WHERE orderid=@id",
            new { ds = (int)DeliverStatus.Cancelled, ps = (int)PayStatus.Cancelled, id = order.orderid });

        return ctx.Ok(new { message = "訂單已取消" });
    }

    // PATCH /admin/orders/{code}/pay — 人工標記已付款
    public async Task<IActionResult> MarkPaid(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "OrderMs", AdminOperation.Update);
        if (guard.Result is not null) return guard.Result;

        var code = ctx.RequirePathParam("code");
        var body = await ctx.TryReadBodyAsync<PayRequest>();
        var payDate = body?.PayDate ?? DateTime.UtcNow.AddHours(8).Date;

        using var conn = await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);

        var rows = await conn.ExecuteAsync(
            "UPDATE Orders SET paystatus=@status, paydate=@payDate WHERE ordercode=@code AND paystatus=@unpaid",
            new { status = (int)PayStatus.Paid, payDate, code, unpaid = (int)PayStatus.Unpaid });

        if (rows == 0) return ctx.UnprocessableEntity("訂單不存在或已不是「未付款」狀態。");
        return ctx.Ok(new { message = "已標記為已付款" });
    }

    // ── Helpers ───────────────────────────────────────────────────────────────────

    private async Task<IActionResult> ChangeDeliverStatus(
        RouteContext ctx, DeliverStatus from, DeliverStatus to)
    {
        var code = ctx.RequirePathParam("code");
        using var conn = await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);

        var rows = await conn.ExecuteAsync(
            "UPDATE Orders SET deliverstatus=@to WHERE ordercode=@code AND deliverstatus=@from",
            new { to = (int)to, code, from = (int)from });

        if (rows == 0) return ctx.UnprocessableEntity($"訂單不存在或目前狀態不是「{from}」。");
        return ctx.Ok(new { message = $"狀態已更新為 {to}" });
    }

    private static (string Sql, object Params) BuildOrderWhere(
        int? deliverStatus, int? payStatus, string keyword,
        DateTime? dateFrom = null, DateTime? dateTo = null)
    {
        var clauses = new List<string>();
        var p = new Dictionary<string, object?>();

        if (deliverStatus.HasValue) { clauses.Add("o.deliverstatus = @ds"); p["ds"] = deliverStatus.Value; }
        if (payStatus.HasValue)     { clauses.Add("o.paystatus = @ps");     p["ps"] = payStatus.Value; }
        if (dateFrom.HasValue)      { clauses.Add("o.orderdate >= @df");    p["df"] = dateFrom.Value; }
        if (dateTo.HasValue)        { clauses.Add("o.orderdate <= @dt");    p["dt"] = dateTo.Value; }
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            clauses.Add("(o.ordercode LIKE @kw OR m.name LIKE @kw OR m.mobile LIKE @kw OR o.recivername LIKE @kw)");
            p["kw"] = $"%{keyword}%";
        }

        var sql = clauses.Count > 0 ? string.Join(" AND ", clauses) : "1=1";
        return (sql, p);
    }

    // ── Row / DTO types ───────────────────────────────────────────────────────────

    private sealed record AdminOrderRow(
        Guid orderid, string ordercode, DateTime orderdate,
        string memberName, string memberMobile,
        int total, int freight, int discount,
        int paytype, int paystatus, int deliverstatus,
        string? codeatm, DateTime? expirepaydate,
        string recivername, string reciveraddress,
        DateTime createdate);

    private sealed record AdminOrderDetailRow(
        Guid orderid, string ordercode, DateTime orderdate,
        Guid memberid, string memberName, string memberMobile,
        int total, int freight, int discount,
        int paytype, int paystatus, int deliverstatus,
        DateTime? paydate, DateTime? deliverdate,
        int invoicetype, int invoicestatus, string? invoicecode,
        string? companytitle, string? companynumber, string? lovecode,
        string? codeatm, DateTime? expirepaydate,
        string recivername, string recivermobile,
        int reciverzipcodeid, string reciveraddress, int recivertime,
        string? remark, string? note, string? trackingnumber,
        DateTime createdate);

    private sealed record AdminOrderLineRow(
        Guid orderdetailid, Guid productid, string productTitle,
        string? productnum, string? photo,
        int qty, int price, int subtotal, int isgift);

    private sealed record StatusRow(Guid orderid, int deliverstatus, int paystatus = 0);

    private sealed record AdminOrderListItem(
        Guid OrderId, string Code, DateTime OrderDate,
        string MemberName, string MemberMobile,
        int Total, int Freight, int Discount,
        PayType PayType, PayStatus PayStatus, DeliverStatus DeliverStatus,
        string? AtmCode, DateTime? AtmExpiry,
        string ReceiverName, string ReceiverAddress,
        DateTime CreatedAt);

    private sealed record ShipRequest(DateTime? DeliverDate, string? TrackingNumber);
    private sealed record PayRequest(DateTime? PayDate);

    private sealed record UpdateOrderRequest(
        string ReceiverName,
        string ReceiverMobile,
        string ReceiverAddress,
        int ReceiverTime,
        int PayType,
        int PayStatus,
        DateTime? PayDate,
        int DeliverStatus,
        DateTime? DeliverDate,
        string? TrackingNumber,
        int InvoiceType,
        string? InvoiceCode,
        string? CompanyTitle,
        string? CompanyNumber,
        string? LoveCode,
        int Freight,
        int Discount,
        int Total,
        string? Note,
        string? Remark,
        List<UpdateOrderItem> Items);

    private sealed record UpdateOrderItem(
        Guid? OrderDetailId,
        Guid ProductId,
        int Qty,
        int Price,
        int Subtotal,
        bool IsGift);
}
