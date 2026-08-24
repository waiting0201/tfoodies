using TFoodies.Infrastructure.Payments.Fisc;

namespace TFoodies.Infrastructure.Tests.Payments;

// 顧客回報「刷卡沒成功」時，errcode/errDesc 是唯一能回答「為什麼」的資料——這些欄位一旦
// 又被解析漏掉，失敗原因就會再次消失無蹤，故以手冊原文樣本鎖住行為。
public class FiscWebposParserTests
{
    // 手冊 v2.7 §3.1.2 主動通知範例（原文照抄）：status=8 發卡行拒絕、errcode=30。
    private const string ManualFailureSample =
        "AuthResp={errcode=30, authCode=null, authRespTime=20200219173137, lastPan4=9104, amtExp=0, " +
        "xid=O-OBJECT-20200219173058.811-0025, errDesc=授權失敗, lidm=FOCAS-T200219173057, authAmt=200, " +
        "merID=829, currency=901, cardBrand=VISA, pan=480254******9104, status=8}";

    [Fact]
    public void ParseAuthResp_manual_failure_sample_keeps_reason()
    {
        var r = FiscWebposParser.ParseAuthResp(ManualFailureSample);

        Assert.False(r.IsSuccess);
        Assert.Equal("FOCAS-T200219173057", r.Lidm);
        Assert.Equal("8", r.Status);
        Assert.Equal("30", r.ErrCode);
        Assert.Equal("授權失敗", r.ErrDesc);
        Assert.Equal("9104", r.LastPan4);
        Assert.Equal("VISA", r.CardBrand);
        Assert.Equal(200, r.AuthAmt);
        Assert.Equal("O-OBJECT-20200219173058.811-0025", r.Xid);
    }

    [Fact]
    public void ParseAuthResp_treats_literal_null_as_missing()
    {
        // 財金無值欄位送字面上的 "null"（範例 authCode=null）；存進 DB 會變成假的授權碼。
        var r = FiscWebposParser.ParseAuthResp(ManualFailureSample);
        Assert.Null(r.AuthCode);
    }

    [Fact]
    public void ParseAuthResp_success_marks_paid()
    {
        var r = FiscWebposParser.ParseAuthResp(
            "AuthResp={errcode=00, authCode=123456, lastPan4=9104, xid=O-OBJECT-1, " +
            "errDesc=, lidm=O20260822001, authAmt=1250, cardBrand=JCB, status=0}");

        Assert.True(r.IsSuccess);
        Assert.Equal("O20260822001", r.Lidm);
        Assert.Equal("123456", r.AuthCode);
        Assert.Equal("00", r.ErrCode);
        Assert.Null(r.ErrDesc);              // 空字串不該存成空白列
        Assert.Equal("FISC authCode:123456 xid:O-OBJECT-1", r.TxnRef);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("garbage-without-braces")]
    public void ParseAuthResp_rejects_unusable_input(string? raw)
    {
        var r = FiscWebposParser.ParseAuthResp(raw);
        Assert.False(r.IsSuccess);
        Assert.Equal("", r.Lidm);
    }

    [Fact]
    public void ParseForm_requires_both_status_zero_and_authcode()
    {
        // status=0 但沒有授權碼 → 不可視為成功（成功判定條件必須兩者兼具）。
        var r = FiscWebposParser.ParseForm(new Dictionary<string, string>
        {
            ["status"] = "0", ["authCode"] = "", ["lidm"] = "O20260822001",
        });
        Assert.False(r.IsSuccess);
    }

    [Fact]
    public void ParseForm_reads_reason_fields_case_insensitively()
    {
        // 導回 form 由 FiscFormReader 以大小寫不敏感字典讀入；欄位名飄移不該讓診斷資料落空。
        var form = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["STATUS"] = "8", ["ERRCODE"] = "51", ["ERRDESC"] = "發卡銀行：消費額度不足",
            ["LIDM"] = "O20260822001", ["LASTPAN4"] = "1234",
        };

        var r = FiscWebposParser.ParseForm(form);

        Assert.False(r.IsSuccess);
        Assert.Equal("51", r.ErrCode);
        Assert.Equal("發卡銀行：消費額度不足", r.ErrDesc);
        Assert.Equal("1234", r.LastPan4);
    }
}
