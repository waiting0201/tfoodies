using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TFoodies.Application.Abstractions;

namespace TFoodies.Infrastructure.Email;

/// <summary>
/// 開發／測試用的假寄信服務：只把郵件內容輸出到 console（log），不真的送出 SMTP。
/// 本機 local.settings.json 的 SMTP 帳密是**正式** relay，一寄就會真的送到顧客信箱，
/// 因此 <c>Smtp:Enabled=false</c> 時由 DI 改註冊本類別。
/// </summary>
public sealed class ConsoleEmailService : IEmailService
{
    private readonly ILogger<ConsoleEmailService> _logger;
    private readonly SmtpOptions _options;

    public ConsoleEmailService(ILogger<ConsoleEmailService> logger, IOptions<SmtpOptions> options)
    {
        _logger = logger;
        _options = options.Value;
    }

    public Task<bool> SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default)
    {
        var bcc = string.Join(", ", _options.Bcc.Where(b => !string.IsNullOrWhiteSpace(b)));

        _logger.LogWarning(
            """
            ┌── [DRY-RUN EMAIL] 未實際寄出（Smtp:Enabled=false）────────────────
            │ From    : {FromName} <{FromAddress}>
            │ To      : {To}
            │ Bcc     : {Bcc}
            │ Subject : {Subject}
            ├── HTML Body ────────────────────────────────────────────────────
            {Body}
            └──────────────────────────────────────────────────────────────────
            """,
            _options.FromName, _options.FromAddress, to, bcc, subject, htmlBody);

        return Task.FromResult(true);
    }
}
