using System.Globalization;

namespace TFoodies.Domain.Common;

/// <summary>
/// NTD money value type. The frozen DB mixes Int32 (Orders.total, Incomes.amount …)
/// and Decimal (Purchasedetails.unitprice …) for amounts. The domain works in
/// <see cref="decimal"/> everywhere; mappers round to whole NTD only at the boundary
/// where a value is written back into an Int32 column (see <see cref="ToWholeNtd"/>).
/// </summary>
public readonly record struct Money(decimal Amount) : IComparable<Money>
{
    public static readonly Money Zero = new(0m);

    public static Money FromNtd(int wholeNtd) => new(wholeNtd);
    public static Money FromDecimal(decimal amount) => new(amount);

    /// <summary>Round to whole NTD (banker's-away rounding, matching legacy MidpointRounding.AwayFromZero).</summary>
    public int ToWholeNtd() => (int)Math.Round(Amount, MidpointRounding.AwayFromZero);

    public static Money operator +(Money a, Money b) => new(a.Amount + b.Amount);
    public static Money operator -(Money a, Money b) => new(a.Amount - b.Amount);
    public static Money operator *(Money a, int qty) => new(a.Amount * qty);
    public static Money operator *(Money a, decimal factor) => new(a.Amount * factor);

    public int CompareTo(Money other) => Amount.CompareTo(other.Amount);
    public static bool operator <(Money a, Money b) => a.Amount < b.Amount;
    public static bool operator >(Money a, Money b) => a.Amount > b.Amount;
    public static bool operator <=(Money a, Money b) => a.Amount <= b.Amount;
    public static bool operator >=(Money a, Money b) => a.Amount >= b.Amount;

    public override string ToString() => Amount.ToString("0.##", CultureInfo.InvariantCulture);
}
