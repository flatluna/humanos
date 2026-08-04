using System.Net;
using System.Web;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>GET /tenants/{tenantId}/skills?category={category} — the
/// tenant's reusable Skill catalog, optionally filtered to one of the 4
/// career-plan categories. Powers the enterprise console's "Catálogo de
/// Skills" page and Capability Studio's target-skill picker. Pass
/// ?summary=true (2026-08-03) to get the lightweight
/// <see cref="SkillSummaryResponse"/> projection instead — just
/// SkillId/Name/JobRoleCount — for callers (like Capability Studio) that
/// only need to let someone pick a skill by name, not manage it.</summary>
public sealed class GetSkillsFunction
{
    private readonly SkillService _skillService;

    public GetSkillsFunction(SkillService skillService)
    {
        _skillService = skillService;
    }

    [Function("GetSkills")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "tenants/{tenantId:guid}/skills")]
        HttpRequestData request,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var query = HttpUtility.ParseQueryString(request.Url.Query);
        var category = string.IsNullOrWhiteSpace(query["category"]) ? null : query["category"];

        if (bool.TryParse(query["summary"], out var summary) && summary)
        {
            var summarySkills = await _skillService.GetSummaryByTenantAsync(tenantId, category, cancellationToken);
            return await FunctionResponseFactory.SuccessResponseAsync(request, summarySkills, cancellationToken: cancellationToken);
        }

        var skills = await _skillService.GetByTenantAsync(tenantId, category, cancellationToken);
        return await FunctionResponseFactory.SuccessResponseAsync(request, skills, cancellationToken: cancellationToken);
    }
}
