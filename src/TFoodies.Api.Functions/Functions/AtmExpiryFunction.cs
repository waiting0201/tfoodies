using Dapper;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using TFoodies.Application.Abstractions;

namespace TFoodies.Api.Functions.Functions;

/// <summary>
/// 每日 ATM 繳費期限到期掃描。
///
/// 排程：每天 01:00 UTC（= 台灣時間 09:00 UTC+8）。
/// 將已逾期且未付款的 ATM 訂單（paytype=2, paystatus=0, expirepaydate &lt; 今天）
/// 標記為：deliverstatus=3（已取消）、paystatus=4（逾期未付）。
/// </summary>
public sealed class AtmExpiryFunction
{
    private readonly IDbConnectionFactory _db;
    private readonly ILogger<AtmExpiryFunction> _logger;

    public AtmExpiryFunction(IDbConnectionFactory db, ILogger<AtmExpiryFunction> logger)
    {
        _db = db;
        _logger = logger;
    }

    [Function("AtmExpiryDaily")]
    public async Task Run([TimerTrigger("0 0 1 * * *")] TimerInfo timerInfo)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(8));

        using var conn = await _db.CreateOpenConnectionAsync(default);

        int rows = await conn.ExecuteAsync(
            "UPDATE Orders SET deliverstatus = 3, paystatus = 4 WHERE paytype = 2 AND paystatus = 0 AND expirepaydate < @today",
            new { today });

        _logger.LogInformation("ATM expiry: {rows} orders expired", rows);
    }
}
