using System.Net;
using System.Text.Json;
using HumanOS.Agents;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace HumanOS.AzureFunctions.Api;

public sealed class RecommendSmartObjectiveRequest
{
    public string? DepartmentObjectiveTitle { get; set; }

    public string? DepartmentObjectiveDescription { get; set; }

    public string? DepartmentObjectiveTargetQuarter { get; set; }

    public string? DepartmentObjectiveTargetDate { get; set; }

    public string? StrategicGoalTitle { get; set; }

    public string? PillarTitle { get; set; }

    public string? DepartmentMetricName { get; set; }

    public double? DepartmentTargetValue { get; set; }

    public string? DepartmentUnit { get; set; }

    public string? EmployeeName { get; set; }

    public string? JobRoleTitle { get; set; }

    public string? JobRoleLevel { get; set; }

    public string? JobRolePurpose { get; set; }

    public string? JobRoleSummary { get; set; }

    public List<string> JobRoleTechnicalSkills { get; set; } = [];

    public List<string> JobRoleSoftSkills { get; set; } = [];

    public string Language { get; set; } = "es";
}

/// <summary>
/// POST /api/objectives/recommend-smart — invokes <see cref="SmartObjectiveRecommendationAgent"/>
/// (Microsoft Agent Framework) to generate an employee SMART objective suggestion and recommend
/// the required technical and soft skills based on the Aligned Department Objective and Job Role skills.
/// </summary>
public sealed class RecommendSmartObjectiveFunction
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly SmartObjectiveRecommendationAgent _agent;
    private readonly ILogger<RecommendSmartObjectiveFunction> _logger;

    public RecommendSmartObjectiveFunction(
        SmartObjectiveRecommendationAgent agent,
        ILogger<RecommendSmartObjectiveFunction> logger)
    {
        _agent = agent;
        _logger = logger;
    }

    [Function("RecommendSmartObjective")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "objectives/recommend-smart")]
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        if (!_agent.IsConfigured)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.ServiceUnavailable,
                "AgentNotConfigured",
                "The Smart Objective Recommendation agent is not configured (missing Azure OpenAI settings).",
                cancellationToken);
        }

        RecommendSmartObjectiveRequest? body;
        try
        {
            body = await JsonSerializer.DeserializeAsync<RecommendSmartObjectiveRequest>(
                request.Body, JsonOptions, cancellationToken);
        }
        catch (JsonException)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidJson", "The request body contains invalid JSON.", cancellationToken);
        }

        if (body is null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "BodyRequired", "Request body is required.", cancellationToken);
        }

        var context = new SmartObjectiveRecommendationContext
        {
            DepartmentObjectiveTitle = body.DepartmentObjectiveTitle,
            DepartmentObjectiveDescription = body.DepartmentObjectiveDescription,
            DepartmentObjectiveTargetQuarter = body.DepartmentObjectiveTargetQuarter,
            DepartmentObjectiveTargetDate = body.DepartmentObjectiveTargetDate,
            StrategicGoalTitle = body.StrategicGoalTitle,
            PillarTitle = body.PillarTitle,
            DepartmentMetricName = body.DepartmentMetricName,
            DepartmentTargetValue = body.DepartmentTargetValue,
            DepartmentUnit = body.DepartmentUnit,
            EmployeeName = body.EmployeeName,
            JobRoleTitle = body.JobRoleTitle,
            JobRoleLevel = body.JobRoleLevel,
            JobRolePurpose = body.JobRolePurpose,
            JobRoleSummary = body.JobRoleSummary,
            JobRoleTechnicalSkills = body.JobRoleTechnicalSkills ?? [],
            JobRoleSoftSkills = body.JobRoleSoftSkills ?? [],
            Language = body.Language ?? "es",
        };

        try
        {
            var result = await _agent.RecommendAsync(context, cancellationToken);
            return await FunctionResponseFactory.SuccessResponseAsync(request, result.Proposal, cancellationToken: cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Error generating SMART objective recommendation.");
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.UnprocessableEntity,
                "RecommendationFailed",
                "Could not generate SMART objective recommendation: " + ex.Message,
                cancellationToken);
        }
    }
}
