using System.Text.Json;
using Microsoft.Extensions.Options;
using TFoodies.Application.Abstractions;
using TFoodies.Domain.Common;
using TFoodies.Domain.Enums;

namespace TFoodies.Infrastructure.Invoicing.EzPay;

/// <summary>
/// ezPay/NewebPay 電子發票 API (EZP_INVI 1.2.2) 實作。
///
/// wire format：
///   - 內層參數 url-encode → AES-256-CBC → lower-hex（PostData_）
///   - POST application/x-www-form-urlencoded：MerchantID_ + PostData_
///   - 回應 JSON：Status / Message / Result（JSON-encoded 字串）
///
/// 依賴 EzPayCodec（純加解密）+ EzPayOptions（設定）。
/// </summary>
public sealed class EzPayInvoiceService : IInvoiceService
{
    private readonly EzPayOptions _opts;
    private readonly EzPayCodec _codec;
    private readonly HttpClient _http;

    public EzPayInvoiceService(IOptions<EzPayOptions> opts, EzPayCodec codec, HttpClient http)
    {
        _opts = opts.Value;
        _codec = codec;
        _http = http;
    }

    // ── IssueAsync ────────────────────────────────────────────────────────────────

    public async Task<Result<InvoiceResult>> IssueAsync(
        InvoiceRequest request, IssueMode mode, CancellationToken ct = default)
    {
        var inner = BuildIssueParams(request, mode);
        return await CallAsync("invoice/issue", inner, ct);
    }

    // ── AllowanceAsync ────────────────────────────────────────────────────────────

    public async Task<Result<InvoiceResult>> AllowanceAsync(
        AllowanceRequest request, CancellationToken ct = default)
    {
        var inner = new List<KeyValuePair<string, string>>
        {
            kv("RespondType",      "JSON"),
            kv("Version",          "1.0"),
            kv("TimeStamp",        DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString()),
            kv("InvoiceNo",        request.InvoiceNo),
            kv("MerchantOrderNo",  request.MerchantOrderNo),
            kv("ItemName",         string.Join("|", request.Items.Select(i => i.Name))),
            kv("ItemCount",        string.Join("|", request.Items.Select(i => i.Count.ToString()))),
            kv("ItemUnit",         string.Join("|", request.Items.Select(i => i.Unit))),
            kv("ItemPrice",        string.Join("|", request.Items.Select(i => i.Price.ToString()))),
            kv("ItemAmt",          string.Join("|", request.Items.Select(i => i.Amount.ToString()))),
            kv("TotalAmt",         request.TotalAmt.ToString()),
        };
        if (!string.IsNullOrEmpty(request.BuyerEmail)) inner.Add(kv("BuyerEmail", request.BuyerEmail));

        return await CallAsync("invoice/allowance", inner, ct);
    }

    // ── VoidAsync ─────────────────────────────────────────────────────────────────

    public async Task<Result<InvoiceResult>> VoidAsync(
        string invoiceNumber, string reason, CancellationToken ct = default)
    {
        var inner = new List<KeyValuePair<string, string>>
        {
            kv("RespondType",  "JSON"),
            kv("Version",      "1.0"),
            kv("TimeStamp",    DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString()),
            kv("InvoiceNo",    invoiceNumber),
            kv("InvalidReason",reason),
        };

        return await CallAsync("invoice/void", inner, ct);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────────

    private List<KeyValuePair<string, string>> BuildIssueParams(InvoiceRequest req, IssueMode mode)
    {
        var p = new List<KeyValuePair<string, string>>
        {
            kv("RespondType",    "JSON"),
            kv("Version",        "1.0"),
            kv("TimeStamp",      DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString()),
            kv("TransNum",       ""),
            kv("MerchantOrderNo",req.MerchantOrderNo),
            kv("Status",         ((int)mode).ToString()),
            kv("Category",       req.BuyerUbn != null ? "B2B" : "B2C"),
            kv("BuyerName",      req.BuyerName),
            kv("PrintFlag",      "Y"),
            kv("TaxType",        "1"),              // 應稅
            kv("TaxRate",        "5"),
            kv("Amt",            TaxExcl(req.TotalAmt).ToString()),
            kv("TaxAmt",         TaxAmount(req.TotalAmt).ToString()),
            kv("TotalAmt",       req.TotalAmt.ToString()),
            kv("ItemName",       string.Join("|", req.Items.Select(i => i.Name))),
            kv("ItemCount",      string.Join("|", req.Items.Select(i => i.Count.ToString()))),
            kv("ItemUnit",       string.Join("|", req.Items.Select(i => i.Unit))),
            kv("ItemPrice",      string.Join("|", req.Items.Select(i => i.Price.ToString()))),
            kv("ItemAmt",        string.Join("|", req.Items.Select(i => i.Amount.ToString()))),
        };

        if (!string.IsNullOrEmpty(req.BuyerEmail)) p.Add(kv("BuyerEmail", req.BuyerEmail));
        if (!string.IsNullOrEmpty(req.BuyerUbn))   p.Add(kv("BuyerUbn",   req.BuyerUbn));

        // 載具 / 捐贈
        if (req.Type == InvoiceType.Donation && !string.IsNullOrEmpty(req.LoveCode))
        {
            p.Add(kv("LoveCode", req.LoveCode));
            p.Add(kv("PrintFlag", "N"));
        }
        else if (!string.IsNullOrEmpty(req.CarrierType) && !string.IsNullOrEmpty(req.CarrierNum))
        {
            p.Add(kv("CarrierType", req.CarrierType));
            p.Add(kv("CarrierNum",  req.CarrierNum));
            p.Add(kv("PrintFlag",   "N"));
        }

        if (req.ScheduledDate.HasValue)
            p.Add(kv("InvoiceDate", req.ScheduledDate.Value.ToString("yyyy-MM-dd")));

        return p;
    }

    private async Task<Result<InvoiceResult>> CallAsync(
        string endpoint, IEnumerable<KeyValuePair<string, string>> inner, CancellationToken ct)
    {
        var postData = _codec.BuildPostData(inner);

        var form = new FormUrlEncodedContent(new[]
        {
            kv("MerchantID_", _opts.MerchantId),
            kv("PostData_",   postData),
        });

        try
        {
            var url = $"{_opts.BaseUrl.TrimEnd('/')}/{endpoint}";
            var resp = await _http.PostAsync(url, form, ct);
            if (!resp.IsSuccessStatusCode)
                return new Error("EZPAY_HTTP", $"HTTP {(int)resp.StatusCode}");

            var json = await resp.Content.ReadAsStringAsync(ct);
            var doc = JsonDocument.Parse(json).RootElement;

            var status = doc.GetProperty("Status").GetString() ?? "";
            var message = doc.GetProperty("Message").GetString() ?? "";

            if (status != "SUCCESS")
                return new Error("EZPAY_DECLINED", message);

            // Result 是 JSON-encoded 字串內的物件
            var resultJson = doc.GetProperty("Result").GetString() ?? "{}";
            var result = JsonDocument.Parse(resultJson).RootElement;

            return new InvoiceResult(
                Success: true,
                Status: status,
                Message: message,
                InvoiceNumber:   TryGet(result, "InvoiceNumber"),
                InvoiceTransNo:  TryGet(result, "InvoiceTransNo"),
                RandomNum:       TryGet(result, "RandomNum"),
                AllowanceNo:     TryGet(result, "AllowanceNo"),
                CheckCode:       TryGet(result, "CheckCode"));
        }
        catch (Exception ex)
        {
            return new Error("EZPAY_ERROR", ex.Message);
        }
    }

    // 台灣 5% VAT：含稅 price → 稅前金額（同舊系統 round(price/1.05)）
    private static int TaxExcl(int taxIncl) => (int)Math.Round(taxIncl / 1.05m, MidpointRounding.AwayFromZero);
    private static int TaxAmount(int taxIncl) => taxIncl - TaxExcl(taxIncl);

    private static KeyValuePair<string, string> kv(string k, string v) => new(k, v);

    private static string? TryGet(JsonElement el, string key) =>
        el.TryGetProperty(key, out var p) ? p.GetString() : null;
}
