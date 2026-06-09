using System.Security.Cryptography;
using System.Text;
using Dapper;
using Microsoft.AspNetCore.Mvc;
using TFoodies.Api.Functions.Router;
using TFoodies.Application.Abstractions;

namespace TFoodies.Api.Functions.Controllers;

/// <summary>
/// 會員認證延伸端點（公開，無需 JWT）。
///   POST /auth/register                  — 會員註冊
///   POST /auth/forgot-password/send      — 發送驗證碼簡訊
///   POST /auth/forgot-password/reset     — 重設密碼
/// </summary>
public sealed class MemberAuthController
{
    private readonly IDbConnectionFactory _db;
    private readonly ISmsService _sms;

    public MemberAuthController(IDbConnectionFactory db, ISmsService sms)
    {
        _db  = db;
        _sms = sms;
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

    // POST /auth/forgot-password/send
    public async Task<IActionResult> ForgotPasswordSend(RouteContext ctx)
    {
        var ct = ctx.Request.HttpContext.RequestAborted;

        var body = await ctx.TryReadBodyAsync<ForgotSendRequest>(ct);
        if (body is null) return ctx.BadRequest("Request body 格式不正確。");
        if (string.IsNullOrWhiteSpace(body.Mobile)) return ctx.BadRequest("缺少 mobile 欄位。");

        using var conn = await _db.CreateOpenConnectionAsync(ct);

        var found = await conn.ExecuteScalarAsync<Guid?>(
            "SELECT memberid FROM Members WHERE mobile = @mobile",
            new { mobile = body.Mobile });

        if (found is null) return ctx.NotFound("找不到該手機號碼對應的會員。");

        var code   = Random.Shared.Next(100_000, 999_999).ToString();
        await _sms.SendAsync(body.Mobile, $"TFoodies 驗證碼：{code}，10分鐘有效", ct);

        var token = BuildResetToken(body.Mobile, code);
        return ctx.Ok(new { resetToken = token });
    }

    // POST /auth/forgot-password/reset
    public async Task<IActionResult> ForgotPasswordReset(RouteContext ctx)
    {
        var ct = ctx.Request.HttpContext.RequestAborted;

        var body = await ctx.TryReadBodyAsync<ForgotResetRequest>(ct);
        if (body is null) return ctx.BadRequest("Request body 格式不正確。");
        if (string.IsNullOrWhiteSpace(body.ResetToken))   return ctx.BadRequest("缺少 resetToken 欄位。");
        if (string.IsNullOrWhiteSpace(body.Code))         return ctx.BadRequest("缺少 code 欄位。");
        if (string.IsNullOrWhiteSpace(body.NewPassword) || body.NewPassword.Length < 6)
            return ctx.BadRequest("newPassword 至少 6 個字元。");

        var dotIdx = body.ResetToken.LastIndexOf('.');
        if (dotIdx < 0) return ctx.BadRequest("resetToken 格式無效。");

        var payloadB64 = body.ResetToken[..dotIdx];
        var sigB64     = body.ResetToken[(dotIdx + 1)..];

        string payload;
        try
        {
            payload = Encoding.UTF8.GetString(Convert.FromBase64String(payloadB64));
        }
        catch
        {
            return ctx.BadRequest("resetToken 格式無效。");
        }

        // 驗證 HMAC 簽章
        var expectedSig = ComputeHmac(payload);
        if (!string.Equals(expectedSig, sigB64, StringComparison.Ordinal))
            return ctx.BadRequest("resetToken 簽章驗證失敗。");

        // 解析 payload: mobile|code|expiry
        var parts = payload.Split('|');
        if (parts.Length != 3) return ctx.BadRequest("resetToken payload 格式無效。");

        var mobile = parts[0];
        var tokenCode   = parts[1];
        if (!long.TryParse(parts[2], out var expiry))
            return ctx.BadRequest("resetToken 過期時間格式無效。");

        if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() >= expiry)
            return ctx.BadRequest("驗證碼已過期，請重新申請。");

        if (!string.Equals(tokenCode, body.Code, StringComparison.Ordinal))
            return ctx.BadRequest("驗證碼不正確。");

        using var conn = await _db.CreateOpenConnectionAsync(ct);

        var hashed = HashPassword(body.NewPassword);
        await conn.ExecuteAsync(
            "UPDATE Members SET password = @password WHERE mobile = @mobile",
            new { password = hashed, mobile });

        return ctx.Ok(new { message = "密碼已重設" });
    }

    // ── Helpers ───────────────────────────────────────────────────────────────────

    private static readonly byte[] HmacKey = Encoding.UTF8.GetBytes("tfverify");

    private static string BuildResetToken(string mobile, string code)
    {
        var expiry  = DateTimeOffset.UtcNow.AddMinutes(10).ToUnixTimeSeconds();
        var payload = $"{mobile}|{code}|{expiry}";
        var payloadB64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(payload));
        var sig        = ComputeHmac(payload);
        return $"{payloadB64}.{sig}";
    }

    private static string ComputeHmac(string payload)
    {
        using var hmac = new HMACSHA256(HmacKey);
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        // base64url（移除 padding 以避免 '.' 衝突）
        return Convert.ToBase64String(hash)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

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
    private sealed record ForgotSendRequest(string? Mobile);
    private sealed record ForgotResetRequest(string? ResetToken, string? Code, string? NewPassword);
}
