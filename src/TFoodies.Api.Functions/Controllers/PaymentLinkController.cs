using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using TFoodies.Api.Functions.Helpers;
using TFoodies.Api.Functions.Router;
using TFoodies.Application.Abstractions;
using TFoodies.Infrastructure.Payments.Fisc;

namespace TFoodies.Api.Functions.Controllers;

/// <summary>
/// 收款連結的客人端端點（全部公開，不需 JWT — 客人不是會員）。
///   GET  /store/paylinks/{token}           — 取連結資訊（項目/金額/狀態）
///   POST /store/paylinks/{token}/checkout  — 送出收件資料，取得刷卡 form 欄位
///   POST /store/payment/return-paylink     — AuthResURL：授權後財金 form 導回，處理後 302 回結果頁
///
/// 主動通知（notify）與訂單共用 /store/payment/notify，由 PaymentController 依 lidm 前綴分派。
/// </summary>
public sealed class PaymentLinkController
{
    private static readonly Regex MobilePattern = new(@"^09\d{8}$", RegexOptions.Compiled);

    private readonly IPaymentLinkService _links;
    private readonly FiscOptions _fisc;

    public PaymentLinkController(IPaymentLinkService links, IOptions<FiscOptions> fisc)
    {
        _links = links;
        _fisc = fisc.Value;
    }

    // GET /store/paylinks/{token}
    public async Task<IActionResult> Get(RouteContext ctx)
    {
        var ct = ctx.Request.HttpContext.RequestAborted;
        var link = await _links.GetByTokenAsync(ctx.RequirePathParam("token"), ct);

        // 通用訊息：不區分「不存在 / 已作廢」，避免成為 token 枚舉的 oracle。
        if (link is null) return ctx.NotFound("連結不存在或已失效");
        return ctx.Ok(link);
    }

    // POST /store/paylinks/{token}/checkout
    public async Task<IActionResult> Checkout(RouteContext ctx)
    {
        var ct = ctx.Request.HttpContext.RequestAborted;

        var body = await ctx.TryReadBodyAsync<CheckoutRequest>(ct);
        if (body is null) return ctx.BadRequest("Request body 格式不正確。");

        var name = body.Name?.Trim() ?? "";
        var mobile = body.Mobile?.Trim() ?? "";
        var address = body.Address?.Trim() ?? "";

        if (name.Length is 0 or > 50) return ctx.BadRequest("請填寫姓名（50 字以內）。");
        if (!MobilePattern.IsMatch(mobile)) return ctx.BadRequest("請輸入正確的手機號碼格式（09 開頭共 10 碼）。");
        if (body.ZipcodeId is null or <= 0) return ctx.BadRequest("請選擇縣市與鄉鎮市區。");
        if (address.Length is 0 or > 200) return ctx.BadRequest("請填寫詳細地址（200 字以內）。");

        // 只有通過白名單的 origin 才帶進 AuthResURL（防把可疑網域塞進 FISC 表單）。
        var origin = FiscRedirect.ResolveAllowedOrigin(
            _fisc, body.ReturnOrigin, ctx.Request.Headers["Origin"].ToString());

        var result = await _links.StartCheckoutAsync(
            ctx.RequirePathParam("token"),
            new PaymentLinkCustomer(name, mobile, body.ZipcodeId.Value, address),
            origin.Length == 0 ? null : origin,
            ct);

        if (result.IsFailure)
        {
            return result.Error.Code switch
            {
                "not_found"  => ctx.NotFound("連結不存在或已失效"),
                "validation" => ctx.BadRequest(result.Error.Message),
                _            => ctx.Conflict(result.Error.Message),
            };
        }

        return ctx.Ok(result.Value);
    }

    // POST /store/payment/return-paylink（AuthResURL）
    public async Task<IActionResult> Return(RouteContext ctx)
    {
        var ct = ctx.Request.HttpContext.RequestAborted;

        var result = FiscWebposParser.ParseForm(await FiscWebposParser.ReadFormSafeAsync(ctx, ct));
        if (result.IsSuccess && !string.IsNullOrEmpty(result.Lidm))
            await _links.MarkPaidAsync(result.Lidm, result.LastPan4, result.TxnRef, ct);

        // 動態回跳：checkout 時帶進 query 的客人所在網域，經白名單再驗證後同網域導回；
        // 不在白名單 / FISC 未保留 query → 退回設定導出的 StoreOrigin（並防 open redirect）。
        var origin = FiscRedirect.ResolveAllowedOrigin(_fisc, ctx.Request.Query["origin"].ToString());
        var baseUrl = origin.Length == 0 ? _fisc.StoreOrigin : origin;
        var paid = result.IsSuccess ? "1" : "0";

        // 成功失敗都導回結果頁，由前端呈現。
        return new RedirectResult($"{baseUrl}/Pay/Result?code={Uri.EscapeDataString(result.Lidm)}&paid={paid}");
    }

    // ── DTOs ──────────────────────────────────────────────────────────────────────

    // ReturnOrigin：前端帶入客人所在網域（window.location.origin），供多網域同網域導回。
    private sealed record CheckoutRequest(
        string? Name, string? Mobile, int? ZipcodeId, string? Address, string? ReturnOrigin);
}
