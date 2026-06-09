using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Options;
using TFoodies.Application.Abstractions;

namespace TFoodies.Infrastructure.Blob;

/// <summary>
/// Azure Blob Storage 實作（Singleton）。
/// Container name 與舊系統一致（tfoodies），公開讀取。
/// </summary>
public sealed class AzureBlobService : IBlobService
{
    private readonly BlobContainerClient _container;

    public AzureBlobService(IOptions<AzureBlobOptions> opts)
    {
        var options = opts.Value;
        var serviceClient = new BlobServiceClient(options.ConnectionString);
        _container = serviceClient.GetBlobContainerClient(options.ContainerName.ToLowerInvariant());
    }

    public async Task<string> UploadAsync(Stream stream, string fileName, string contentType, CancellationToken ct = default)
    {
        await _container.CreateIfNotExistsAsync(PublicAccessType.Blob, cancellationToken: ct);

        var blob = _container.GetBlobClient(fileName);
        await blob.UploadAsync(stream, new BlobHttpHeaders { ContentType = contentType }, cancellationToken: ct);

        return blob.Uri.AbsoluteUri;
    }

    public async Task DeleteAsync(string fileName, CancellationToken ct = default)
    {
        var blob = _container.GetBlobClient(fileName);
        await blob.DeleteIfExistsAsync(cancellationToken: ct);
    }
}
