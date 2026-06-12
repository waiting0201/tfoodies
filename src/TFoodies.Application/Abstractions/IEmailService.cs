namespace TFoodies.Application.Abstractions;

/// <summary>
/// 電子郵件發送抽象介面。
/// </summary>
public interface IEmailService
{
    /// <summary>
    /// 發送一封 HTML 郵件。
    /// </summary>
    /// <param name="to">收件者 Email（單一位址）。</param>
    /// <param name="subject">郵件主旨。</param>
    /// <param name="htmlBody">HTML 內容。</param>
    /// <param name="ct">取消 token。</param>
    /// <returns>true 表示發送成功；false 表示發送失敗。</returns>
    Task<bool> SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default);
}
