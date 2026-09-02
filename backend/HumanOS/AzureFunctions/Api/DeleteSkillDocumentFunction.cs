using System.Net;
using HumanOS.Services;
using HumanOS.Storage;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>
/// Deletes the PDF reference document for a Skill from Data Lake and updates the DB record.
/// DELETE /skills/{skillId:guid}/document
/// </summary>
public sealed class DeleteSkillDocumentFunction
{
    private readonly SkillService _skillService;
    private readonly SkillDocumentStorageService _storageService;

    public DeleteSkillDocumentFunction(SkillService skillService, SkillDocumentStorageService storageService)
    {
        _skillService = skillService;
        _storageService = storageService;
    }

    [Function("DeleteSkillDocument")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "skills/{skillId:guid}/document")]
        HttpRequestData request,
        Guid skillId,
        CancellationToken cancellationToken)
    {
        var skill = await _skillService.GetEntityByIdAsync(skillId, cancellationToken);
        if (skill is null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "SkillNotFound",
                $"No skill found with id {skillId}.", cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(skill.DocumentStoragePath))
        {
            await _storageService.DeleteSkillDocumentAsync(skill.TenantId, skill.DocumentStoragePath, cancellationToken);
        }

        var updatedSkill = await _skillService.UpdateDocumentPathAsync(skillId, null, null, cancellationToken);
        return await FunctionResponseFactory.SuccessResponseAsync(request, updatedSkill, cancellationToken: cancellationToken);
    }
}
