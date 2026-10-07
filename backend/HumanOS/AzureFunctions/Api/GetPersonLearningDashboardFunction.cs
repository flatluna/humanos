using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>
/// GET people/{personId}/learning-dashboard (2026-10-03) — full cross-course
/// learning dashboard for one person (courses completed/in progress,
/// engrams mastered, token/cost/time totals, Recall/Assessment pass rates,
/// per-course breakdown), merged with their own Workplace Companion (Buddy)
/// voice usage cost/time. Powers the admin "Users" person detail page's
/// "Dashboard" tab. See <see cref="StructuredLearningEngramStatsService"/>
/// and <see cref="TenantBuddyUsageService"/>.
/// </summary>
public sealed class GetPersonLearningDashboardFunction
{
    private readonly StructuredLearningEngramStatsService _statsService;
    private readonly TenantBuddyUsageService _buddyUsageService;

    public GetPersonLearningDashboardFunction(
        StructuredLearningEngramStatsService statsService,
        TenantBuddyUsageService buddyUsageService)
    {
        _statsService = statsService;
        _buddyUsageService = buddyUsageService;
    }

    [Function("GetPersonLearningDashboard")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "people/{personId:guid}/learning-dashboard")]
        HttpRequestData request,
        Guid personId,
        CancellationToken cancellationToken)
    {
        var dashboard = await _statsService.GetPersonLearningDashboardAsync(personId, cancellationToken);
        var buddyUsage = await _buddyUsageService.GetPersonBuddyUsageAsync(personId, cancellationToken);

        dashboard.Buddy = buddyUsage;
        dashboard.TotalEstimatedCostUsd += buddyUsage.EstimatedCostUsd;
        dashboard.TotalTimeSeconds += buddyUsage.TotalDurationSeconds;

        return await FunctionResponseFactory.SuccessResponseAsync(request, dashboard, cancellationToken: cancellationToken);
    }
}
