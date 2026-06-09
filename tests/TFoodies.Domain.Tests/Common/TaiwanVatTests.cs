using TFoodies.Domain.Common;
using Xunit;

namespace TFoodies.Domain.Tests.Common;

public class TaiwanVatTests
{
    // Parity guard: replicates Convert.ToInt32(Math.Round(total / 1.05, AwayFromZero))
    // from the legacy code so the new split can never silently drift.
    private static (int net, int tax) Legacy(int total)
    {
        int net = Convert.ToInt32(Math.Round(total / 1.05, MidpointRounding.AwayFromZero));
        return (net, total - net);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(100)]
    [InlineData(105)]
    [InlineData(2000)]
    [InlineData(2100)]
    [InlineData(999)]
    [InlineData(123456)]
    public void SplitInclusive_MatchesLegacyFormula(int total)
    {
        var (expectedNet, expectedTax) = Legacy(total);

        var (net, tax) = TaiwanVat.SplitInclusive(total);

        Assert.Equal(expectedNet, net);
        Assert.Equal(expectedTax, tax);
        Assert.Equal(total, net + tax); // net + tax must always reconstitute the total
    }

    [Fact]
    public void SplitInclusive_TypicalTaxInclusivePrice()
    {
        var (net, tax) = TaiwanVat.SplitInclusive(105);
        Assert.Equal(100, net);
        Assert.Equal(5, tax);
    }

    [Theory]
    [InlineData(100, 5)]   // round((100*1.05)-100) = 5
    [InlineData(99, 5)]    // round(4.95) = 5
    [InlineData(10, 1)]    // round(0.5, AwayFromZero) = 1
    public void TaxOnExclusive_MatchesLegacyCommissionFormula(int @base, int expectedTax)
        => Assert.Equal(expectedTax, TaiwanVat.TaxOnExclusive(@base));
}
