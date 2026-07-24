using System.Net;
using System.Text.Json;
using HumanOS.Contracts.RoleExperience;
using HumanOS.Data;
using HumanOS.Storage;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.AzureFunctions.Api;

/// <summary>
/// Uploads a job description or résumé PDF/DOCX to Data Lake storage for
/// later agent-based extraction. Résumés use the shared "role-documents"
/// container; job descriptions use a per-tenant container (see
/// RoleDocumentStorageService.UploadJobDescriptionAsync) so extraction
/// (ExtractJobDescriptionFunction) can read them back per-tenant.
///
/// TODO: Derive PersonId from the validated Microsoft Entra token.
/// </summary>
public sealed class UploadRoleDocumentFunction
{
    private static readonly string[] AllowedDocumentTypes = { "job-description", "resume" };

    private static readonly string[] AllowedContentTypes =
    {
        "application/pdf",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
    };

    private readonly RoleDocumentStorageService _roleDocumentStorageService;
    private readonly HumanOsDbContext _dbContext;

    public UploadRoleDocumentFunction(RoleDocumentStorageService roleDocumentStorageService, HumanOsDbContext dbContext)
    {
        _roleDocumentStorageService = roleDocumentStorageService;
        _dbContext = dbContext;
    }

    [Function("UploadRoleDocument")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "post",
            Route = "people/{personId:guid}/role-documents")]
        HttpRequestData request,
        Guid personId,
        CancellationToken cancellationToken)
    {
        if (!_roleDocumentStorageService.IsConfigured)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.ServiceUnavailable,
                "DataLakeNotConfigured",
                "Document storage is not yet configured for this environment.",
                cancellationToken);
        }

        UploadRoleDocumentRequest? uploadRequest;

        try
        {
            uploadRequest = await JsonSerializer.DeserializeAsync<UploadRoleDocumentRequest>(
                request.Body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
                cancellationToken);
        }
        catch (JsonException)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidJson", "The request body contains invalid JSON.", cancellationToken);
        }

        if (uploadRequest is null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "RequestBodyRequired", "A request body is required.", cancellationToken);
        }

        if (!AllowedDocumentTypes.Contains(uploadRequest.DocumentType))
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.BadRequest,
                "InvalidDocumentType",
                $"documentType must be one of: {string.Join(", ", AllowedDocumentTypes)}.",
                cancellationToken);
        }

        if (!AllowedContentTypes.Contains(uploadRequest.ContentType))
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.BadRequest,
                "UnsupportedFileType",
                "Only PDF and DOCX files are supported.",
                cancellationToken);
        }

        byte[] fileBytes;

        try
        {
            fileBytes = Convert.FromBase64String(uploadRequest.ContentBase64);
        }
        catch (FormatException)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.BadRequest,
                "InvalidFileContent",
                "The file content is not valid base64.",
                cancellationToken);
        }

        using var contentStream = new MemoryStream(fileBytes);

        string storagePath;

        if (uploadRequest.DocumentType == "job-description")
        {
            var person = await _dbContext.People
                .AsNoTracking()
                .SingleOrDefaultAsync(p => p.PersonId == personId, cancellationToken);

            if (person is null)
            {
                return await FunctionResponseFactory.ErrorResponseAsync(
                    request, HttpStatusCode.NotFound, "PersonNotFound", "No person was found with that id.", cancellationToken);
            }

            if (person.TenantId is not Guid tenantId)
            {
                return await FunctionResponseFactory.ErrorResponseAsync(
                    request,
                    HttpStatusCode.BadRequest,
                    "NoOrganization",
                    "Uploading a job description requires an organization \u2014 this person has no Tenant.",
                    cancellationToken);
            }

            storagePath = await _roleDocumentStorageService.UploadJobDescriptionAsync(
                tenantId,
                personId,
                uploadRequest.FileName,
                contentStream,
                uploadRequest.ContentType,
                cancellationToken);
        }
        else
        {
            storagePath = await _roleDocumentStorageService.UploadAsync(
                personId,
                uploadRequest.DocumentType,
                uploadRequest.FileName,
                contentStream,
                uploadRequest.ContentType,
                cancellationToken);
        }

        var response = new UploadRoleDocumentResponse
        {
            PersonId = personId,
            DocumentType = uploadRequest.DocumentType,
            StoragePath = storagePath,
            UploadedDate = DateTime.UtcNow,
        };

        return await FunctionResponseFactory.CreatedResponseAsync(request, response, cancellationToken);
    }
}
