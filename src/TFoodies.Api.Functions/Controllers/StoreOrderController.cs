using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using TFoodies.Api.Functions.Router;
using TFoodies.Application.Abstractions;
using TFoodies.Domain.Enums;

namespace TFoodies.Api.Functions.Controllers;

/// <summary>
/// 前台下單相關端點。
///   POST /store/orders          — 建立訂單（JWT 可選：登入會員帶 Bearer，匿名結帳不帶）
///   GET  /store/orders/{code}   — 查詢訂單（JWT 可選）
///   POST /store/discount/apply  — 折扣碼預覽（公開）
/// </summary>
public sealed class StoreOrderController
{
    private readonly IOrderService _orders;
    private readonly IDiscountService _discounts;
    private readonly IEmailService _email;

    public StoreOrderController(IOrderService orders, IDiscountService discounts, IEmailService email)
    {
        _orders = orders;
        _discounts = discounts;
        _email = email;
    }

    // POST /store/orders
    public async Task<IActionResult> PlaceOrder(RouteContext ctx)
    {
        var body = await ctx.TryReadBodyAsync<PlaceOrderRequestDto>();
        if (body is null) return ctx.BadRequest("Request body 格式不正確。");

        var validation = body.Validate();
        if (validation is not null) return ctx.BadRequest(validation);

        var memberId = ExtractMemberId(ctx.CurrentUser);

        var request = body.ToRequest();
        var result = await _orders.PlaceOrderAsync(request, memberId, ctx.Request.HttpContext.RequestAborted);

        if (!result.IsSuccess)
        {
            return result.Error.Code switch
            {
                "order.empty_cart" or "order.invalid_product" => ctx.BadRequest(result.Error.Message),
                "order.insufficient_stock" => ctx.UnprocessableEntity(result.Error.Message),
                _ => ctx.BadRequest(result.Error.Message),
            };
        }

        // 訂單成立通知信（對齊舊系統 MainMs/ShoppingSuccess）。Best-effort：
        // SendAsync 內部已 catch 回傳 false，寄信失敗不影響下單成功。僅在有收件 Email 時寄送。
        if (!string.IsNullOrWhiteSpace(body.BuyerEmail))
        {
            await _email.SendAsync(
                body.BuyerEmail!.Trim(),
                $"食在呼 TFoodies–訂單通知 {result.Value!.OrderCode}",
                BuildOrderMailHtml(body.BuyerName ?? "顧客", result.Value!),
                ctx.Request.HttpContext.RequestAborted);
        }

        return ctx.Created(result.Value!);
    }

    // GET /store/orders/{code}
    public async Task<IActionResult> GetOrder(RouteContext ctx)
    {
        var code = ctx.RequirePathParam("code");
        var summary = await _orders.GetOrderAsync(code, ctx.Request.HttpContext.RequestAborted);
        if (summary is null) return ctx.NotFound("找不到該訂單");
        return ctx.Ok(summary);
    }

    // POST /store/discount/apply
    public async Task<IActionResult> ApplyDiscount(RouteContext ctx)
    {
        var body = await ctx.TryReadBodyAsync<DiscountApplyRequest>();
        if (body is null || string.IsNullOrWhiteSpace(body.DiscountCode))
            return ctx.BadRequest("缺少 discountCode 欄位。");

        var memberId = ExtractMemberId(ctx.CurrentUser);
        var subtotal = body.OrderSubtotal > 0 ? body.OrderSubtotal : 0;

        var result = await _discounts.ValidateAsync(
            body.DiscountCode, subtotal, memberId, ctx.Request.HttpContext.RequestAborted);

        if (!result.IsSuccess)
            return ctx.BadRequest(result.Error.Message);

        return ctx.Ok(new DiscountApplyResponse(result.Value!.DiscountCode, result.Value.DiscountAmount));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────────

    private static Guid? ExtractMemberId(ClaimsPrincipal? user)
    {
        if (user is null) return null;
        var role = user.FindFirstValue(ClaimTypes.Role);
        if (!string.Equals(role, "member", StringComparison.OrdinalIgnoreCase)) return null;
        var id = user.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(id, out var guid) ? guid : null;
    }

    // ── 訂單通知信版型 ───────────────────────────────────────────────────────────────

    // 會員中心訂單查詢頁。
    private const string OrderUrl = "https://www.tfoodies.com/Member/Orders";

    private static string PayTypeLabel(string payTypeKey) => payTypeKey switch
    {
        "credit"   => "信用卡線上付款",
        "atmcode"  => "ATM 虛擬帳號轉帳",
        "delivery" => "貨到付款",
        _          => "無須付款",
    };

    // 響應式、相容主流郵件客戶端（Outlook/Gmail）的純 table + inline-style 版型，
    // 與忘記密碼通知信共用同一套品牌視覺（主色 #26B7BC，深色 #156467）。
    private static string BuildOrderMailHtml(string buyerName, PlaceOrderResult order)
    {
        var orderDate = DateTime.UtcNow.AddHours(8).ToString("yyyy-MM-dd");
        var payable   = order.Total + order.Freight - order.Discount;
        var isAtm     = order.PayTypeKey == "atmcode" && !string.IsNullOrWhiteSpace(order.AtmCode);

        // 折扣列僅在有折扣時顯示。
        var discountRow = order.Discount > 0
            ? $@"<tr>
                  <td style=""padding:7px 0; font-size:14px; color:#5a6666;"">折扣折抵</td>
                  <td align=""right"" style=""padding:7px 0; font-size:14px; color:#d9534f;"">- NT$ {order.Discount:N0}</td>
                </tr>"
            : string.Empty;

        // ATM 匯款資訊卡片（僅 ATM 付款時呈現）。
        var atmBlock = isAtm
            ? $@"<tr>
            <td style=""padding:8px 40px 0 40px;"">
              <table role=""presentation"" cellspacing=""0"" cellpadding=""0"" border=""0"" width=""100%"" style=""background-color:#fff8ec; border:1px solid #f3dca6; border-radius:10px;"">
                <tr>
                  <td style=""padding:20px 24px;"">
                    <div style=""font-size:14px; font-weight:600; color:#b8860b; margin-bottom:14px; letter-spacing:1px;"">ATM 轉帳資訊</div>
                    <table role=""presentation"" cellspacing=""0"" cellpadding=""0"" border=""0"" width=""100%"">
                      <tr>
                        <td style=""padding:5px 0; font-size:14px; color:#7a6a3a; width:96px;"">轉帳銀行</td>
                        <td style=""padding:5px 0; font-size:14px; color:#3a3320; font-weight:600;"">013 國泰世華銀行</td>
                      </tr>
                      <tr>
                        <td style=""padding:5px 0; font-size:14px; color:#7a6a3a;"">匯款帳號</td>
                        <td style=""padding:5px 0; font-size:17px; color:#156467; font-weight:700; font-family:'Courier New',Consolas,monospace; letter-spacing:1px;"">{order.AtmCode}</td>
                      </tr>
                      <tr>
                        <td style=""padding:5px 0; font-size:14px; color:#7a6a3a;"">轉帳金額</td>
                        <td style=""padding:5px 0; font-size:14px; color:#3a3320; font-weight:600;"">NT$ {payable:N0}</td>
                      </tr>
                      <tr>
                        <td style=""padding:5px 0; font-size:14px; color:#7a6a3a;"">繳款截止日</td>
                        <td style=""padding:5px 0; font-size:14px; color:#d9534f; font-weight:600;"">{order.AtmExpiry:yyyy-MM-dd}</td>
                      </tr>
                    </table>
                    <p style=""font-size:12px; line-height:1.6; color:#a89567; margin:14px 0 0 0;"">請於繳款截止日前完成轉帳，逾期訂單將自動取消。</p>
                  </td>
                </tr>
              </table>
            </td>
          </tr>"
            : string.Empty;

        return $@"<!DOCTYPE html>
<html lang=""zh-Hant"">
<head>
  <meta charset=""utf-8"">
  <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
  <meta name=""x-apple-disable-message-reformatting"">
  <title>訂單通知</title>
</head>
<body style=""margin:0; padding:0; background-color:#f4f5f7; -webkit-text-size-adjust:100%; -ms-text-size-adjust:100%;"">
  <!-- 預覽文字（收件匣摘要，不顯示於信件本文）-->
  <div style=""display:none; max-height:0; overflow:hidden; opacity:0; font-size:1px; line-height:1px; color:#f4f5f7;"">我們已收到您的訂單 {order.OrderCode}，感謝您的訂購。</div>

  <table role=""presentation"" cellspacing=""0"" cellpadding=""0"" border=""0"" width=""100%"" style=""background-color:#f4f5f7;"">
    <tr>
      <td align=""center"" style=""padding:32px 16px;"">
        <table role=""presentation"" cellspacing=""0"" cellpadding=""0"" border=""0"" width=""600"" style=""width:600px; max-width:600px; background-color:#ffffff; border-radius:14px; overflow:hidden; box-shadow:0 2px 8px rgba(0,0,0,0.06); font-family:'Helvetica Neue', Arial, 'PingFang TC', 'Microsoft JhengHei', sans-serif;"">

          <!-- 品牌標頭 -->
          <tr>
            <td align=""center"" style=""background-color:#26b7bc; background-image:linear-gradient(135deg,#26b7bc 0%,#1d8e92 100%); padding:34px 24px;"">
              <div style=""font-size:26px; font-weight:700; letter-spacing:2px; color:#ffffff; line-height:1.2;"">食在呼 TFoodies</div>
              <div style=""font-size:13px; color:#e6f6f6; margin-top:6px; letter-spacing:1px;"">訂單確認通知</div>
            </td>
          </tr>

          <!-- 內文 -->
          <tr>
            <td style=""padding:36px 40px 8px 40px;"">
              <h1 style=""font-size:20px; font-weight:600; color:#2c3e3e; margin:0 0 14px 0;"">親愛的 {buyerName} 您好：</h1>
              <p style=""font-size:15px; line-height:1.7; color:#5a6666; margin:0 0 24px 0;"">感謝您訂購《食在呼 TFoodies》的優質商品，我們已經收到您的訂購資訊！以下為您的訂單摘要。</p>
            </td>
          </tr>

          <!-- 訂單編號方塊 -->
          <tr>
            <td style=""padding:0 40px;"">
              <table role=""presentation"" cellspacing=""0"" cellpadding=""0"" border=""0"" width=""100%"" style=""background-color:#e6f6f6; border:1px solid #b9e6e7; border-radius:10px;"">
                <tr>
                  <td style=""padding:18px 24px;"">
                    <div style=""font-size:13px; color:#1d8e92; letter-spacing:1px; margin-bottom:6px;"">訂單編號</div>
                    <div style=""font-size:22px; font-weight:700; color:#156467; letter-spacing:1px;"">{order.OrderCode}</div>
                    <div style=""font-size:12px; color:#5a9a9c; margin-top:6px;"">訂購日期 {orderDate}　·　付款方式 {PayTypeLabel(order.PayTypeKey)}</div>
                  </td>
                </tr>
              </table>
            </td>
          </tr>

          <!-- 金額摘要 -->
          <tr>
            <td style=""padding:24px 40px 0 40px;"">
              <table role=""presentation"" cellspacing=""0"" cellpadding=""0"" border=""0"" width=""100%"">
                <tr>
                  <td style=""padding:7px 0; font-size:14px; color:#5a6666;"">商品小計</td>
                  <td align=""right"" style=""padding:7px 0; font-size:14px; color:#2c3e3e;"">NT$ {order.Total:N0}</td>
                </tr>
                <tr>
                  <td style=""padding:7px 0; font-size:14px; color:#5a6666;"">運費</td>
                  <td align=""right"" style=""padding:7px 0; font-size:14px; color:#2c3e3e;"">NT$ {order.Freight:N0}</td>
                </tr>
                {discountRow}
                <tr><td colspan=""2"" style=""padding:4px 0;""><div style=""border-top:1px solid #eef0f0; font-size:0; line-height:0;"">&nbsp;</div></td></tr>
                <tr>
                  <td style=""padding:7px 0; font-size:16px; font-weight:700; color:#2c3e3e;"">應付總額</td>
                  <td align=""right"" style=""padding:7px 0; font-size:20px; font-weight:700; color:#156467;"">NT$ {payable:N0}</td>
                </tr>
              </table>
            </td>
          </tr>
{atmBlock}
          <!-- CTA 按鈕 -->
          <tr>
            <td align=""center"" style=""padding:30px 40px 12px 40px;"">
              <table role=""presentation"" cellspacing=""0"" cellpadding=""0"" border=""0"">
                <tr>
                  <td align=""center"" bgcolor=""#26b7bc"" style=""border-radius:8px;"">
                    <a href=""{OrderUrl}"" target=""_blank"" style=""display:inline-block; padding:14px 40px; font-size:16px; font-weight:600; color:#ffffff; text-decoration:none; border-radius:8px; background-color:#26b7bc;"">查看訂單明細</a>
                  </td>
                </tr>
              </table>
            </td>
          </tr>

          <!-- 補充說明 -->
          <tr>
            <td style=""padding:14px 40px 4px 40px;"">
              <p style=""font-size:13px; line-height:1.7; color:#9aa3a3; margin:0;"">更多詳細訂單資訊，請登入「食在呼－會員中心」查詢。如有任何問題，歡迎與客服聯繫。</p>
            </td>
          </tr>

          <!-- 分隔線 -->
          <tr>
            <td style=""padding:24px 40px 0 40px;""><div style=""border-top:1px solid #eef0f0; font-size:0; line-height:0;"">&nbsp;</div></td>
          </tr>

          <!-- 頁尾 -->
          <tr>
            <td align=""center"" style=""padding:18px 40px 32px 40px;"">
              <p style=""font-size:12px; line-height:1.6; color:#aab2b2; margin:0;"">此為系統自動發送之通知信，請勿直接回覆。</p>
              <p style=""font-size:12px; line-height:1.6; color:#aab2b2; margin:6px 0 0 0;"">© 食在呼 TFoodies　感謝您的支持</p>
            </td>
          </tr>

        </table>
      </td>
    </tr>
  </table>
</body>
</html>";
    }

    // ── Request / Response DTOs ───────────────────────────────────────────────────

    private sealed record PlaceOrderRequestDto(
        List<CartLineDto>? Lines,
        string? BuyerName, string? BuyerMobile, string? BuyerEmail,
        int? BuyerZipcodeId, string? BuyerAddress, int? Gender,
        string? Password, DateOnly? Birthday,
        string? ReceiverName, string? ReceiverMobile,
        int? ReceiverZipcodeId, string? ReceiverAddress, int? ReceiverTime,
        int PayType, int InvoiceType,
        string? CompanyTitle, string? CompanyNumber,
        string? LoveCode, string? CarrierType, string? CarrierNum,
        string? DiscountCode, string? Remark)
    {
        public string? Validate()
        {
            if (Lines is null || Lines.Count == 0) return "購物車不能為空。";
            if (string.IsNullOrWhiteSpace(BuyerName)) return "缺少 buyerName 欄位。";
            if (string.IsNullOrWhiteSpace(BuyerMobile)) return "缺少 buyerMobile 欄位。";
            if (string.IsNullOrWhiteSpace(ReceiverName)) return "缺少 receiverName 欄位。";
            if (string.IsNullOrWhiteSpace(ReceiverMobile)) return "缺少 receiverMobile 欄位。";
            if (ReceiverZipcodeId is null) return "缺少 receiverZipcodeId 欄位。";
            if (string.IsNullOrWhiteSpace(ReceiverAddress)) return "缺少 receiverAddress 欄位。";
            return null;
        }

        public PlaceOrderRequest ToRequest() => new(
            Lines!.Select(l => new CartLineRequest(l.ProductId, l.Qty)).ToList(),
            BuyerName ?? string.Empty, BuyerMobile!, BuyerEmail,
            BuyerZipcodeId, BuyerAddress, Gender,
            Password, Birthday,
            ReceiverName!, ReceiverMobile!, ReceiverZipcodeId!.Value, ReceiverAddress!, ReceiverTime ?? 0,
            (Domain.Enums.PayType)PayType, (Domain.Enums.InvoiceType)InvoiceType,
            CompanyTitle, CompanyNumber, LoveCode, CarrierType, CarrierNum,
            DiscountCode, Remark);
    }

    private sealed record CartLineDto(Guid ProductId, int Qty);
    private sealed record DiscountApplyRequest(string? DiscountCode, int OrderSubtotal);
    private sealed record DiscountApplyResponse(string DiscountCode, int DiscountAmount);
}
