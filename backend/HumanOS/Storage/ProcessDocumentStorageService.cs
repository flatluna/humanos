using System.Text.Encodings.Web;
using System.Text.Json;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using HumanOS.Contracts.ProcessDocuments;
using Microsoft.Extensions.Configuration;

namespace HumanOS.Storage;

/// <summary>
/// Stores process documents (original PDF, raw extraction, structured JSON)
/// in Azure Data Lake Storage.
/// Container: "process-documents" (private).
/// Structure:
///   process-documents/{organization_id}/{document_id}/source/{file_name}
///   process-documents/{organization_id}/{document_id}/extracted/document-intelligence-result.json
///   process-documents/{organization_id}/{document_id}/structured/process-structure.json
/// </summary>
public sealed class ProcessDocumentStorageService : IProcessDocumentStorageService
{
    public const string DefaultContainerName = "process-documents";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNameCaseInsensitive = true
    };

    private readonly BlobServiceClient? _blobServiceClient;

    public ProcessDocumentStorageService(IConfiguration configuration)
    {
        var connectionString = configuration["DataLakeStorage"];
        _blobServiceClient = string.IsNullOrWhiteSpace(connectionString)
            ? null
            : new BlobServiceClient(connectionString);
    }

    public ProcessDocumentStorageService(BlobServiceClient blobServiceClient)
    {
        _blobServiceClient = blobServiceClient;
    }

    public bool IsConfigured => _blobServiceClient is not null;

    private static string NormalizeContainerName(string organizationId)
    {
        if (string.IsNullOrWhiteSpace(organizationId))
        {
            return DefaultContainerName;
        }

        var cleaned = organizationId.Trim().ToLowerInvariant();
        if (System.Text.RegularExpressions.Regex.IsMatch(cleaned, @"^[a-z0-9][a-z0-9-]{1,61}[a-z0-9]$"))
        {
            return cleaned;
        }

        return DefaultContainerName;
    }

    private BlobContainerClient GetContainerClient(string organizationId)
    {
        if (_blobServiceClient is null)
        {
            throw new InvalidOperationException(
                "Data Lake storage is not configured. Set the 'DataLakeStorage' connection string.");
        }

        var containerName = NormalizeContainerName(organizationId);
        return _blobServiceClient.GetBlobContainerClient(containerName);
    }

    public async Task<string> UploadSourcePdfAsync(
        string organizationId,
        string documentId,
        string fileName,
        Stream content,
        CancellationToken cancellationToken = default)
    {
        var containerClient = GetContainerClient(organizationId);
        await containerClient.CreateIfNotExistsAsync(
            PublicAccessType.None,
            cancellationToken: cancellationToken);

        var safeFileName = string.IsNullOrWhiteSpace(fileName) ? "original.pdf" : Path.GetFileName(fileName);
        var blobPath = $"process-documents/{documentId}/source/{safeFileName}";
        var blobClient = containerClient.GetBlobClient(blobPath);

        if (content.CanSeek)
        {
            content.Position = 0;
        }

        await blobClient.UploadAsync(
            content,
            new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders { ContentType = "application/pdf" }
            },
            cancellationToken);

        return $"{containerClient.Name}/{blobPath}";
    }

    public async Task<string> SaveRawExtractionAsync(
        string organizationId,
        string documentId,
        RawDocumentExtractionResult rawResult,
        CancellationToken cancellationToken = default)
    {
        var containerClient = GetContainerClient(organizationId);
        await containerClient.CreateIfNotExistsAsync(
            PublicAccessType.None,
            cancellationToken: cancellationToken);

        var blobPath = $"process-documents/{documentId}/extracted/document-intelligence-result.json";
        var blobClient = containerClient.GetBlobClient(blobPath);

        var json = JsonSerializer.Serialize(rawResult, JsonOptions);
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));

        await blobClient.UploadAsync(
            stream,
            new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders { ContentType = "application/json; charset=utf-8" }
            },
            cancellationToken);

        return $"{containerClient.Name}/{blobPath}";
    }

    public async Task<string> SaveStructuredResultAsync(
        string organizationId,
        string documentId,
        ProcessDocumentStructure structuredResult,
        CancellationToken cancellationToken = default)
    {
        var containerClient = GetContainerClient(organizationId);
        await containerClient.CreateIfNotExistsAsync(
            PublicAccessType.None,
            cancellationToken: cancellationToken);

        var blobPath = $"process-documents/{documentId}/structured/process-structure.json";
        var blobClient = containerClient.GetBlobClient(blobPath);

        var json = JsonSerializer.Serialize(structuredResult, JsonOptions);
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));

        await blobClient.UploadAsync(
            stream,
            new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders { ContentType = "application/json; charset=utf-8" }
            },
            cancellationToken);

        return $"{containerClient.Name}/{blobPath}";
    }

    public async Task<ProcessDocumentStructure?> GetStructuredResultAsync(
        string organizationId,
        string documentId,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured) return null;

        var containerClient = GetContainerClient(organizationId);
        var blobPath = $"process-documents/{documentId}/structured/process-structure.json";
        var blobClient = containerClient.GetBlobClient(blobPath);

        if (!await blobClient.ExistsAsync(cancellationToken))
        {
            // Fallback for legacy layout in DefaultContainerName
            var fallbackClient = _blobServiceClient!.GetBlobContainerClient(DefaultContainerName);
            if (await fallbackClient.ExistsAsync(cancellationToken))
            {
                var legacyBlob = fallbackClient.GetBlobClient($"{organizationId}/{documentId}/structured/process-structure.json");
                if (await legacyBlob.ExistsAsync(cancellationToken))
                {
                    var legacyDownload = await legacyBlob.DownloadContentAsync(cancellationToken);
                    return JsonSerializer.Deserialize<ProcessDocumentStructure>(legacyDownload.Value.Content, JsonOptions);
                }
            }
            return null;
        }

        var download = await blobClient.DownloadContentAsync(cancellationToken);
        return JsonSerializer.Deserialize<ProcessDocumentStructure>(download.Value.Content, JsonOptions);
    }

    public async Task<bool> HasDocumentAsync(
        string organizationId,
        string documentId,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured) return false;

        var containerClient = GetContainerClient(organizationId);
        var blobPath = $"process-documents/{documentId}/structured/process-structure.json";
        var blobClient = containerClient.GetBlobClient(blobPath);
        if (await blobClient.ExistsAsync(cancellationToken)) return true;

        var fallbackClient = _blobServiceClient!.GetBlobContainerClient(DefaultContainerName);
        if (await fallbackClient.ExistsAsync(cancellationToken))
        {
            var legacyBlob = fallbackClient.GetBlobClient($"{organizationId}/{documentId}/structured/process-structure.json");
            return await legacyBlob.ExistsAsync(cancellationToken);
        }
        return false;
    }

    public async Task<string> SaveEmployeeAssignmentAsync(
        string organizationId,
        string assignmentId,
        EmployeeStepLearningAssignmentResponse assignment,
        CancellationToken cancellationToken = default)
    {
        var containerClient = GetContainerClient(organizationId);
        await containerClient.CreateIfNotExistsAsync(
            PublicAccessType.None,
            cancellationToken: cancellationToken);

        var blobPath = $"process-documents/employee-assignments/{assignmentId}.json";
        var blobClient = containerClient.GetBlobClient(blobPath);

        var json = JsonSerializer.Serialize(assignment, JsonOptions);
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));

        await blobClient.UploadAsync(
            stream,
            new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders { ContentType = "application/json; charset=utf-8" }
            },
            cancellationToken);

        return $"{containerClient.Name}/{blobPath}";
    }

    public async Task<EmployeeStepLearningAssignmentResponse?> GetEmployeeAssignmentAsync(
        string organizationId,
        string assignmentId,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured) return null;

        var containerClient = GetContainerClient(organizationId);
        var blobPath = $"process-documents/employee-assignments/{assignmentId}.json";
        var blobClient = containerClient.GetBlobClient(blobPath);

        if (!await blobClient.ExistsAsync(cancellationToken))
        {
            return null;
        }

        var download = await blobClient.DownloadContentAsync(cancellationToken);
        return JsonSerializer.Deserialize<EmployeeStepLearningAssignmentResponse>(download.Value.Content, JsonOptions);
    }
}
