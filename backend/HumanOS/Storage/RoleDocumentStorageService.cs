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

    public async Task<string> UploadThesisAsync(
        Guid tenantId,
        Guid personId,
        string fileName,
        Stream content,
        CancellationToken cancellationToken = default)
    {
        if (_blobServiceClient is null)
        {
            throw new InvalidOperationException(
                "Data Lake storage is not configured. Set the 'DataLakeStorage' " +
                "connection string application setting once credentials are provided.");
        }

        var containerClient = _blobServiceClient.GetBlobContainerClient(tenantId.ToString("D").ToLowerInvariant());
        await containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

        var safeFileName = string.IsNullOrWhiteSpace(fileName) ? "tesis.pdf" : Path.GetFileName(fileName);
        var blobPath = $"documents/tesis/{personId:D}/{safeFileName}";
        await containerClient.GetBlobClient(blobPath).UploadAsync(
            content,
            new BlobUploadOptions { HttpHeaders = new BlobHttpHeaders { ContentType = "application/pdf" } },
            cancellationToken);

        return blobPath;
    }

    /// <summary>Finds the PDF file name for a previously-uploaded thesis, or null if none exists.</summary>
    public async Task<string?> FindThesisFileNameAsync(Guid tenantId, Guid personId, CancellationToken cancellationToken = default)
    {
        if (_blobServiceClient is null) throw new InvalidOperationException("Data Lake storage is not configured.");
        var containerClient = _blobServiceClient.GetBlobContainerClient(tenantId.ToString("D").ToLowerInvariant());
        var prefix = $"documents/tesis/{personId:D}/";
        await foreach (var blob in containerClient.GetBlobsAsync(prefix: prefix, cancellationToken: cancellationToken))
        {
            var name = blob.Name[prefix.Length..];
            if (name.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)) return name;
        }
        return null;
    }

    public async Task<Stream> DownloadThesisAsync(
        Guid tenantId,
        Guid personId,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        if (_blobServiceClient is null)
        {
            throw new InvalidOperationException("Data Lake storage is not configured.");
        }

        var containerClient = _blobServiceClient.GetBlobContainerClient(tenantId.ToString("D").ToLowerInvariant());
        var blobPath = $"documents/tesis/{personId:D}/{Path.GetFileName(fileName)}";
        var response = await containerClient.GetBlobClient(blobPath).DownloadStreamingAsync(cancellationToken: cancellationToken);
        return response.Value.Content;
    }

    public async Task<string> UploadThesisMarkdownAsync(Guid tenantId, Guid personId, string fileName, Stream content, CancellationToken cancellationToken = default)
    {
        if (_blobServiceClient is null) throw new InvalidOperationException("Data Lake storage is not configured.");
        var containerClient = _blobServiceClient.GetBlobContainerClient(tenantId.ToString("D").ToLowerInvariant());
        await containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
        var safeBaseName = Path.GetFileNameWithoutExtension(fileName);
        if (string.IsNullOrWhiteSpace(safeBaseName)) safeBaseName = "tesis";
        var blobPath = $"documents/tesis/{personId:D}/{safeBaseName}.md";
        await containerClient.GetBlobClient(blobPath).UploadAsync(content, new BlobUploadOptions { HttpHeaders = new BlobHttpHeaders { ContentType = "text/markdown; charset=utf-8" } }, cancellationToken);
        return blobPath;
    }

    public async Task DeleteThesisAsync(Guid tenantId, Guid personId, string fileName, CancellationToken cancellationToken = default)
    {
        if (_blobServiceClient is null) throw new InvalidOperationException("Data Lake storage is not configured.");
        var containerClient = _blobServiceClient.GetBlobContainerClient(tenantId.ToString("D").ToLowerInvariant());
        var safeFileName = Path.GetFileName(fileName);
        await containerClient.GetBlobClient($"documents/tesis/{personId:D}/{safeFileName}").DeleteIfExistsAsync(cancellationToken: cancellationToken);
        await containerClient.GetBlobClient($"documents/tesis/{personId:D}/{Path.GetFileNameWithoutExtension(safeFileName)}.md").DeleteIfExistsAsync(cancellationToken: cancellationToken);
    }

    public async Task<Stream> DownloadThesisMarkdownAsync(Guid tenantId, Guid personId, string fileName, CancellationToken cancellationToken = default)
    {
        if (_blobServiceClient is null) throw new InvalidOperationException("Data Lake storage is not configured.");
        var containerClient = _blobServiceClient.GetBlobContainerClient(tenantId.ToString("D").ToLowerInvariant());
        var markdownName = $"{Path.GetFileNameWithoutExtension(Path.GetFileName(fileName))}.md";
        var response = await containerClient.GetBlobClient($"documents/tesis/{personId:D}/{markdownName}").DownloadStreamingAsync(cancellationToken: cancellationToken);
        return response.Value.Content;
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
