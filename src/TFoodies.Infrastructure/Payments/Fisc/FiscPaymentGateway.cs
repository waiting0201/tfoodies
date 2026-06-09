using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using TFoodies.Application.Abstractions;
using TFoodies.Domain.Common;

namespace TFoodies.Infrastructure.Payments.Fisc;

/// <summary>
/// 財金網路收單共用系統 特店 API 2.0 (v1.2.8) 實作。
/// 負責：
///   - 組建 HMAC-SHA256 verifyCode + AES-GCM 欄位加密
///   - 以 HTTPS POST 呼叫財金端點
///   - 解析/驗章回應
///   - 解析 NotifyURL callback
///
/// 依賴 FiscMessageCodec（純加解密）+ FiscOptions（設定）。
/// </summary>
public sealed class FiscPaymentGateway : IPaymentGateway
{
    private readonly FiscOptions _opts;
    private readonly FiscMessageCodec _codec;
    private readonly HttpClient _http;

    public FiscPaymentGateway(IOptions<FiscOptions> opts, FiscMessageCodec codec, HttpClient http)
    {
        _opts = opts.Value;
        _codec = codec;
        _http = http;
    }

    // ── CreateAsync ───────────────────────────────────────────────────────────────

    public async Task<Result<PaymentInit>> CreateAsync(PaymentRequest request, CancellationToken ct = default)
    {
        var fields = BuildOrderFields(request);
        fields["verifyCode"] = _codec.BuildVerifyCode(fields);

        var response = await PostAsync("orderCreation", fields, ct);
        if (response is null)
            return new Error("FISC_ERROR", "財金 API 無回應");

        if (!_codec.VerifyVerifyCode(response, response.GetValueOrDefault("verifyCode") ?? ""))
            return new Error("FISC_SIGNATURE", "財金回應驗章失敗");

        var respCode = response.GetValueOrDefault("responseCode");
        if (respCode != "00")
            return new Error("FISC_DECLINED", $"財金拒絕：{respCode}");

        var redirectUrl = response.GetValueOrDefault("redirectUrl") ?? _opts.BaseUrl;
        return new PaymentInit(request.OrderNumber, redirectUrl, response);
    }

    // ── QueryAsync ────────────────────────────────────────────────────────────────

    public async Task<Result<PaymentQuery>> QueryAsync(string merchantOrderNo, CancellationToken ct = default)
    {
        var fields = new Dictionary<string, string>
        {
            ["merchantId"] = _opts.MerchantId,
            ["terminalId"] = _opts.TerminalId,
            ["merchantOrderNo"] = merchantOrderNo,
        };
        fields["verifyCode"] = _codec.BuildVerifyCode(fields);

        var response = await PostAsync("orderQuery", fields, ct);
        if (response is null)
            return new Error("FISC_ERROR", "財金 API 無回應");

        var outcome = ParseOutcome(response.GetValueOrDefault("orderStatus"));
        var refund = int.TryParse(response.GetValueOrDefault("cumulativeRefundAmount"), out var r) ? r : 0;
        return new PaymentQuery(merchantOrderNo, outcome, refund);
    }

    // ── RefundAsync ───────────────────────────────────────────────────────────────

    public async Task<Result<RefundResult>> RefundAsync(RefundRequest request, CancellationToken ct = default)
    {
        var fields = new Dictionary<string, string>
        {
            ["merchantId"]         = _opts.MerchantId,
            ["terminalId"]         = _opts.TerminalId,
            ["acqBank"]            = _opts.AcqBank,
            ["merchantOrderNo"]    = request.RefundOrderNumber,
            ["oriMerchantOrderNo"] = request.OriginalOrderNumber,
            ["amount"]             = request.Amount.ToString(),
            ["cardNo"]             = request.CardNumber,
        };
        fields["verifyCode"] = _codec.BuildVerifyCode(fields);

        var response = await PostAsync("orderRefund", fields, ct);
        if (response is null)
            return new Error("FISC_ERROR", "財金 API 無回應");

        var respCode = response.GetValueOrDefault("responseCode") ?? "XX";
        var approved = respCode == "00";
        var srrn = response.GetValueOrDefault("srrn");
        return new RefundResult(approved, respCode, srrn);
    }

    // ── ParseNotify ───────────────────────────────────────────────────────────────

    public PaymentNotice ParseNotify(IReadOnlyDictionary<string, string> form)
    {
        var received = form.GetValueOrDefault("verifyCode") ?? "";
        if (!_codec.VerifyVerifyCode(form, received))
            throw new InvalidOperationException("財金 notify 驗章失敗。");

        var orderNo     = form.GetValueOrDefault("merchantOrderNo") ?? "";
        var outcome     = ParseOutcome(form.GetValueOrDefault("orderStatus"));
        var responseCode= form.GetValueOrDefault("responseCode") ?? "";
        var srrn        = form.GetValueOrDefault("srrn");
        var lastPan4    = form.GetValueOrDefault("cardNo4No");
        var txId        = form.GetValueOrDefault("transactionId") ?? "";

        return new PaymentNotice(orderNo, outcome, responseCode, srrn, lastPan4, txId);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────────

    private Dictionary<string, string> BuildOrderFields(PaymentRequest request) =>
        new()
        {
            ["merchantId"]    = _opts.MerchantId,
            ["terminalId"]    = _opts.TerminalId,
            ["acqBank"]       = _opts.AcqBank,
            ["merchantOrderNo"] = request.OrderNumber,
            ["amount"]        = request.Amount.ToString(),
            ["returnUrl"]     = request.ReturnUrl,
            ["notifyUrl"]     = request.NotifyUrl,
            ["is3d"]          = request.Require3ds ? "1" : "0",
        };

    private async Task<Dictionary<string, string>?> PostAsync(
        string endpoint, Dictionary<string, string> fields, CancellationToken ct)
    {
        try
        {
            var url = $"{_opts.BaseUrl.TrimEnd('/')}/{endpoint}";
            var resp = await _http.PostAsJsonAsync(url, fields, ct);
            if (!resp.IsSuccessStatusCode) return null;
            return await resp.Content.ReadFromJsonAsync<Dictionary<string, string>>(
                cancellationToken: ct);
        }
        catch
        {
            return null;
        }
    }

    private static PaymentOutcome ParseOutcome(string? status) => status switch
    {
        "9" => PaymentOutcome.Authorized,
        "8" => PaymentOutcome.Refunded,
        "3" => PaymentOutcome.Cancelled,
        "X" => PaymentOutcome.Failed,
        _   => PaymentOutcome.Unknown,
    };
}
