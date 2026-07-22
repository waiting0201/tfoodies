using TFoodies.Infrastructure.Payments;

namespace TFoodies.Infrastructure.Tests.Payments;

// 作廢後重開時 ezPay 不允許 MerchantOrderNo 重複；開立與作廢須以「開立序」推導出相同的號，
// 否則重開撞號、或作廢送錯號被 ezPay 退。鎖住此規則。
public class MerchantOrderNoTests
{
    [Theory]
    [InlineData("O20260722004", 1, "O20260722004")]      // 首開 = orderCode
    [InlineData("O20260722004", 2, "O20260722004R1")]    // 第 2 次（第 1 次重開）
    [InlineData("O20260722004", 3, "O20260722004R2")]    // 第 3 次
    [InlineData("20180104001", 1, "20180104001")]        // 舊制訂單編號亦適用
    public void MerchantOrderNoFor_uses_orderCode_first_then_R_suffix(string orderCode, int ordinal, string expected)
        => Assert.Equal(expected, PaymentCompletionService.MerchantOrderNoFor(orderCode, ordinal));

    [Fact]
    public void MerchantOrderNoFor_ordinal_zero_or_negative_falls_back_to_orderCode()
    {
        Assert.Equal("O1", PaymentCompletionService.MerchantOrderNoFor("O1", 0));
        Assert.Equal("O1", PaymentCompletionService.MerchantOrderNoFor("O1", -3));
    }
}
