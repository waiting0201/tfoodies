using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using TFoodies.Application.Abstractions;
using TFoodies.Domain.Common;
using TFoodies.Domain.Enums;

namespace TFoodies.Infrastructure.Orders;

/// <summary>
/// 下單業務邏輯。
///
/// 交易邊界：整個 PlaceOrderAsync 在單一 SqlTransaction 中執行，包含：
///   1. 匿名建會員（若需要）
///   2. ICodeNumberService.NextAsync（Ordercodes UPDLOCK）
///   3. ATM 碼產生（Atmcodes UPDLOCK，若 PayType = AtmTransfer）
///   4. IStockAllocator.AllocateAsync（Warehousestocks UPDLOCK ROWLOCK）
///   5. INSERT Order / Orderdetails / Orderdetailstocks
///
/// 讀取（GetOrderAsync / GetMemberOrdersAsync）使用 Dapper 查詢，無交易。
/// </summary>
public sealed class OrderService : IOrderService
{
    private const string OnlineWarehouseQuery =
        "SELECT TOP 1 warehouseid FROM Warehouses WHERE warehousetype = @type";

    private readonly IDbConnectionFactory _db;
    private readonly ICodeNumberService _codes;
    private readonly IStockAllocator _stocks;
    private readonly IDiscountService _discounts;
    private readonly OrderSettings _settings;

    public OrderService(
        IDbConnectionFactory db,
        ICodeNumberService codes,
        IStockAllocator stocks,
        IDiscountService discounts,
        IOptions<OrderSettings> settings)
    {
        _db = db;
        _codes = codes;
        _stocks = stocks;
        _discounts = discounts;
        _settings = settings.Value;
    }

    // ─── PlaceOrderAsync ───────────────────────────────────────────────────────────

    public async Task<Result<PlaceOrderResult>> PlaceOrderAsync(
        PlaceOrderRequest req, Guid? memberId, CancellationToken ct = default)
    {
        if (req.Lines.Count == 0)
            return new Error("order.empty_cart", "購物車為空");

        using var conn = (SqlConnection)await _db.CreateOpenConnectionAsync(ct);
        using var tx = conn.BeginTransaction(IsolationLevel.ReadCommitted);

        try
        {
            // 1. 找/建會員
            var resolvedMemberId = memberId ?? await ResolveGuestMemberAsync(req, conn, tx, ct);
            if (resolvedMemberId == Guid.Empty)
                return new Error("order.member_error", "會員資料錯誤");

            // 2. 取得線上倉倉庫 ID
            var warehouseId = await conn.QuerySingleOrDefaultAsync<Guid?>(
                OnlineWarehouseQuery, new { type = (int)WarehouseType.Online }, tx);
            if (warehouseId is null)
                return new Error("order.no_warehouse", "找不到線上倉");

            // 3. 驗證商品並計算小計
            var unavailable = new List<string>();
            var lines = await ResolveOrderLinesAsync(req.Lines, conn, tx, unavailable);
            if (lines is null)
                return new Error("order.invalid_product",
                    unavailable.Count > 0
                        ? $"「{string.Join("」、「", unavailable)}」已下架或無法購買，請從購物車移除後再送出。"
                        : "商品不存在或已下架");

            var subtotal = lines.Sum(l => l.Subtotal);

            // 4. 運費
            var freight = subtotal >= _settings.FreightLimit ? 0 : _settings.FreightAmount;

            // 5. 折扣
            int discountAmount = 0;
            Guid? discountId = null;
            if (!string.IsNullOrWhiteSpace(req.DiscountCode))
            {
                // enforcePerMemberLimit: false —— 前台「套用」預覽時拿不到 memberId（store/* 為公開
                // 路由），到這裡才以手機號解析出會員。若在此擋下 isonetime=2，顧客會看到「套用成功
                // 卻送不出訂單」且無從補救，因此已通過預覽的碼一律放行。（每會員限用一次因而對前台
                // 線上訂單不具強制力；需要嚴格控管的活動請改用 isonetime=1 全域一次性。）
                var discResult = await _discounts.ValidateAsync(
                    req.DiscountCode, subtotal, resolvedMemberId,
                    enforcePerMemberLimit: false, ct: ct);
                if (!discResult.IsSuccess)
                    return discResult.Error;
                discountAmount = discResult.Value!.DiscountAmount;
                discountId = discResult.Value.DiscountId;
            }

            // 應付總額（= 商品小計 + 運費 − 折扣）：供 ATM 虛擬帳號金額與回傳用。
            // ⚠️ DB 的 Orders.total 欄位語意 = 純商品小計（對齊舊系統），運費/折扣不併入，
            // 由各消費端（發票/FISC/入帳/會計）自行 total + freight − discount 還原應付。
            var payable = subtotal + freight - discountAmount;

            // 6. 訂單號碼
            var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(8));
            var orderCode = await _codes.NextAsync(CodeKind.Order, today, tx, ct);

            // 7. ATM 虛擬帳號（如需要）
            string? atmCode = null;
            DateOnly? atmExpiry = null;
            if (req.PayType == PayType.AtmTransfer)
            {
                var atmExpDate = today.AddDays(_settings.AtmExpiryDays);
                var atmSeq = await NextAtmSeqAsync(conn, tx, atmExpDate);
                atmCode = BuildAtmCode(_settings.AtmPrefix, atmExpDate, atmSeq, payable);
                atmExpiry = atmExpDate;
            }

            // 8. 庫存揀貨（FIFO）
            var orderId = Guid.NewGuid();
            var detailInserts = new List<(Guid detailId, IReadOnlyList<StockPick> picks)>();
            foreach (var line in lines)
            {
                var alloc = await _stocks.AllocateAsync(warehouseId.Value, line.ProductId, line.Qty, tx, ct);
                if (!alloc.IsSufficient)
                    // 訊息要能讓顧客自行處理：講商品名稱（不是 GUID）並說明還剩幾件。
                    return new Error("order.insufficient_stock", alloc.Available > 0
                        ? $"「{line.Title}」庫存不足，目前僅剩 {alloc.Available} 件，請調整數量後再送出。"
                        : $"「{line.Title}」已售完，請從購物車移除後再送出。");
                detailInserts.Add((line.DetailId, alloc.Picks));
            }

            // 9. 決定付款狀態
            // 應付為 0（例如 100% 折扣碼 + 滿額免運）時一律視為「無須付款」：金流不收 0 元，
            // 讓它留在「未付款」只會讓顧客被導去刷 0 元、卡在金流頁，訂單也永遠不會被標記付款。
            var payStatus = payable <= 0
                ? (int)PayStatus.NoPayment
                : req.PayType switch
                {
                    PayType.NoPayment => (int)PayStatus.NoPayment,
                    _ => (int)PayStatus.Unpaid,
                };

            // 10. INSERT Order
            await conn.ExecuteAsync(@"
INSERT INTO Orders (
    orderid, memberid, ordertype, warehousetypeid, warehouseid,
    ordercode, orderdate,
    recivername, recivermobile, reciverzipcodeid, reciveraddress, recivertime,
    freight, discount, total, paytype, paystatus, deliverstatus,
    invoicetype, companytitle, companynumber, lovecode,
    codeatm, expirepaydate, discountid, remark, isdeclaration, createdate
) VALUES (
    @orderid, @memberid, @ordertype, @warehousetypeid, @warehouseid,
    @ordercode, @orderdate,
    @recivername, @recivermobile, @reciverzipcodeid, @reciveraddress, @recivertime,
    @freight, @discount, @total, @paytype, @paystatus, @deliverstatus,
    @invoicetype, @companytitle, @companynumber, @lovecode,
    @codeatm, @expirepaydate, @discountid, @remark, 0, @createdate
)",
                new
                {
                    orderid = orderId,
                    memberid = resolvedMemberId,
                    ordertype = (int)OrderType.Online,
                    warehousetypeid = (int)WarehouseType.Online,
                    warehouseid = warehouseId.Value,
                    ordercode = orderCode,
                    orderdate = today,
                    recivername = req.ReceiverName,
                    recivermobile = req.ReceiverMobile,
                    reciverzipcodeid = req.ReceiverZipcodeId,
                    reciveraddress = req.ReceiverAddress,
                    recivertime = req.ReceiverTime,
                    freight,
                    discount = discountAmount > 0 ? (int?)discountAmount : null,
                    total = subtotal, // 商品小計（不含運費/折扣）— 見上方 payable 註解
                    paytype = (int)req.PayType,
                    paystatus = payStatus,
                    deliverstatus = (int)DeliverStatus.NotShipped,
                    invoicetype = (int)req.InvoiceType,
                    companytitle = req.CompanyTitle,
                    companynumber = req.CompanyNumber,
                    lovecode = req.LoveCode,
                    codeatm = atmCode,
                    expirepaydate = atmExpiry,
                    discountid = discountId,
                    remark = req.Remark,
                    createdate = DateTime.UtcNow.AddHours(8),
                }, tx);

            // 11. INSERT Orderdetails + Orderdetailstocks
            foreach (var line in lines)
            {
                await conn.ExecuteAsync(@"
INSERT INTO Orderdetails (orderdetailid, orderid, productid, qty, price, subtotal, isgift, status)
VALUES (@orderdetailid, @orderid, @productid, @qty, @price, @subtotal, 0, 0)",
                    new
                    {
                        orderdetailid = line.DetailId,
                        orderid = orderId,
                        productid = line.ProductId,
                        qty = line.Qty,
                        price = line.Price,
                        subtotal = line.Subtotal,
                    }, tx);

                var (detailId, picks) = detailInserts.First(d => d.detailId == line.DetailId);
                foreach (var pick in picks)
                {
                    await conn.ExecuteAsync(@"
INSERT INTO Orderdetailstocks (orderdetailstockid, orderdetailid, warehousestockid, qty, createdate)
VALUES (NEWID(), @orderdetailid, @warehousestockid, @qty, @createdate)",
                        new
                        {
                            orderdetailid = detailId,
                            warehousestockid = pick.WarehouseStockId,
                            qty = pick.Quantity,
                            createdate = DateTime.UtcNow.AddHours(8),
                        }, tx);
                }
            }

            tx.Commit();

            var payTypeKey = req.PayType switch
            {
                PayType.CreditCard => "credit",
                PayType.LinePay => "linepay",
                PayType.AtmTransfer => "atmcode",
                PayType.CashOnDelivery => "delivery",
                PayType.NoPayment => "nopay",
                _ => "other",
            };

            // Total = 商品小計（消費端如訂單通知信自行 + freight − discount 還原應付）。
            return new PlaceOrderResult(orderCode, payTypeKey, atmCode, atmExpiry, subtotal, freight, discountAmount);
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    // ─── IsOrderOwnedByMemberAsync ─────────────────────────────────────────────────

    public async Task<bool> IsOrderOwnedByMemberAsync(string orderCode, Guid memberId, CancellationToken ct = default)
    {
        using var conn = await _db.CreateOpenConnectionAsync(ct);
        return await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM Orders WHERE ordercode = @orderCode AND memberid = @memberId",
            new { orderCode, memberId }) > 0;
    }

    // ─── GetOrderAsync ─────────────────────────────────────────────────────────────

    public async Task<OrderSummary?> GetOrderAsync(string orderCode, CancellationToken ct = default)
    {
        using var conn = await _db.CreateOpenConnectionAsync(ct);

        using var multi = await conn.QueryMultipleAsync(@"
SELECT o.orderid, o.ordercode, o.orderdate, o.total, o.freight,
       ISNULL(o.discount,0) AS discount,
       o.paytype, o.paystatus, o.paydate, o.deliverstatus, o.deliverdate, o.invoicetype,
       o.codeatm, o.expirepaydate,
       ISNULL(m.name,'')   AS buyername,
       ISNULL(m.mobile,'') AS buyermobile,
       o.recivername, o.recivermobile, o.reciveraddress, o.remark
FROM Orders o
LEFT JOIN Members m ON m.memberid = o.memberid
WHERE o.ordercode = @orderCode;

SELECT od.productid, p.title AS productTitle,
       ISNULL(p.photo,'') AS productPhoto,
       od.qty, od.price, od.subtotal,
       ISNULL(p.capacity,'') AS capacity, od.isgift
FROM Orderdetails od
JOIN Products p ON p.productid = od.productid
JOIN Orders o2 ON o2.orderid = od.orderid
WHERE o2.ordercode = @orderCode;",
            new { orderCode });

        var header = await multi.ReadSingleOrDefaultAsync<OrderHeaderRow>();
        if (header is null) return null;

        var lines = (await multi.ReadAsync<OrderLineRow>())
            .Select(r => new OrderLineItem(r.productid, r.productTitle, r.productPhoto,
                r.qty, r.price, r.subtotal, r.capacity, r.isgift))
            .ToList();

        return new OrderSummary(
            header.orderid, header.ordercode, header.orderdate,
            header.total, header.freight, header.discount,
            (PayType)header.paytype, (PayStatus)header.paystatus,
            header.paydate is { } pd ? DateOnly.FromDateTime(pd) : null,
            (DeliverStatus)header.deliverstatus,
            header.deliverdate is { } dd ? DateOnly.FromDateTime(dd) : null,
            (InvoiceType)header.invoicetype,
            header.codeatm, header.expirepaydate,
            header.buyername, header.buyermobile,
            header.recivername, header.recivermobile, header.reciveraddress,
            header.remark,
            lines);
    }

    // ─── GetMemberOrdersAsync ──────────────────────────────────────────────────────

    public async Task<(IReadOnlyList<OrderListItem> Items, int TotalCount)> GetMemberOrdersAsync(
        Guid memberId, int page, int pageSize, CancellationToken ct = default)
    {
        using var conn = await _db.CreateOpenConnectionAsync(ct);

        var offset = (page - 1) * pageSize;

        using var multi = await conn.QueryMultipleAsync(@"
SELECT COUNT(1) FROM Orders WHERE memberid = @memberId;

SELECT orderid, ordercode, orderdate, total,
       ISNULL(freight,0) AS freight, ISNULL(discount,0) AS discount,
       paystatus, deliverstatus
FROM Orders
WHERE memberid = @memberId
ORDER BY createdate DESC
OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY;",
            new { memberId, offset, pageSize });

        var total = await multi.ReadSingleAsync<int>();
        var items = (await multi.ReadAsync<OrderListRow>())
            .Select(r => new OrderListItem(r.orderid, r.ordercode, r.orderdate,
                r.total, r.freight, r.discount, (PayStatus)r.paystatus, (DeliverStatus)r.deliverstatus))
            .ToList();

        return (items, total);
    }

    // ─── Private helpers ──────────────────────────────────────────────────────────

    private static async Task<Guid> ResolveGuestMemberAsync(
        PlaceOrderRequest req, IDbConnection conn, IDbTransaction tx, CancellationToken ct)
    {
        // 以手機號尋找現有會員。Members.mobile 沒有唯一索引（舊系統遺留），同號碼可能有多筆；
        // 用 QuerySingleOrDefault 會直接丟例外，讓那位客人永遠無法下單。取最早建立的那筆即可。
        var existing = await conn.QueryFirstOrDefaultAsync<Guid?>(
            "SELECT TOP 1 memberid FROM Members WHERE mobile = @mobile ORDER BY createdate ASC",
            new { mobile = req.BuyerMobile }, tx);

        if (existing.HasValue) return existing.Value;

        // 建立新會員（匿名結帳）
        if (string.IsNullOrWhiteSpace(req.BuyerName))
            return Guid.Empty;

        var newId = Guid.NewGuid();
        // 訪客結帳不要求設定密碼，因此新會員一律寫入一組無人知悉的隨機密碼＝「尚未設定密碼」。
        // 顧客日後要登入，走會員中心的「忘記密碼」（以手機 + Email 核對後寄出新密碼）。
        // ⚠️ 舊行為是把手機號當預設密碼存成明文——手機號在訂單、收件資訊裡到處都是，
        //    等於任何拿到號碼的人都能登入該帳號，切勿恢復。
        // req.Password 分支保留給仍會送密碼的舊版前端 / 其他呼叫端（後台代客下單）。
        var password = HashPassword(
            string.IsNullOrWhiteSpace(req.Password)
                ? Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(24))
                : req.Password);

        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(8));

        await conn.ExecuteAsync(@"
INSERT INTO Members (memberid, name, mobile, gender, birthday, password, email,
                     zipcodeid, address, isagent, agentdiscount, createdate, ismember, isenable)
VALUES (@memberid, @name, @mobile, @gender, @birthday, @password, @email,
        @zipcodeid, @address, 0, 1.0, @createdate, 1, 1)",
            new
            {
                memberid = newId,
                name = req.BuyerName,
                mobile = req.BuyerMobile,
                gender = req.Gender,
                birthday = req.Birthday,
                password,
                email = req.BuyerEmail,
                zipcodeid = req.BuyerZipcodeId,
                address = req.BuyerAddress,
                createdate = today,
            }, tx);

        return newId;
    }

    /// <summary>
    /// 驗證購物車品項並帶出價格/名稱。回傳 null 代表有商品下架或不存在，
    /// 此時 <paramref name="unavailable"/> 為那些商品的名稱（查不到名稱者退回 GUID）——
    /// 顧客的購物車存在 localStorage 可以放很久，商品下架後若只回「商品不存在或已下架」，
    /// 他無從得知要移除哪一項，等於永遠送不出訂單。
    /// </summary>
    private static async Task<List<OrderLine>?> ResolveOrderLinesAsync(
        IReadOnlyList<CartLineRequest> cartLines,
        IDbConnection conn, IDbTransaction tx,
        List<string>? unavailable = null)
    {
        var productIds = cartLines.Select(l => l.ProductId).Distinct().ToList();

        var products = (await conn.QueryAsync<ProductPriceRow>(
            @"SELECT productid, price, title FROM Products
              WHERE productid IN @ids AND isdisabled = 0",
            new { ids = productIds }, tx)).ToDictionary(p => p.productid);

        if (products.Count != productIds.Count)
        {
            if (unavailable is not null)
            {
                var missing = productIds.Where(id => !products.ContainsKey(id)).ToList();
                // 下架商品在 Products 裡還在，查得到名稱；真的不存在才退回 GUID。
                var names = (await conn.QueryAsync<ProductNameRow>(
                    "SELECT productid, title FROM Products WHERE productid IN @ids",
                    new { ids = missing }, tx)).ToDictionary(x => x.productid, x => x.title);
                unavailable.AddRange(missing.Select(id => names.TryGetValue(id, out var t) ? t : id.ToString()));
            }
            return null;
        }

        return cartLines.Select(l =>
        {
            var p = products[l.ProductId];
            var subtotal = p.price * l.Qty;
            return new OrderLine(Guid.NewGuid(), l.ProductId, p.title, l.Qty, p.price, subtotal);
        }).ToList();
    }

    // ATM: atomically increment Atmcodes for the expiry date, return raw sequence integer
    private static async Task<int> NextAtmSeqAsync(
        IDbConnection conn, IDbTransaction tx, DateOnly date)
    {
        var year = date.Year.ToString();
        var month = date.Month.ToString("D2");
        var day = date.Day.ToString("D2");

        return await conn.ExecuteScalarAsync<int>(@"
MERGE Atmcodes WITH (HOLDLOCK) AS target
USING (SELECT @year AS y, @month AS m, @day AS d) AS src
    ON target.year=src.y AND target.month=src.m AND target.day=src.d
WHEN MATCHED THEN
    UPDATE SET code = target.code + 1
WHEN NOT MATCHED THEN
    INSERT (atmcodeid, year, month, day, code)
    VALUES (NEWID(), @year, @month, @day, 1)
OUTPUT INSERTED.code;",
            new { year, month, day }, tx);
    }

    /// <summary>
    /// 國泰 ATM 虛擬帳號演算法（移植自 Librarys.GetAtmCode / GetCheckNumber）。
    /// 格式：prefix(4) + yy(2) + mm(2) + dd(2) + seq(5) + checkDigit(1) = 16 chars
    /// </summary>
    public static string BuildAtmCode(string prefix, DateOnly expDate, int seq, int total)
    {
        var yy = expDate.Year.ToString().Substring(2);
        var mm = expDate.Month.ToString("D2");
        var dd = expDate.Day.ToString("D2");
        var seqStr = seq.ToString().PadLeft(5, '0');

        // First 15 chars (before check digit)
        var body = prefix + yy + mm + dd + seqStr;

        var checkDigit = GetCheckNumber(body, total.ToString().PadLeft(8, '0'));
        return body + checkDigit;
    }

    // 國泰驗證碼演算法（移植自 Librarys.GetCheckNumber）
    private static string GetCheckNumber(string atmNumber, string priceTotal)
    {
        int cc = 4, xx = 1, aa = 0;
        for (int k = 0; k < atmNumber.Length; k++)
        {
            int h = atmNumber[k] - '0';
            if (k < 6) aa += Digit(h * cc++);
            else       aa += Digit(h * xx++);
        }
        int dd = Digit(10 - Digit(aa));

        int pp = 8, a = 0;
        foreach (char c in priceTotal)
        {
            a += Digit((c - '0') * pp--);
        }
        int bd = Digit(10 - Digit(a));

        return Digit(dd + bd).ToString();

        static int Digit(int n) => n % 10;
    }

    private static string HashPassword(string plaintext)
    {
        const int Iterations = 260_000;
        var salt = System.Security.Cryptography.RandomNumberGenerator.GetBytes(16);
        var hash = System.Security.Cryptography.Rfc2898DeriveBytes.Pbkdf2(
            System.Text.Encoding.UTF8.GetBytes(plaintext),
            salt, Iterations,
            System.Security.Cryptography.HashAlgorithmName.SHA256, 32);
        return $"pbkdf2:{Iterations}:{Convert.ToBase64String(salt)}:{Convert.ToBase64String(hash)}";
    }

    // ─── Private row types ─────────────────────────────────────────────────────────

    private sealed record OrderLine(Guid DetailId, Guid ProductId, string Title, int Qty, int Price, int Subtotal);
    private sealed record ProductPriceRow(Guid productid, int price, string title);
    private sealed record ProductNameRow(Guid productid, string title);
    private sealed record OrderHeaderRow(
        Guid orderid, string ordercode, DateOnly orderdate,
        int total, int freight, int discount,
        int paytype, int paystatus, DateTime? paydate,
        int deliverstatus, DateTime? deliverdate, int invoicetype,
        string? codeatm, DateOnly? expirepaydate,
        string buyername, string buyermobile,
        string recivername, string recivermobile, string reciveraddress,
        string? remark);
    private sealed record OrderLineRow(
        Guid productid, string productTitle, string productPhoto,
        int qty, int price, int subtotal,
        string? capacity, int isgift);
    private sealed record OrderListRow(
        Guid orderid, string ordercode, DateOnly orderdate,
        int total, int freight, int discount, int paystatus, int deliverstatus);
}
