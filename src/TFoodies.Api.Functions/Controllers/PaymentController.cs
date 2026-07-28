using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using TFoodies.Api.Functions.Helpers;
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
    private readonly IPaymentLinkService _paymentLinks;
    private readonly FiscOptions _fisc;

    public PaymentController(
        IOrderService orders, IPaymentCompletionService completion,
        IPaymentLinkService paymentLinks, IOptions<FiscOptions> fisc)
    {
        _orders = orders;
        _completion = completion;
        _paymentLinks = paymentLinks;
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
        // 財金不收 0 元（100% 折扣 + 免運會發生）：送過去只會得到錯誤頁，顧客卡在刷卡頁。
        // 擺在狀態檢查之前，訊息才會是「無須刷卡」而不是語意不符的「訂單已付款」。
        // 下單時 payable<=0 已標記為「免付款」，這裡是防禦既有訂單與直接呼叫。
        if (summary.Total + summary.Freight - summary.Discount <= 0)
            return ctx.BadRequest("本訂單應付金額為 0，無須刷卡。");
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

    // 從候選來源正規化出白名單內的 origin（防 open redirect）。同時用於 create（決定是否帶 query）
    // 與 return（決定是否同網域導回）——兩端都驗證。與收款連結共用 FiscRedirect。
    private string ResolveAllowedOrigin(params string?[] candidates)
        => FiscRedirect.ResolveAllowedOrigin(_fisc, candidates);

    // 解析財金 form 回傳 + 冪等標記已付款（store / admin 返回共用，差別僅在最終 redirect 目標）。
    private async Task<FiscWebposParser.WebposResult> CompleteFromFormAsync(RouteContext ctx, CancellationToken ct)
    {
        var form = await FiscWebposParser.ReadFormSafeAsync(ctx, ct);
        var result = FiscWebposParser.ParseForm(form);
        if (result.IsSuccess && !string.IsNullOrEmpty(result.Lidm))
            await _completion.MarkPaidAsync(result.Lidm, result.LastPan4, result.TxnRef, ct: ct);
        return result;
    }

    // 無論成功失敗都導回結果頁，由前端呈現付款結果。
    private static IActionResult RedirectToResultPage(string baseUrl, FiscWebposParser.WebposResult result)
    {
        var paid = result.IsSuccess ? "1" : "0";
        var url = $"{baseUrl}?code={Uri.EscapeDataString(result.Lidm)}&paid={paid}";
        return new RedirectResult(url);
    }

    // POST /store/payment/notify（主動通知，AuthResp 字串）
    // 財金的主動通知網址在特店端只登錄一組，訂單與收款連結的交易都會打到這裡，
    // 因此依 lidm 前綴分派：訂單標記不到（不存在/已付款）且單號為 PL 開頭時，再試收款連結。
    public async Task<IActionResult> Notify(RouteContext ctx)
    {
        var ct = ctx.Request.HttpContext.RequestAborted;

        var form = await FiscWebposParser.ReadFormSafeAsync(ctx, ct);
        var authResp = form.GetValueOrDefault("AuthResp");
        if (string.IsNullOrEmpty(authResp))
            authResp = await FiscWebposParser.ReadRawBodyAsync(ctx, ct); // 後援：body 即 AuthResp 字串

        var result = FiscWebposParser.ParseAuthResp(authResp);
        if (result.IsSuccess && !string.IsNullOrEmpty(result.Lidm))
        {
            var marked = await _completion.MarkPaidAsync(result.Lidm, result.LastPan4, result.TxnRef, ct: ct);
            if (!marked && result.Lidm.StartsWith("PL", StringComparison.OrdinalIgnoreCase))
                await _paymentLinks.MarkPaidAsync(result.Lidm, result.LastPan4, result.TxnRef, ct);
        }

        // 財金期待 http 200，未回 200 會重試最多 3 次。
        return ctx.Ok(new { received = true, orderNumber = result.Lidm, paid = result.IsSuccess });
    }

    // ── DTOs ──────────────────────────────────────────────────────────────────────

    // ReturnOrigin：前端帶入使用者結帳所在的 store 網域（window.location.origin），供多網域同網域導回。
    private sealed record CreatePaymentRequest(string? OrderCode, string? ReturnOrigin);
    private sealed record CreatePaymentResponse(string ActionUrl, IReadOnlyDictionary<string, string> Fields);
}
