using System.Net;
using System.Text;
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
    private static readonly string[] AllowedDocumentTypes = { "job-description", "resume", "thesis" };

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

            if (uploadRequest.DocumentType == "thesis" &&
                (!string.Equals(uploadRequest.ContentType, "application/pdf", StringComparison.OrdinalIgnoreCase) ||
                 !uploadRequest.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)))
            {
                return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "OnlyPdfAllowed",
                "Only PDF files are allowed for a thesis.", cancellationToken);
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
        else if (uploadRequest.DocumentType == "thesis")
        {
            var person = await _dbContext.People
                .AsNoTracking()
                .SingleOrDefaultAsync(p => p.PersonId == personId, cancellationToken);

            if (person is null)
            {
                return await FunctionResponseFactory.ErrorResponseAsync(
                    request, HttpStatusCode.NotFound, "PersonNotFound", "No person was found with that id.", cancellationToken);
            }

            var tenantId = person.TenantId;
            if (tenantId is null && Guid.TryParse(person.AzureTid, out var personTenantId))
            {
                tenantId = personTenantId;
            }

            if (tenantId is null &&
                request.Headers.TryGetValues("X-Azure-TID", out var tenantValues) &&
                Guid.TryParse(tenantValues.FirstOrDefault(), out var headerTenantId))
            {
                tenantId = headerTenantId;
            }

            if (tenantId is not Guid resolvedTenantId)
            {
                return await FunctionResponseFactory.ErrorResponseAsync(
                    request, HttpStatusCode.BadRequest, "NoOrganization",
                    "Uploading a thesis requires an organization — this student has no tenant.", cancellationToken);
            }

            storagePath = await _roleDocumentStorageService.UploadThesisAsync(
                resolvedTenantId, personId, uploadRequest.FileName, contentStream, cancellationToken);
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

    [Function("DownloadThesisDocument")]
    public async Task<HttpResponseData> DownloadThesisAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "people/{personId:guid}/thesis/{fileName}")] HttpRequestData request,
        Guid personId,
        string fileName,
        CancellationToken cancellationToken)
    {
        var person = await _dbContext.People.AsNoTracking().SingleOrDefaultAsync(p => p.PersonId == personId, cancellationToken);
        if (person is null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(request, HttpStatusCode.NotFound, "PersonNotFound", "No person was found with that id.", cancellationToken);
        }

        var tenantId = person.TenantId;
        if (tenantId is null && Guid.TryParse(person.AzureTid, out var personTenantId)) tenantId = personTenantId;
        if (tenantId is null && request.Headers.TryGetValues("X-Azure-TID", out var tenantValues) && Guid.TryParse(tenantValues.FirstOrDefault(), out var headerTenantId)) tenantId = headerTenantId;
        if (tenantId is not Guid resolvedTenantId)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(request, HttpStatusCode.BadRequest, "NoOrganization", "The student has no tenant.", cancellationToken);
        }

        try
        {
            var content = await _roleDocumentStorageService.DownloadThesisAsync(resolvedTenantId, personId, fileName, cancellationToken);
            var response = request.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/pdf");
            response.Headers.Add("Content-Disposition", $"attachment; filename=\"{Path.GetFileName(fileName)}\"");
            await content.CopyToAsync(response.Body, cancellationToken);
            await content.DisposeAsync();
            return response;
        }
        catch (Azure.RequestFailedException exception) when (exception.Status == 404)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(request, HttpStatusCode.NotFound, "ThesisNotFound", "The thesis PDF was not found.", cancellationToken);
        }
    }

    [Function("ProcessThesisDocument")]
    public async Task<HttpResponseData> ProcessThesisAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "people/{personId:guid}/thesis/{fileName}/extract")] HttpRequestData request,
        Guid personId, string fileName, CancellationToken cancellationToken)
    {
        var tenantId = await ResolveTenantIdAsync(request, personId, cancellationToken);
        if (tenantId is null) return await FunctionResponseFactory.ErrorResponseAsync(request, HttpStatusCode.BadRequest, "NoOrganization", "The student has no tenant.", cancellationToken);
        try
        {
            await using var downloadedPdf = await _roleDocumentStorageService.DownloadThesisAsync(tenantId.Value, personId, fileName, cancellationToken);
            await using var pdf = new MemoryStream();
            await downloadedPdf.CopyToAsync(pdf, cancellationToken);
            pdf.Position = 0;
            var pages = PdfTextExtractor.ExtractPagesWithImages(pdf);
            var markdown = new StringBuilder($"# {Path.GetFileNameWithoutExtension(fileName)}\n\n> Texto extraído del PDF. Páginas: {pages.Count}\n\n");
            foreach (var page in pages)
            {
                markdown.AppendLine($"## Página {page.PageNumber}").AppendLine().AppendLine(string.IsNullOrWhiteSpace(page.Text) ? "_Sin texto extraíble en esta página._" : page.Text.Trim()).AppendLine();
            }
            await using var markdownStream = new MemoryStream(Encoding.UTF8.GetBytes(markdown.ToString()));
            var storagePath = await _roleDocumentStorageService.UploadThesisMarkdownAsync(tenantId.Value, personId, fileName, markdownStream, cancellationToken);
            return await FunctionResponseFactory.SuccessResponseAsync(request, new { personId, storagePath, pageCount = pages.Count }, cancellationToken: cancellationToken);
        }
        catch (Azure.RequestFailedException exception) when (exception.Status == 404)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(request, HttpStatusCode.NotFound, "ThesisNotFound", "The thesis PDF was not found.", cancellationToken);
        }
    }

    [Function("DeleteThesisDocument")]
    public async Task<HttpResponseData> DeleteThesisAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "people/{personId:guid}/thesis/{fileName}")] HttpRequestData request,
        Guid personId, string fileName, CancellationToken cancellationToken)
    {
        var tenantId = await ResolveTenantIdAsync(request, personId, cancellationToken);
        if (tenantId is null) return await FunctionResponseFactory.ErrorResponseAsync(request, HttpStatusCode.BadRequest, "NoOrganization", "The student has no tenant.", cancellationToken);
        await _roleDocumentStorageService.DeleteThesisAsync(tenantId.Value, personId, fileName, cancellationToken);
        return request.CreateResponse(HttpStatusCode.NoContent);
    }

    [Function("DownloadThesisMarkdown")]
    public async Task<HttpResponseData> DownloadThesisMarkdownAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "people/{personId:guid}/thesis/{fileName}/markdown")] HttpRequestData request,
        Guid personId, string fileName, CancellationToken cancellationToken)
    {
        var tenantId = await ResolveTenantIdAsync(request, personId, cancellationToken);
        if (tenantId is null) return await FunctionResponseFactory.ErrorResponseAsync(request, HttpStatusCode.BadRequest, "NoOrganization", "The student has no tenant.", cancellationToken);
        try
        {
            await using var content = await _roleDocumentStorageService.DownloadThesisMarkdownAsync(tenantId.Value, personId, fileName, cancellationToken);
            var response = request.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "text/markdown; charset=utf-8");
            response.Headers.Add("Content-Disposition", $"attachment; filename=\"{Path.GetFileNameWithoutExtension(fileName)}.md\"");
            await content.CopyToAsync(response.Body, cancellationToken);
            return response;
        }
        catch (Azure.RequestFailedException exception) when (exception.Status == 404)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(request, HttpStatusCode.NotFound, "ThesisMarkdownNotFound", "The extracted Markdown was not found. Process the thesis first.", cancellationToken);
        }
    }

    [Function("GetThesisStatus")]
    public async Task<HttpResponseData> GetThesisStatusAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "people/{personId:guid}/thesis")] HttpRequestData request,
        Guid personId,
        CancellationToken cancellationToken)
    {
        var tenantId = await ResolveTenantIdAsync(request, personId, cancellationToken);
        if (tenantId is null) return await FunctionResponseFactory.ErrorResponseAsync(request, HttpStatusCode.BadRequest, "NoOrganization", "The student has no tenant.", cancellationToken);

        var fileName = await _roleDocumentStorageService.FindThesisFileNameAsync(tenantId.Value, personId, cancellationToken);
        if (fileName is null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(request, HttpStatusCode.NotFound, "ThesisNotFound", "No thesis has been uploaded yet.", cancellationToken);
        }

        return await FunctionResponseFactory.SuccessResponseAsync(request, new { personId, fileName }, cancellationToken: cancellationToken);
    }

    private async Task<Guid?> ResolveTenantIdAsync(HttpRequestData request, Guid personId, CancellationToken cancellationToken)
    {
        var person = await _dbContext.People.AsNoTracking().SingleOrDefaultAsync(p => p.PersonId == personId, cancellationToken);
        if (person is null) return null;
        if (person.TenantId is Guid tenantId) return tenantId;
        if (Guid.TryParse(person.AzureTid, out var personTenantId)) return personTenantId;
        if (request.Headers.TryGetValues("X-Azure-TID", out var values) && Guid.TryParse(values.FirstOrDefault(), out var headerTenantId)) return headerTenantId;
        return null;
    }
}
