using System.Security.Claims;
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
/// 財金 FISC FOCAS_WEBPOS 信用卡金流端點（對齊舊系統 + 技術手冊 v2.7）。
///   POST /store/payment/create  — 取得刷卡 form 欄位（前端 auto-submit 至財金刷卡頁）
///   POST /store/payment/return  — AuthResURL：持卡人刷卡後財金以 form 導回，處理後 302 回前台
///   POST /store/payment/notify  — 主動通知：財金背景 POST AuthResp 字串（補償，冪等）
/// 全部公開，不需 JWT。
///
/// 每一次回呼（成功與失敗）都寫進 Paymentattempts（<see cref="IPaymentAttemptLog"/>）並記 log：
/// 顧客回報「刷卡沒成功」時，財金的 errcode/errDesc 是唯一能回答「為什麼」的資料。
/// </summary>
public sealed class PaymentController
{
    private readonly IOrderService _orders;
    private readonly IPaymentCompletionService _completion;
    private readonly IPaymentLinkService _paymentLinks;
    private readonly IPaymentAttemptLog _attempts;
    private readonly JwtHelper _jwt;
    private readonly ILogger<PaymentController> _logger;
    private readonly FiscOptions _fisc;

    public PaymentController(
        IOrderService orders, IPaymentCompletionService completion,
        IPaymentLinkService paymentLinks, IPaymentAttemptLog attempts,
        JwtHelper jwt, ILogger<PaymentController> logger, IOptions<FiscOptions> fisc)
    {
        _orders = orders;
        _completion = completion;
        _paymentLinks = paymentLinks;
        _attempts = attempts;
        _jwt = jwt;
        _logger = logger;
        _fisc = fisc.Value;
    }

    // POST /store/payment/create
    public async Task<IActionResult> CreatePayment(RouteContext ctx)
    {
        var ct = ctx.Request.HttpContext.RequestAborted;

        var body = await ctx.TryReadBodyAsync<CreatePaymentRequest>(ct);
        if (body is null || string.IsNullOrWhiteSpace(body.OrderCode))
            return ctx.BadRequest("缺少 orderCode 欄位。");

        var orderCode = body.OrderCode.Trim();

        var summary = await _orders.GetOrderAsync(orderCode, ct);
        if (summary is null) return ctx.NotFound("找不到該訂單");

        // 會員自助重新付款（會員中心訂單詳情）會帶上 JWT：帶了就必須是自己的訂單。
        // 訂單編號可被猜出（O+日期+3 碼流水），不驗歸屬等於任何人都能以他人單號發起刷卡、
        // 從刷卡頁窺見金額。結帳流程的訪客單不帶 token，維持原行為。
        // 註：store/* 為公開路由，JwtAuthMiddleware 直接放行、不會設定 CurrentUser，故此處自行驗證。
        var memberId = TryGetMemberId(ctx);
        if (memberId is not null && !await _orders.IsOrderOwnedByMemberAsync(orderCode, memberId.Value, ct))
            return ctx.NotFound("找不到該訂單");

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
        var result = await CompleteFromFormAsync(ctx, "return", ct);
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
        var result = await CompleteFromFormAsync(ctx, "return-admin", ct);
        var paid = result.IsSuccess ? "1" : "0";
        var url = $"{_fisc.AdminSuccessUrl.TrimEnd('/')}/{Uri.EscapeDataString(result.Lidm)}?paid={paid}";
        if (!result.IsSuccess && !string.IsNullOrEmpty(result.ErrCode))
            url += $"&err={Uri.EscapeDataString(result.ErrCode)}";
        return new RedirectResult(url);
    }

    // 從候選來源正規化出白名單內的 origin（防 open redirect）。同時用於 create（決定是否帶 query）
    // 與 return（決定是否同網域導回）——兩端都驗證。與收款連結共用 FiscRedirect。
    private string ResolveAllowedOrigin(params string?[] candidates)
        => FiscRedirect.ResolveAllowedOrigin(_fisc, candidates);

    // 解析財金 form 回傳 + 記錄授權結果 + 冪等標記已付款（store / admin 返回共用，
    // 差別僅在最終 redirect 目標）。
    private async Task<FiscWebposParser.WebposResult> CompleteFromFormAsync(
        RouteContext ctx, string source, CancellationToken ct)
    {
        var form = await FiscFormReader.ReadFormSafeAsync(ctx, ct);
        var result = FiscWebposParser.ParseForm(form);

        string? note = null;
        if (result.IsSuccess && !string.IsNullOrEmpty(result.Lidm))
        {
            try
            {
                await _completion.MarkPaidAsync(result.Lidm, result.LastPan4, result.TxnRef, ct: ct);
            }
            catch (Exception ex)
            {
                // 卡確實已授權（AutoCap=1 已自動請款）。此時若讓例外冒到中介層變成 500，
                // 顧客會停在 API 網域看到一段 JSON、以為刷卡失敗。入帳交給財金主動通知
                // （/notify，冪等）補償，這裡照常導回「付款完成」結果頁。
                note = $"授權成功但入帳處理失敗：{ex.Message}";
                _logger.LogError(ex,
                    "刷卡授權成功但入帳處理拋出例外，單號 {Lidm}（來源 {Source}）；已導回成功頁，入帳待主動通知補償",
                    result.Lidm, source);
            }
        }

        await RecordAttemptAsync(result, source, note, ct);
        return result;
    }

    // 無論成功失敗都導回結果頁，由前端呈現付款結果。失敗時附上財金錯誤代碼，
    // 前端據以顯示白話原因（額度不足 / 有效期限錯誤 / 請洽發卡行…），顧客才知道下一步該做什麼。
    private static IActionResult RedirectToResultPage(string baseUrl, FiscWebposParser.WebposResult result)
    {
        var paid = result.IsSuccess ? "1" : "0";
        var url = $"{baseUrl}?code={Uri.EscapeDataString(result.Lidm)}&paid={paid}";
        if (!result.IsSuccess && !string.IsNullOrEmpty(result.ErrCode))
            url += $"&err={Uri.EscapeDataString(result.ErrCode)}";
        return new RedirectResult(url);
    }

    // POST /store/payment/notify（主動通知，AuthResp 字串）
    // 財金的主動通知網址在特店端只登錄一組，訂單與收款連結的交易都會打到這裡，
    // 因此依 lidm 前綴分派：訂單標記不到（不存在/已付款）且單號為 PL 開頭時，再試收款連結。
    public async Task<IActionResult> Notify(RouteContext ctx)
    {
        var ct = ctx.Request.HttpContext.RequestAborted;

        var form = await FiscFormReader.ReadFormSafeAsync(ctx, ct);
        var authResp = form.GetValueOrDefault("AuthResp");
        if (string.IsNullOrEmpty(authResp))
            authResp = await FiscFormReader.ReadRawBodyAsync(ctx, ct); // 後援：body 即 AuthResp 字串

        var result = FiscWebposParser.ParseAuthResp(authResp);

        string? note = null;
        if (result.IsSuccess && !string.IsNullOrEmpty(result.Lidm))
        {
            try
            {
                var marked = await _completion.MarkPaidAsync(result.Lidm, result.LastPan4, result.TxnRef, ct: ct);
                if (!marked && result.Lidm.StartsWith("PL", StringComparison.OrdinalIgnoreCase))
                    await _paymentLinks.MarkPaidAsync(result.Lidm, result.LastPan4, result.TxnRef, ct);
            }
            catch (Exception ex)
            {
                // 未回 200 財金會重試最多 3 次，補償仍有機會成功；但不能讓例外吃掉這筆紀錄。
                note = $"主動通知入帳處理失敗：{ex.Message}";
                _logger.LogError(ex, "主動通知入帳處理拋出例外，單號 {Lidm}", result.Lidm);
            }
        }

        await RecordAttemptAsync(result, "notify", note, ct);

        // 財金期待 http 200，未回 200 會重試最多 3 次。
        return ctx.Ok(new { received = true, orderNumber = result.Lidm, paid = result.IsSuccess });
    }

    // ── 授權結果紀錄 ──────────────────────────────────────────────────────────────

    // 寫 Paymentattempts（後台查得到）+ 記 log（App Insights 查得到）。兩者都是 best-effort。
    private async Task RecordAttemptAsync(
        FiscWebposParser.WebposResult r, string source, string? note, CancellationToken ct)
    {
        if (r.IsSuccess)
            _logger.LogInformation(
                "刷卡授權成功，單號 {Lidm}（來源 {Source}，末四碼 {LastPan4}，卡別 {CardBrand}）",
                r.Lidm, source, r.LastPan4, r.CardBrand);
        else
            _logger.LogWarning(
                "刷卡授權失敗，單號 {Lidm}（來源 {Source}）：status={Status} errcode={ErrCode} errDesc={ErrDesc}",
                r.Lidm, source, r.Status, r.ErrCode, r.ErrDesc);

        await _attempts.RecordAsync(new PaymentAttempt(
            Lidm: r.Lidm, Source: source, IsSuccess: r.IsSuccess,
            Status: r.Status, ErrCode: r.ErrCode, ErrDesc: r.ErrDesc,
            AuthCode: r.AuthCode, Xid: r.Xid, LastPan4: r.LastPan4,
            CardBrand: r.CardBrand, AuthAmt: r.AuthAmt, Note: note), ct);
    }

    // store/* 是公開路由（JwtAuthMiddleware 放行、不設 CurrentUser），故自行驗證 Bearer token。
    // 沒帶或無效一律回 null＝視為訪客，維持既有結帳流程。
    private Guid? TryGetMemberId(RouteContext ctx)
    {
        var header = ctx.Request.Headers.Authorization.ToString();
        if (string.IsNullOrWhiteSpace(header) ||
            !header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) return null;

        var principal = _jwt.ValidateToken(header["Bearer ".Length..].Trim());
        if (principal is null) return null;
        if (!string.Equals(principal.FindFirstValue(ClaimTypes.Role), "member", StringComparison.OrdinalIgnoreCase))
            return null;
        return Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
    }

    // ── DTOs ──────────────────────────────────────────────────────────────────────

    // ReturnOrigin：前端帶入使用者結帳所在的 store 網域（window.location.origin），供多網域同網域導回。
    private sealed record CreatePaymentRequest(string? OrderCode, string? ReturnOrigin);
    private sealed record CreatePaymentResponse(string ActionUrl, IReadOnlyDictionary<string, string> Fields);
}
