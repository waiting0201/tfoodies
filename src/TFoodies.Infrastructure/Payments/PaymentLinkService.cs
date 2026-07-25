using System.Data;
using System.Net;
using System.Security.Cryptography;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TFoodies.Application.Abstractions;
using TFoodies.Domain.Common;
using TFoodies.Domain.Enums;
using TFoodies.Infrastructure.Payments.Fisc;

namespace TFoodies.Infrastructure.Payments;

/// <summary>
/// 刷卡收款連結（Paymentlinks）。詳見 <see cref="IPaymentLinkService"/>。
///
/// 與 <see cref="PaymentCompletionService"/> 的關係：兩者都處理 FISC 授權成功後的入帳，
/// 但收款連結沒有訂單也沒有會員，所以不共用 MarkPaidAsync——這裡只做「冪等標記 + 通知信」。
/// </summary>
public sealed class PaymentLinkService : IPaymentLinkService
{
    /// <summary>付款通知信收件人（營運人工入帳與開立發票）。</summary>
    private const string NotifyTo = "angela@tfoodies.com";

    // status
    private const int Unpaid = 0;
    private const int Paid = 1;
    private const int Cancelled = 2;

    private readonly IDbConnectionFactory _db;
    private readonly ICodeNumberService _codes;
    private readonly IEmailService _email;
    private readonly ILinePayClient _linePay;
    private readonly FiscOptions _fisc;
    private readonly ILogger<PaymentLinkService> _logger;

    public PaymentLinkService(
        IDbConnectionFactory db, ICodeNumberService codes, IEmailService email,
        ILinePayClient linePay, IOptions<FiscOptions> fisc, ILogger<PaymentLinkService> logger)
    {
        _db = db; _codes = codes; _email = email; _linePay = linePay;
        _fisc = fisc.Value; _logger = logger;
    }

    private static DateTime Now => DateTime.UtcNow.AddHours(8);   // 台北時間，與訂單流程一致

    // ── 建立 ──────────────────────────────────────────────────────────────────────

    public async Task<PaymentLinkCreated> CreateAsync(
        string title, string? note, int amount, int? validDays, int payMethod, int adminId,
        CancellationToken ct = default)
    {
        var now = Now;
        var id = Guid.NewGuid();
        var token = NewToken();
        DateTime? expire = validDays is > 0 ? now.AddDays(validDays.Value) : null;

        using var conn = (SqlConnection)await _db.CreateOpenConnectionAsync(ct);
        using var tx = conn.BeginTransaction(IsolationLevel.ReadCommitted);
        try
        {
            // 單號與資料列在同一 transaction：回滾時號碼一併釋放（同 OrderService 的建單流程）。
            var code = await _codes.NextAsync(CodeKind.PaymentLink, DateOnly.FromDateTime(now), tx, ct);

            await conn.ExecuteAsync(@"
INSERT INTO Paymentlinks (paymentlinkid, paymentlinkcode, token, title, note, amount, status,
                          paymethod, expiredate, createadminid, createdate)
VALUES (@id, @code, @token, @title, @note, @amount, 0, @payMethod, @expire, @adminId, @now)",
                new { id, code, token, title, note, amount, payMethod, expire, adminId, now }, tx);

            tx.Commit();
            return new PaymentLinkCreated(id, code, token, BuildUrl(token));
        }
        catch { tx.Rollback(); throw; }
    }

    /// <summary>128-bit CSPRNG → 32 字元小寫 hex。選 hex 而非 base64url：URL 大小寫不敏感的
    /// 中介軟體與人工抄寫都不會出錯。唯一索引 UQ_Paymentlinks_Token 為最後防線。</summary>
    private static string NewToken() => Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();

    private string BuildUrl(string token) => $"{_fisc.StoreOrigin}/Pay/{token}";

    // ── 後台列表 ──────────────────────────────────────────────────────────────────

    public async Task<(IReadOnlyList<PaymentLinkRow> Items, int Total)> ListAsync(
        int? status, bool? isExpired, int page, int pageSize, CancellationToken ct = default)
    {
        using var conn = (SqlConnection)await _db.CreateOpenConnectionAsync(ct);
        var now = Now;

        // 逾期是「未付款且已過期」的衍生狀態，不是獨立 status 值，因此用同一組條件在 SQL 端切分，
        // 才能讓 COUNT 與分頁一致（在記憶體過濾會讓 total 與實際頁數對不起來）。
        var conds = new List<string>();
        if (status is not null) conds.Add("p.status = @status");
        if (isExpired is true) conds.Add("p.status = 0 AND p.expiredate IS NOT NULL AND p.expiredate <= @now");
        if (isExpired is false) conds.Add("p.status = 0 AND (p.expiredate IS NULL OR p.expiredate > @now)");
        var where = conds.Count == 0 ? "" : "WHERE " + string.Join(" AND ", conds);

        var sql = $@"
SELECT COUNT(*) FROM Paymentlinks p {where};

SELECT p.paymentlinkid, p.paymentlinkcode, p.token, p.title, p.note, p.amount, p.status,
       p.paymethod, p.customername, p.customermobile, p.customeraddress, p.lastpan4,
       p.paydate, p.expiredate, p.createdate,
       z.city, z.area, z.zipcode
FROM Paymentlinks p
LEFT JOIN Zipcodes z ON z.zipcodeid = p.customerzipcodeid
{where}
ORDER BY p.createdate DESC
OFFSET @skip ROWS FETCH NEXT @take ROWS ONLY;";

        using var multi = await conn.QueryMultipleAsync(
            new CommandDefinition(sql,
                new { status, now, skip = (page - 1) * pageSize, take = pageSize },
                cancellationToken: ct));

        var total = await multi.ReadSingleAsync<int>();
        var rows = (await multi.ReadAsync<LinkRow>()).ToList();

        var items = rows.Select(r => new PaymentLinkRow(
            r.paymentlinkid, r.paymentlinkcode, r.token, BuildUrl(r.token),
            r.title, r.note, r.amount, r.status,
            IsExpired(r.status, r.expiredate, now), r.paymethod,
            r.customername, r.customermobile, FullAddress(r.zipcode, r.city, r.area, r.customeraddress),
            r.lastpan4, r.paydate, r.expiredate, r.createdate)).ToList();

        return (items, total);
    }

    // 只有「未付款」才談得上過期；已付/已作廢的狀態本身就是終局。
    private static bool IsExpired(int status, DateTime? expire, DateTime now)
        => status == Unpaid && expire is not null && expire <= now;

    /// <summary>付款方式標籤（值對齊 Domain PayType；收款連結目前僅開放信用卡與 LINE Pay）。</summary>
    private static string PayMethodLabel(int payMethod) => (PayType)payMethod switch
    {
        PayType.CreditCard => "信用卡線上刷卡",
        PayType.LinePay => "LINE Pay",
        _ => $"付款方式 {payMethod}",
    };

    private static string? FullAddress(string? zipcode, string? city, string? area, string? address)
    {
        if (string.IsNullOrWhiteSpace(address)) return null;
        var prefix = $"{zipcode}{city}{area}";
        return string.IsNullOrWhiteSpace(prefix) ? address : $"{prefix}{address}";
    }

    // ── 客人端 ────────────────────────────────────────────────────────────────────

    public async Task<PaymentLinkPublic?> GetByTokenAsync(string token, CancellationToken ct = default)
    {
        using var conn = (SqlConnection)await _db.CreateOpenConnectionAsync(ct);
        var row = await conn.QuerySingleOrDefaultAsync<PublicRow>(new CommandDefinition(
            "SELECT paymentlinkcode, title, amount, status, paymethod, expiredate FROM Paymentlinks WHERE token=@token",
            new { token }, cancellationToken: ct));

        if (row is null) return null;
        return new PaymentLinkPublic(
            row.paymentlinkcode, row.title, row.amount, row.status,
            IsExpired(row.status, row.expiredate, Now), row.paymethod);
    }

    public async Task<Result<PaymentLinkCharge>> StartCheckoutAsync(
        string token, PaymentLinkCustomer customer, string? returnOrigin, CancellationToken ct = default)
    {
        using var conn = (SqlConnection)await _db.CreateOpenConnectionAsync(ct);

        var row = await conn.QuerySingleOrDefaultAsync<CheckoutRow>(new CommandDefinition(
            "SELECT paymentlinkid, paymentlinkcode, amount, status, paymethod, expiredate FROM Paymentlinks WHERE token=@token",
            new { token }, cancellationToken: ct));

        if (row is null) return Error.NotFound("收款連結");
        if (row.status == Paid) return Error.Conflict("本筆款項已完成付款。");
        if (row.status == Cancelled) return Error.Conflict("此收款連結已失效。");
        if (IsExpired(row.status, row.expiredate, Now)) return Error.Conflict("此收款連結已逾期。");

        // 郵遞區號必須真實存在，否則 FK 會在寫入時才爆，錯誤訊息對客人毫無意義。
        var zipExists = await conn.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(1) FROM Zipcodes WHERE zipcodeid=@id",
            new { id = customer.ZipcodeId }, cancellationToken: ct));
        if (zipExists == 0) return Error.Validation("請選擇正確的縣市與鄉鎮市區。");

        await conn.ExecuteAsync(new CommandDefinition(@"
UPDATE Paymentlinks
SET customername=@name, customermobile=@mobile, customerzipcodeid=@zipcodeId,
    customeraddress=@address, updatedate=@now
WHERE paymentlinkid=@id AND status=0",
            new
            {
                name = customer.Name, mobile = customer.Mobile,
                zipcodeId = customer.ZipcodeId, address = customer.Address,
                now = Now, id = row.paymentlinkid,
            }, cancellationToken: ct));

        // 多網域服務：把客人所在網域帶進回呼網址的 query，授權返回時據以同網域導回。
        // 只在通過白名單時才帶（見 FiscRedirect）；未帶則 return 端退回設定的 StoreOrigin。
        static string WithOrigin(string url, string? origin) =>
            string.IsNullOrEmpty(origin) ? url : $"{url}?origin={Uri.EscapeDataString(origin)}";

        // 金額一律取自 DB，前端完全不傳金額。
        if (row.paymethod == (int)PayType.LinePay)
        {
            var reserve = await _linePay.RequestAsync(new LinePayReserveRequest(
                row.paymentlinkcode,
                row.amount,
                $"食在呼 TFoodies {row.paymentlinkcode}",
                WithOrigin(_fisc.LinePayPaylinkConfirmUrl, returnOrigin),
                WithOrigin(_fisc.LinePayPaylinkCancelUrl, returnOrigin)), ct);

            return reserve.IsSuccess
                ? PaymentLinkCharge.Redirect(reserve.Value.PaymentUrlWeb)
                : Result.Failure<PaymentLinkCharge>(reserve.Error);
        }

        var authResUrl = WithOrigin(_fisc.PayLinkAuthResUrl, returnOrigin);
        var fields = FiscWebpos.BuildFields(row.paymentlinkcode, row.amount, _fisc, authResUrl);
        return PaymentLinkCharge.Form(_fisc.ActionUrl, fields);
    }

    // ── 付款完成 ──────────────────────────────────────────────────────────────────

    public async Task<bool> MarkPaidAsync(string code, string? lastPan4, string txnRef, CancellationToken ct = default)
    {
        using var conn = (SqlConnection)await _db.CreateOpenConnectionAsync(ct);
        var now = Now;

        // 冪等護欄：條件式 UPDATE 本身即原子，rows==0 表示不存在或已付款（return 與 notify
        // 雙觸發時只有第一個會拿到 1，因此只會寄一封信）。
        var rows = await conn.ExecuteAsync(new CommandDefinition(@"
UPDATE Paymentlinks
SET status=1, paydate=@now, lastpan4=@pan4, txnref=@txnRef, updatedate=@now
WHERE paymentlinkcode=@code AND status=0",
            new { now, pan4 = lastPan4, txnRef, code }, cancellationToken: ct));

        if (rows == 0) return false;

        await SendPaidNoticeAsync(conn, code, ct);
        return true;
    }

    public async Task<Result> CompleteLinePayAsync(
        string code, string transactionId, CancellationToken ct = default)
    {
        using var conn = (SqlConnection)await _db.CreateOpenConnectionAsync(ct);

        var row = await conn.QuerySingleOrDefaultAsync<CheckoutRow>(new CommandDefinition(
            "SELECT paymentlinkid, paymentlinkcode, amount, status, paymethod, expiredate FROM Paymentlinks WHERE paymentlinkcode=@code",
            new { code }, cancellationToken: ct));

        if (row is null) return Result.Failure(Error.NotFound("收款連結"));

        // 已付款：confirm 回跳被重放（使用者重整/重複點擊）。LINE Pay 端也會回 1172，
        // 但這裡先短路，連 API 都不必打。
        if (row.status == Paid) return Result.Success();
        if (row.status != Unpaid) return Result.Failure(Error.Conflict("此收款連結已失效。"));

        // 請款金額取自 DB，不採信回跳參數。
        var confirm = await _linePay.ConfirmAsync(transactionId, row.amount, ct);
        if (confirm.IsFailure) return Result.Failure(confirm.Error);

        await MarkPaidAsync(code, lastPan4: null, txnRef: $"LINEPay transactionId:{transactionId}", ct);
        return Result.Success();
    }

    public async Task<Result> MarkPaidManuallyAsync(Guid id, int adminId, CancellationToken ct = default)
    {
        using var conn = (SqlConnection)await _db.CreateOpenConnectionAsync(ct);
        var now = Now;

        var code = await conn.ExecuteScalarAsync<string?>(new CommandDefinition(
            "SELECT paymentlinkcode FROM Paymentlinks WHERE paymentlinkid=@id",
            new { id }, cancellationToken: ct));
        if (code is null) return Result.Failure(Error.NotFound("收款連結"));

        var rows = await conn.ExecuteAsync(new CommandDefinition(@"
UPDATE Paymentlinks
SET status=1, paydate=@now, txnref=@txnRef, updatedate=@now
WHERE paymentlinkid=@id AND status=0",
            new { now, txnRef = $"後台手動標記已付款（AdminID {adminId}）", id }, cancellationToken: ct));

        if (rows == 0) return Result.Failure(Error.Conflict("此連結已付款或已作廢，無法重複標記。"));

        await SendPaidNoticeAsync(conn, code, ct);
        return Result.Success();
    }

    public async Task<Result> CancelAsync(Guid id, CancellationToken ct = default)
    {
        using var conn = (SqlConnection)await _db.CreateOpenConnectionAsync(ct);

        var rows = await conn.ExecuteAsync(new CommandDefinition(
            "UPDATE Paymentlinks SET status=2, updatedate=@now WHERE paymentlinkid=@id AND status=0",
            new { now = Now, id }, cancellationToken: ct));

        return rows == 0
            ? Result.Failure(Error.Conflict("此連結不存在、已付款或已作廢。"))
            : Result.Success();
    }

    // ── 通知信 ────────────────────────────────────────────────────────────────────

    // best-effort：SendAsync 內部已 catch，失敗不影響「款項已收到」這個事實。
    private async Task SendPaidNoticeAsync(SqlConnection conn, string code, CancellationToken ct)
    {
        var row = await conn.QuerySingleOrDefaultAsync<NoticeRow>(new CommandDefinition(@"
SELECT p.paymentlinkcode, p.title, p.note, p.amount, p.paymethod, p.customername, p.customermobile,
       p.customeraddress, p.lastpan4, p.paydate, p.createadminid,
       z.city, z.area, z.zipcode, a.Username AS adminName
FROM Paymentlinks p
LEFT JOIN Zipcodes z ON z.zipcodeid = p.customerzipcodeid
LEFT JOIN Admins  a ON a.AdminID    = p.createadminid
WHERE p.paymentlinkcode = @code",
            new { code }, cancellationToken: ct));

        if (row is null) return;

        var sent = await _email.SendAsync(
            NotifyTo,
            $"食在呼 TFoodies–刷卡收款連結付款完成 {row.paymentlinkcode}",
            BuildNoticeHtml(row),
            ct);

        if (!sent)
            _logger.LogWarning("收款連結 {Code} 已付款，但通知信寄送失敗（款項狀態不受影響）", code);
    }

    // 版型沿用付款完成信（600px 卡片、#26b7bc/#156467），內容改為營運入帳所需欄位。
    // 所有使用者輸入一律 HtmlEncode 後才內嵌。
    private static string BuildNoticeHtml(NoticeRow r)
    {
        static string E(string? s) => WebUtility.HtmlEncode(s ?? "");

        var address = FullAddress(r.zipcode, r.city, r.area, r.customeraddress);
        var paidAt = (r.paydate ?? Now).ToString("yyyy-MM-dd HH:mm");
        var creator = string.IsNullOrWhiteSpace(r.adminName)
            ? $"AdminID {r.createadminid}"
            : $"{r.adminName}（AdminID {r.createadminid}）";

        static string Row(string label, string? value) =>
            string.IsNullOrWhiteSpace(value) ? "" : $@"
                <tr>
                  <td style=""padding:7px 0; font-size:14px; color:#5a6666; white-space:nowrap;"">{label}</td>
                  <td align=""right"" style=""padding:7px 0 7px 16px; font-size:14px; color:#2c3e3e;"">{value}</td>
                </tr>";

        return $@"<!DOCTYPE html>
<html lang=""zh-Hant"">
<head>
  <meta charset=""utf-8"">
  <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
  <meta name=""x-apple-disable-message-reformatting"">
  <title>刷卡收款連結付款完成</title>
</head>
<body style=""margin:0; padding:0; background-color:#f4f5f7; -webkit-text-size-adjust:100%; -ms-text-size-adjust:100%;"">
  <div style=""display:none; max-height:0; overflow:hidden; opacity:0; font-size:1px; line-height:1px; color:#f4f5f7;"">收款連結 {E(r.paymentlinkcode)} 已完成付款，金額 NT$ {r.amount:N0}。</div>

  <table role=""presentation"" cellspacing=""0"" cellpadding=""0"" border=""0"" width=""100%"" style=""background-color:#f4f5f7;"">
    <tr>
      <td align=""center"" style=""padding:32px 16px;"">
        <table role=""presentation"" cellspacing=""0"" cellpadding=""0"" border=""0"" width=""600"" style=""width:600px; max-width:600px; background-color:#ffffff; border-radius:14px; overflow:hidden; box-shadow:0 2px 8px rgba(0,0,0,0.06); font-family:'Helvetica Neue', Arial, 'PingFang TC', 'Microsoft JhengHei', sans-serif;"">

          <tr>
            <td align=""center"" style=""background-color:#26b7bc; background-image:linear-gradient(135deg,#26b7bc 0%,#1d8e92 100%); padding:34px 24px;"">
              <div style=""font-size:26px; font-weight:700; letter-spacing:2px; color:#ffffff; line-height:1.2;"">食在呼 TFoodies</div>
              <div style=""font-size:13px; color:#e6f6f6; margin-top:6px; letter-spacing:1px;"">刷卡收款連結 · 付款完成通知</div>
            </td>
          </tr>

          <tr>
            <td style=""padding:32px 40px 0 40px;"">
              <table role=""presentation"" cellspacing=""0"" cellpadding=""0"" border=""0"" width=""100%"" style=""background-color:#e6f6f6; border:1px solid #b9e6e7; border-radius:10px;"">
                <tr>
                  <td style=""padding:18px 24px;"">
                    <div style=""font-size:13px; color:#1d8e92; letter-spacing:1px; margin-bottom:6px;"">收款單號</div>
                    <div style=""font-size:22px; font-weight:700; color:#156467; letter-spacing:1px;"">{E(r.paymentlinkcode)}</div>
                    <div style=""font-size:12px; color:#5a9a9c; margin-top:6px;"">付款時間 {paidAt}</div>
                  </td>
                </tr>
              </table>
            </td>
          </tr>

          <tr>
            <td style=""padding:24px 40px 0 40px;"">
              <table role=""presentation"" cellspacing=""0"" cellpadding=""0"" border=""0"" width=""100%"">
                <tr>
                  <td style=""padding:7px 0; font-size:16px; font-weight:700; color:#2c3e3e;"">實收金額</td>
                  <td align=""right"" style=""padding:7px 0; font-size:20px; font-weight:700; color:#156467;"">NT$ {r.amount:N0}</td>
                </tr>
                {Row("付款方式", PayMethodLabel(r.paymethod))}
                {Row("收款項目", E(r.title))}
                {Row("內部備註", E(r.note))}
                {Row("付款人姓名", E(r.customername))}
                {Row("聯絡手機", E(r.customermobile))}
                {Row("地址", E(address))}
                {Row("卡號末四碼", E(r.lastpan4))}
                {Row("連結建立者", E(creator))}
              </table>
            </td>
          </tr>

          <tr>
            <td style=""padding:24px 40px 0 40px;"">
              <table role=""presentation"" cellspacing=""0"" cellpadding=""0"" border=""0"" width=""100%"" style=""background-color:#fff7e8; border:1px solid #f5dcae; border-radius:10px;"">
                <tr>
                  <td style=""padding:14px 20px; font-size:13px; line-height:1.7; color:#8a6a2b;"">
                    本筆為收款連結，<strong>不會自動開立電子發票，也未寫入訂單與會計收入</strong>，請人工完成入帳與發票作業。
                  </td>
                </tr>
              </table>
            </td>
          </tr>

          <tr>
            <td style=""padding:24px 40px 0 40px;""><div style=""border-top:1px solid #eef0f0; font-size:0; line-height:0;"">&nbsp;</div></td>
          </tr>

          <tr>
            <td align=""center"" style=""padding:18px 40px 32px 40px;"">
              <p style=""font-size:12px; line-height:1.6; color:#aab2b2; margin:0;"">此為系統自動發送之通知信，請勿直接回覆。</p>
              <p style=""font-size:12px; line-height:1.6; color:#aab2b2; margin:6px 0 0 0;"">© 食在呼 TFoodies</p>
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

    private sealed record LinkRow(
        Guid paymentlinkid, string paymentlinkcode, string token, string title, string? note,
        int amount, int status, int paymethod,
        string? customername, string? customermobile, string? customeraddress,
        string? lastpan4, DateTime? paydate, DateTime? expiredate, DateTime createdate,
        string? city, string? area, string? zipcode);

    private sealed record PublicRow(
        string paymentlinkcode, string title, int amount, int status, int paymethod, DateTime? expiredate);

    private sealed record CheckoutRow(
        Guid paymentlinkid, string paymentlinkcode, int amount, int status, int paymethod, DateTime? expiredate);

    private sealed record NoticeRow(
        string paymentlinkcode, string title, string? note, int amount, int paymethod,
        string? customername, string? customermobile, string? customeraddress,
        string? lastpan4, DateTime? paydate, int createadminid,
        string? city, string? area, string? zipcode, string? adminName);
}
