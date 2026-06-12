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
    private readonly IEmailService _email;

    public PaymentNotifyController(
        IPaymentGateway gateway, IInvoiceService invoices,
        IDbConnectionFactory db, ICodeNumberService codes, IEmailService email)
    {
        _gateway = gateway; _invoices = invoices; _db = db; _codes = codes; _email = email;
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
        PaidOrderInfo? paid;
        try
        {
            paid = await MarkOrderPaidAsync(notice, ctx.Request.HttpContext.RequestAborted);
        }
        catch
        {
            // DB 失敗：回 200 讓 Fisc 重試（冪等：若已 Paid 則 MarkOrderPaid 直接跳過）
            return ctx.Ok(new { received = true, dbError = true });
        }

        // 5. 首次標記為已付款（paid != null）才執行後續動作
        if (paid is not null)
        {
            // 5a. 付款完成通知信（best-effort：SendAsync 內部 catch，失敗不影響 Fisc 流程）
            if (!string.IsNullOrWhiteSpace(paid.Email))
            {
                await _email.SendAsync(
                    paid.Email!.Trim(),
                    $"食在呼 TFoodies–付款完成通知 {notice.OrderNumber}",
                    BuildPaidMailHtml(paid.Name, notice.OrderNumber, paid.Payable, notice.LastPan4),
                    ctx.Request.HttpContext.RequestAborted);
            }

            // 5b. 最大努力開立電子發票（失敗不影響 Fisc 重試邏輯）
            _ = Task.Run(async () =>
            {
                try { await IssueEzPayInvoiceAsync(notice.OrderNumber, ctx.Request.HttpContext.RequestAborted); }
                catch { /* fire-and-forget — 可在後台補開 */ }
            });
        }

        return ctx.Ok(new { received = true, orderNumber = notice.OrderNumber, incomeCode = paid?.IncomeCode });
    }

    // ── Private helpers ───────────────────────────────────────────────────────────

    /// <summary>
    /// 冪等：若已是 Paid(1) 狀態回傳 null（不重複寄信/開票）；首次轉為已付款時回傳
    /// 含 incomecode 與會員收件資訊的 <see cref="PaidOrderInfo"/>。
    /// </summary>
    private async Task<PaidOrderInfo?> MarkOrderPaidAsync(PaymentNotice notice, CancellationToken ct)
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
            return new PaidOrderInfo(
                incomeCode,
                order.memberEmail,
                order.memberName,
                order.total + order.freight - order.discount);
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

    // ── 付款完成通知信版型 ───────────────────────────────────────────────────────────

    // 會員中心訂單查詢頁。
    private const string OrderUrl = "https://www.tfoodies.com/Member/Orders";

    // 響應式、相容主流郵件客戶端（Outlook/Gmail）的純 table + inline-style 版型，
    // 與訂單通知／忘記密碼信共用同一套品牌視覺（主色 #26B7BC，深色 #156467）。
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
  <!-- 預覽文字（收件匣摘要，不顯示於信件本文）-->
  <div style=""display:none; max-height:0; overflow:hidden; opacity:0; font-size:1px; line-height:1px; color:#f4f5f7;"">我們已收到您訂單 {orderCode} 的付款，感謝您的支持。</div>

  <table role=""presentation"" cellspacing=""0"" cellpadding=""0"" border=""0"" width=""100%"" style=""background-color:#f4f5f7;"">
    <tr>
      <td align=""center"" style=""padding:32px 16px;"">
        <table role=""presentation"" cellspacing=""0"" cellpadding=""0"" border=""0"" width=""600"" style=""width:600px; max-width:600px; background-color:#ffffff; border-radius:14px; overflow:hidden; box-shadow:0 2px 8px rgba(0,0,0,0.06); font-family:'Helvetica Neue', Arial, 'PingFang TC', 'Microsoft JhengHei', sans-serif;"">

          <!-- 品牌標頭 -->
          <tr>
            <td align=""center"" style=""background-color:#26b7bc; background-image:linear-gradient(135deg,#26b7bc 0%,#1d8e92 100%); padding:34px 24px;"">
              <div style=""font-size:26px; font-weight:700; letter-spacing:2px; color:#ffffff; line-height:1.2;"">食在呼 TFoodies</div>
              <div style=""font-size:13px; color:#e6f6f6; margin-top:6px; letter-spacing:1px;"">付款完成通知</div>
            </td>
          </tr>

          <!-- 成功圖示 + 內文 -->
          <tr>
            <td align=""center"" style=""padding:34px 40px 0 40px;"">
              <div style=""width:64px; height:64px; line-height:64px; border-radius:50%; background-color:#e6f6f6; color:#1d8e92; font-size:34px; font-weight:700; margin:0 auto 18px auto;"">&#10003;</div>
              <h1 style=""font-size:20px; font-weight:600; color:#2c3e3e; margin:0 0 12px 0;"">付款成功！</h1>
              <p style=""font-size:15px; line-height:1.7; color:#5a6666; margin:0 0 24px 0;"">親愛的 {buyerName}，我們已收到您的付款，訂單將盡快為您安排出貨。</p>
            </td>
          </tr>

          <!-- 訂單編號方塊 -->
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

          <!-- 付款金額 -->
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

          <!-- CTA 按鈕 -->
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

          <!-- 補充說明 -->
          <tr>
            <td style=""padding:14px 40px 4px 40px;"">
              <p style=""font-size:13px; line-height:1.7; color:#9aa3a3; margin:0;"">電子發票將另行開立並寄送。更多詳細資訊請登入「食在呼－會員中心」查詢。如有任何問題，歡迎與客服聯繫。</p>
            </td>
          </tr>

          <!-- 分隔線 -->
          <tr>
            <td style=""padding:24px 40px 0 40px;""><div style=""border-top:1px solid #eef0f0; font-size:0; line-height:0;"">&nbsp;</div></td>
          </tr>

          <!-- 頁尾 -->
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

    private sealed record OrderPayRow(
        Guid orderid, Guid memberid, int paystatus,
        int total, int freight, int discount,
        string memberName, string? memberEmail);

    /// <summary>首次標記為已付款後，寄信/開票所需的訂單摘要。</summary>
    private sealed record PaidOrderInfo(string IncomeCode, string? Email, string Name, int Payable);

    private sealed record InvoiceOrderRow(
        Guid orderid, int invoicetype, int invoicestatus,
        string? companynumber, string? lovecode,
        int total, int freight, int discount,
        string memberName, string? memberEmail);

    private sealed record InvoiceLineRow(string title, int qty, int price, int subtotal);
}
