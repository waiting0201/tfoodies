using System.Data;
using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using TFoodies.Api.Functions.Helpers;
using TFoodies.Api.Functions.Router;
using TFoodies.Application.Abstractions;
using TFoodies.Contracts.Common;
using TFoodies.Domain.Enums;
using TFoodies.Infrastructure.Payments.Fisc;

namespace TFoodies.Api.Functions.Controllers.Admin;

/// <summary>
/// 後台訂單管理。涵蓋舊系統 OrderMsController 的 Orders / Shipments / Shipped / Canceled
/// 各佇列（以 deliverStatus 篩選）、手動建單、出貨扣庫存、以及各式 Excel 匯出。
///   GET   /admin/orders                  — 訂單列表（篩選/分頁；佇列以 deliverStatus 區分）
///   POST  /admin/orders                  — 手動建單（線下單）
///   GET   /admin/orders/export           — 訂單匯出（category=tfoodies|shopcom）
///   GET   /admin/orders/picking          — 揀貨單（orderIds=逗號分隔；FIFO 解析批號）
///   GET   /admin/orders/{code}           — 訂單明細
///   GET   /admin/orders/{code}/deliver   — 出貨單（deliver.xlsx 範本）
///   PATCH /admin/orders/{code}/pending   — 未出貨 → 待出貨
///   PATCH /admin/orders/{code}/ship      — 待出貨 → 已出貨（扣庫存）
///   PATCH /admin/orders/{code}/cancel    — 取消訂單
///   PATCH /admin/orders/{code}/pay       — 標記已付款（後台人工確認，走完整流程：建 Income + 開票 + 寄信）
///   POST  /admin/orders/{code}/charge    — 對未付款的信用卡訂單發起財金線上刷卡
///   POST  /admin/orders/{code}/invoice   — 補開電子發票
///   PUT   /admin/orders/{code}           — 編輯訂單（含明細差異更新）
/// </summary>
public sealed class OrderAdminController
{
    private readonly IAdminPermissionService _perms;
    private readonly IDbConnectionFactory _db;
    private readonly ICodeNumberService _codes;
    private readonly IStockAllocator _stocks;
    private readonly IPaymentCompletionService _completion;
    private readonly IOrderService _orders;
    private readonly FiscOptions _fisc;

    private const string LoveCode = "01170";   // 舊系統發票捐贈碼

    public OrderAdminController(
        IAdminPermissionService perms, IDbConnectionFactory db,
        ICodeNumberService codes, IStockAllocator stocks,
        IPaymentCompletionService completion, IOrderService orders, IOptions<FiscOptions> fisc)
    {
        _perms = perms;
        _db = db;
        _codes = codes;
        _stocks = stocks;
        _completion = completion;
        _orders = orders;
        _fisc = fisc.Value;
    }

    private static string DeliverTemplatePath =>
        Path.Combine(AppContext.BaseDirectory, "Templates", "deliver.xlsx");

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

    // POST /admin/orders — 手動建單（線下單）。對應舊系統 OrderMsController.AddOrders。
    // 不在此配貨（如舊系統 AddOrders 亦不扣庫存）；庫存於出貨（Ship）時才扣。
    public async Task<IActionResult> Create(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "OrderMs", AdminOperation.Add);
        if (guard.Result is not null) return guard.Result;

        var body = await ctx.TryReadBodyAsync<CreateOrderRequest>();
        if (body is null) return ctx.BadRequest("請求內容無效");
        if (body.MemberId == Guid.Empty) return ctx.BadRequest("請選擇會員。");
        if (string.IsNullOrWhiteSpace(body.ReceiverName)) return ctx.BadRequest("缺少收件人姓名。");
        if (string.IsNullOrWhiteSpace(body.ReceiverMobile)) return ctx.BadRequest("缺少收件人手機。");
        if (body.Items is null || body.Items.Count == 0) return ctx.BadRequest("請至少新增一項商品。");

        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(8));
        var now = DateTime.UtcNow.AddHours(8);
        var ct = ctx.Request.HttpContext.RequestAborted;

        using var conn = (SqlConnection)await _db.CreateOpenConnectionAsync(ct);
        using var tx = conn.BeginTransaction(IsolationLevel.ReadCommitted);
        try
        {
            // 收件地區（reciverzipcodeid 為必填 FK）；未指定則回退用會員登記的 zipcodeid。
            var zipcodeId = body.ReceiverZipcodeId > 0
                ? body.ReceiverZipcodeId
                : await conn.ExecuteScalarAsync<int?>(
                    "SELECT zipcodeid FROM Members WHERE memberid=@id", new { id = body.MemberId }, tx) ?? 0;
            if (zipcodeId <= 0)
            { tx.Rollback(); return ctx.BadRequest("缺少收件地區（郵遞區號），且會員未設定地區。"); }

            // 出貨倉的 warehousetype（推導 warehousetypeid；無指定倉則預設線上倉）
            int warehouseTypeId = (int)WarehouseType.Online;
            if (body.WarehouseId.HasValue)
            {
                warehouseTypeId = await conn.ExecuteScalarAsync<int?>(
                    "SELECT warehousetype FROM Warehouses WHERE warehouseid=@id",
                    new { id = body.WarehouseId.Value }, tx) ?? (int)WarehouseType.Online;
            }

            var orderId = Guid.NewGuid();
            var orderCode = await _codes.NextAsync(CodeKind.Order, today, tx, ct);

            var payStatus = body.PayType == (int)PayType.NoPayment
                ? (int)PayStatus.NoPayment
                : body.PayStatus;

            var loveCode = body.InvoiceType == (int)InvoiceType.Donation
                ? (body.LoveCode ?? LoveCode)
                : null;

            await conn.ExecuteAsync(@"
INSERT INTO Orders (
    orderid, memberid, ordertype, warehousetypeid, warehouseid, logisticid,
    ordercode, orderdate,
    recivername, recivermobile, reciverzipcodeid, reciveraddress, recivertime,
    freight, discount, total, paytype, paystatus, paydate, deliverstatus, deliverdate,
    invoicetype, invoicestatus, invoicecode, companytitle, companynumber, lovecode,
    trackingnumber, note, remark, isdeclaration, createdate
) VALUES (
    @orderid, @memberid, @ordertype, @warehousetypeid, @warehouseid, @logisticid,
    @ordercode, @orderdate,
    @recivername, @recivermobile, @reciverzipcodeid, @reciveraddress, @recivertime,
    @freight, @discount, @total, @paytype, @paystatus, @paydate, @deliverstatus, @deliverdate,
    @invoicetype, 0, @invoicecode, @companytitle, @companynumber, @lovecode,
    @trackingnumber, @note, @remark, 0, @createdate
)",
                new
                {
                    orderid = orderId,
                    memberid = body.MemberId,
                    ordertype = body.OrderType <= 0 ? (int)OrderType.Offline : body.OrderType,
                    warehousetypeid = warehouseTypeId,
                    warehouseid = body.WarehouseId,
                    logisticid = body.LogisticId,
                    ordercode = orderCode,
                    orderdate = body.OrderDate ?? today,  // 管理員可補登日期；未指定則當天
                    recivername = body.ReceiverName,
                    recivermobile = body.ReceiverMobile,
                    reciverzipcodeid = zipcodeId,
                    reciveraddress = body.ReceiverAddress ?? string.Empty,
                    recivertime = body.ReceiverTime,
                    freight = body.Freight,
                    discount = body.Discount > 0 ? (int?)body.Discount : null,
                    total = body.Total,
                    paytype = body.PayType,
                    paystatus = payStatus,
                    paydate = body.PayDate,
                    deliverstatus = body.DeliverStatus,
                    deliverdate = body.DeliverDate,
                    invoicetype = body.InvoiceType,
                    invoicecode = body.InvoiceCode,
                    companytitle = body.CompanyTitle,
                    companynumber = body.CompanyNumber,
                    lovecode = loveCode,
                    trackingnumber = body.TrackingNumber,
                    note = body.Note,
                    remark = body.Remark,
                    createdate = now,
                }, tx);

            foreach (var item in body.Items)
            {
                await conn.ExecuteAsync(@"
INSERT INTO Orderdetails (orderdetailid, orderid, productid, qty, price, discount, subtotal, isgift, status)
VALUES (@orderdetailid, @orderid, @productid, @qty, @price, @discount, @subtotal, @isgift, 0)",
                    new
                    {
                        orderdetailid = Guid.NewGuid(),
                        orderid = orderId,
                        productid = item.ProductId,
                        qty = item.Qty,
                        price = item.Price,
                        // discount 為「折數」（如 8 = 八折），對齊舊系統 OrderMs/AddOrders；金額效果已反映於 subtotal。
                        discount = item.Discount is > 0 and < 10 ? item.Discount : null,
                        subtotal = item.Subtotal > 0 ? item.Subtotal : item.Price * item.Qty,
                        isgift = item.IsGift ? 1 : 0,
                    }, tx);
            }

            tx.Commit();
            return ctx.Created(new { orderCode });
        }
        catch { tx.Rollback(); throw; }
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
       o.warehouseid, o.logisticid,
       m.memberid, m.name AS memberName, m.mobile AS memberMobile,
       o.total, o.freight, ISNULL(o.discount,0) AS discount,
       o.paytype, o.paystatus, o.deliverstatus, o.paydate, o.deliverdate,
       o.invoicetype, o.invoicestatus, o.invoicecode,
       o.companytitle, o.companynumber, o.lovecode,
       o.codeatm, o.expirepaydate,
       o.recivername, o.recivermobile, o.reciverzipcodeid, o.reciveraddress, o.recivertime,
       z.city AS reciverCity, z.area AS reciverArea,
       o.remark, o.note, o.trackingnumber, o.createdate
FROM Orders o JOIN Members m ON m.memberid=o.memberid
LEFT JOIN Zipcodes z ON z.zipcodeid = o.reciverzipcodeid
WHERE o.ordercode = @code;

SELECT od.orderdetailid, od.productid, p.title AS productTitle,
       p.productnum, p.photo,
       od.qty, od.price, od.discount, od.subtotal, od.isgift
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
            warehouseId    = header.warehouseid,
            logisticId     = header.logisticid,
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
            receiverZipcodeId = header.reciverzipcodeid,
            receiverCity   = header.reciverCity,
            receiverArea   = header.reciverArea,
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
                discount    = l.discount,
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

        // 出貨倉變更時重算 warehousetypeid（與 Create 一致；無指定倉則維持線上倉）
        int warehouseTypeId = (int)WarehouseType.Online;
        if (body.WarehouseId.HasValue)
        {
            warehouseTypeId = await conn.ExecuteScalarAsync<int?>(
                "SELECT warehousetype FROM Warehouses WHERE warehouseid=@id",
                new { id = body.WarehouseId.Value }) ?? (int)WarehouseType.Online;
        }

        // Update order header
        await conn.ExecuteAsync(@"
UPDATE Orders SET
    warehouseid      = @WarehouseId,
    warehousetypeid  = @WarehouseTypeId,
    logisticid       = @LogisticId,
    recivername      = @ReceiverName,
    recivermobile    = @ReceiverMobile,
    reciverzipcodeid = @ReceiverZipcodeId,
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
                WarehouseId     = body.WarehouseId,
                WarehouseTypeId = warehouseTypeId,
                LogisticId      = body.LogisticId,
                ReceiverName     = body.ReceiverName,
                ReceiverMobile   = body.ReceiverMobile,
                ReceiverZipcodeId= body.ReceiverZipcodeId,
                ReceiverAddress  = body.ReceiverAddress,
                ReceiverTime     = body.ReceiverTime,
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
UPDATE Orderdetails SET qty=@Qty, price=@Price, discount=@Discount, subtotal=@Subtotal, isgift=@IsGift
WHERE orderdetailid=@Id",
                    new { Qty = item.Qty, Price = item.Price,
                          Discount = item.Discount is > 0 and < 10 ? item.Discount : null,
                          Subtotal = item.Subtotal,
                          IsGift = item.IsGift ? 1 : 0, Id = item.OrderDetailId.Value });
            }
            else
            {
                await conn.ExecuteAsync(@"
INSERT INTO Orderdetails (orderdetailid, orderid, productid, qty, price, discount, subtotal, isgift)
VALUES (@Id, @OrderId, @ProductId, @Qty, @Price, @Discount, @Subtotal, @IsGift)",
                    new { Id = Guid.NewGuid(), OrderId = orderId.Value,
                          ProductId = item.ProductId, Qty = item.Qty,
                          Price = item.Price,
                          Discount = item.Discount is > 0 and < 10 ? item.Discount : null,
                          Subtotal = item.Subtotal,
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

    // PATCH /admin/orders/{code}/ship — 待出貨(4) → 已出貨(1)，並扣庫存。
    // 移植舊系統 Ajax.ChangeToShipped 的 CheckInventory/SetInventory：FIFO 依效期扣庫存。
    // 線上單在下單時已配貨（已有 Orderdetailstock），出貨時不重複扣；手動建單則於此首次配貨。
    public async Task<IActionResult> Ship(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "OrderMs", AdminOperation.Update);
        if (guard.Result is not null) return guard.Result;

        var body = await ctx.TryReadBodyAsync<ShipRequest>();
        var code = ctx.RequirePathParam("code");
        var ct = ctx.Request.HttpContext.RequestAborted;

        using var conn = (SqlConnection)await _db.CreateOpenConnectionAsync(ct);
        using var tx = conn.BeginTransaction(IsolationLevel.ReadCommitted);
        try
        {
            var order = await conn.QuerySingleOrDefaultAsync<ShipOrderRow>(
                "SELECT orderid, deliverstatus, warehouseid FROM Orders WHERE ordercode = @code",
                new { code }, tx);
            if (order is null) { tx.Rollback(); return ctx.NotFound("找不到訂單"); }
            if (order.deliverstatus != (int)DeliverStatus.PendingShipment)
            { tx.Rollback(); return ctx.UnprocessableEntity("訂單目前不是「待出貨」狀態。"); }

            // 對「尚未配貨」的明細執行 FIFO 配貨（避免線上單重複扣庫存）
            var details = (await conn.QueryAsync<ShipDetailRow>(@"
SELECT od.orderdetailid, od.productid, od.qty,
       (SELECT COUNT(1) FROM Orderdetailstocks ods WHERE ods.orderdetailid = od.orderdetailid) AS allocated
FROM Orderdetails od WHERE od.orderid = @orderId",
                new { orderId = order.orderid }, tx)).ToList();

            var unallocated = details.Where(d => d.allocated == 0 && d.qty > 0).ToList();
            if (unallocated.Count > 0)
            {
                if (order.warehouseid is null)
                { tx.Rollback(); return ctx.UnprocessableEntity("此訂單未指定出貨倉，無法扣庫存。"); }

                var now = DateTime.UtcNow.AddHours(8);
                foreach (var d in unallocated)
                {
                    var alloc = await _stocks.AllocateAsync(order.warehouseid.Value, d.productid, d.qty, tx, ct);
                    if (!alloc.IsSufficient)
                    { tx.Rollback(); return ctx.UnprocessableEntity($"商品庫存不足，無法出貨（商品 {d.productid}）。"); }

                    foreach (var pick in alloc.Picks)
                        await conn.ExecuteAsync(@"
INSERT INTO Orderdetailstocks (orderdetailstockid, orderdetailid, warehousestockid, qty, createdate)
VALUES (NEWID(), @orderdetailid, @warehousestockid, @qty, @createdate)",
                            new
                            {
                                orderdetailid = d.orderdetailid,
                                warehousestockid = pick.WarehouseStockId,
                                qty = pick.Quantity,
                                createdate = now,
                            }, tx);
                }
            }

            var deliverDate = body?.DeliverDate is { } dd
                ? DateOnly.FromDateTime(dd)
                : DateOnly.FromDateTime(DateTime.UtcNow.AddHours(8));

            await conn.ExecuteAsync(
                @"UPDATE Orders SET deliverstatus=@status, deliverdate=@deliverDate,
                  trackingnumber = COALESCE(@tracking, trackingnumber)
                  WHERE orderid=@id",
                new
                {
                    status = (int)DeliverStatus.Shipped,
                    deliverDate,
                    tracking = body?.TrackingNumber,
                    id = order.orderid,
                }, tx);

            tx.Commit();
            return ctx.Ok(new { message = "已標記為已出貨" });
        }
        catch { tx.Rollback(); throw; }
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

    // PATCH /admin/orders/{code}/pay — 人工標記已付款（走完整流程：建 Income + 開電子發票 + 寄信）
    public async Task<IActionResult> MarkPaid(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "OrderMs", AdminOperation.Update);
        if (guard.Result is not null) return guard.Result;

        var code = ctx.RequirePathParam("code");
        var body = await ctx.TryReadBodyAsync<PayRequest>();
        var payDate = body?.PayDate is { } pd ? DateOnly.FromDateTime(pd) : (DateOnly?)null;

        var paid = await _completion.MarkPaidAsync(
            code, lastPan4: null, txnRef: "後台標記已付款", payDate: payDate,
            ct: ctx.Request.HttpContext.RequestAborted);

        if (!paid) return ctx.UnprocessableEntity("訂單不存在或已不是「未付款」狀態。");
        return ctx.Ok(new { message = "已標記為已付款" });
    }

    // POST /admin/orders/{code}/charge — 對未付款的信用卡訂單發起財金 WEBPOS 線上刷卡。
    // 回傳 form action 與欄位，前端 auto-submit 把管理員整頁導向財金刷卡頁；結果由財金導回 /store/payment/return-admin。
    public async Task<IActionResult> Charge(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "OrderMs", AdminOperation.Update);
        if (guard.Result is not null) return guard.Result;

        var code = ctx.RequirePathParam("code");
        var ct = ctx.Request.HttpContext.RequestAborted;

        var summary = await _orders.GetOrderAsync(code, ct);
        if (summary is null) return ctx.NotFound("找不到該訂單");
        if (summary.PayType != PayType.CreditCard)
            return ctx.BadRequest("此訂單非信用卡付款，無法發起刷卡。");
        if (summary.PayStatus != PayStatus.Unpaid)
            return ctx.Conflict("訂單已付款或目前狀態不可發起刷卡。");

        // 後台專屬 AuthResURL（導回 /store/payment/return-admin → 後台訂單詳情頁）。
        var fields = FiscWebpos.BuildFields(summary, _fisc, _fisc.AdminAuthResUrl);
        return ctx.Ok(new { actionUrl = _fisc.ActionUrl, fields });
    }

    // POST /admin/orders/{code}/invoice — 補開電子發票（開票失敗或當下未開的訂單）。
    public async Task<IActionResult> IssueInvoice(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "InvoiceMs", AdminOperation.Update);
        if (guard.Result is not null) return guard.Result;

        var code = ctx.RequirePathParam("code");
        var result = await _completion.IssueInvoiceAsync(code, incomeId: null,
            ct: ctx.Request.HttpContext.RequestAborted);

        if (result.IsFailure) return ctx.UnprocessableEntity($"開立發票失敗：{result.Error.Message}");
        return ctx.Ok(new { message = "已開立電子發票" });
    }

    // ══════════════════════════════════════════════════════════════════
    // EXPORTS（Excel）
    // ══════════════════════════════════════════════════════════════════

    // GET /admin/orders/export?category=tfoodies|shopcom&deliverStatus=&payStatus=&keyword=&dateFrom=&dateTo=
    // 對應舊系統 OrdersExport（未出貨）與 ShippedExport（已出貨 tfoodies/shopcom）。
    public async Task<IActionResult> ExportOrders(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "OrderMs", AdminOperation.Read);
        if (guard.Result is not null) return guard.Result;

        var q = ctx.Request.Query;
        var category = q["category"].ToString().Trim().ToLowerInvariant();
        if (category != "shopcom") category = "tfoodies";
        int? deliverStatus = int.TryParse(q["deliverStatus"], out var ds) && ds >= 0 ? ds : null;
        int? payStatus = int.TryParse(q["payStatus"], out var ps) && ps >= 0 ? ps : null;
        var keyword = q["keyword"].ToString().Trim();
        DateTime? dateFrom = DateTime.TryParse(q["dateFrom"], out var df) ? df.Date : null;
        DateTime? dateTo = DateTime.TryParse(q["dateTo"], out var dt) ? dt.Date.AddDays(1).AddSeconds(-1) : null;

        var where = BuildOrderWhere(deliverStatus, payStatus, keyword, dateFrom, dateTo);
        var ct = ctx.Request.HttpContext.RequestAborted;
        using var conn = await _db.CreateOpenConnectionAsync(ct);

        var headerSql = $@"
SELECT o.orderid, o.ordercode, o.orderdate, o.ordertype,
       o.recivername, o.recivermobile, o.paytype, o.invoicetype, o.invoicecode,
       o.total, o.note, o.RID, o.Click_ID,
       l.title AS logisticTitle, w.title AS warehouseTitle, m.name AS memberName,
       o.deliverstatus
FROM Orders o
JOIN Members m ON m.memberid = o.memberid
LEFT JOIN Logistics l ON l.logisticid = o.logisticid
LEFT JOIN Warehouses w ON w.warehouseid = o.warehouseid
WHERE {where.Sql}
ORDER BY o.orderdate DESC, o.ordercode DESC";
        var headers = (await conn.QueryAsync<ExportHeaderRow>(headerSql, where.Params)).ToList();

        if (category == "shopcom")
            headers = headers.Where(h => h.RID != null && h.Click_ID != null).ToList();

        var orderIds = headers.Select(h => h.orderid).ToList();
        var lines = orderIds.Count == 0
            ? new List<ExportLineRow>()
            : (await conn.QueryAsync<ExportLineRow>(@"
SELECT od.orderid, od.qty, od.price, od.discount, od.isgift,
       p.productid, p.title, p.productnum, p.isset
FROM Orderdetails od
JOIN Products p ON p.productid = od.productid
WHERE od.orderid IN @ids", new { ids = orderIds })).ToList();

        // 套裝組件（給 tfoodies 版面顯示）
        var setProductIds = lines.Where(l => l.isset).Select(l => l.productid).Distinct().ToList();
        var setComps = setProductIds.Count == 0
            ? new List<SetCompRow>()
            : (await conn.QueryAsync<SetCompRow>(@"
SELECT sp.oproductid, cp.title, cp.capacity, sp.qty
FROM Setproducts sp JOIN Products cp ON cp.productid = sp.productid
WHERE sp.oproductid IN @ids", new { ids = setProductIds })).ToList();

        byte[] bytes;
        if (category == "shopcom")
        {
            var models = headers.Select(h => new OrderExcelReport.ShopcomOrderModel(
                h.orderdate.ToDateTime(TimeOnly.MinValue), h.ordercode, h.memberName,
                h.RID, h.Click_ID,
                lines.Where(l => l.orderid == h.orderid).Select(l =>
                    new OrderExcelReport.ShopcomLine(l.productnum, l.title, l.qty, l.price, l.price * l.qty)).ToList()
            )).ToList();
            bytes = OrderExcelReport.BuildShopcomSheet(models);
        }
        else
        {
            var models = headers.Select(h => new OrderExcelReport.OrderExportModel(
                h.orderdate.ToDateTime(TimeOnly.MinValue), h.ordercode, h.ordertype,
                h.recivername, h.recivermobile, h.paytype, h.invoicetype, h.invoicecode, h.logisticTitle,
                h.total, h.warehouseTitle, h.deliverstatus, h.note,
                lines.Where(l => l.orderid == h.orderid).Select(l => new OrderExcelReport.OrderExportLine(
                    l.title, l.isgift == 1,
                    l.isset
                        ? setComps.Where(s => s.oproductid == l.productid)
                                  .Select(s => $"{s.title} {s.capacity} {s.qty}").ToList()
                        : new List<string>(),
                    l.qty, l.price, l.discount.HasValue ? $"{l.discount} 折" : "-")).ToList()
            )).ToList();
            bytes = OrderExcelReport.BuildOrdersSheet(models);
        }

        var fileName = $"{DateTime.UtcNow.AddHours(8):yyyyMMdd}_{category}_export.xlsx";
        return ctx.File(bytes, OrderExcelReport.ContentType, fileName);
    }

    // GET /admin/orders/picking?orderIds=guid,guid — 揀貨單（ShipmentsExport）。
    // 以唯讀方式按 FIFO（效期 ASC）解析各倉批號，依通知編號彙整數量。
    public async Task<IActionResult> ExportPicking(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "OrderMs", AdminOperation.Read);
        if (guard.Result is not null) return guard.Result;

        var ids = ctx.Request.Query["orderIds"].ToString()
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => Guid.TryParse(s, out var g) ? g : (Guid?)null)
            .Where(g => g.HasValue).Select(g => g!.Value).ToList();
        if (ids.Count == 0) return ctx.BadRequest("請至少選擇一筆訂單。");

        var ct = ctx.Request.HttpContext.RequestAborted;
        using var conn = await _db.CreateOpenConnectionAsync(ct);

        // 各訂單明細（含出貨倉）
        var details = (await conn.QueryAsync<PickingDetailRow>(@"
SELECT o.warehouseid, od.productid, od.qty
FROM Orders o
JOIN Orderdetails od ON od.orderid = o.orderid
WHERE o.orderid IN @ids AND o.warehouseid IS NOT NULL
ORDER BY o.ordercode", new { ids })).ToList();

        var now = DateTime.UtcNow.AddHours(8);
        var picks = new List<OrderExcelReport.PickUpModel>();

        foreach (var d in details)
        {
            // 唯讀 FIFO：同倉同品依效期 ASC 取批次（不上鎖、不扣減）
            var batches = (await conn.QueryAsync<PickingBatchRow>(@"
SELECT s.noticenumber, s.barcode, s.expiredate, w.title AS warehouseTitle,
       p.title AS productTitle, ws.quantity_left
FROM Warehousestocks ws
JOIN Stocks s ON s.stockid = ws.stockid
JOIN Purchasedetails pd ON pd.purchasedetailid = s.purchasedetailid
JOIN Products p ON p.productid = pd.productid
JOIN Warehouses w ON w.warehouseid = ws.warehouseid
WHERE ws.warehouseid = @warehouseId AND pd.productid = @productId AND ws.quantity_left > 0
ORDER BY s.expiredate ASC, ws.transdate ASC",
                new { warehouseId = d.warehouseid, productId = d.productid })).ToList();

            var remaining = d.qty;
            foreach (var b in batches)
            {
                if (remaining <= 0) break;
                var take = Math.Min(remaining, b.quantity_left);

                var existing = picks.FirstOrDefault(x => x.Noticenumber == b.noticenumber);
                if (existing is null)
                    picks.Add(new OrderExcelReport.PickUpModel(
                        b.noticenumber, b.barcode, b.productTitle, take,
                        b.expiredate?.ToDateTime(TimeOnly.MinValue), b.warehouseTitle, now));
                else
                    picks[picks.IndexOf(existing)] = existing with { Quantity = existing.Quantity + take };

                remaining -= take;
            }
        }

        var bytes = OrderExcelReport.BuildPickingSheet(picks);
        return ctx.File(bytes, OrderExcelReport.ContentType, $"{now:yyyyMMdd}_shipment.xlsx");
    }

    // GET /admin/orders/{code}/deliver — 出貨單（ExportDeliver / ExportOrders，deliver.xlsx 範本）。
    public async Task<IActionResult> ExportDeliver(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "OrderMs", AdminOperation.Read);
        if (guard.Result is not null) return guard.Result;

        var code = ctx.RequirePathParam("code");
        var ct = ctx.Request.HttpContext.RequestAborted;
        using var conn = await _db.CreateOpenConnectionAsync(ct);

        var header = await conn.QuerySingleOrDefaultAsync<DeliverHeaderRow>(@"
SELECT o.orderid, o.ordercode, o.orderdate, o.deliverdate, o.paydate, o.paytype,
       o.invoicetype, o.invoicecode, o.freight, ISNULL(o.discount,0) AS discount, o.total, o.note,
       o.reciveraddress, m.name AS memberName, m.mobile AS memberMobile, m.email AS memberEmail,
       z.city, z.area
FROM Orders o
JOIN Members m ON m.memberid = o.memberid
LEFT JOIN Zipcodes z ON z.zipcodeid = o.reciverzipcodeid
WHERE o.ordercode = @code", new { code });
        if (header is null) return ctx.NotFound("找不到訂單");

        var lines = (await conn.QueryAsync<DeliverLineRow>(@"
SELECT od.qty, od.price, od.discount, od.subtotal, od.isgift,
       p.title, p.fixprice
FROM Orderdetails od
JOIN Products p ON p.productid = od.productid
WHERE od.orderid = @orderId
ORDER BY od.isgift", new { orderId = header.orderid })).ToList();

        var invoiceText = header.invoicetype == (int)InvoiceType.Donation ? "捐贈" : (header.invoicecode ?? "");

        var model = new OrderExcelReport.DeliverNoteModel(
            header.deliverdate?.ToDateTime(TimeOnly.MinValue), header.ordercode, header.memberName,
            header.orderdate.ToDateTime(TimeOnly.MinValue), header.memberMobile,
            header.paydate?.ToDateTime(TimeOnly.MinValue),
            (header.city ?? "") + (header.area ?? "") + header.reciveraddress,
            header.paytype, invoiceText, header.memberEmail,
            lines.Select(l => new OrderExcelReport.DeliverLine(
                l.title, l.qty, l.fixprice ?? 0, l.discount.HasValue ? $"{l.discount}折" : "", l.price, l.subtotal)).ToList(),
            header.total, header.freight, header.discount, header.note);

        var bytes = OrderExcelReport.BuildDeliverNote(DeliverTemplatePath, model);
        return ctx.File(bytes, OrderExcelReport.ContentType, $"{header.ordercode}_deliver.xlsx");
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
        Guid? warehouseid, Guid? logisticid,
        Guid memberid, string memberName, string memberMobile,
        int total, int freight, int discount,
        int paytype, int paystatus, int deliverstatus,
        DateTime? paydate, DateTime? deliverdate,
        int invoicetype, int invoicestatus, string? invoicecode,
        string? companytitle, string? companynumber, string? lovecode,
        string? codeatm, DateTime? expirepaydate,
        string recivername, string recivermobile,
        int reciverzipcodeid, string reciveraddress, int recivertime,
        string? reciverCity, string? reciverArea,
        string? remark, string? note, string? trackingnumber,
        DateTime createdate);

    private sealed record AdminOrderLineRow(
        Guid orderdetailid, Guid productid, string productTitle,
        string? productnum, string? photo,
        int qty, int price, int? discount, int subtotal, int isgift);

    private sealed record StatusRow(Guid orderid, int deliverstatus, int paystatus = 0);

    private sealed record ShipOrderRow(Guid orderid, int deliverstatus, Guid? warehouseid);
    private sealed record ShipDetailRow(Guid orderdetailid, Guid productid, int qty, int allocated);

    private sealed record ExportHeaderRow(
        Guid orderid, string ordercode, DateOnly orderdate, int ordertype,
        string recivername, string recivermobile, int paytype, int invoicetype, string? invoicecode,
        int total, string? note, string? RID, string? Click_ID,
        string? logisticTitle, string? warehouseTitle, string memberName, int deliverstatus);
    private sealed record ExportLineRow(
        Guid orderid, int qty, int price, int? discount, int isgift,
        Guid productid, string title, string? productnum, bool isset);
    private sealed record SetCompRow(Guid oproductid, string title, string? capacity, int qty);

    private sealed record PickingDetailRow(Guid warehouseid, Guid productid, int qty);
    private sealed record PickingBatchRow(
        string? noticenumber, string? barcode, DateOnly? expiredate,
        string warehouseTitle, string productTitle, int quantity_left);

    private sealed record DeliverHeaderRow(
        Guid orderid, string ordercode, DateOnly orderdate, DateOnly? deliverdate, DateOnly? paydate,
        int paytype, int invoicetype, string? invoicecode, int freight, int discount, int total, string? note,
        string reciveraddress, string memberName, string memberMobile, string? memberEmail,
        string? city, string? area);
    private sealed record DeliverLineRow(
        int qty, int price, int? discount, int subtotal, int isgift, string title, int? fixprice);

    private sealed record CreateOrderRequest(
        Guid MemberId,
        int OrderType,
        DateOnly? OrderDate,
        Guid? WarehouseId,
        Guid? LogisticId,
        string ReceiverName,
        string ReceiverMobile,
        int ReceiverZipcodeId,
        string? ReceiverAddress,
        int ReceiverTime,
        int PayType,
        int PayStatus,
        DateOnly? PayDate,
        int DeliverStatus,
        DateOnly? DeliverDate,
        int InvoiceType,
        string? InvoiceCode,
        string? CompanyTitle,
        string? CompanyNumber,
        string? LoveCode,
        int Freight,
        int Discount,
        int Total,
        string? TrackingNumber,
        string? Note,
        string? Remark,
        List<CreateOrderItem> Items);

    private sealed record CreateOrderItem(
        Guid ProductId, int Qty, int Price, int Subtotal, bool IsGift, int? Discount);

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
        Guid? WarehouseId,
        Guid? LogisticId,
        string ReceiverName,
        string ReceiverMobile,
        int ReceiverZipcodeId,
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
        int? Discount,
        int Subtotal,
        bool IsGift);
}
