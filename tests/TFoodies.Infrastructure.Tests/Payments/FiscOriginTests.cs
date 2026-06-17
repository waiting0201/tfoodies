using TFoodies.Infrastructure.Payments.Fisc;

namespace TFoodies.Infrastructure.Tests.Payments;

// 多網域刷卡動態回跳的防護核心：NormalizeOrigin + 白名單。錯了會造成 open redirect 或漏單，故鎖行為。
public class FiscOriginTests
{
    [Theory]
    [InlineData("https://www.tfoodies.com", "https://www.tfoodies.com")]
    [InlineData("https://www.tfoodies.com/", "https://www.tfoodies.com")]            // 去尾斜線
    [InlineData("https://www.tfoodies.com/Order/Success?x=1", "https://www.tfoodies.com")] // 丟路徑/query
    [InlineData("HTTPS://WWW.TFOODIES.COM", "https://www.tfoodies.com")]              // 轉小寫
    [InlineData("https://tfoodies.com:443", "https://tfoodies.com")]                 // 預設埠省略（與無埠等價）
    [InlineData("https://tfoodies.com:8443", "https://tfoodies.com:8443")]           // 非預設埠保留
    public void NormalizeOrigin_keeps_scheme_host_only(string input, string expected)
        => Assert.Equal(expected, FiscOptions.NormalizeOrigin(input));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-url")]
    [InlineData("/Order/Success")]                  // 相對路徑非絕對 URL
    [InlineData("javascript:alert(1)")]             // 非 http(s) scheme
    [InlineData("ftp://tfoodies.com")]
    public void NormalizeOrigin_rejects_invalid(string? input)
        => Assert.Equal("", FiscOptions.NormalizeOrigin(input));

    [Fact]
    public void AllowedStoreOriginSet_parses_and_normalizes_list()
    {
        var opts = new FiscOptions
        {
            AllowedStoreOrigins = "https://www.tfoodies.com; https://tfoodies.com/ ,https://www.tfoodies.com.tw;https://tfoodies.com.tw",
        };

        var set = opts.AllowedStoreOriginSet;

        Assert.Equal(4, set.Count);
        Assert.Contains("https://www.tfoodies.com", set);
        Assert.Contains("https://tfoodies.com", set);       // 尾斜線已正規化掉
        Assert.Contains("https://www.tfoodies.com.tw", set);
        Assert.Contains("https://tfoodies.com.tw", set);
        Assert.DoesNotContain("https://evil.com", set);     // 未列入即拒
    }

    [Theory]
    [InlineData("https://www.tfoodies.com/Order/Success", "/Order/Success")]
    [InlineData("https://www.tfoodies.com", "/")]            // 無路徑 → "/"
    [InlineData("", "/Order/Success")]                       // 非合法 URL → 預設路徑
    public void StoreSuccessPath_extracts_path(string successUrl, string expected)
        => Assert.Equal(expected, new FiscOptions { StoreSuccessUrl = successUrl }.StoreSuccessPath);
}
