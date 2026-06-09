using TFoodies.Domain.Common;

namespace TFoodies.Application.Tests.Tax;

/// <summary>
/// Unit tests for <see cref="TaiwanVat"/> — the single source of truth for Taiwan 5% VAT.
/// Covers SplitInclusive, TaxOfInclusive, and TaxOnExclusive with boundary/edge cases.
/// </summary>
public sealed class TaiwanVatApplicationTests
{
    // ── SplitInclusive ────────────────────────────────────────────────────────

    [Fact]
    public void SplitInclusive_Zero_ReturnsBothZero()
    {
        var (net, tax) = TaiwanVat.SplitInclusive(0);
        Assert.Equal(0, net);
        Assert.Equal(0, tax);
    }

    [Fact]
    public void SplitInclusive_105_Returns100Net5Tax()
    {
        var (net, tax) = TaiwanVat.SplitInclusive(105);
        Assert.Equal(100, net);
        Assert.Equal(5, tax);
    }

    [Fact]
    public void SplitInclusive_1050_Returns1000Net50Tax()
    {
        var (net, tax) = TaiwanVat.SplitInclusive(1050);
        Assert.Equal(1000, net);
        Assert.Equal(50, tax);
    }

    [Fact]
    public void SplitInclusive_10500_Returns10000Net500Tax()
    {
        var (net, tax) = TaiwanVat.SplitInclusive(10500);
        Assert.Equal(10000, net);
        Assert.Equal(500, tax);
    }

    [Fact]
    public void SplitInclusive_RoundTrip_NetPlusTaxEqualsOriginal()
    {
        // Non-round amount: ensures net + tax always reconstructs the original total
        foreach (var total in new[] { 1, 99, 100, 101, 999, 1000, 1001, 9999 })
        {
            var (net, tax) = TaiwanVat.SplitInclusive(total);
            Assert.Equal(total, net + tax);
        }
    }

    // ── TaxOfInclusive ────────────────────────────────────────────────────────

    [Fact]
    public void TaxOfInclusive_Zero_ReturnsZero()
    {
        Assert.Equal(0, TaiwanVat.TaxOfInclusive(0));
    }

    [Fact]
    public void TaxOfInclusive_105_Returns5()
    {
        Assert.Equal(5, TaiwanVat.TaxOfInclusive(105));
    }

    [Fact]
    public void TaxOfInclusive_1050_Returns50()
    {
        Assert.Equal(50, TaiwanVat.TaxOfInclusive(1050));
    }

    [Fact]
    public void TaxOfInclusive_10500_Returns500()
    {
        Assert.Equal(500, TaiwanVat.TaxOfInclusive(10500));
    }

    [Fact]
    public void TaxOfInclusive_MatchesSplitInclusiveTaxComponent()
    {
        // TaxOfInclusive must always equal SplitInclusive(...).TaxAmount
        foreach (var total in new[] { 0, 105, 1050, 10500, 210, 2100 })
        {
            var expected = TaiwanVat.SplitInclusive(total).TaxAmount;
            Assert.Equal(expected, TaiwanVat.TaxOfInclusive(total));
        }
    }

    // ── TaxOnExclusive ────────────────────────────────────────────────────────

    [Fact]
    public void TaxOnExclusive_Zero_ReturnsZero()
    {
        Assert.Equal(0, TaiwanVat.TaxOnExclusive(0));
    }

    [Fact]
    public void TaxOnExclusive_100_Returns5()
    {
        // 100 * 1.05 - 100 = 5.0 → 5
        Assert.Equal(5, TaiwanVat.TaxOnExclusive(100));
    }

    [Fact]
    public void TaxOnExclusive_1050_Returns53()
    {
        // 1050 * 1.05 - 1050 = 52.5 → rounds AwayFromZero → 53
        Assert.Equal(53, TaiwanVat.TaxOnExclusive(1050));
    }

    [Fact]
    public void TaxOnExclusive_10500_Returns525()
    {
        // 10500 * 1.05 - 10500 = 525.0 → 525
        Assert.Equal(525, TaiwanVat.TaxOnExclusive(10500));
    }

    [Fact]
    public void TaxOnExclusive_105_Returns5()
    {
        // 105 * 1.05 - 105 = 5.25 → rounds AwayFromZero → 5
        Assert.Equal(5, TaiwanVat.TaxOnExclusive(105));
    }
}
