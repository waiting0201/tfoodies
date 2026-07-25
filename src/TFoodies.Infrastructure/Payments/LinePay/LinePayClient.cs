using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TFoodies.Application.Abstractions;
using TFoodies.Domain.Common;

namespace TFoodies.Infrastructure.Payments.LinePay;

/// <summary>
/// LINE Pay Online API v4 實作。
///
/// wire format：
///   - POST application/json，簽章標頭 X-LINE-ChannelId / X-LINE-Authorization-Nonce / X-LINE-Authorization
///   - 回應 JSON：returnCode / returnMessage / info
///   - returnCode "0000" = 成功；"1172" = 該筆交易先前已完成（視同成功，保 confirm 回跳被重放時的冪等）
///
/// v4 vs v3：簽章方式、標頭、request/confirm 的 body 欄位**完全相同**，差別只有路徑前綴。
/// v4 為台灣《電子支付機構管理條例》而生，confirm 回應多一個 <c>info.paymentProvider</c>（TSP/EPI），
/// 但官方註明 Online API 一律回 TSP（EPI 交易不適用線上情境），故本實作不讀該欄位。
///
/// package 刻意只送「一個 package、一個 product」，金額 = 應付總額，不拆運費/折扣
/// （對齊財金 WEBPOS 只送 purchAmt 的作法，避開 Orders.total 語意為純商品小計的坑）。
/// </summary>
public sealed class LinePayClient : ILinePayClient
{
    /// <summary>API 版本前綴。升版時只需改這裡（v3↔v4 的請求/回應結構相同）。</summary>
    private const string ApiVersion = "v4";

    private const string RequestPath = $"/{ApiVersion}/payments/request";
    private const string SuccessCode = "0000";

    /// <summary>該筆交易已完成（重複 confirm）。視同成功，避免回跳被重放時誤報失敗。</summary>
    private const string AlreadyCompletedCode = "1172";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly LinePayOptions _opts;
    private readonly HttpClient _http;
    private readonly ILogger<LinePayClient> _logger;

    public LinePayClient(IOptions<LinePayOptions> opts, HttpClient http, ILogger<LinePayClient> logger)
    {
        _opts = opts.Value;
        _http = http;
        _logger = logger;

        if (_opts.TimeoutSeconds > 0)
            _http.Timeout = TimeSpan.FromSeconds(_opts.TimeoutSeconds);
    }

    public bool IsEnabled => _opts.IsUsable;

    // ── RequestAsync ──────────────────────────────────────────────────────────────

    public async Task<Result<LinePayReservation>> RequestAsync(
        LinePayReserveRequest request, CancellationToken ct = default)
    {
        if (!IsEnabled) return new Error("LINEPAY_DISABLED", "LINE Pay 目前未啟用。");
        if (request.Amount <= 0) return Error.Validation("付款金額必須大於 0。");

        var payload = new
        {
            amount = request.Amount,
            currency = _opts.Currency,
            orderId = request.OrderId,
            packages = new[]
            {
                new
                {
                    id = request.OrderId,
                    amount = request.Amount,
                    name = request.ProductName,
                    products = new[]
                    {
                        new { name = request.ProductName, quantity = 1, price = request.Amount },
                    },
                },
            },
            redirectUrls = new
            {
                confirmUrl = request.ConfirmUrl,
                cancelUrl = request.CancelUrl,
            },
            options = new
            {
                // capture=true：confirm 時一併請款（不做預授權後另行請款）
                payment = new { capture = true },
            },
        };

        var result = await CallAsync(RequestPath, payload, ct);
        if (result.IsFailure) return result.Error;

        var info = result.Value;
        var transactionId = ReadTransactionId(info);
        var web = info.TryGetProperty("paymentUrl", out var urls) && urls.TryGetProperty("web", out var w)
            ? w.GetString()
            : null;
        var app = info.TryGetProperty("paymentUrl", out var urls2) && urls2.TryGetProperty("app", out var a)
            ? a.GetString()
            : null;

        if (string.IsNullOrEmpty(transactionId) || string.IsNullOrEmpty(web))
            return new Error("LINEPAY_BAD_RESPONSE", "LINE Pay 未回傳付款網址。");

        _logger.LogInformation(
            "LINE Pay request 成功 orderId={OrderId} transactionId={TransactionId}",
            request.OrderId, transactionId);

        return new LinePayReservation(transactionId, web, app);
    }

    // ── ConfirmAsync ──────────────────────────────────────────────────────────────

    public async Task<Result<LinePayConfirmation>> ConfirmAsync(
        string transactionId, int amount, CancellationToken ct = default)
    {
        if (!IsEnabled) return new Error("LINEPAY_DISABLED", "LINE Pay 目前未啟用。");
        if (string.IsNullOrWhiteSpace(transactionId)) return Error.Validation("缺少 transactionId。");

        // 路徑含 transactionId，簽章的 requestUri 必須與實際請求路徑一致。
        var path = $"/{ApiVersion}/payments/{Uri.EscapeDataString(transactionId)}/confirm";
        var payload = new { amount, currency = _opts.Currency };

        var result = await CallAsync(path, payload, ct);
        if (result.IsFailure)
        {
            // 1172＝已完成：付款有效，只是本次不是首次完成（回跳被重放 / 使用者重整）。
            if (result.Error.Code == $"LINEPAY_{AlreadyCompletedCode}")
            {
                _logger.LogInformation(
                    "LINE Pay confirm 交易已完成（1172），視同成功 transactionId={TransactionId}", transactionId);
                return new LinePayConfirmation(transactionId, null, AlreadyCompleted: true);
            }
            return result.Error;
        }

        var orderId = result.Value.TryGetProperty("orderId", out var o) ? o.GetString() : null;
        return new LinePayConfirmation(transactionId, orderId, AlreadyCompleted: false);
    }

    // ── 共用呼叫 ──────────────────────────────────────────────────────────────────

    /// <summary>簽章 + POST + 解析。成功回傳 info 物件；失敗回傳帶 returnCode 的 Error。</summary>
    private async Task<Result<JsonElement>> CallAsync(string path, object payload, CancellationToken ct)
    {
        // ⚠️ 簽章必須對「實際送出的 body 字串」計算，故先序列化成字串再重複使用同一份。
        var body = JsonSerializer.Serialize(payload, JsonOpts);
        var nonce = LinePaySigner.NewNonce();
        var signature = LinePaySigner.Sign(_opts.ChannelSecret, path, body, nonce);

        var url = $"{_opts.BaseUrl.TrimEnd('/')}{path}";
        using var req = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
        req.Headers.TryAddWithoutValidation("X-LINE-ChannelId", _opts.ChannelId);
        req.Headers.TryAddWithoutValidation("X-LINE-Authorization-Nonce", nonce);
        req.Headers.TryAddWithoutValidation("X-LINE-Authorization", signature);
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        try
        {
            _logger.LogInformation("LINE Pay 呼叫 {Path} → {Url}", path, url);

            var resp = await _http.SendAsync(req, ct);
            var raw = await resp.Content.ReadAsStringAsync(ct);

            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("LINE Pay {Path} HTTP {StatusCode}", path, (int)resp.StatusCode);
                return new Error("LINEPAY_HTTP", $"HTTP {(int)resp.StatusCode}");
            }

            using var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;
            var returnCode = root.TryGetProperty("returnCode", out var rc) ? rc.GetString() ?? "" : "";
            var returnMessage = root.TryGetProperty("returnMessage", out var rm) ? rm.GetString() ?? "" : "";

            if (returnCode != SuccessCode)
            {
                _logger.LogWarning(
                    "LINE Pay {Path} 失敗 returnCode={ReturnCode} returnMessage={ReturnMessage}",
                    path, returnCode, returnMessage);
                return new Error($"LINEPAY_{returnCode}", LinePayErrors.Describe(returnCode, returnMessage));
            }

            // JsonDocument 會在離開 using 後失效，故複製一份 info 出去。
            var info = root.TryGetProperty("info", out var i) ? i.Clone() : default;
            return info;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            _logger.LogError(ex, "LINE Pay {Path} 呼叫失敗", path);
            return new Error("LINEPAY_UNAVAILABLE", "LINE Pay 服務暫時無法使用，請稍後再試。");
        }
    }

    /// <summary>transactionId 在 LINE Pay 回應中是 JSON number（可超過 int 範圍），一律轉字串使用。</summary>
    private static string? ReadTransactionId(JsonElement info)
    {
        if (info.ValueKind != JsonValueKind.Object) return null;
        if (!info.TryGetProperty("transactionId", out var t)) return null;
        return t.ValueKind switch
        {
            JsonValueKind.Number => t.GetInt64().ToString(),
            JsonValueKind.String => t.GetString(),
            _ => null,
        };
    }
}
