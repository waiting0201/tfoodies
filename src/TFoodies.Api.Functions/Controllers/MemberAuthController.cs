using System.Security.Cryptography;
using System.Text;
using Dapper;
using Microsoft.AspNetCore.Mvc;
using TFoodies.Api.Functions.Router;
using TFoodies.Application.Abstractions;

namespace TFoodies.Api.Functions.Controllers;

/// <summary>
/// 會員認證延伸端點（公開，無需 JWT）。
///   POST /auth/register          — 會員註冊
///   POST /auth/forgot-password   — 忘記密碼：比對手機+Email，產生新密碼寄至信箱（對齊舊系統）
/// </summary>
public sealed class MemberAuthController
{
    private readonly IDbConnectionFactory _db;
    private readonly IEmailService _email;

    public MemberAuthController(IDbConnectionFactory db, IEmailService email)
    {
        _db    = db;
        _email = email;
    }

    // POST /auth/register
    public async Task<IActionResult> Register(RouteContext ctx)
    {
        var ct = ctx.Request.HttpContext.RequestAborted;

        var body = await ctx.TryReadBodyAsync<RegisterRequest>(ct);
        if (body is null) return ctx.BadRequest("Request body 格式不正確。");

        if (string.IsNullOrWhiteSpace(body.Mobile) || body.Mobile.Length != 10 || !body.Mobile.All(char.IsDigit))
            return ctx.BadRequest("mobile 必須為 10 位數字。");
        if (string.IsNullOrWhiteSpace(body.Name))
            return ctx.BadRequest("name 不可為空。");
        if (string.IsNullOrWhiteSpace(body.Password) || body.Password.Length < 6)
            return ctx.BadRequest("password 至少 6 個字元。");

        using var conn = await _db.CreateOpenConnectionAsync(ct);

        var exists = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM Members WHERE mobile = @mobile",
            new { mobile = body.Mobile });

        if (exists > 0) return ctx.Conflict("該手機號碼已註冊。");

        var memberId = Guid.NewGuid();
        var hashed   = HashPassword(body.Password);
        var now      = DateTime.UtcNow.AddHours(8);

        await conn.ExecuteAsync(@"
INSERT INTO Members (memberid, mobile, password, name, email, ismember, isenable, createdate)
VALUES (@memberid, @mobile, @password, @name, @email, 1, 1, @createdate)",
            new
            {
                memberid   = memberId,
                mobile     = body.Mobile,
                password   = hashed,
                name       = body.Name,
                email      = body.Email,
                createdate = now,
            });

        return ctx.Created(new { memberId });
    }

    // POST /auth/forgot-password
    // 對齊舊系統 Ajax/PasswordSend：以 mobile + email 比對會員，產生一組亂數新密碼，
    // 更新後寄至會員信箱，請其登入後自行修改密碼。
    public async Task<IActionResult> ForgotPassword(RouteContext ctx)
    {
        var ct = ctx.Request.HttpContext.RequestAborted;

        var body = await ctx.TryReadBodyAsync<ForgotPasswordRequest>(ct);
        if (body is null) return ctx.BadRequest("Request body 格式不正確。");
        if (string.IsNullOrWhiteSpace(body.Mobile)) return ctx.BadRequest("缺少 mobile 欄位。");
        if (string.IsNullOrWhiteSpace(body.Email))  return ctx.BadRequest("缺少 email 欄位。");

        var mobile = body.Mobile.Trim();
        var email  = body.Email.Trim();

        using var conn = await _db.CreateOpenConnectionAsync(ct);

        var memberId = await conn.ExecuteScalarAsync<Guid?>(
            "SELECT memberid FROM Members WHERE mobile = @mobile AND email = @email",
            new { mobile, email });

        // 訊息與舊系統一致：查無相符會員回「查無此會員」。
        if (memberId is null) return ctx.NotFound("查無此會員");

        var newPassword = GeneratePassword();
        var hashed      = HashPassword(newPassword);

        await conn.ExecuteAsync(
            "UPDATE Members SET password = @password WHERE memberid = @memberid",
            new { password = hashed, memberid = memberId.Value });

        var sent = await _email.SendAsync(email, "食在呼 TFoodies–忘記密碼通知", BuildResetMailHtml(newPassword), ct);
        if (!sent) return ctx.UnprocessableEntity("密碼已重設，但通知信寄送失敗，請聯繫客服。");

        return ctx.Ok(new { message = "密碼已寄出，請至您的Email信箱收信！" });
    }

    // ── Helpers ───────────────────────────────────────────────────────────────────

    // 與舊系統相同的字元集（排除易混淆的 I L O o l 1）。
    private const string PasswordChars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz023456789";

    private static string GeneratePassword(int length = 6)
    {
        var chars = new char[length];
        for (var i = 0; i < length; i++)
            chars[i] = PasswordChars[Random.Shared.Next(PasswordChars.Length)];
        return new string(chars);
    }

    // 會員中心登入頁（重設後請使用者登入並自行修改密碼）。
    private const string LoginUrl = "https://www.tfoodies.com/Member/Login";

    // 響應式、相容主流郵件客戶端（Outlook/Gmail）的純 table + inline-style 版型，
    // 配色對齊前台品牌色（主色 #26B7BC，深色 #156467）。
    private static string BuildResetMailHtml(string newPassword) => $@"<!DOCTYPE html>
<html lang=""zh-Hant"">
<head>
  <meta charset=""utf-8"">
  <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
  <meta name=""x-apple-disable-message-reformatting"">
  <title>忘記密碼通知</title>
</head>
<body style=""margin:0; padding:0; background-color:#f4f5f7; -webkit-text-size-adjust:100%; -ms-text-size-adjust:100%;"">
  <!-- 預覽文字（收件匣摘要，不顯示於信件本文）-->
  <div style=""display:none; max-height:0; overflow:hidden; opacity:0; font-size:1px; line-height:1px; color:#f4f5f7;"">您的新密碼已產生，請登入會員中心並儘速修改密碼。</div>

  <table role=""presentation"" cellspacing=""0"" cellpadding=""0"" border=""0"" width=""100%"" style=""background-color:#f4f5f7;"">
    <tr>
      <td align=""center"" style=""padding:32px 16px;"">
        <table role=""presentation"" cellspacing=""0"" cellpadding=""0"" border=""0"" width=""600"" style=""width:600px; max-width:600px; background-color:#ffffff; border-radius:14px; overflow:hidden; box-shadow:0 2px 8px rgba(0,0,0,0.06); font-family:'Helvetica Neue', Arial, 'PingFang TC', 'Microsoft JhengHei', sans-serif;"">

          <!-- 品牌標頭 -->
          <tr>
            <td align=""center"" style=""background-color:#26b7bc; background-image:linear-gradient(135deg,#26b7bc 0%,#1d8e92 100%); padding:34px 24px;"">
              <div style=""font-size:26px; font-weight:700; letter-spacing:2px; color:#ffffff; line-height:1.2;"">食在呼 TFoodies</div>
              <div style=""font-size:13px; color:#e6f6f6; margin-top:6px; letter-spacing:1px;"">會員帳號安全通知</div>
            </td>
          </tr>

          <!-- 內文 -->
          <tr>
            <td style=""padding:36px 40px 8px 40px;"">
              <h1 style=""font-size:20px; font-weight:600; color:#2c3e3e; margin:0 0 14px 0;"">親愛的會員，您好：</h1>
              <p style=""font-size:15px; line-height:1.7; color:#5a6666; margin:0 0 24px 0;"">我們已為您重新產生一組臨時密碼。請使用下方新密碼登入會員中心，並儘速於「修改密碼」頁面更新為您慣用的密碼，以確保帳號安全。</p>
            </td>
          </tr>

          <!-- 新密碼方塊 -->
          <tr>
            <td style=""padding:0 40px;"">
              <table role=""presentation"" cellspacing=""0"" cellpadding=""0"" border=""0"" width=""100%"" style=""background-color:#e6f6f6; border:1px solid #b9e6e7; border-radius:10px;"">
                <tr>
                  <td align=""center"" style=""padding:22px 16px;"">
                    <div style=""font-size:13px; color:#1d8e92; letter-spacing:1px; margin-bottom:8px;"">您的新密碼</div>
                    <div style=""font-family:'Courier New', Consolas, monospace; font-size:30px; font-weight:700; letter-spacing:6px; color:#156467;"">{newPassword}</div>
                  </td>
                </tr>
              </table>
            </td>
          </tr>

          <!-- CTA 按鈕 -->
          <tr>
            <td align=""center"" style=""padding:30px 40px 12px 40px;"">
              <table role=""presentation"" cellspacing=""0"" cellpadding=""0"" border=""0"">
                <tr>
                  <td align=""center"" bgcolor=""#26b7bc"" style=""border-radius:8px;"">
                    <a href=""{LoginUrl}"" target=""_blank"" style=""display:inline-block; padding:14px 40px; font-size:16px; font-weight:600; color:#ffffff; text-decoration:none; border-radius:8px; background-color:#26b7bc;"">登入會員中心</a>
                  </td>
                </tr>
              </table>
            </td>
          </tr>

          <!-- 安全提醒 -->
          <tr>
            <td style=""padding:14px 40px 4px 40px;"">
              <p style=""font-size:13px; line-height:1.7; color:#9aa3a3; margin:0;"">基於帳號安全，建議您登入後立即修改密碼。若您並未提出忘記密碼的申請，請忽略本信，並儘速與客服聯繫。</p>
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

    private static string HashPassword(string plaintext)
    {
        const int Iterations = 260_000;
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(plaintext),
            salt, Iterations,
            HashAlgorithmName.SHA256, 32);
        return $"pbkdf2:{Iterations}:{Convert.ToBase64String(salt)}:{Convert.ToBase64String(hash)}";
    }

    // ── Request records ───────────────────────────────────────────────────────────

    private sealed record RegisterRequest(string? Mobile, string? Password, string? Name, string? Email);
    private sealed record ForgotPasswordRequest(string? Mobile, string? Email);
}
