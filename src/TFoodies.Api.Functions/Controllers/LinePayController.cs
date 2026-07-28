using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TFoodies.Api.Functions.Helpers;
using TFoodies.Api.Functions.Router;
using TFoodies.Application.Abstractions;
using TFoodies.Domain.Enums;
using TFoodies.Infrastructure.Payments.Fisc;

namespace TFoodies.Api.Functions.Controllers;

/// <summary>
/// LINE Pay Online API v3 金流端點（直連自有商店）。
///   GET  /store/payment/methods                  — 前台可用付款方式清單
///   POST /store/payment/linepay/create           — 建立交易，回傳 LINE Pay 付款頁網址
///   GET  /store/payment/linepay/confirm          — 訂單付款完成回跳：請款確認 + 標記已付款
///   GET  /store/payment/linepay/cancel           — 訂單付款取消回跳（不動訂單狀態）
///   GET  /store/payment/linepay/confirm-paylink  — 收款連結付款完成回跳
///   GET  /store/payment/linepay/cancel-paylink   — 收款連結付款取消回跳
/// 全部公開，不需 JWT。
///
/// 與財金 WEBPOS 的差異：LINE Pay 為 reserve → confirm 兩段式，**未 confirm 就不會扣款**
/// （逾時自動失效），因此不需要 notify 這類補償路徑；使用者中途離開最壞是訂單留在未付款。
/// 回跳為 LINE Pay 主動 GET 導向，query 會帶 transactionId 與 orderId。
/// </summary>
public sealed class LinePayController
{
    private readonly IOrderService _orders;
    private readonly IPaymentCompletionService _completion;
    private readonly IPaymentLinkService _paymentLinks;
    private readonly ILinePayClient _linePay;
    private readonly FiscOptions _site;
    private readonly ILogger<LinePayController> _logger;

    // FiscOptions 於此作為「站台網址與回跳白名單」使用（見該類別註解），非財金專用。
    public LinePayController(
        IOrderService orders, IPaymentCompletionService completion, IPaymentLinkService paymentLinks,
        ILinePayClient linePay, IOptions<FiscOptions> site, ILogger<LinePayController> logger)
    {
        _orders = orders;
        _completion = completion;
        _paymentLinks = paymentLinks;
        _linePay = linePay;
        _site = site.Value;
        _logger = logger;
    }

    // GET /store/payment/methods
    public Task<IActionResult> Methods(RouteContext ctx)
        => Task.FromResult(ctx.Ok(new { methods = StorePaymentMethods.Available(_linePay.IsEnabled) }));

    // POST /store/payment/linepay/create
    public async Task<IActionResult> CreatePayment(RouteContext ctx)
    {
        var ct = ctx.Request.HttpContext.RequestAborted;

        if (!_linePay.IsEnabled) return ctx.BadRequest("LINE Pay 目前未啟用。");

        var body = await ctx.TryReadBodyAsync<CreatePaymentRequest>(ct);
        if (body is null || string.IsNullOrWhiteSpace(body.OrderCode))
            return ctx.BadRequest("缺少 orderCode 欄位。");

        var orderCode = body.OrderCode.Trim();
        var summary = await _orders.GetOrderAsync(orderCode, ct);
        if (summary is null) return ctx.NotFound("找不到該訂單");

        if (summary.PayType != PayType.LinePay)
            return ctx.BadRequest("此訂單非 LINE Pay 付款，無法發起交易。");
        // 金流不收 0 元（100% 折扣 + 免運會發生）。擺在狀態檢查之前，訊息才精確
        // （下單時 payable<=0 已標記為「免付款」，PayStatus 檢查會先回「訂單已付款」誤導人）。
        if (summary.Total + summary.Freight - summary.Discount <= 0)
            return ctx.BadRequest("本訂單應付金額為 0，無須線上付款。");
        if (summary.PayStatus != PayStatus.Unpaid)
            return ctx.Conflict("訂單已付款或目前狀態不可發起付款。");

        // 多網域服務：把使用者結帳所在的網域帶進回跳網址的 query，完成後同網域導回。
        // 只在通過白名單時才帶（防把可疑網域塞進第三方），回跳時再驗一次（防 open redirect）。
        var origin = FiscRedirect.ResolveAllowedOrigin(
            _site, body.ReturnOrigin, ctx.Request.Headers["Origin"].ToString());

        // 應付金額由後端權威計算（Orders.total 語意為純商品小計）。
        var payable = summary.Total + summary.Freight - summary.Discount;

        var reserve = await _linePay.RequestAsync(new LinePayReserveRequest(
            orderCode,
            payable,
            $"食在呼 TFoodies 訂單 {orderCode}",
            WithOrigin(_site.LinePayConfirmUrl, origin),
            WithOrigin(_site.LinePayCancelUrl, origin)), ct);

        if (reserve.IsFailure) return ctx.BadRequest(reserve.Error.Message);

        return ctx.Ok(new CreatePaymentResponse(reserve.Value.PaymentUrlWeb, reserve.Value.TransactionId));
    }

    // GET /store/payment/linepay/confirm — 訂單付款完成回跳
    public async Task<IActionResult> Confirm(RouteContext ctx)
    {
        var ct = ctx.Request.HttpContext.RequestAborted;
        var orderCode = ctx.Request.Query["orderId"].ToString();
        var transactionId = ctx.Request.Query["transactionId"].ToString();

        var paid = await CompleteOrderAsync(orderCode, transactionId, ct);
        return RedirectToStore(ctx, $"?code={Uri.EscapeDataString(orderCode)}&paid={(paid ? "1" : "0")}",
            _site.StoreSuccessPath);
    }

    // GET /store/payment/linepay/cancel — 使用者於 LINE Pay 取消，訂單維持未付款
    public IActionResult Cancel(RouteContext ctx)
    {
        var orderCode = ctx.Request.Query["orderId"].ToString();
        return RedirectToStore(ctx, $"?code={Uri.EscapeDataString(orderCode)}&paid=0", _site.StoreSuccessPath);
    }

    // GET /store/payment/linepay/confirm-paylink — 收款連結付款完成回跳
    public async Task<IActionResult> ConfirmPaylink(RouteContext ctx)
    {
        var ct = ctx.Request.HttpContext.RequestAborted;
        var code = ctx.Request.Query["orderId"].ToString();
        var transactionId = ctx.Request.Query["transactionId"].ToString();

        var paid = false;
        if (!string.IsNullOrEmpty(code) && !string.IsNullOrEmpty(transactionId))
        {
            var result = await _paymentLinks.CompleteLinePayAsync(code, transactionId, ct);
            paid = result.IsSuccess;
            if (!result.IsSuccess)
                _logger.LogWarning("LINE Pay 收款連結 {Code} 請款失敗：{Error}", code, result.Error);
        }

        return RedirectToStore(ctx, $"?code={Uri.EscapeDataString(code)}&paid={(paid ? "1" : "0")}", "/Pay/Result");
    }

    // GET /store/payment/linepay/cancel-paylink
    public IActionResult CancelPaylink(RouteContext ctx)
    {
        var code = ctx.Request.Query["orderId"].ToString();
        return RedirectToStore(ctx, $"?code={Uri.EscapeDataString(code)}&paid=0", "/Pay/Result");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────────

    /// <summary>
    /// 請款確認 + 冪等標記已付款。金額一律由 DB 重算，不採信回跳參數。
    /// 回傳 true 表示這筆訂單目前是已付款狀態（含「先前已完成、本次回跳為重放」）。
    /// </summary>
    private async Task<bool> CompleteOrderAsync(string orderCode, string transactionId, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(orderCode) || string.IsNullOrEmpty(transactionId))
        {
            _logger.LogWarning("LINE Pay 回跳缺少 orderId / transactionId");
            return false;
        }

        var summary = await _orders.GetOrderAsync(orderCode, ct);
        if (summary is null)
        {
            _logger.LogWarning("LINE Pay 回跳找不到訂單 {OrderCode}", orderCode);
            return false;
        }

        // 回跳被重放（使用者重整/上一頁）：訂單已付款就直接視為成功，連 API 都不必打。
        if (summary.PayStatus == PayStatus.Paid) return true;

        var payable = summary.Total + summary.Freight - summary.Discount;
        var confirm = await _linePay.ConfirmAsync(transactionId, payable, ct);
        if (confirm.IsFailure)
        {
            _logger.LogWarning("LINE Pay 訂單 {OrderCode} 請款失敗：{Error}", orderCode, confirm.Error);
            return false;
        }

        // 既有共用核心：冪等標記已付款 + 建 Income + 開發票 + 寄付款完成信。
        await _completion.MarkPaidAsync(
            orderCode, lastPan4: null, txnRef: $"LINEPay transactionId:{transactionId}", ct: ct);
        return true;
    }

    private static string WithOrigin(string url, string origin)
        => origin.Length == 0 ? url : $"{url}?origin={Uri.EscapeDataString(origin)}";

    /// <summary>
    /// 導回前台。優先用 create 時帶進 query 的使用者網域（經白名單再驗證），
    /// 不在白名單則退回設定的 StoreOrigin（防 open redirect）。
    /// </summary>
    private RedirectResult RedirectToStore(RouteContext ctx, string query, string path)
    {
        var origin = FiscRedirect.ResolveAllowedOrigin(_site, ctx.Request.Query["origin"].ToString());
        var baseUrl = origin.Length == 0 ? _site.StoreOrigin : origin;
        return new RedirectResult($"{baseUrl}{path}{query}");
    }

    // ── DTOs ──────────────────────────────────────────────────────────────────────

    private sealed record CreatePaymentRequest(string? OrderCode, string? ReturnOrigin);
    private sealed record CreatePaymentResponse(string PaymentUrl, string TransactionId);
}
