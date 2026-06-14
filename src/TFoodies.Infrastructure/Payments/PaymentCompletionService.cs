using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using TFoodies.Application.Abstractions;
using TFoodies.Domain.Common;
using TFoodies.Domain.Enums;

namespace TFoodies.Infrastructure.Payments;

/// <summary>
/// 信用卡授權成功後的共用處理（對應舊系統 MainMs/ShoppingSuccess 成功分支）。
/// 由 WEBPOS 導回(/return)與主動通知(/notify)共用：冪等標記已付款 + 建 Income +
/// 寄付款完成信 + 最大努力開立電子發票。
/// </summary>
public sealed class PaymentCompletionService : IPaymentCompletionService
{
    private readonly IDbConnectionFactory _db;
    private readonly ICodeNumberService _codes;
    private readonly IInvoiceService _invoices;
    private readonly IEmailService _email;

    public PaymentCompletionService(
        IDbConnectionFactory db, ICodeNumberService codes,
        IInvoiceService invoices, IEmailService email)
    {
        _db = db; _codes = codes; _invoices = invoices; _email = email;
    }

    public async Task<bool> MarkPaidAsync(string orderCode, string? lastPan4, string txnRef, DateOnly? payDate = null, CancellationToken ct = default)
    {
        var paid = await MarkOrderPaidAsync(orderCode, lastPan4, txnRef, payDate, ct);
        if (paid is null) return false; // 訂單不存在或已付款（冪等）

        // 付款完成通知信（best-effort：SendAsync 內部 catch，失敗不影響後續）
        if (!string.IsNullOrWhiteSpace(paid.Email))
        {
            await _email.SendAsync(
                paid.Email!.Trim(),
                $"食在呼 TFoodies–付款完成通知 {orderCode}",
                BuildPaidMailHtml(paid.Name, orderCode, paid.Payable, lastPan4),
                ct);
        }

        // 開立電子發票（即時）。失敗不影響「付款已完成」這個事實：invoicestatus 留「未開」，
        // 後台可手動補開。try/catch 涵蓋未設定金鑰等非預期例外（EzPayCodec 建 PostData 會 throw）。
        try { await IssueInvoiceAsync(orderCode, paid.IncomeId, ct); }
        catch { /* 開票失敗不影響付款完成；狀態維持未開供後台補開 */ }

        return true;
    }

    // ── 標記已付款 + 建 Income（交易內，冪等）───────────────────────────────────────

    private async Task<PaidOrderInfo?> MarkOrderPaidAsync(string orderCode, string? lastPan4, string txnRef, DateOnly? payDate, CancellationToken ct)
    {
        using var conn = (SqlConnection)await _db.CreateOpenConnectionAsync(ct);
        using var tx = conn.BeginTransaction(IsolationLevel.RepeatableRead);
        try
        {
            var order = await conn.QuerySingleOrDefaultAsync<OrderPayRow>(@"
SELECT o.orderid, o.memberid, o.paystatus, o.total, o.freight, ISNULL(o.discount,0) AS discount,
       m.name AS memberName, m.email AS memberEmail
FROM Orders o JOIN Members m ON m.memberid=o.memberid
WHERE o.ordercode=@code",
                new { code = orderCode }, tx);

            if (order is null) { tx.Rollback(); return null; }

            // 冪等：已付款直接結束（不重複寄信/開票）
            if (order.paystatus == (int)PayStatus.Paid) { tx.Rollback(); return null; }

            var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(8));
            var effectivePayDate = payDate ?? today;
            var now = DateTime.UtcNow.AddHours(8);

            await conn.ExecuteAsync(
                "UPDATE Orders SET paystatus=1, paydate=@payDate, lastpan4=@pan4 WHERE orderid=@id",
                new { payDate = effectivePayDate, pan4 = lastPan4, id = order.orderid }, tx);

            var incomeId = Guid.NewGuid();
            var incomeCode = await _codes.NextAsync(CodeKind.Income, today, tx, ct);
            await conn.ExecuteAsync(@"
INSERT INTO Incomes (incomeid, memberid, incomecode, incomedate, amount, fee, note, createdate)
VALUES (@incomeId, @memberid, @incomeCode, @now, @amount, 0, @note, @now)",
                new
                {
                    incomeId,
                    memberid = order.memberid,
                    incomeCode,
                    now,
                    amount = order.total + order.freight - order.discount,
                    note = txnRef,
                }, tx);

            tx.Commit();
            return new PaidOrderInfo(
                order.memberEmail, order.memberName,
                order.total + order.freight - order.discount, incomeId);
        }
        catch { tx.Rollback(); throw; }
    }

    // ── 開立電子發票（ezPay）+ 建本地 Invoices/Invoicedetails（對齊舊系統會計流程）──────
    //   供 MarkPaidAsync 付款完成自動呼叫，亦供後台「補開發票」端點單獨呼叫。冪等。

    // 舊系統銷貨收入會計科目（DB seed，沿用同一 Guid）。
    private static readonly Guid SalesAccountingId = new("f6cfd53f-13ca-4843-881f-141b579b4a5b");

    public async Task<Result> IssueInvoiceAsync(string orderCode, Guid? incomeId = null, CancellationToken ct = default)
    {
        using var conn = (SqlConnection)await _db.CreateOpenConnectionAsync(ct);

        InvoiceOrderRow? order;
        List<InvoiceLineRow> lines;
        // 先把資料讀完並關閉 reader，之後才呼叫 ezPay（HTTP）與開交易，避免 reader 與交易衝突。
        using (var multi = await conn.QueryMultipleAsync(@"
SELECT o.orderid, o.orderdate, o.memberid, o.invoicetype, o.invoicestatus,
       o.companynumber, o.lovecode, o.note,
       o.total, o.freight, ISNULL(o.discount,0) AS discount,
       m.name AS memberName, m.email AS memberEmail
FROM Orders o JOIN Members m ON m.memberid=o.memberid
WHERE o.ordercode=@orderCode;

SELECT p.title, od.qty, od.price, od.subtotal
FROM Orderdetails od JOIN Products p ON p.productid=od.productid
JOIN Orders o2 ON o2.orderid=od.orderid WHERE o2.ordercode=@orderCode;",
            new { orderCode }))
        {
            order = await multi.ReadSingleOrDefaultAsync<InvoiceOrderRow>();
            lines = order is null ? new() : (await multi.ReadAsync<InvoiceLineRow>()).ToList();
        }

        if (order is null) return Result.Failure(Error.NotFound("訂單"));
        if (order.invoicetype == (int)InvoiceType.None) return Result.Failure(Error.Validation("此訂單免開發票"));
        if (order.invoicestatus != (int)InvoiceStatus.NotIssued) return Result.Failure(Error.Conflict("發票已開立或已作廢"));

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

        // ezPay 加密/HTTP 例外（如未設定金鑰）轉為 Result.Failure，讓後台補開端點回乾淨訊息而非 500。
        Result<InvoiceResult> result;
        try { result = await _invoices.IssueAsync(request, IssueMode.Immediate, ct); }
        catch (Exception ex) { return Result.Failure(new Error("ezpay", ex.Message)); }

        if (!result.IsSuccess) return Result.Failure(result.Error);
        if (result.Value is null || !result.Value.Success || string.IsNullOrWhiteSpace(result.Value.InvoiceNumber))
            return Result.Failure(new Error("ezpay", result.Value?.Message ?? "ezPay 未回傳發票號碼"));

        var invoiceNumber = result.Value.InvoiceNumber!;

        // 稅額（含稅 → 稅前 → 稅額），與舊系統一致：TaxAmt = TotalAmt - round(TotalAmt/1.05)
        var taxExcl = (int)Math.Round(totalAmt / 1.05m, MidpointRounding.AwayFromZero);
        var taxAmt = totalAmt - taxExcl;
        var now = DateTime.UtcNow.AddHours(8);

        using var tx = conn.BeginTransaction(IsolationLevel.RepeatableRead);
        try
        {
            // 冪等護欄：只有仍為「未開」時才寫入，避免 return + notify 雙觸發重複建檔。
            var rows = await conn.ExecuteAsync(
                "UPDATE Orders SET invoicestatus=1, invoicecode=@num WHERE ordercode=@orderCode AND invoicestatus=0",
                new { num = invoiceNumber, orderCode }, tx);
            if (rows == 0) { tx.Rollback(); return Result.Success(); } // 已被其他流程開立

            var invoiceId = Guid.NewGuid();
            await conn.ExecuteAsync(@"
INSERT INTO Invoices (invoiceid, incomeid, invoicecode, memberid, requestdate, note, createdate)
VALUES (@invoiceId, @incomeId, @invoiceCode, @memberid, @requestdate, @note, @createdate)",
                new
                {
                    invoiceId,
                    incomeId,
                    invoiceCode = invoiceNumber,
                    memberid = order.memberid,
                    requestdate = order.orderdate,
                    note = (string?)null,
                    createdate = now,
                }, tx);

            await conn.ExecuteAsync(@"
INSERT INTO Invoicedetails (invoicedetailid, invoiceid, accountingid, orderid, price, tax, note)
VALUES (NEWID(), @invoiceId, @accountingId, @orderid, @price, @tax, @note)",
                new
                {
                    invoiceId,
                    accountingId = SalesAccountingId,
                    orderid = order.orderid,
                    price = totalAmt,
                    tax = taxAmt,
                    note = order.note,
                }, tx);

            tx.Commit();
            return Result.Success();
        }
        catch { tx.Rollback(); throw; }
    }

    // ── 付款完成通知信版型（與訂單/忘記密碼信共用品牌視覺）─────────────────────────────

    private const string OrderUrl = "https://www.tfoodies.com/Member/Orders";

    private static string BuildPaidMailHtml(string buyerName, string orderCode, int payable, string? lastPan4)
    {
        var paidDate = DateTime.UtcNow.AddHours(8).ToString("yyyy-MM-dd HH:mm");
        var cardLine = !string.IsNullOrWhiteSpace(lastPan4)
            ? $@"<div style=""font-size:12px; color:#5a9a9c; margin-top:6px;"">信用卡末四碼 {lastPan4}　·　付款時間 {paidDate}</div>"
            : $@"<div style=""font-size:12px; color:#5a9a9c; margin-top:6px;"">付款時間 {paidDate}</div>";

        return $@"<!DOCTYPE html>
<html lang=""zh-Hant"">
<head>
  <meta charset=""utf-8"">
  <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
  <meta name=""x-apple-disable-message-reformatting"">
  <title>付款完成通知</title>
</head>
<body style=""margin:0; padding:0; background-color:#f4f5f7; -webkit-text-size-adjust:100%; -ms-text-size-adjust:100%;"">
  <div style=""display:none; max-height:0; overflow:hidden; opacity:0; font-size:1px; line-height:1px; color:#f4f5f7;"">我們已收到您訂單 {orderCode} 的付款，感謝您的支持。</div>

  <table role=""presentation"" cellspacing=""0"" cellpadding=""0"" border=""0"" width=""100%"" style=""background-color:#f4f5f7;"">
    <tr>
      <td align=""center"" style=""padding:32px 16px;"">
        <table role=""presentation"" cellspacing=""0"" cellpadding=""0"" border=""0"" width=""600"" style=""width:600px; max-width:600px; background-color:#ffffff; border-radius:14px; overflow:hidden; box-shadow:0 2px 8px rgba(0,0,0,0.06); font-family:'Helvetica Neue', Arial, 'PingFang TC', 'Microsoft JhengHei', sans-serif;"">

          <tr>
            <td align=""center"" style=""background-color:#26b7bc; background-image:linear-gradient(135deg,#26b7bc 0%,#1d8e92 100%); padding:34px 24px;"">
              <div style=""font-size:26px; font-weight:700; letter-spacing:2px; color:#ffffff; line-height:1.2;"">食在呼 TFoodies</div>
              <div style=""font-size:13px; color:#e6f6f6; margin-top:6px; letter-spacing:1px;"">付款完成通知</div>
            </td>
          </tr>

          <tr>
            <td align=""center"" style=""padding:34px 40px 0 40px;"">
              <div style=""width:64px; height:64px; line-height:64px; border-radius:50%; background-color:#e6f6f6; color:#1d8e92; font-size:34px; font-weight:700; margin:0 auto 18px auto;"">&#10003;</div>
              <h1 style=""font-size:20px; font-weight:600; color:#2c3e3e; margin:0 0 12px 0;"">付款成功！</h1>
              <p style=""font-size:15px; line-height:1.7; color:#5a6666; margin:0 0 24px 0;"">親愛的 {buyerName}，我們已收到您的付款，訂單將盡快為您安排出貨。</p>
            </td>
          </tr>

          <tr>
            <td style=""padding:0 40px;"">
              <table role=""presentation"" cellspacing=""0"" cellpadding=""0"" border=""0"" width=""100%"" style=""background-color:#e6f6f6; border:1px solid #b9e6e7; border-radius:10px;"">
                <tr>
                  <td style=""padding:18px 24px;"">
                    <div style=""font-size:13px; color:#1d8e92; letter-spacing:1px; margin-bottom:6px;"">訂單編號</div>
                    <div style=""font-size:22px; font-weight:700; color:#156467; letter-spacing:1px;"">{orderCode}</div>
                    {cardLine}
                  </td>
                </tr>
              </table>
            </td>
          </tr>

          <tr>
            <td style=""padding:24px 40px 0 40px;"">
              <table role=""presentation"" cellspacing=""0"" cellpadding=""0"" border=""0"" width=""100%"">
                <tr>
                  <td style=""padding:7px 0; font-size:16px; font-weight:700; color:#2c3e3e;"">實付金額</td>
                  <td align=""right"" style=""padding:7px 0; font-size:20px; font-weight:700; color:#156467;"">NT$ {payable:N0}</td>
                </tr>
              </table>
            </td>
          </tr>

          <tr>
            <td align=""center"" style=""padding:30px 40px 12px 40px;"">
              <table role=""presentation"" cellspacing=""0"" cellpadding=""0"" border=""0"">
                <tr>
                  <td align=""center"" bgcolor=""#26b7bc"" style=""border-radius:8px;"">
                    <a href=""{OrderUrl}"" target=""_blank"" style=""display:inline-block; padding:14px 40px; font-size:16px; font-weight:600; color:#ffffff; text-decoration:none; border-radius:8px; background-color:#26b7bc;"">查看訂單狀態</a>
                  </td>
                </tr>
              </table>
            </td>
          </tr>

          <tr>
            <td style=""padding:14px 40px 4px 40px;"">
              <p style=""font-size:13px; line-height:1.7; color:#9aa3a3; margin:0;"">電子發票將另行開立並寄送。更多詳細資訊請登入「食在呼－會員中心」查詢。如有任何問題，歡迎與客服聯繫。</p>
            </td>
          </tr>

          <tr>
            <td style=""padding:24px 40px 0 40px;""><div style=""border-top:1px solid #eef0f0; font-size:0; line-height:0;"">&nbsp;</div></td>
          </tr>

          <tr>
            <td align=""center"" style=""padding:18px 40px 32px 40px;"">
              <p style=""font-size:12px; line-height:1.6; color:#aab2b2; margin:0;"">此為系統自動發送之通知信，請勿直接回覆。</p>
              <p style=""font-size:12px; line-height:1.6; color:#aab2b2; margin:6px 0 0 0;"">© 食在呼 TFoodies　感謝您的支持</p>
            </td>
          </tr>

        </table>
      </td>
    </tr>
  </table>
</body>
</html>";
    }

    // ── Row types ─────────────────────────────────────────────────────────────────

    private sealed record PaidOrderInfo(string? Email, string Name, int Payable, Guid IncomeId);

    private sealed record OrderPayRow(
        Guid orderid, Guid memberid, int paystatus,
        int total, int freight, int discount,
        string memberName, string? memberEmail);

    private sealed record InvoiceOrderRow(
        Guid orderid, DateTime orderdate, Guid memberid, int invoicetype, int invoicestatus,
        string? companynumber, string? lovecode, string? note,
        int total, int freight, int discount,
        string memberName, string? memberEmail);

    private sealed record InvoiceLineRow(string title, int qty, int price, int subtotal);
}
