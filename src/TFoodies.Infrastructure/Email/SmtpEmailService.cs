using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;
using TFoodies.Application.Abstractions;

namespace TFoodies.Infrastructure.Email;

/// <summary>
/// SMTP 郵件服務實作（對應舊系統 Librarys.SendMail，預設走 Sendinblue/Brevo relay）。
/// 與舊版差異：失敗時回傳 false 而不無限遞迴重送（舊系統的已知 bug）。
/// </summary>
public sealed class SmtpEmailService : IEmailService
{
    private readonly SmtpOptions _options;

    public SmtpEmailService(IOptions<SmtpOptions> options)
    {
        _options = options.Value;
    }

    // SmtpClient.Timeout 只作用於同步 Send，SendMailAsync 不理會它——relay 一不通就會卡到 TCP 逾時
    // （下單 API 是同步 await 寄信，畫面會整個停在「送出中…」）。改用 CancellationToken 自行設上限。
    private static readonly TimeSpan SendTimeout = TimeSpan.FromSeconds(10);

    public async Task<bool> SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default)
    {
        try
        {
            using var mail = new MailMessage
            {
                From = new MailAddress(_options.FromAddress, _options.FromName),
                Subject = subject,
                Body = htmlBody,
                IsBodyHtml = true,
                Priority = MailPriority.Normal,
            };
            mail.To.Add(to);

            // 舊系統固定密件副本給營運信箱，沿用以維持通知一致性（可由設定關閉）。
            // 跳過與收件人相同者：營運信箱本身就在 Bcc 名單內，若某封信正是寄給他
            // （如收款連結的付款通知），To + Bcc 會造成重複投遞。
            foreach (var bcc in _options.Bcc)
            {
                if (string.IsNullOrWhiteSpace(bcc)) continue;
                if (bcc.Trim().Equals(to?.Trim(), StringComparison.OrdinalIgnoreCase)) continue;
                mail.Bcc.Add(bcc.Trim());
            }

            using var client = new SmtpClient(_options.Host, _options.Port)
            {
                EnableSsl = _options.EnableSsl,
                Credentials = new NetworkCredential(_options.Username, _options.Password),
                Timeout = (int)SendTimeout.TotalMilliseconds,
            };

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(SendTimeout);

            await client.SendMailAsync(mail, timeoutCts.Token);
            return true;
        }
        catch
        {
            return false;
        }
    }
}

/// <summary>
/// SMTP 組態。對應 appsettings.json 中的 "Smtp" 節。
/// </summary>
public sealed class SmtpOptions
{
    public const string SectionName = "Smtp";

    public string Host { get; set; } = "smtp-relay.sendinblue.com";
    public int Port { get; set; } = 587;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FromAddress { get; set; } = "noreply@tfoodies.com";
    public string FromName { get; set; } = "食在呼 TFoodies";
    public bool EnableSsl { get; set; } = true;
    public string[] Bcc { get; set; } = [];

    /// <summary>
    /// 是否真的送出郵件。false 時改用 <see cref="ConsoleEmailService"/>，只把信件內容印到 console。
    /// 本機／測試環境務必設 false——local.settings.json 用的是正式 relay，會真的寄到顧客信箱。
    /// 預設 true，正式環境不需額外設定此鍵。
    /// </summary>
    public bool Enabled { get; set; } = true;
}
