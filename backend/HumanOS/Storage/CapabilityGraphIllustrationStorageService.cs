using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using HumanOS.Data;
using Microsoft.EntityFrameworkCore;
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

    /// <summary>Same as <see cref="UploadIllustrationAsync"/> but for a
    /// generated video file (2026-08-21 — Google Veo) — same container/path
    /// shape, just an ".mp4" extension and "video-" prefix instead of
    /// "image-" so both media types can coexist side by side for one
    /// section without colliding. Path is keyed by the video's OWN Guid
    /// (2026-08-22 fix — was a COUNT-based sequential index, which raced:
    /// two videos generated close together both computed the same count
    /// before either row was saved, producing the SAME blob path and
    /// silently overwriting one video's bytes with the other's).</summary>
    public async Task<string> UploadVideoAsync(
        Guid tenantId,
        Guid capabilityId,
        Guid nodeId,
        Guid videoId,
        Stream videoContent,
        string contentType = "video/mp4",
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

        var blobPath = $"{tenantId:D}/{capabilityId:D}/{nodeId:D}/video-{videoId:D}.mp4";
        var blobClient = containerClient.GetBlobClient(blobPath);

        await blobClient.UploadAsync(
            videoContent,
            new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders { ContentType = contentType },
            },
            cancellationToken);

        return blobPath;
    }

    /// <summary>Overwrites an EXISTING video blob at a known StoragePath in
    /// place (2026-08-22) — used for in-place edits like trimming, where
    /// the illustration row/id/path must stay the same, only the bytes
    /// change.</summary>
    public async Task OverwriteVideoAsync(string storagePath, Stream videoContent, CancellationToken cancellationToken = default)
    {
        if (_blobServiceClient is null)
        {
            throw new InvalidOperationException(
                "Data Lake storage is not configured. Set the 'DataLakeStorage' " +
                "connection string application setting.");
        }

        var containerClient = _blobServiceClient.GetBlobContainerClient(ContainerName);
        var blobClient = containerClient.GetBlobClient(storagePath);

        await blobClient.UploadAsync(
            videoContent,
            new BlobUploadOptions { HttpHeaders = new BlobHttpHeaders { ContentType = "video/mp4" } },
            cancellationToken);
    }

    /// <summary>
    /// Uploads a single course-level cover image representing the WHOLE
    /// capability (distinct from per-node illustrations) and returns its
    /// blob StoragePath (to be saved on CapabilityGraph.CoverImageStoragePath).
    /// Same "capability-graphs" container, path shape
    /// "{tenantId}/{capabilityId}/cover.png" (no nodeId segment).
    /// </summary>
    public async Task<string> UploadCoverImageAsync(
        Guid tenantId,
        Guid capabilityId,
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

        var blobPath = $"{tenantId:D}/{capabilityId:D}/cover.png";
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

    /// <summary>Same blob as <see cref="DownloadIllustrationAsync"/> but
    /// buffered into memory with its exact byte length known upfront
    /// (2026-08-22) — needed by callers that must set a real Content-Length/
    /// Accept-Ranges response instead of chunked transfer, which browsers
    /// require to reliably locate a trailing 'moov' atom in an .mp4 whose
    /// metadata wasn't written "faststart" (the video plays but its audio
    /// track silently fails to be recognized otherwise).</summary>
    public async Task<BinaryData> DownloadIllustrationBytesAsync(
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

        var response = await blobClient.DownloadContentAsync(cancellationToken: cancellationToken);
        return response.Value.Content;
    }


    /// <summary>Deletes one illustration/video blob (2026-08-21, used for
    /// Veo video deletion) — a no-op (not an error) if the blob is already
    /// gone, same "best-effort cleanup" convention as the rest of this
    /// class's callers.</summary>
    public async Task DeleteIllustrationAsync(string storagePath, CancellationToken cancellationToken = default)
    {
        if (_blobServiceClient is null)
        {
            throw new InvalidOperationException(
                "Data Lake storage is not configured. Set the 'DataLakeStorage' " +
                "connection string application setting.");
        }

        var containerClient = _blobServiceClient.GetBlobContainerClient(ContainerName);
        var blobClient = containerClient.GetBlobClient(storagePath);
        await blobClient.DeleteIfExistsAsync(cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Uploads the company/brand logo a designer attached at capability-
    /// creation time (2026-08-04) and returns its blob StoragePath (to be
    /// saved on CapabilityGraph.BrandLogoStoragePath). Same "capability-graphs"
    /// container, path shape "{tenantId}/{capabilityId}/brand-logo.png" (no
    /// nodeId segment, same as the cover image). Always stored as PNG — the
    /// gpt-image-1.5 image-edit endpoint that consumes this file requires a
    /// valid PNG, so the frontend upload control only accepts PNG files.
    /// </summary>
    public async Task<string> UploadBrandLogoAsync(
        Guid tenantId,
        Guid capabilityId,
        Stream imageContent,
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

        var blobPath = $"{tenantId:D}/{capabilityId:D}/brand-logo.png";
        var blobClient = containerClient.GetBlobClient(blobPath);

        await blobClient.UploadAsync(
            imageContent,
            new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders { ContentType = "image/png" },
            },
            cancellationToken);

        return blobPath;
    }

    /// <summary>Downloads a previously-uploaded brand logo image by its StoragePath.</summary>
    public async Task<Stream> DownloadBrandLogoAsync(
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

    /// <summary>
    /// Resolves the capability's brand logo (if one was attached at creation
    /// time) and downloads its bytes, ready to feed into
    /// <see cref="Agents.Studio.GraphIllustrationImageService.GenerateAsync"/>
    /// as a reference image. Used by every call site that generates
    /// illustrations AFTER initial capability creation (Assessment, Tutor
    /// Recall, BlueprintReview edits, AddCapabilityGraphNode) so the same
    /// brand logo keeps being used consistently, not just in the first batch
    /// of images. Best-effort: returns null on ANY failure (no logo attached,
    /// storage not configured, blob missing) so a missing/broken logo never
    /// blocks image generation.
    /// </summary>
    public async Task<byte[]?> TryGetBrandLogoBytesAsync(
        HumanOsDbContext dbContext,
        Guid capabilityId,
        CancellationToken cancellationToken = default)
    {
        if (_blobServiceClient is null)
        {
            return null;
        }

        try
        {
            var storagePath = await dbContext.CapabilityGraphs
                .AsNoTracking()
                .Where(g => g.CapabilityId == capabilityId)
                .Select(g => g.BrandLogoStoragePath)
                .FirstOrDefaultAsync(cancellationToken);

            if (string.IsNullOrWhiteSpace(storagePath))
            {
                return null;
            }

            await using var stream = await DownloadBrandLogoAsync(storagePath, cancellationToken);
            using var memoryStream = new MemoryStream();
            await stream.CopyToAsync(memoryStream, cancellationToken);
            return memoryStream.ToArray();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return null;
        }
    }
}
