namespace TFoodies.Infrastructure.Blob;

public sealed class AzureBlobOptions
{
    public const string SectionName = "AzureBlob";

    public string ConnectionString { get; init; } = string.Empty;
    public string ContainerName    { get; init; } = "tfoodies";
    /// <summary>
    /// Storage account 公開 URL（不含 container、不含結尾 /），對應舊系統 azure.blob.url。
    /// 完整圖片 URL = BaseUrl + "/" + ContainerName + "/" + fileName
    /// 例：https://tfoodiesblob.blob.core.windows.net
    /// </summary>
    public string BaseUrl          { get; init; } = string.Empty;

    /// <summary>BaseUrl + "/" + ContainerName，等同舊系統 BlobUrl。</summary>
    public string BlobUrl => $"{BaseUrl.TrimEnd('/')}/{ContainerName.ToLowerInvariant()}";
}
