using System.Net;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>DELETE /skills/{skillId} — hard-deletes a catalog Skill and
/// (via cascade) every Job Role link to it. Never touches any Job Role's
/// own free-text list.</summary>
public sealed class DeleteSkillFunction
{
    private readonly SkillService _skillService;

    public DeleteSkillFunction(SkillService skillService)
    {
        _skillService = skillService;
    }

    [Function("DeleteSkill")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "skills/{skillId:guid}")]
        HttpRequestData request,
        Guid skillId,
        CancellationToken cancellationToken)
    {
        var deleted = await _skillService.DeleteAsync(skillId, cancellationToken);

        if (!deleted)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "SkillNotFound", "The requested skill was not found.", cancellationToken);
        }

        return request.CreateResponse(HttpStatusCode.NoContent);
    }
}
