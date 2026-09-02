using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Configuration;

namespace HumanOS.Storage;

/// <summary>
/// Stores skill-related reference documents (PDFs) in Azure Data Lake / Blob Storage.
/// Container name: tenantId (lowercase GUID, e.g. "81c35f10-60ab-4ca4-b2ec-6c4bee8d0c0b").
/// Blob path: "skills/{skillId}/Documents/{fileName}".
/// Reuses the same "DataLakeStorage" connection string as other storage services.
/// </summary>
public sealed class SkillDocumentStorageService
{
    private readonly BlobServiceClient? _blobServiceClient;

    public SkillDocumentStorageService(IConfiguration configuration)
    {
        var connectionString = configuration["DataLakeStorage"];

        _blobServiceClient = string.IsNullOrWhiteSpace(connectionString)
            ? null
            : new BlobServiceClient(connectionString);
    }

    public bool IsConfigured => _blobServiceClient is not null;

    /// <summary>
    /// Uploads a PDF document for a skill and returns its storage path.
    /// </summary>
    public async Task<string> UploadSkillDocumentAsync(
        Guid tenantId,
        Guid skillId,
        string fileName,
        Stream content,
        string contentType = "application/pdf",
        CancellationToken cancellationToken = default)
    {
        if (_blobServiceClient is null)
        {
            throw new InvalidOperationException(
                "Data Lake storage is not configured. Set the 'DataLakeStorage' connection string.");
        }

        var containerName = tenantId.ToString("D").ToLowerInvariant();
        var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
        await containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

        var safeFileName = string.IsNullOrWhiteSpace(fileName) ? "document.pdf" : fileName;
        var blobPath = $"skills/{skillId:D}/Documents/{safeFileName}";
        var blobClient = containerClient.GetBlobClient(blobPath);

        await blobClient.UploadAsync(
            content,
            new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders { ContentType = contentType },
            },
            cancellationToken);

        return blobPath;
    }

    /// <summary>
    /// Downloads a skill PDF document from the tenant's container.
    /// </summary>
    public async Task<Stream> DownloadSkillDocumentAsync(
        Guid tenantId,
        string storagePath,
        CancellationToken cancellationToken = default)
    {
        if (_blobServiceClient is null)
        {
            throw new InvalidOperationException(
                "Data Lake storage is not configured. Set the 'DataLakeStorage' connection string.");
        }

        var containerName = tenantId.ToString("D").ToLowerInvariant();
        var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
        var blobClient = containerClient.GetBlobClient(storagePath);

        var response = await blobClient.DownloadStreamingAsync(cancellationToken: cancellationToken);
        return response.Value.Content;
    }

    /// <summary>
    /// Deletes a skill PDF document from the tenant's container if it exists.
    /// </summary>
    public async Task DeleteSkillDocumentAsync(
        Guid tenantId,
        string storagePath,
        CancellationToken cancellationToken = default)
    {
        if (_blobServiceClient is null) return;

        var containerName = tenantId.ToString("D").ToLowerInvariant();
        var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
        var blobClient = containerClient.GetBlobClient(storagePath);

        await blobClient.DeleteIfExistsAsync(cancellationToken: cancellationToken);
    }
}
