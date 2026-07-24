using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Configuration;

namespace HumanOS.Storage;

/// <summary>
/// Stores a Program's logo/cover image in Azure Data Lake / Blob Storage.
/// Same "resolve reference in SQL (Program.LogoStoragePath), stream blob
/// separately" split as <see cref="CapabilityGraphIllustrationStorageService"/>,
/// own fixed container so Program assets never mix with capability-graph
/// assets. Blob path uses the ProgramId only:
///
///   {programId}/logo.{ext}
/// </summary>
public sealed class ProgramLogoStorageService
{
    private const string ContainerName = "program-logos";

    private readonly BlobServiceClient? _blobServiceClient;

    public ProgramLogoStorageService(IConfiguration configuration)
    {
        var connectionString = configuration["DataLakeStorage"];

        _blobServiceClient = string.IsNullOrWhiteSpace(connectionString)
            ? null
            : new BlobServiceClient(connectionString);
    }

    public bool IsConfigured => _blobServiceClient is not null;

    /// <summary>Uploads a Program's logo and returns its blob StoragePath
    /// (to be saved on Program.LogoStoragePath). Overwrites any existing
    /// logo for this Program.</summary>
    public async Task<string> UploadLogoAsync(
        Guid programId,
        Stream imageContent,
        string contentType,
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

        var extension = contentType switch
        {
            "image/png" => "png",
            "image/jpeg" or "image/jpg" => "jpg",
            "image/webp" => "webp",
            "image/svg+xml" => "svg",
            _ => "png",
        };

        var blobPath = $"{programId:D}/logo.{extension}";
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

    public async Task<Stream> DownloadLogoAsync(
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
