using TFoodies.Infrastructure.Orders;

namespace TFoodies.Infrastructure.Tests.Orders;

public class OrderServiceAtmTests
{
    // Spot-check against a known value produced by the reference Librarys.GetAtmCode.
    // "1943" + "26" + "06" + "08" + "00001" = 15 chars, total = 2500
    // GetCheckNumber manually:
    //   cc=4..9, xx=1..9
    //   k=0: h=1, k<6, AA += (1*4)%10 = 4; cc=5
    //   k=1: h=9, k<6, AA += (9*5)%10 = 5; cc=6  → AA=9
    //   k=2: h=4, k<6, AA += (4*6)%10 = 4; cc=7  → AA=13
    //   k=3: h=3, k<6, AA += (3*7)%10 = 1; cc=8  → AA=14
    //   k=4: h=2, k<6, AA += (2*8)%10 = 6; cc=9  → AA=20
    //   k=5: h=6, k<6, AA += (6*9)%10 = 4; cc=10 → AA=24
    //   k=6: h=0, k>=6, AA += (0*1)%10 = 0; xx=2 → AA=24
    //   k=7: h=6, k>=6, AA += (6*2)%10 = 2; xx=3 → AA=26
    //   k=8: h=0, k>=6, AA += (0*3)%10 = 0; xx=4 → AA=26
    //   k=9: h=8, k>=6, AA += (8*4)%10 = 2; xx=5 → AA=28
    //   k=10: h=0, k>=6, AA += (0*5)%10 = 0; xx=6 → AA=28
    //   k=11: h=0, k>=6, AA += (0*6)%10 = 0; xx=7 → AA=28
    //   k=12: h=0, k>=6, AA += (0*7)%10 = 0; xx=8 → AA=28
    //   k=13: h=0, k>=6, AA += (0*8)%10 = 0; xx=9 → AA=28
    //   k=14: h=1, k>=6, AA += (1*9)%10 = 9; xx=10 → AA=37
    //   DD = (10 - 37%10)%10 = (10-7)%10 = 3
    //
    //   total=2500 → "00002500", pp=8..1
    //   k=0: h=0, A += 0*8=0; pp=7
    //   k=1: h=0, A += 0; pp=6 → A=0
    //   k=2: h=0, A += 0; pp=5 → A=0
    //   k=3: h=0, A += 0; pp=4 → A=0
    //   k=4: h=2, A += (2*4)%10=8; pp=3 → A=8
    //   k=5: h=5, A += (5*3)%10=5; pp=2 → A=13
    //   k=6: h=0, A += 0; pp=1 → A=13
    //   k=7: h=0, A += 0; pp=0 → A=13
    //   BD = (10 - 13%10)%10 = (10-3)%10 = 7
    //
    //   checkDigit = (3+7)%10 = 0
    //   ATM code = "194326060800001" + "0" = "1943260608000010"

    [Fact]
    public void BuildAtmCode_KnownInputs_ReturnsExpected()
    {
        var date = new DateOnly(2026, 6, 8);
        var result = OrderService.BuildAtmCode("1943", date, 1, 2500);
        Assert.Equal(16, result.Length);
        Assert.Equal("1943260608000010", result);
    }

    [Fact]
    public void BuildAtmCode_AlwaysProduces16Chars()
    {
        var date = new DateOnly(2026, 12, 31);
        var result = OrderService.BuildAtmCode("1943", date, 99999, 12345);
        Assert.Equal(16, result.Length);
    }
}
