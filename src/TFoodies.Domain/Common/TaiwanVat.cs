namespace TFoodies.Domain.Common;

/// <summary>
/// Single source of truth for Taiwan 5% VAT, replacing the formula that was duplicated
/// across the legacy code (OrdersService.GetInvoicedetailsByMemberID, MainController.cs:91,
/// AccountingMsController.cs:724, OrderMsController.cs:397, AjaxController.cs:480/723,
/// MainMsController.cs:440). All tax-inclusive prices use the same rounding the legacy used:
/// Amt = round(total / 1.05, AwayFromZero); Tax = total - Amt.
/// </summary>
public static class TaiwanVat
{
    public const decimal Rate = 0.05m;
    private const decimal TaxInclusiveFactor = 1.05m;

    /// <summary>Split a tax-INCLUSIVE whole-NTD total into (netAmount, taxAmount), both whole NTD.</summary>
    public static (int NetAmount, int TaxAmount) SplitInclusive(int taxInclusiveTotal)
    {
        int net = (int)Math.Round(taxInclusiveTotal / TaxInclusiveFactor, MidpointRounding.AwayFromZero);
        int tax = taxInclusiveTotal - net;
        return (net, tax);
    }

    /// <summary>Tax portion of a tax-inclusive whole-NTD total.</summary>
    public static int TaxOfInclusive(int taxInclusiveTotal) => SplitInclusive(taxInclusiveTotal).TaxAmount;

    /// <summary>
    /// Tax ADDED on top of a tax-EXCLUSIVE base (e.g. commission), matching
    /// OrderMsController.cs:944 — round((base * 1.05) - base, AwayFromZero).
    /// </summary>
    public static int TaxOnExclusive(int taxExclusiveBase)
        => (int)Math.Round((taxExclusiveBase * TaxInclusiveFactor) - taxExclusiveBase, MidpointRounding.AwayFromZero);
}
