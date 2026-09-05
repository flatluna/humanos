using System.Text.Json;
using Azure.AI.OpenAI;
using Azure.Identity;
using HumanOS.Agents.Studio;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using OpenAI.Chat;

namespace HumanOS.Agents;

/// <summary>
/// Input context supplied to <see cref="SmartObjectiveRecommendationAgent"/> to generate
/// a tailored employee SMART objective and recommended skills aligned with a department objective.
/// </summary>
public sealed class SmartObjectiveRecommendationContext
{
    public string? DepartmentObjectiveTitle { get; init; }

    public string? DepartmentObjectiveDescription { get; init; }

    public string? DepartmentObjectiveTargetQuarter { get; init; }

    public string? DepartmentObjectiveTargetDate { get; init; }

    public string? StrategicGoalTitle { get; init; }

    public string? PillarTitle { get; init; }

    public string? DepartmentMetricName { get; init; }

    public double? DepartmentTargetValue { get; init; }

    public string? DepartmentUnit { get; init; }

    public string? EmployeeName { get; init; }

    public string? JobRoleTitle { get; init; }

    public string? JobRoleLevel { get; init; }

    public string? JobRolePurpose { get; init; }

    public string? JobRoleSummary { get; init; }

    public List<string> JobRoleTechnicalSkills { get; init; } = [];

    public List<string> JobRoleSoftSkills { get; init; } = [];

    public string Language { get; init; } = "es";
}

/// <summary>
/// The structured recommendation proposal produced by <see cref="SmartObjectiveRecommendationAgent"/>.
/// Contains suggested SMART fields for the employee and the specific technical and soft skills recommended
/// to achieve the objective.
/// </summary>
public sealed class SmartObjectiveRecommendationProposal
{
    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string MetricName { get; set; } = string.Empty;

    public double CurrentValue { get; set; }

    public double TargetValue { get; set; } = 100;

    public string Unit { get; set; } = "%";

    public string MeasurementMethod { get; set; } = string.Empty;

    public string ActionPlan { get; set; } = string.Empty;

    public string BusinessImpact { get; set; } = string.Empty;

    public string TargetQuarter { get; set; } = "Q4 2026";

    public string TargetDate { get; set; } = "2026-12-31";

    public List<string> RecommendedTechnicalSkills { get; set; } = [];

    public List<string> RecommendedSoftSkills { get; set; } = [];

    public string RecommendationReasoning { get; set; } = string.Empty;
}

public sealed class SmartObjectiveRecommendationResult
{
    public SmartObjectiveRecommendationProposal Proposal { get; set; } = null!;

    public AgentTokenUsage TokenUsage { get; set; } = null!;
}

/// <summary>
/// AI Agent built with Microsoft Agent Framework (Microsoft.Agents.AI) that recommends
/// personalized SMART objectives for employees based on the Aligned Department Objective
/// and selects the necessary technical and soft skills from the employee's Job Role.
/// </summary>
public sealed class SmartObjectiveRecommendationAgent
{
    private const string Instructions = """
        You are the Smart Objective & Skill Alignment Agent for Human OS.
        Your mission is to help an employee formulate an actionable, high-impact individual SMART objective
        that directly contributes to their Department's Strategic Objective, and to recommend the exact Technical
        and Soft Skills from their Job Role that they need to leverage and develop to succeed.

        ROLE & METHODOLOGY:
        1. SMART Objective Generation:
           - Specific (S): Clear title and description specifying what the individual contributor/lead will build, deliver, or optimize.
           - Measurable (M): Concrete metric name, baseline current value (e.g. 0), quantifiable target value, unit, and precise data verification method.
           - Achievable (A): Practical, structured action plan with 2-4 numbered milestones.
           - Relevant (R): Explicit articulation of how this employee objective drives the department's tactical goal and corporate mission.
           - Time-Bound (T): Target quarter and realistic completion deadline aligned with the department target.

        2. Skill Recommendation & Alignment:
           - Analyze all the employee's Job Role Technical Skills and Soft Skills provided in the context.
           - Select and recommend 2 to 4 Technical Skills and 2 to 3 Soft Skills from the role (or complementary industry-standard competencies)
             that are directly required to execute and complete this specific objective.
           - Set `RecommendedTechnicalSkills` and `RecommendedSoftSkills` accordingly.

        3. Language & Tone:
           - Match the requested language ('es' for Spanish, 'en' for English).
           - Use professional, empowering, and concrete business phrasing.
        """;

    private readonly AzureOpenAIClient? _client;
    private readonly string? _deploymentName;

    public SmartObjectiveRecommendationAgent(IConfiguration configuration)
    {
        var endpoint = configuration["AzureOpenAIEndpoint"];
        var deploymentName = configuration["AzureOpenAIDeploymentName"];
        var apiKey = configuration["AzureOpenAIApiKey"];

        _deploymentName = deploymentName;

        if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(deploymentName))
        {
            _client = null;
            return;
        }

        _client = string.IsNullOrWhiteSpace(apiKey)
            ? new AzureOpenAIClient(new Uri(endpoint), new DefaultAzureCredential())
            : new AzureOpenAIClient(new Uri(endpoint), new System.ClientModel.ApiKeyCredential(apiKey));
    }

    public bool IsConfigured => _client is not null;

    public async Task<SmartObjectiveRecommendationResult> RecommendAsync(
        SmartObjectiveRecommendationContext context,
        CancellationToken cancellationToken = default)
    {
        if (_client is null || _deploymentName is null)
        {
            throw new InvalidOperationException(
                "The Smart Objective Recommendation agent is not configured. Set AzureOpenAIEndpoint and AzureOpenAIDeploymentName.");
        }

        var agent = _client
            .GetChatClient(_deploymentName)
            .AsAIAgent(instructions: Instructions, name: "SmartObjectiveRecommendationAgent");

        var prompt = BuildPrompt(context);

        var response = await agent.RunAsync<SmartObjectiveRecommendationProposal>(prompt, cancellationToken: cancellationToken);

        var usage = response.Usage;
        var tokenUsage = new AgentTokenUsage
        {
            AgentName = "SmartObjectiveRecommendationAgent",
            ModelName = _deploymentName,
            InputTokens = (int)(usage?.InputTokenCount ?? 0),
            OutputTokens = (int)(usage?.OutputTokenCount ?? 0),
            CachedInputTokens = (int)(usage?.CachedInputTokenCount ?? 0),
        };

        return new SmartObjectiveRecommendationResult
        {
            Proposal = response.Result,
            TokenUsage = tokenUsage,
        };
    }

    private static string BuildPrompt(SmartObjectiveRecommendationContext ctx)
    {
        var lines = new List<string>
        {
            $"LANGUAGE: {(ctx.Language.StartsWith("en", StringComparison.OrdinalIgnoreCase) ? "English" : "Spanish")}",
            string.Empty,
            "=== ALIGNED DEPARTMENT OBJECTIVE (THE PARENT GOAL TO CONTRIBUTE TO) ===",
            $"Department Objective Title: {ctx.DepartmentObjectiveTitle ?? "(Not specified)"}",
            $"Department Objective Description: {ctx.DepartmentObjectiveDescription ?? "(Not specified)"}",
            $"Department Target Quarter: {ctx.DepartmentObjectiveTargetQuarter ?? "Q4 2026"}",
            $"Department Target Date: {ctx.DepartmentObjectiveTargetDate ?? "2026-12-31"}",
            $"Department Metric: {ctx.DepartmentMetricName ?? "(None)"} (Target: {ctx.DepartmentTargetValue} {ctx.DepartmentUnit})",
            $"Corporate Strategic Goal: {ctx.StrategicGoalTitle ?? "(General)"}",
            $"Strategic Pillar: {ctx.PillarTitle ?? "(Innovation & Excellence)"}",
            string.Empty,
            "=== EMPLOYEE & JOB ROLE CONTEXT ===",
            $"Employee Name: {ctx.EmployeeName ?? "Employee"}",
            $"Job Role Title: {ctx.JobRoleTitle ?? "Team Member"}",
            $"Job Role Level: {ctx.JobRoleLevel ?? "Standard"}",
            $"Job Role Purpose: {ctx.JobRolePurpose ?? "(Not specified)"}",
            $"Job Role Summary: {ctx.JobRoleSummary ?? "(Not specified)"}",
            string.Empty,
            "=== JOB ROLE SKILLS (SELECT FROM THESE FOR THIS OBJECTIVE) ===",
            "Technical Skills in Job Role:",
            ctx.JobRoleTechnicalSkills.Count > 0
                ? string.Join("\n", ctx.JobRoleTechnicalSkills.Select(s => $"- {s}"))
                : "- (No technical skills explicitly listed in role)",
            string.Empty,
            "Soft Skills in Job Role:",
            ctx.JobRoleSoftSkills.Count > 0
                ? string.Join("\n", ctx.JobRoleSoftSkills.Select(s => $"- {s}"))
                : "- (No soft skills explicitly listed in role)",
            string.Empty,
            "Generate a complete, personalized SmartObjectiveRecommendationProposal now with all SMART fields and the list of recommended technical and soft skills to achieve it.",
        };

        return string.Join("\n", lines);
    }
}
