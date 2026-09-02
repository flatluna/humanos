using System.Net;
using HumanOS.Services;
using HumanOS.Storage;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>
/// Uploads a PDF reference document for a Skill into Azure Data Lake.
/// Container name: tenantId (lowercase GUID, e.g. "81c35f10-60ab-4ca4-b2ec-6c4bee8d0c0b").
/// Blob path: "skills/{skillId}/Documents/{fileName}".
/// Accepts PDF files only.
/// </summary>
public sealed class UploadSkillDocumentFunction
{
    private readonly SkillService _skillService;
    private readonly SkillDocumentStorageService _storageService;

    public UploadSkillDocumentFunction(SkillService skillService, SkillDocumentStorageService storageService)
    {
        _skillService = skillService;
        _storageService = storageService;
    }

    [Function("UploadSkillDocument")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "skills/{skillId:guid}/document")]
        HttpRequestData request,
        Guid skillId,
        CancellationToken cancellationToken)
    {
        if (!_storageService.IsConfigured)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.ServiceUnavailable, "StorageNotConfigured",
                "SkillDocumentStorageService is not configured (missing 'DataLakeStorage' connection string).",
                cancellationToken);
        }

        var skill = await _skillService.GetEntityByIdAsync(skillId, cancellationToken);
        if (skill is null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "SkillNotFound",
                $"No skill found with id {skillId}.", cancellationToken);
        }

        // Get file name from query parameter ?fileName=... or header X-File-Name or default to document.pdf
        var query = System.Web.HttpUtility.ParseQueryString(request.Url.Query);
        var fileName = query["fileName"];
        if (string.IsNullOrWhiteSpace(fileName) && request.Headers.TryGetValues("X-File-Name", out var headerValues))
        {
            fileName = headerValues.FirstOrDefault();
        }
        if (string.IsNullOrWhiteSpace(fileName))
        {
            fileName = "document.pdf";
        }

        // Validate PDF extension or content-type
        var contentType = request.Headers.TryGetValues("Content-Type", out var ctValues) ? ctValues.First() : "application/pdf";
        if (!fileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) && !contentType.Contains("pdf", StringComparison.OrdinalIgnoreCase))
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "OnlyPdfAllowed",
                "Only PDF files are allowed for skill reference documents.", cancellationToken);
        }

        if (!fileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            fileName += ".pdf";
        }

        var storagePath = await _storageService.UploadSkillDocumentAsync(
            skill.TenantId,
            skillId,
            fileName,
            request.Body,
            "application/pdf",
            cancellationToken);

        var updatedSkill = await _skillService.UpdateDocumentPathAsync(
            skillId,
            storagePath,
            fileName,
            cancellationToken);

        return await FunctionResponseFactory.SuccessResponseAsync(request, updatedSkill, cancellationToken: cancellationToken);
    }
}
