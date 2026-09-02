using System.Net;
using HumanOS.Services;
using HumanOS.Storage;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>
/// Streams back the uploaded PDF reference document for a Skill directly from Data Lake.
/// GET /skills/{skillId:guid}/document
/// </summary>
public sealed class GetSkillDocumentFunction
{
    private readonly SkillService _skillService;
    private readonly SkillDocumentStorageService _storageService;

    public GetSkillDocumentFunction(SkillService skillService, SkillDocumentStorageService storageService)
    {
        _skillService = skillService;
        _storageService = storageService;
    }

    [Function("GetSkillDocument")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "skills/{skillId:guid}/document")]
        HttpRequestData request,
        Guid skillId,
        CancellationToken cancellationToken)
    {
        if (!_storageService.IsConfigured)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.ServiceUnavailable, "StorageNotConfigured",
                "SkillDocumentStorageService is not configured.", cancellationToken);
        }

        var skill = await _skillService.GetEntityByIdAsync(skillId, cancellationToken);
        if (skill is null || string.IsNullOrWhiteSpace(skill.DocumentStoragePath))
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "DocumentNotFound",
                "No document is attached to this skill.", cancellationToken);
        }

        try
        {
            var stream = await _storageService.DownloadSkillDocumentAsync(
                skill.TenantId, skill.DocumentStoragePath, cancellationToken);

            var response = request.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/pdf");
            var dispositionName = string.IsNullOrWhiteSpace(skill.DocumentFileName) ? "document.pdf" : skill.DocumentFileName;
            response.Headers.Add("Content-Disposition", $"inline; filename=\"{dispositionName}\"");

            await stream.CopyToAsync(response.Body, cancellationToken);
            return response;
        }
        catch (Exception ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.InternalServerError, "DownloadError", ex.Message, cancellationToken);
        }
    }
}
