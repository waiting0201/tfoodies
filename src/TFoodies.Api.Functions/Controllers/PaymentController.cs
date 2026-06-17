using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using TFoodies.Api.Functions.Router;
using TFoodies.Application.Abstractions;
using TFoodies.Domain.Enums;
using TFoodies.Infrastructure.Payments.Fisc;

namespace TFoodies.Api.Functions.Controllers;

/// <summary>
/// 財金 FISC FOCAS_WEBPOS 信用卡金流端點（對齊舊系統 + 技術手冊 v2.7）。
///   POST /store/payment/create  — 取得刷卡 form 欄位（前端 auto-submit 至財金刷卡頁）
///   POST /store/payment/return  — AuthResURL：持卡人刷卡後財金以 form 導回，處理後 302 回前台
///   POST /store/payment/notify  — 主動通知：財金背景 POST AuthResp 字串（補償，冪等）
/// 全部公開，不需 JWT。
/// </summary>
public sealed class PaymentController
{
    private readonly IOrderService _orders;
    private readonly IPaymentCompletionService _completion;
    private readonly FiscOptions _fisc;

    public PaymentController(
        IOrderService orders, IPaymentCompletionService completion, IOptions<FiscOptions> fisc)
    {
        _orders = orders;
        _completion = completion;
        _fisc = fisc.Value;
    }

    // POST /store/payment/create
    public async Task<IActionResult> CreatePayment(RouteContext ctx)
    {
        var ct = ctx.Request.HttpContext.RequestAborted;

        var body = await ctx.TryReadBodyAsync<CreatePaymentRequest>(ct);
        if (body is null || string.IsNullOrWhiteSpace(body.OrderCode))
            return ctx.BadRequest("缺少 orderCode 欄位。");

        var summary = await _orders.GetOrderAsync(body.OrderCode.Trim(), ct);
        if (summary is null) return ctx.NotFound("找不到該訂單");

        if (summary.PayType != PayType.CreditCard)
            return ctx.BadRequest("此訂單非信用卡付款，無法發起刷卡。");
        if (summary.PayStatus != PayStatus.Unpaid)
            return ctx.Conflict("訂單已付款或目前狀態不可發起刷卡。");

        // 多網域服務：把使用者結帳所在的 store 網域帶進 AuthResURL 的 query，刷卡返回時據以同網域導回
        // （見 Return）。只在 origin 通過白名單時才帶（防把可疑網域塞進 FISC 表單）；FISC 若不保留 query
        // string，Return 會自動退回設定的 StoreSuccessUrl，故為「安全網」設計、無退步風險。
        var origin = ResolveAllowedOrigin(body.ReturnOrigin, ctx.Request.Headers["Origin"].ToString());
        var authResUrl = origin.Length == 0
            ? _fisc.AuthResUrl
            : $"{_fisc.AuthResUrl}?origin={Uri.EscapeDataString(origin)}";

        // WEBPOS hidden 欄位（手冊 3.1.1）。purchAmt 由後端權威計算，避免前端竄改。store 與後台共用 helper。
        var fields = FiscWebpos.BuildFields(summary, _fisc, authResUrl);
        return ctx.Ok(new CreatePaymentResponse(_fisc.ActionUrl, fields));
    }

    // POST /store/payment/return（AuthResURL）— 前台刷卡返回，導回前台結果頁
    public async Task<IActionResult> Return(RouteContext ctx)
    {
        var ct = ctx.Request.HttpContext.RequestAborted;
        var result = await CompleteFromFormAsync(ctx, ct);
        // 動態回跳：create 時帶進 query 的使用者結帳網域，經白名單再驗證後同網域導回（避免多網域跨域漏單）；
        // 不在白名單 / FISC 未保留 query → 退回設定的 StoreSuccessUrl（最壞=現狀，並防 open redirect）。
        var origin = ResolveAllowedOrigin(ctx.Request.Query["origin"].ToString());
        var successUrl = origin.Length == 0 ? _fisc.StoreSuccessUrl : $"{origin}{_fisc.StoreSuccessPath}";
        return RedirectToResultPage(successUrl, result);
    }

    // POST /store/payment/return-admin（後台線上刷卡的 AuthResURL）— 導回後台訂單詳情頁
    // 後台詳情頁為 path 參數（/admin/orders/{code}），與前台 query 式結果頁不同。
    public async Task<IActionResult> ReturnAdmin(RouteContext ctx)
    {
        var ct = ctx.Request.HttpContext.RequestAborted;
        var result = await CompleteFromFormAsync(ctx, ct);
        var paid = result.IsSuccess ? "1" : "0";
        var url = $"{_fisc.AdminSuccessUrl.TrimEnd('/')}/{Uri.EscapeDataString(result.Lidm)}?paid={paid}";
        return new RedirectResult(url);
    }

    // 從候選來源（依序取第一個非空）正規化出 origin，且必須在 Fisc 白名單內才回傳，否則回空字串。
    // 同時用於 create（決定是否帶 query）與 return（決定是否同網域導回）——兩端都驗證，防 open redirect。
    private string ResolveAllowedOrigin(params string?[] candidates)
    {
        foreach (var c in candidates)
        {
            var o = FiscOptions.NormalizeOrigin(c);
            if (o.Length > 0 && _fisc.AllowedStoreOriginSet.Contains(o)) return o;
        }
        return "";
    }

    // 解析財金 form 回傳 + 冪等標記已付款（store / admin 返回共用，差別僅在最終 redirect 目標）。
    private async Task<WebposResult> CompleteFromFormAsync(RouteContext ctx, CancellationToken ct)
    {
        var form = await ReadFormSafeAsync(ctx, ct);
        var result = ParseFields(form);
        if (result.IsSuccess && !string.IsNullOrEmpty(result.Lidm))
            await _completion.MarkPaidAsync(result.Lidm, result.LastPan4, result.TxnRef, ct: ct);
        return result;
    }

    // 無論成功失敗都導回結果頁，由前端呈現付款結果。
    private static IActionResult RedirectToResultPage(string baseUrl, WebposResult result)
    {
        var paid = result.IsSuccess ? "1" : "0";
        var url = $"{baseUrl}?code={Uri.EscapeDataString(result.Lidm)}&paid={paid}";
        return new RedirectResult(url);
    }

    // POST /store/payment/notify（主動通知，AuthResp 字串）
    public async Task<IActionResult> Notify(RouteContext ctx)
    {
        var ct = ctx.Request.HttpContext.RequestAborted;

        var form = await ReadFormSafeAsync(ctx, ct);
        var authResp = form.GetValueOrDefault("AuthResp");
        if (string.IsNullOrEmpty(authResp))
            authResp = await ReadRawBodyAsync(ctx, ct); // 後援：body 即 AuthResp 字串

        var result = ParseAuthResp(authResp);
        if (result.IsSuccess && !string.IsNullOrEmpty(result.Lidm))
            await _completion.MarkPaidAsync(result.Lidm, result.LastPan4, result.TxnRef, ct: ct);

        // 財金期待 http 200，未回 200 會重試最多 3 次。
        return ctx.Ok(new { received = true, orderNumber = result.Lidm, paid = result.IsSuccess });
    }

    // ── 解析 ──────────────────────────────────────────────────────────────────────

    // status=="0" 且 authCode 非空 = 授權成功（手冊 3.1.2 / 3.4.3）。
    private static WebposResult ParseFields(IReadOnlyDictionary<string, string> f)
    {
        var status   = f.GetValueOrDefault("status");
        var authCode = f.GetValueOrDefault("authCode");
        var lidm     = f.GetValueOrDefault("lidm") ?? "";
        var lastPan4 = f.GetValueOrDefault("lastPan4");
        var xid      = f.GetValueOrDefault("xid");

        var success = status == "0" && !string.IsNullOrWhiteSpace(authCode);
        var txnRef  = $"FISC authCode:{authCode} xid:{xid}";
        return new WebposResult(success, lidm, lastPan4, txnRef);
    }

    // 主動通知字串：AuthResp={status=0, authCode=123456, lidm=..., lastPan4=9104, ...}
    private static WebposResult ParseAuthResp(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return new WebposResult(false, "", null, "");

        var s = raw.Trim();
        var eq = s.IndexOf('=');
        if (s.StartsWith("AuthResp", StringComparison.OrdinalIgnoreCase) && eq >= 0)
            s = s[(eq + 1)..];
        s = s.Trim().Trim('{', '}');

        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in s.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var i = pair.IndexOf('=');
            if (i <= 0) continue;
            dict[pair[..i].Trim()] = pair[(i + 1)..].Trim();
        }
        return ParseFields(dict);
    }

    private static async Task<IReadOnlyDictionary<string, string>> ReadFormSafeAsync(RouteContext ctx, CancellationToken ct)
    {
        try
        {
            if (!ctx.Request.HasFormContentType) return new Dictionary<string, string>();
            var form = await ctx.Request.ReadFormAsync(ct);
            return form.ToDictionary(kv => kv.Key, kv => kv.Value.ToString());
        }
        catch
        {
            return new Dictionary<string, string>();
        }
    }

    private static async Task<string> ReadRawBodyAsync(RouteContext ctx, CancellationToken ct)
    {
        try
        {
            using var reader = new StreamReader(ctx.Request.Body);
            return await reader.ReadToEndAsync(ct);
        }
        catch
        {
            return "";
        }
    }

    // ── DTOs ──────────────────────────────────────────────────────────────────────

    // ReturnOrigin：前端帶入使用者結帳所在的 store 網域（window.location.origin），供多網域同網域導回。
    private sealed record CreatePaymentRequest(string? OrderCode, string? ReturnOrigin);
    private sealed record CreatePaymentResponse(string ActionUrl, IReadOnlyDictionary<string, string> Fields);
    private sealed record WebposResult(bool IsSuccess, string Lidm, string? LastPan4, string TxnRef);
}
