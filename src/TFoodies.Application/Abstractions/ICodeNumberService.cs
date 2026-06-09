using System.Data;

namespace TFoodies.Application.Abstractions;

/// <summary>
/// The date-partitioned document-number sequencers. The frozen DB keeps 9 near-identical
/// <c>*codes</c> tables (Ordercodes, Purchasecodes, Refoundcodes, Invoicecodes, Atmcodes …),
/// shape (year, month, day, code). The legacy generators (Libs.Librarys.New*Code) read a
/// per-day counter from Session and incremented it — non-atomic and race-prone. This single
/// service does an atomic UPDATE ... WITH (UPDLOCK, HOLDLOCK) on the SAME transaction as the
/// business write, so concurrent same-day requests can never collide.
/// </summary>
public interface ICodeNumberService
{
    /// <summary>
    /// Reserve the next number for <paramref name="kind"/> on <paramref name="date"/> and
    /// return the formatted document code (e.g. O20260601001). Must run inside the caller's
    /// transaction (<paramref name="transaction"/>) so a rollback also releases the number.
    /// </summary>
    Task<string> NextAsync(CodeKind kind, DateOnly date, IDbTransaction transaction, CancellationToken ct = default);
}

/// <summary>Identifies which legacy <c>*codes</c> table / prefix to use.</summary>
public enum CodeKind
{
    Order,
    Purchase,
    Return,
    Refound,   // legacy misspelling preserved at the data layer
    Expenditure,
    Income,
    Invoice,
    Outcome,
    Atm,
}
