using System.Net;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>GET /skills/{skillId}/linked-processes — reverse lookup of
/// ProcessSkillRequirement.MatchedSkillId, listing every business process
/// that has linked this catalog skill (Step 4 "Link to Process"). Lets HR
/// see the impact of editing/deleting a Skill from the catalog.</summary>
public sealed class GetSkillLinkedProcessesFunction
{
    private readonly SkillService _skillService;

    public GetSkillLinkedProcessesFunction(SkillService skillService)
    {
        _skillService = skillService;
    }

    [Function("GetSkillLinkedProcesses")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "skills/{skillId:guid}/linked-processes")]
        HttpRequestData request,
        Guid skillId,
        CancellationToken cancellationToken)
    {
        var linkedProcesses = await _skillService.GetLinkedProcessesAsync(skillId, cancellationToken);
        return await FunctionResponseFactory.SuccessResponseAsync(request, linkedProcesses, cancellationToken: cancellationToken);
    }
}
