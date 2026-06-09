namespace TFoodies.Application.Abstractions;

/// <summary>
/// 簡訊發送抽象介面。
/// </summary>
public interface ISmsService
{
    /// <summary>
    /// 發送簡訊至指定手機號碼。
    /// </summary>
    /// <param name="mobile">收訊手機號碼（台灣格式，如 0912345678）。</param>
    /// <param name="message">簡訊內容。</param>
    /// <param name="ct">取消 token。</param>
    /// <returns>true 表示發送成功；false 表示發送失敗。</returns>
    Task<bool> SendAsync(string mobile, string message, CancellationToken ct = default);
}
