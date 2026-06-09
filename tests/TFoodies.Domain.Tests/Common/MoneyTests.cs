using TFoodies.Domain.Common;
using Xunit;

namespace TFoodies.Domain.Tests.Common;

public class MoneyTests
{
    [Fact]
    public void Arithmetic_AddsSubtractsAndMultiplies()
    {
        var a = Money.FromNtd(100);
        var b = Money.FromNtd(50);

        Assert.Equal(Money.FromNtd(150), a + b);
        Assert.Equal(Money.FromNtd(50), a - b);
        Assert.Equal(Money.FromNtd(300), a * 3);
    }

    [Theory]
    [InlineData(100.4, 100)]
    [InlineData(100.5, 101)] // AwayFromZero
    [InlineData(99.5, 100)]
    public void ToWholeNtd_RoundsAwayFromZero(double amount, int expected)
        => Assert.Equal(expected, Money.FromDecimal((decimal)amount).ToWholeNtd());

    [Fact]
    public void Comparisons_Work()
    {
        Assert.True(Money.FromNtd(10) < Money.FromNtd(20));
        Assert.True(Money.FromNtd(20) >= Money.FromNtd(20));
        Assert.Equal(Money.Zero, Money.FromNtd(0));
    }
}
