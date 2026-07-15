using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Configuration;

namespace HumanOS.Storage;

/// <summary>
/// Stores job description and résumé documents (PDF/DOCX) in Azure Data
/// Lake / Blob Storage so a future agent-based extraction function can
/// read them back out and populate structured Job Description /
/// Professional Profile data.
///
/// TODO: Set the "DataLakeStorage" connection string application setting
/// once real Data Lake credentials are provided. Until then,
/// <see cref="IsConfigured"/> is false and uploads are rejected with a
/// clear error instead of silently failing or writing to a fake location.
///
/// TODO: Once credentials exist, confirm the "role-documents" container
/// uses private (non-public) access, add a lifecycle/retention policy,
/// and add virus scanning and a file-size limit before production use.
/// </summary>
public sealed class RoleDocumentStorageService
{
    private const string ContainerName = "role-documents";

    private readonly BlobServiceClient? _blobServiceClient;

    public RoleDocumentStorageService(IConfiguration configuration)
    {
        var connectionString = configuration["DataLakeStorage"];

        _blobServiceClient = string.IsNullOrWhiteSpace(connectionString)
            ? null
            : new BlobServiceClient(connectionString);
    }

    public bool IsConfigured => _blobServiceClient is not null;

    /// <summary>
    /// Uploads a document for the given person and returns its storage
    /// path. Throws <see cref="InvalidOperationException"/> if storage is
    /// not yet configured; callers should check <see cref="IsConfigured"/>
    /// first to return a clean HTTP error instead of an exception.
    /// </summary>
    public async Task<string> UploadAsync(
        Guid personId,
        string documentType,
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

        var containerClient = _blobServiceClient.GetBlobContainerClient(ContainerName);
        await containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

        var blobPath = $"{personId}/{documentType}/{Guid.NewGuid()}-{fileName}";
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
    /// Uploads a Job Description PDF/DOCX using a per-tenant container
    /// (container name = <paramref name="tenantId"/>, folder =
    /// "jobdescriptions") rather than the shared "role-documents"
    /// container used by <see cref="UploadAsync"/>. Keeping job
    /// descriptions in a tenant-isolated container makes it
    /// straightforward to apply per-tenant access policies later and
    /// matches how the source documents will be organized for the
    /// upcoming agent-based extraction pipeline
    /// (<see cref="HumanOS.Agents.JobDescriptionExtractionAgent"/>).
    /// </summary>
    public async Task<string> UploadJobDescriptionAsync(
        Guid tenantId,
        Guid personId,
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
        var containerClient = _blobServiceClient.GetBlobContainerClient(tenantId.ToString("D").ToLowerInvariant());
        await containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

        var blobPath = $"jobdescriptions/{personId}/{Guid.NewGuid()}-{fileName}";
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
    /// Downloads a previously-uploaded Job Description document from its
    /// tenant-scoped container so it can be handed to
    /// <see cref="HumanOS.Agents.JobDescriptionExtractionAgent"/> for
    /// text extraction.
    /// </summary>
    public async Task<Stream> DownloadJobDescriptionAsync(
        Guid tenantId,
        string storagePath,
        CancellationToken cancellationToken = default)
    {
        if (_blobServiceClient is null)
        {
            throw new InvalidOperationException(
                "Data Lake storage is not configured. Set the 'DataLakeStorage' " +
                "connection string application setting once credentials are provided.");
        }

        var containerClient = _blobServiceClient.GetBlobContainerClient(tenantId.ToString("D").ToLowerInvariant());
        var blobClient = containerClient.GetBlobClient(storagePath);

        var response = await blobClient.DownloadStreamingAsync(cancellationToken: cancellationToken);
        return response.Value.Content;
    }
}
