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
        return await CallAsync("invoice_issue", inner, ct);   // EZP_INVI_1.2.2 §四-(一) 串接網址：/Api/invoice_issue
    }

    // ── AllowanceAsync ────────────────────────────────────────────────────────────

    public async Task<Result<InvoiceResult>> AllowanceAsync(
        AllowanceRequest request, CancellationToken ct = default)
    {
        var inner = new List<KeyValuePair<string, string>>
        {
            kv("RespondType",      "JSON"),
            kv("Version",          "1.3"),   // 開立折讓固定帶 1.3（EZP_INVI_1.2.2 §六-(一)）
            kv("TimeStamp",        DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString()),
            kv("InvoiceNo",        request.InvoiceNo),
            kv("MerchantOrderNo",  request.MerchantOrderNo),
            kv("ItemName",         string.Join("|", request.Items.Select(i => i.Name))),
            kv("ItemCount",        string.Join("|", request.Items.Select(i => i.Count.ToString()))),
            kv("ItemUnit",         string.Join("|", request.Items.Select(i => i.Unit))),
            kv("ItemPrice",        string.Join("|", request.Items.Select(i => i.Price.ToString()))),
            kv("ItemAmt",          string.Join("|", request.Items.Select(i => i.Amount.ToString()))),
            // ItemTaxAmt 為必填；本系統 ItemPrice 帶含稅金額，依手冊此時稅額帶 0（申報時不抵扣該項營業稅額）。
            kv("ItemTaxAmt",       string.Join("|", request.Items.Select(_ => "0"))),
            kv("TotalAmt",         request.TotalAmt.ToString()),
            kv("Status",           "1"),     // 1=立即確認折讓（必填，EZP_INVI_1.2.2 §六-(一)）
        };
        if (!string.IsNullOrEmpty(request.BuyerEmail)) inner.Add(kv("BuyerEmail", request.BuyerEmail));

        return await CallAsync("allowance_issue", inner, ct);   // EZP_INVI_1.2.2 §六-(一) 串接網址：/Api/allowance_issue
    }

    // ── VoidAsync ─────────────────────────────────────────────────────────────────

    public async Task<Result<InvoiceResult>> VoidAsync(
        string invoiceNumber, string reason, CancellationToken ct = default)
    {
        // ⚠️ 比照舊系統 AjaxController/CancelInv：作廢只送 InvoiceNumber + InvalidReason（外加
        //    RespondType/Version/TimeStamp），且 RespondType 用 "String"（非 JSON）。
        //    實測此 ezPay 帳號在 RespondType=JSON 時會對 invoice_invalid 套用「開立」等級的嚴格驗證，
        //    逐一索取 MerchantOrderNo/BuyerName/Category…；改回 String（舊系統多年可用的請求）即免。
        var inner = new List<KeyValuePair<string, string>>
        {
            kv("RespondType",   "String"),
            kv("Version",       "1.0"),            // 作廢發票固定帶 1.0（EZP_INVI_1.2.2 §五-(一)）
            kv("TimeStamp",     DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString()),
            kv("InvoiceNumber", invoiceNumber),    // 作廢 API 參數名為 InvoiceNumber（非 InvoiceNo）
            kv("InvalidReason", reason),
        };

        // RespondType=String → 回應為 query-string（非 JSON），以 jsonResponse:false 解析。
        return await CallAsync("invoice_invalid", inner, ct, jsonResponse: false);   // EZP_INVI_1.2.2 §五-(一) 串接網址：/Api/invoice_invalid
    }

    // ── Helpers ───────────────────────────────────────────────────────────────────

    private List<KeyValuePair<string, string>> BuildIssueParams(InvoiceRequest req, IssueMode mode)
    {
        var p = new List<KeyValuePair<string, string>>
        {
            kv("RespondType",    "JSON"),
            kv("Version",        "1.5"),   // 開立發票固定帶 1.5（EZP_INVI_1.2.2 §四-(一)）；舊系統用 1.4，1.0 會被 ezPay 拒絕
            kv("TimeStamp",      DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString()),
            kv("TransNum",       ""),
            kv("MerchantOrderNo",req.MerchantOrderNo),
            kv("Status",         ((int)mode).ToString()),
            // Category 與下方 BuyerUbn 參數須一致：有統編才算 B2B，否則 B2C。
            // （用 IsNullOrWhiteSpace 一致判斷，避免空字串/空白被判成 B2B 卻無 BuyerUbn 而被 ezPay 以「統編沒有」拒絕。）
            kv("Category",       !string.IsNullOrWhiteSpace(req.BuyerUbn) ? "B2B" : "B2C"),
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
        // ⚠️ ezPay 參數名大小寫敏感，統編欄位為 BuyerUBN（全大寫，同舊系統 AjaxController）。
        // 曾誤植為 BuyerUbn，導致 ezPay 收不到統編、卻因 Category=B2B 而回「買受人統編不可為空白」。
        if (!string.IsNullOrWhiteSpace(req.BuyerUbn)) p.Add(kv("BuyerUBN", req.BuyerUbn.Trim()));

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
        string endpoint, IEnumerable<KeyValuePair<string, string>> inner, CancellationToken ct,
        bool jsonResponse = true)
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

            var raw = await resp.Content.ReadAsStringAsync(ct);

            // RespondType=String（如作廢）→ 回應為 query-string（同舊系統 ParseQueryString）；
            // 只需 Status/Message 判定成敗（作廢不需 Result 內容）。
            if (!jsonResponse)
            {
                var q = ParseQueryString(raw);
                var qStatus = q.GetValueOrDefault("Status", "");
                var qMessage = q.GetValueOrDefault("Message", "");
                if (qStatus != "SUCCESS")
                    return new Error("EZPAY_DECLINED", string.IsNullOrEmpty(qMessage) ? raw : qMessage);
                return new InvoiceResult(Success: true, Status: qStatus, Message: qMessage,
                    InvoiceNumber: null, InvoiceTransNo: null, RandomNum: null, AllowanceNo: null, CheckCode: null);
            }

            var doc = JsonDocument.Parse(raw).RootElement;

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

    // ezPay RespondType=String 的回應是 application/x-www-form-urlencoded 的 query-string
    // （如 Status=SUCCESS&Message=%E4%BD%9C%E5%BB%A2%E6%88%90%E5%8A%9F）。
    private static Dictionary<string, string> ParseQueryString(string s)
    {
        var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in s.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var idx = pair.IndexOf('=');
            if (idx < 0) { d[Uri.UnescapeDataString(pair)] = ""; continue; }
            var k = Uri.UnescapeDataString(pair[..idx]);
            var v = Uri.UnescapeDataString(pair[(idx + 1)..].Replace('+', ' '));
            d[k] = v;
        }
        return d;
    }

    // 台灣 5% VAT：含稅 price → 稅前金額（同舊系統 round(price/1.05)）
    private static int TaxExcl(int taxIncl) => (int)Math.Round(taxIncl / 1.05m, MidpointRounding.AwayFromZero);
    private static int TaxAmount(int taxIncl) => taxIncl - TaxExcl(taxIncl);

    private static KeyValuePair<string, string> kv(string k, string v) => new(k, v);

    private static string? TryGet(JsonElement el, string key) =>
        el.TryGetProperty(key, out var p) ? p.GetString() : null;
}
