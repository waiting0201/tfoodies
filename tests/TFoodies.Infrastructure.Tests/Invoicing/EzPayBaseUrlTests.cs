using TFoodies.Infrastructure.Invoicing.EzPay;

namespace TFoodies.Infrastructure.Tests.Invoicing;

// 防呆：EzPay__BaseUrl 曾被誤設為 .../API/invoice_issue（開立端點），導致每個呼叫（含作廢）都被
// ezPay 依 /invoice_issue 路由到「開立」端點。NormalizeBaseUrl 須把誤含的端點名剝除，確保
// issue/invalid/allowance 各自打對端點。錯了會讓作廢/折讓永遠打到開立 API。
public class EzPayBaseUrlTests
{
    [Theory]
    // 正確的 base：原樣（僅去尾斜線）
    [InlineData("https://inv.ezpay.com.tw/API", "https://inv.ezpay.com.tw/API")]
    [InlineData("https://inv.ezpay.com.tw/API/", "https://inv.ezpay.com.tw/API")]
    [InlineData("https://cinv.ezpay.com.tw/Api", "https://cinv.ezpay.com.tw/Api")]
    // 誤含端點名：剝除，還原成 base
    [InlineData("https://inv.ezpay.com.tw/API/invoice_issue", "https://inv.ezpay.com.tw/API")]
    [InlineData("https://inv.ezpay.com.tw/API/invoice_invalid", "https://inv.ezpay.com.tw/API")]
    [InlineData("https://inv.ezpay.com.tw/API/allowance_issue", "https://inv.ezpay.com.tw/API")]
    [InlineData("https://inv.ezpay.com.tw/API/invoice_issue/", "https://inv.ezpay.com.tw/API")]
    public void NormalizeBaseUrl_strips_accidental_endpoint_suffix(string input, string expected)
        => Assert.Equal(expected, EzPayInvoiceService.NormalizeBaseUrl(input));
}
