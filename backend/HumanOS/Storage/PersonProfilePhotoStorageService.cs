using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Configuration;

namespace HumanOS.Storage;

/// <summary>
/// Stores a Person's own profile photo in Azure Data Lake / Blob Storage.
/// Same "resolve reference in SQL (PersonProfile.ProfilePhotoUrl), stream
/// blob separately" split as <see cref="ProgramLogoStorageService"/>, own
/// fixed container so profile photos never mix with other assets. Blob
/// path uses the PersonId only:
///
///   {personId}/photo.{ext}
/// </summary>
public sealed class PersonProfilePhotoStorageService
{
    private const string ContainerName = "person-profile-photos";

    private readonly BlobServiceClient? _blobServiceClient;

    public PersonProfilePhotoStorageService(IConfiguration configuration)
    {
        var connectionString = configuration["DataLakeStorage"];

        _blobServiceClient = string.IsNullOrWhiteSpace(connectionString)
            ? null
            : new BlobServiceClient(connectionString);
    }

    public bool IsConfigured => _blobServiceClient is not null;

    /// <summary>Uploads a person's profile photo and returns its blob
    /// StoragePath (to be saved on PersonProfile.ProfilePhotoUrl).
    /// Overwrites any existing photo for this person.</summary>
    public async Task<string> UploadPhotoAsync(
        Guid personId,
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
            _ => "png",
        };

        var blobPath = $"{personId:D}/photo.{extension}";
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

    public async Task<Stream> DownloadPhotoAsync(
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
