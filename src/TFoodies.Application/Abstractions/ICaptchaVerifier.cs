namespace TFoodies.Application.Abstractions;

/// <summary>
/// 人機驗證（Google reCAPTCHA v3）抽象介面。前台公開寫入端點（如缺貨通知登記）
/// 在處理前先驗證使用者送出的 token，以阻擋自動化濫用。
/// </summary>
public interface ICaptchaVerifier
{
    /// <summary>
    /// 驗證前端 grecaptcha.execute 取得的 token。
    /// </summary>
    /// <param name="token">前端 reCAPTCHA token（v3）。</param>
    /// <param name="action">預期的 action 名稱（v3 會回傳 action，用於比對；傳 null 則略過比對）。</param>
    /// <param name="ct">取消 token。</param>
    /// <returns>true 表示通過（或未設定金鑰時略過驗證）；false 表示驗證失敗。</returns>
    Task<bool> VerifyAsync(string? token, string? action = null, CancellationToken ct = default);
}
