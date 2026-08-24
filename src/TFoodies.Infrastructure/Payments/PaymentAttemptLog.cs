using Dapper;
using Microsoft.Extensions.Logging;
using TFoodies.Application.Abstractions;

namespace TFoodies.Infrastructure.Payments;

/// <summary>
/// 把每一次財金 WEBPOS 授權回呼寫進 Paymentattempts（建表腳本 scripts/add-paymentattempts.sql）。
///
/// 全部方法都是 best-effort：資料表還沒建、DB 短暫不可用等狀況只記 log，
/// **絕不讓紀錄失敗影響刷卡/入帳流程**（顧客的錢比我們的稽核重要）。
/// </summary>
public sealed class PaymentAttemptLog : IPaymentAttemptLog
{
    private readonly IDbConnectionFactory _db;
    private readonly ILogger<PaymentAttemptLog> _logger;

    public PaymentAttemptLog(IDbConnectionFactory db, ILogger<PaymentAttemptLog> logger)
    {
        _db = db; _logger = logger;
    }

    public async Task<bool> RecordAsync(PaymentAttempt a, CancellationToken ct = default)
    {
        try
        {
            using var conn = await _db.CreateOpenConnectionAsync(ct);
            await conn.ExecuteAsync(@"
INSERT INTO Paymentattempts
    (paymentattemptid, lidm, source, issuccess, status, errcode, errdesc,
     authcode, xid, lastpan4, cardbrand, authamt, note, createdate)
VALUES
    (@id, @lidm, @source, @issuccess, @status, @errcode, @errdesc,
     @authcode, @xid, @lastpan4, @cardbrand, @authamt, @note, @createdate)",
                new
                {
                    id         = Guid.NewGuid(),
                    lidm       = Cut(a.Lidm, 20),
                    source     = Cut(a.Source, 20),
                    issuccess  = a.IsSuccess,
                    status     = Cut(a.Status, 2),
                    errcode    = Cut(a.ErrCode, 4),
                    errdesc    = Cut(a.ErrDesc, 512),
                    authcode   = Cut(a.AuthCode, 10),
                    xid        = Cut(a.Xid, 64),
                    lastpan4   = Cut(a.LastPan4, 4),
                    cardbrand  = Cut(a.CardBrand, 20),
                    authamt    = a.AuthAmt,
                    note       = Cut(a.Note, 500),
                    // 台北時間，與 Orders.paydate / Incomes.createdate 一致（DB 存 local time）
                    createdate = a.CreateDate ?? DateTime.UtcNow.AddHours(8),
                });
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "寫入刷卡紀錄失敗，單號 {Lidm}（不影響付款流程）", a.Lidm);
            return false;
        }
    }

    public async Task<IReadOnlyList<PaymentAttempt>> GetByLidmAsync(string lidm, int take = 10, CancellationToken ct = default)
    {
        try
        {
            using var conn = await _db.CreateOpenConnectionAsync(ct);
            var rows = await conn.QueryAsync<Row>(@"
SELECT TOP (@take) lidm, source, issuccess, status, errcode, errdesc,
       authcode, xid, lastpan4, cardbrand, authamt, note, createdate
FROM Paymentattempts
WHERE lidm = @lidm
ORDER BY createdate DESC",
                new { lidm, take });

            return rows.Select(r => new PaymentAttempt(
                r.lidm, r.source, r.issuccess, r.status, r.errcode, r.errdesc,
                r.authcode, r.xid, r.lastpan4, r.cardbrand, r.authamt, r.note, r.createdate)).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "讀取刷卡紀錄失敗，單號 {Lidm}", lidm);
            return [];
        }
    }

    // 財金欄位長度以手冊為準，但回傳值不受我方控制；截斷比讓 INSERT 因截斷而整筆失敗好。
    private static string? Cut(string? v, int max)
        => string.IsNullOrWhiteSpace(v) ? null : (v.Length <= max ? v : v[..max]);

    // Dapper 需要可寫的扁平型別；record 建構子只能用 DateTime（見 docs/09 的 DateOnly 限制）。
    private sealed class Row
    {
        public string lidm { get; set; } = "";
        public string source { get; set; } = "";
        public bool issuccess { get; set; }
        public string? status { get; set; }
        public string? errcode { get; set; }
        public string? errdesc { get; set; }
        public string? authcode { get; set; }
        public string? xid { get; set; }
        public string? lastpan4 { get; set; }
        public string? cardbrand { get; set; }
        public int? authamt { get; set; }
        public string? note { get; set; }
        public DateTime createdate { get; set; }
    }
}
