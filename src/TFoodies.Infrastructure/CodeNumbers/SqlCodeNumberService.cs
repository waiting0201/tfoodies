using System.Data;
using Dapper;
using TFoodies.Application.Abstractions;

namespace TFoodies.Infrastructure.CodeNumbers;

/// <summary>
/// 原子日期分組流水號服務。
///
/// 舊系統（Libs.Librarys.New*Code）從 Session 讀計數器再 +1 — 非原子，高併發下有 race。
/// 本實作在同一 transaction 內以 UPDLOCK + HOLDLOCK hint 讀取並更新 *codes 表，
/// 保證同一天同一種類的號碼不重複。
///
/// 若當天尚無記錄，先 INSERT（一次性操作，極少發生），使用 MERGE 避免 race。
/// </summary>
public sealed class SqlCodeNumberService : ICodeNumberService
{
    // 各 CodeKind 對應的 DB 資訊
    private static readonly Dictionary<CodeKind, CodeTable> Tables = new()
    {
        [CodeKind.Order]       = new("Ordercodes",      "ordercodeid",      "O"),
        [CodeKind.Purchase]    = new("Purchasecodes",   "purchasecodeid",   "P"),
        [CodeKind.Return]      = new("Returncodes",     "returncodeid",     "R"),
        [CodeKind.Refound]     = new("Refoundcodes",    "refoundcodeid",    "RF"),
        [CodeKind.Expenditure] = new("Expenditurecodes","expenditurecodeid","E"),
        [CodeKind.Income]      = new("Incomecodes",     "incomecodeid",     "I"),
        [CodeKind.Invoice]     = new("Invoicecodes",    "invoicecodeid",    "IV"),
        [CodeKind.Outcome]     = new("Outcomecodes",    "outcomecodeid",    "OC"),
        [CodeKind.Atm]         = new("Atmcodes",        "atmcodeid",        "A"),
    };

    public async Task<string> NextAsync(
        CodeKind kind, DateOnly date, IDbTransaction transaction, CancellationToken ct = default)
    {
        var t = Tables[kind];
        var conn = transaction.Connection!;

        var year  = date.Year.ToString();
        var month = date.Month.ToString("D2");
        var day   = date.Day.ToString("D2");

        // MERGE：若不存在則 INSERT(code=1)，否則 UPDATE code += 1，回傳新值
        // UPDLOCK+HOLDLOCK 防止兩個 tx 同時讀到 code=0 再各自 INSERT。
        var sql = $@"
MERGE {t.TableName} WITH (HOLDLOCK) AS target
USING (SELECT @year AS y, @month AS m, @day AS d) AS src
    ON target.year=src.y AND target.month=src.m AND target.day=src.d
WHEN MATCHED THEN
    UPDATE SET code = target.code + 1
WHEN NOT MATCHED THEN
    INSERT ({t.PkColumn}, year, month, day, code)
    VALUES (NEWID(), @year, @month, @day, 1)
OUTPUT INSERTED.code;";

        var newCode = await conn.ExecuteScalarAsync<int>(
            sql,
            new { year, month, day },
            transaction);

        // 格式：前綴 + yyyymmdd + 3 位序號 （例：O20260601001）
        return $"{t.Prefix}{year}{month}{day}{newCode:D3}";
    }

    private sealed record CodeTable(string TableName, string PkColumn, string Prefix);
}
