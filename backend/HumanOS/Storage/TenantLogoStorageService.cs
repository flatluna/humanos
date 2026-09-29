using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Configuration;

namespace HumanOS.Storage;

public sealed class TenantLogoStorageService
{
    private const string ContainerName = "tenant-assets";
    private readonly BlobServiceClient? _blobServiceClient;

    public TenantLogoStorageService(IConfiguration configuration)
    {
        var connectionString = configuration["DataLakeStorage"];
        _blobServiceClient = string.IsNullOrWhiteSpace(connectionString) ? null : new BlobServiceClient(connectionString);
    }

    public bool IsConfigured => _blobServiceClient is not null;

    public async Task UploadLogoAsync(Guid tenantId, Stream imageContent, string contentType, CancellationToken cancellationToken = default)
    {
        if (_blobServiceClient is null) throw new InvalidOperationException("Data Lake storage is not configured.");
        var container = _blobServiceClient.GetBlobContainerClient(ContainerName);
        await container.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
        var blob = container.GetBlobClient($"{tenantId:D}/logo/logo");
        await blob.UploadAsync(imageContent, new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders { ContentType = contentType },
        }, cancellationToken);
    }

    public async Task<BlobDownloadStreamingResult> DownloadLogoAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        if (_blobServiceClient is null) throw new InvalidOperationException("Data Lake storage is not configured.");
        var blob = _blobServiceClient.GetBlobContainerClient(ContainerName).GetBlobClient($"{tenantId:D}/logo/logo");
        return await blob.DownloadStreamingAsync(cancellationToken: cancellationToken);
    }
}
