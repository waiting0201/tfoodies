using System.Data;
using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using TFoodies.Api.Functions.Router;
using TFoodies.Application.Abstractions;
using TFoodies.Domain.Common;
using TFoodies.Domain.Enums;

namespace TFoodies.Api.Functions.Controllers;

/// <summary>
/// 金流回調端點（公開，不需 JWT）。
///   POST /store/payment/notify — 智付通 Fisc server-to-server webhook
///
/// 流程：
///   1. ParseNotify：HMAC-SHA256 驗簽 + AES-GCM 解密
///   2. 若 Outcome == Authorized：
///      a. Transaction：UPDATE Orders.paystatus=Paid + INSERT Income
///      b. 最大努力：呼叫 EzPay 開立電子發票
///   3. 永遠回傳 200（Fisc 期待任何 2xx 視為確認收到）
/// </summary>
public sealed class PaymentNotifyController
{
    private readonly IPaymentGateway _gateway;
    private readonly IInvoiceService _invoices;
    private readonly IDbConnectionFactory _db;
    private readonly ICodeNumberService _codes;

    public PaymentNotifyController(
        IPaymentGateway gateway, IInvoiceService invoices,
        IDbConnectionFactory db, ICodeNumberService codes)
    {
        _gateway = gateway; _invoices = invoices; _db = db; _codes = codes;
    }

    // POST /store/payment/notify
    public async Task<IActionResult> Notify(RouteContext ctx)
    {
        // 1. 讀取 form 欄位
        IReadOnlyDictionary<string, string> form;
        try
        {
            var rawForm = await ctx.Request.ReadFormAsync(ctx.Request.HttpContext.RequestAborted);
            form = rawForm.ToDictionary(kv => kv.Key, kv => kv.Value.ToString());
        }
        catch
        {
            // Fisc 需要 200；記錄失敗但不回 4xx
            return ctx.Ok(new { received = false, reason = "form_parse_error" });
        }

        // 2. 驗簽 + 解密
        PaymentNotice notice;
        try { notice = _gateway.ParseNotify(form); }
        catch
        {
            return ctx.Ok(new { received = false, reason = "signature_invalid" });
        }

        // 3. 只處理授權成功
        if (notice.Outcome != PaymentOutcome.Authorized)
            return ctx.Ok(new { received = true, outcome = notice.Outcome.ToString() });

        // 4. 在 transaction 內更新訂單並建 Income
        string? incomeCode = null;
        try
        {
            incomeCode = await MarkOrderPaidAsync(notice, ctx.Request.HttpContext.RequestAborted);
        }
        catch
        {
            // DB 失敗：回 200 讓 Fisc 重試（冪等：若已 Paid 則 MarkOrderPaid 直接跳過）
            return ctx.Ok(new { received = true, dbError = true });
        }

        // 5. 最大努力開立電子發票（失敗不影響 Fisc 重試邏輯）
        if (incomeCode is not null)
        {
            _ = Task.Run(async () =>
            {
                try { await IssueEzPayInvoiceAsync(notice.OrderNumber, ctx.Request.HttpContext.RequestAborted); }
                catch { /* fire-and-forget — 可在後台補開 */ }
            });
        }

        return ctx.Ok(new { received = true, orderNumber = notice.OrderNumber, incomeCode });
    }

    // ── Private helpers ───────────────────────────────────────────────────────────

    /// <summary>
    /// 冪等：若已是 Paid(1) 狀態，直接回傳既有 incomecode（或 null）。
    /// </summary>
    private async Task<string?> MarkOrderPaidAsync(PaymentNotice notice, CancellationToken ct)
    {
        using var conn = (SqlConnection)await _db.CreateOpenConnectionAsync(ct);
        using var tx = conn.BeginTransaction(IsolationLevel.RepeatableRead);
        try
        {
            var order = await conn.QuerySingleOrDefaultAsync<OrderPayRow>(
                "SELECT orderid, memberid, paystatus, total, freight, ISNULL(discount,0) AS discount FROM Orders WHERE ordercode=@code",
                new { code = notice.OrderNumber }, tx);

            if (order is null) { tx.Rollback(); return null; }

            // 冪等：已付款直接結束
            if (order.paystatus == (int)PayStatus.Paid)
            {
                tx.Rollback();
                return null;
            }

            var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(8));
            var now = DateTime.UtcNow.AddHours(8);

            await conn.ExecuteAsync(
                "UPDATE Orders SET paystatus=1, paydate=@today, lastpan4=@pan4 WHERE orderid=@id",
                new { today, pan4 = notice.LastPan4, id = order.orderid }, tx);

            var incomeCode = await _codes.NextAsync(CodeKind.Income, today, tx, ct);
            await conn.ExecuteAsync(@"
INSERT INTO Incomes (incomeid, memberid, incomecode, incomedate, amount, fee, note, createdate)
VALUES (NEWID(), @memberid, @incomeCode, @now, @amount, 0, @note, @now)",
                new
                {
                    memberid = order.memberid,
                    incomeCode,
                    now,
                    amount = order.total + order.freight - order.discount,
                    note = $"Fisc TxID:{notice.TransactionId}",
                }, tx);

            tx.Commit();
            return incomeCode;
        }
        catch { tx.Rollback(); throw; }
    }

    private async Task IssueEzPayInvoiceAsync(string orderCode, CancellationToken ct)
    {
        using var conn = await _db.CreateOpenConnectionAsync(ct);

        using var multi = await conn.QueryMultipleAsync(@"
SELECT o.orderid, o.invoicetype, o.invoicestatus,
       o.companynumber, o.lovecode,
       o.total, o.freight, ISNULL(o.discount,0) AS discount,
       m.name AS memberName, m.email AS memberEmail
FROM Orders o JOIN Members m ON m.memberid=o.memberid
WHERE o.ordercode=@orderCode;

SELECT p.title, od.qty, od.price, od.subtotal
FROM Orderdetails od JOIN Products p ON p.productid=od.productid
JOIN Orders o2 ON o2.orderid=od.orderid WHERE o2.ordercode=@orderCode;",
            new { orderCode });

        var order = await multi.ReadSingleOrDefaultAsync<InvoiceOrderRow>();
        if (order is null) return;

        // 已開或免開：跳過
        if (order.invoicestatus != (int)InvoiceStatus.NotIssued) return;
        if (order.invoicetype == (int)InvoiceType.None) return;

        var lines = (await multi.ReadAsync<InvoiceLineRow>()).ToList();
        var totalAmt = order.total + order.freight - order.discount;

        var items = lines
            .Select(l => new InvoiceItem(l.title, l.qty, "份", l.price, l.subtotal))
            .ToList();

        if (order.freight > 0)
            items.Add(new InvoiceItem("運費", 1, "次", order.freight, order.freight));

        var request = new InvoiceRequest(
            MerchantOrderNo: orderCode,
            Type: (InvoiceType)order.invoicetype,
            BuyerName: order.memberName,
            TotalAmt: totalAmt,
            Items: items,
            BuyerUbn: order.companynumber,
            BuyerEmail: order.memberEmail,
            LoveCode: order.lovecode);

        var result = await _invoices.IssueAsync(request, IssueMode.Immediate, ct);
        if (!result.IsSuccess || result.Value is null || !result.Value.Success) return;

        // 更新發票號碼與狀態
        await conn.ExecuteAsync(
            "UPDATE Orders SET invoicestatus=1, invoicecode=@code WHERE ordercode=@orderCode",
            new { code = result.Value.InvoiceNumber, orderCode });
    }

    // ── Row types ─────────────────────────────────────────────────────────────────

    private sealed record OrderPayRow(
        Guid orderid, Guid memberid, int paystatus,
        int total, int freight, int discount);

    private sealed record InvoiceOrderRow(
        Guid orderid, int invoicetype, int invoicestatus,
        string? companynumber, string? lovecode,
        int total, int freight, int discount,
        string memberName, string? memberEmail);

    private sealed record InvoiceLineRow(string title, int qty, int price, int subtotal);
}
