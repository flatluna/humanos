using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Configuration;

namespace HumanOS.Storage;

/// <summary>
/// Stores CapabilityGraphNode illustration images in Azure Data Lake / Blob
/// Storage. Images are NEVER stored in SQL — only their StoragePath (plus
/// generation metadata) is persisted as <see cref="HumanOS.Models.Capabilities.Graph.CapabilityGraphNodeIllustration"/>.
///
/// Container: "capability-graphs" (fixed, lowercase — Azure Blob Storage
/// requirement). Blob path uses IDs only, never human-readable names, so
/// paths stay stable across renames and don't leak content through the URL:
///
///   {tenantId}/{capabilityId}/{nodeId}/image-{NN}.png
///
/// Reuses the same "DataLakeStorage" connection string as
/// <see cref="RoleDocumentStorageService"/>.
/// </summary>
public sealed class CapabilityGraphIllustrationStorageService
{
    private const string ContainerName = "capability-graphs";

    private readonly BlobServiceClient? _blobServiceClient;

    public CapabilityGraphIllustrationStorageService(IConfiguration configuration)
    {
        var connectionString = configuration["DataLakeStorage"];

        _blobServiceClient = string.IsNullOrWhiteSpace(connectionString)
            ? null
            : new BlobServiceClient(connectionString);
    }

    public bool IsConfigured => _blobServiceClient is not null;

    /// <summary>
    /// Uploads one illustration image and returns its blob StoragePath
    /// (to be saved on a CapabilityGraphNodeIllustration row).
    /// </summary>
    public async Task<string> UploadIllustrationAsync(
        Guid tenantId,
        Guid capabilityId,
        Guid nodeId,
        int imageIndex,
        Stream imageContent,
        string contentType = "image/png",
        CancellationToken cancellationToken = default)
    {
        if (_blobServiceClient is null)
        {
            throw new InvalidOperationException(
                "Data Lake storage is not configured. Set the 'DataLakeStorage' " +
                "connection string application setting.");
        }

        var containerClient = _blobServiceClient.GetBlobContainerClient(ContainerName);
        await containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

        var blobPath = $"{tenantId:D}/{capabilityId:D}/{nodeId:D}/image-{imageIndex:D2}.png";
        var blobClient = containerClient.GetBlobClient(blobPath);

        await blobClient.UploadAsync(
            imageContent,
            new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders { ContentType = contentType },
            },
            cancellationToken);

        return blobPath;
    }

    /// <summary>Downloads a previously-uploaded illustration image by its StoragePath.</summary>
    public async Task<Stream> DownloadIllustrationAsync(
        string storagePath,
        CancellationToken cancellationToken = default)
    {
        if (_blobServiceClient is null)
        {
            throw new InvalidOperationException(
                "Data Lake storage is not configured. Set the 'DataLakeStorage' " +
                "connection string application setting.");
        }

        var containerClient = _blobServiceClient.GetBlobContainerClient(ContainerName);
        var blobClient = containerClient.GetBlobClient(storagePath);

        var response = await blobClient.DownloadStreamingAsync(cancellationToken: cancellationToken);
        return response.Value.Content;
    }
}
