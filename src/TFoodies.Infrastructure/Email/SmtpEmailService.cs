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
            foreach (var bcc in _options.Bcc)
            {
                if (!string.IsNullOrWhiteSpace(bcc)) mail.Bcc.Add(bcc.Trim());
            }

            using var client = new SmtpClient(_options.Host, _options.Port)
            {
                EnableSsl = _options.EnableSsl,
                Credentials = new NetworkCredential(_options.Username, _options.Password),
            };

            await client.SendMailAsync(mail, ct);
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
}
