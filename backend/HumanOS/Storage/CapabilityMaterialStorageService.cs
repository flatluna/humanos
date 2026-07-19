using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Configuration;

namespace HumanOS.Storage;

/// <summary>
/// Stores the original raw-material files (PDFs, etc.) a subject-matter
/// expert attaches during Human OS Studio capability creation — its own,
/// dedicated, tenant-scoped container. Deliberately separate from
/// <see cref="RoleDocumentStorageService"/>'s "role-documents" /
/// per-tenant "jobdescriptions" containers, which serve a different
/// bounded concern (résumés/job descriptions for Role Experience).
///
/// Container name = "{tenantId}-capability-materials" — both
/// tenant-isolated (like RoleDocumentStorageService's per-tenant Job
/// Description container) AND its own dedicated container (not shared
/// with any other document type).
/// </summary>
public sealed class CapabilityMaterialStorageService
{
    private const string ContainerSuffix = "-capability-materials";

    private readonly BlobServiceClient? _blobServiceClient;

    public CapabilityMaterialStorageService(IConfiguration configuration)
    {
        var connectionString = configuration["DataLakeStorage"];

        _blobServiceClient = string.IsNullOrWhiteSpace(connectionString)
            ? null
            : new BlobServiceClient(connectionString);
    }

    public bool IsConfigured => _blobServiceClient is not null;

    /// <summary>
    /// Uploads a raw-material file (e.g. a PDF a user attaches while
    /// creating a capability in Human OS Studio) and returns its storage
    /// path within the tenant's dedicated capability-materials container.
    /// </summary>
    public async Task<string> UploadAsync(
        Guid tenantId,
        string fileName,
        Stream content,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        if (_blobServiceClient is null)
        {
            throw new InvalidOperationException(
                "Data Lake storage is not configured. Set the 'DataLakeStorage' " +
                "connection string application setting once credentials are provided.");
        }

        // Azure Blob Storage container names must be lowercase.
        var containerName = $"{tenantId.ToString("D").ToLowerInvariant()}{ContainerSuffix}";
        var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
        await containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

        var blobPath = $"{Guid.NewGuid()}-{fileName}";
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
}
