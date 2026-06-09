namespace TFoodies.Application.Abstractions;

public interface IBlobService
{
    /// <summary>
    /// 上傳串流到 Blob Storage，回傳完整的公開 URL。
    /// </summary>
    Task<string> UploadAsync(Stream stream, string fileName, string contentType, CancellationToken ct = default);

    /// <summary>
    /// 刪除指定檔名的 blob（找不到時靜默忽略）。
    /// </summary>
    Task DeleteAsync(string fileName, CancellationToken ct = default);
}
