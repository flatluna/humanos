using System.Diagnostics;
using System.Text;
using System.Text.Json.Serialization;
using Azure.AI.OpenAI;
using Azure.Identity;
using HumanOS.Contracts.ProcessDocuments;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;

namespace HumanOS.Services.ProcessDocuments;

public sealed class ClassRankingLlmResponse
{
    [JsonPropertyName("rankedClasses")]
    public List<RankedClassLlmItem> RankedClasses { get; set; } = [];

    [JsonPropertyName("unmatchedRequirements")]
    public List<string> UnmatchedRequirements { get; set; } = [];

    [JsonPropertyName("warnings")]
    public List<string> Warnings { get; set; } = [];
}

public sealed class RankedClassLlmItem
{
    [JsonPropertyName("classId")]
    public string ClassId { get; set; } = string.Empty;

    [JsonPropertyName("matchedAbilities")]
    public List<string> MatchedAbilities { get; set; } = [];

    [JsonPropertyName("matchedSkillRequirements")]
    public List<MatchedSkillRequirement> MatchedSkillRequirements { get; set; } = [];

    [JsonPropertyName("relevanceReason")]
    public string RelevanceReason { get; set; } = string.Empty;

    [JsonPropertyName("relevanceScore")]
    public double RelevanceScore { get; set; }

    [JsonPropertyName("limitations")]
    public List<string> Limitations { get; set; } = [];
}

public sealed class EngramStudioClassCandidateFinder : IEngramStudioClassCandidateFinder
{
    private const string Instructions = """
        You are an expert curriculum and capability-matching agent in Engram Studio.
        Your goal is to evaluate candidate Classes from the Engram Studio catalog against the specific requirements of an analyzed work step, and identify the most relevant Classes that prepare an employee to perform that step.

        CRITICAL CONSTRAINTS:
        1. DO NOT invent, suggest, or propose new Classes, Learning Paths, or Engrams.
        2. Select ONLY from the real candidate Classes provided in the input prompt. Use their exact ClassId.
        3. A Class is relevant ONLY if its title AND description meaningfully prepare the employee for one or more specific work-step requirements. Do NOT select a Class solely because its title contains a generic word (such as 'Discovery', 'Communication', 'Leadership', 'Documentation', 'AI', 'Business') if its description does not actually support the specific work-step activities or skills.
        4. Avoid returning multiple Classes that provide redundant capabilities unless their descriptions show a distinct, meaningful difference. Prefer the smallest, highest-quality set of Classes (up to the requested maximum) that collectively cover the requirements.
        5. For each selected Class:
           - "matchedAbilities": which employee abilities from the work step this Class supports.
           - "matchedSkillRequirements": which skills (category and name) this Class supports.
           - "relevanceReason": concise explanation grounded in the Class description.
           - "relevanceScore": numeric score between 0.0 and 1.0 reflecting relevance.
           - "limitations": uncertainties or caveats (e.g. "Engrams have not yet been reviewed").
        6. Identify any requirements (abilities, skills, tools) that have NO matching candidate Class in "unmatchedRequirements".
        7. All selections are strictly provisional because underlying Engrams have not been inspected yet.
        """;

    private readonly AIAgent? _agent;
    private readonly IEngramStudioCatalogService _catalogService;
    private readonly ILogger<EngramStudioClassCandidateFinder>? _logger;

    public EngramStudioClassCandidateFinder(
        IConfiguration configuration,
        IEngramStudioCatalogService catalogService,
        ILogger<EngramStudioClassCandidateFinder>? logger = null)
    {
        _catalogService = catalogService;
        _logger = logger;

        var endpoint = configuration["AzureOpenAIEndpoint"];
        var deploymentName = configuration["AzureOpenAIEconomyDeploymentName"] ?? configuration["AzureOpenAIDeploymentName"];
        var apiKey = configuration["AzureOpenAIApiKey"];

        DeploymentName = deploymentName;

        if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(deploymentName))
        {
            _agent = null;
            return;
        }

        AzureOpenAIClient client = string.IsNullOrWhiteSpace(apiKey)
            ? new AzureOpenAIClient(new Uri(endpoint), new DefaultAzureCredential())
            : new AzureOpenAIClient(new Uri(endpoint), new System.ClientModel.ApiKeyCredential(apiKey));

        _agent = client
            .GetChatClient(deploymentName)
            .AsAIAgent(instructions: Instructions, name: "EngramStudioClassCandidateFinder");
    }

    public bool IsConfigured => _agent is not null;

    public string? DeploymentName { get; }

    public async Task<CandidateClassSearchResponse> FindCandidateClassesAsync(
        WorkStepRequirementsAnalysisResult requirements,
        string? organizationId = null,
        string? language = null,
        int maxResults = 5,
        CancellationToken cancellationToken = default)
    {
        if (requirements is null)
        {
            throw new ArgumentNullException(nameof(requirements));
        }

        var stopwatch = Stopwatch.StartNew();

        // 1. Retrieve lightweight metadata from the Engram Studio catalog
        IReadOnlyList<EngramStudioClassSummary> rawCatalogClasses;
        try
        {
            rawCatalogClasses = await _catalogService.GetAvailableClassesAsync(
                organizationId,
                language,
                cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger?.LogError(ex, "Failed to retrieve classes from Engram Studio catalog");
            throw new InvalidOperationException($"Engram Studio catalog is unavailable: {ex.Message}", ex);
        }

        // 2. Deterministic filtering
        var eligibleClasses = FilterEligibleClasses(rawCatalogClasses, organizationId, language);

        // All requirements list for tracking unmatched requirements
        var allRequirements = CollectAllRequirements(requirements);

        // 3. Handle empty catalog case
        if (eligibleClasses.Count == 0)
        {
            stopwatch.Stop();
            _logger?.LogWarning("No eligible published classes found in Engram Studio for organization {OrgId}", organizationId);

            return new CandidateClassSearchResponse
            {
                DocumentId = requirements.DocumentId,
                StepId = requirements.StepId,
                StepNumber = requirements.StepNumber,
                WorkStep = requirements.WorkStep,
                CandidateClasses = [],
                RequirementsWithoutCandidateClass = allRequirements,
                Warnings =
                [
                    "No relevant published Classes were found in the Engram Studio catalog.",
                    "Candidate Classes are provisional until their Engrams are reviewed."
                ],
                SourcePages = requirements.SourcePages != null ? [.. requirements.SourcePages] : []
            };
        }

        // 4. If agent is not configured, throw handled configuration error
        if (_agent is null)
        {
            throw new InvalidOperationException(
                "EngramStudioClassCandidateFinder is not configured. Set 'AzureOpenAIEndpoint' and 'AzureOpenAIDeploymentName' configuration keys.");
        }

        // 5. Prompt preparation & Semantic ranking via Microsoft Agent Framework
        var prompt = BuildPrompt(requirements, eligibleClasses, maxResults);

        try
        {
            var agentResponse = await _agent.RunAsync<ClassRankingLlmResponse>(
                prompt,
                cancellationToken: cancellationToken);

            stopwatch.Stop();

            var llmResult = agentResponse.Result ?? new ClassRankingLlmResponse();

            // 6. Deterministic post-processing of ranked classes
            var candidateClasses = new List<CandidateClassDto>();
            var eligibleById = eligibleClasses.ToDictionary(c => c.ClassId, StringComparer.OrdinalIgnoreCase);
            var coveredRequirements = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var rankedItem in llmResult.RankedClasses)
            {
                if (string.IsNullOrWhiteSpace(rankedItem.ClassId)) continue;

                // Match back to verified catalog Class
                if (!eligibleById.TryGetValue(rankedItem.ClassId, out var catalogClass))
                {
                    // Exclude any hallucinated or non-existent class IDs
                    continue;
                }

                // Exclude duplicate class IDs in the output
                if (candidateClasses.Any(c => string.Equals(c.ClassId, catalogClass.ClassId, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                // Exclude classes with no meaningful relationship or zero/negligible score
                if (rankedItem.RelevanceScore <= 0.0 && rankedItem.MatchedAbilities.Count == 0 && rankedItem.MatchedSkillRequirements.Count == 0)
                {
                    continue;
                }

                var limitations = new List<string>(rankedItem.Limitations);
                if (!limitations.Any(l => l.Contains("Engram", StringComparison.OrdinalIgnoreCase)))
                {
                    limitations.Add("Engrams have not yet been reviewed.");
                }

                candidateClasses.Add(new CandidateClassDto
                {
                    LearningPathId = catalogClass.LearningPathId,
                    LearningPathName = catalogClass.LearningPathName,
                    ClassId = catalogClass.ClassId,
                    ClassName = catalogClass.ClassName,
                    ClassDescription = catalogClass.ClassDescription,
                    PublicationStatus = catalogClass.PublicationStatus,
                    Language = catalogClass.Language,
                    MatchedAbilities = rankedItem.MatchedAbilities,
                    MatchedSkillRequirements = rankedItem.MatchedSkillRequirements,
                    RelevanceReason = !string.IsNullOrWhiteSpace(rankedItem.RelevanceReason)
                        ? rankedItem.RelevanceReason
                        : $"Prepares employees for {catalogClass.ClassName}.",
                    RelevanceScore = Math.Round(Math.Clamp(rankedItem.RelevanceScore, 0.0, 1.0), 2),
                    Limitations = limitations
                });

                // Track covered requirements
                foreach (var ability in rankedItem.MatchedAbilities) coveredRequirements.Add(ability);
                foreach (var skill in rankedItem.MatchedSkillRequirements) coveredRequirements.Add(skill.Name);

                // Stop at maximum requested results (default 5)
                if (candidateClasses.Count >= maxResults)
                {
                    break;
                }
            }

            // 7. Calculate requirements without candidate Class
            var unmatched = allRequirements
                .Where(r => !coveredRequirements.Contains(r))
                .ToList();

            if (llmResult.UnmatchedRequirements != null)
            {
                foreach (var ur in llmResult.UnmatchedRequirements)
                {
                    if (!unmatched.Contains(ur, StringComparer.OrdinalIgnoreCase) && !coveredRequirements.Contains(ur))
                    {
                        unmatched.Add(ur);
                    }
                }
            }

            var warnings = new List<string>
            {
                "Candidate Classes are provisional until their Engrams are reviewed."
            };

            if (candidateClasses.Count == 0)
            {
                warnings.Add("No relevant published Classes were found in the Engram Studio catalog for the given requirements.");
            }

            if (requirements.Warnings != null && requirements.Warnings.Count > 0)
            {
                foreach (var w in requirements.Warnings)
                {
                    warnings.Add($"Step 2 context warning: {w}");
                }
            }

            var response = new CandidateClassSearchResponse
            {
                DocumentId = requirements.DocumentId,
                StepId = requirements.StepId,
                StepNumber = requirements.StepNumber,
                WorkStep = requirements.WorkStep,
                CandidateClasses = candidateClasses,
                RequirementsWithoutCandidateClass = unmatched,
                Warnings = warnings,
                SourcePages = requirements.SourcePages != null ? [.. requirements.SourcePages] : []
            };

            var obs = new CandidateClassSearchObservability
            {
                DocumentId = requirements.DocumentId,
                StepId = requirements.StepId,
                OrganizationId = organizationId ?? "global",
                ClassesConsidered = eligibleClasses.Count,
                ClassesReturned = candidateClasses.Count,
                UnmatchedRequirementsCount = unmatched.Count,
                Duration = stopwatch.Elapsed,
                ModelDeployment = DeploymentName ?? "unknown",
                Success = true,
                WarningsCount = warnings.Count
            };

            _logger?.LogInformation(
                "Searched candidate classes for step {StepId}: {Returned}/{Considered} returned, {Unmatched} unmatched in {DurationMs}ms",
                obs.StepId,
                obs.ClassesReturned,
                obs.ClassesConsidered,
                obs.UnmatchedRequirementsCount,
                obs.Duration.TotalMilliseconds);

            return response;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            stopwatch.Stop();
            _logger?.LogError(ex, "Failed to rank candidate classes for step {StepId}", requirements.StepId);
            throw;
        }
    }

    private static List<EngramStudioClassSummary> FilterEligibleClasses(
        IEnumerable<EngramStudioClassSummary> rawClasses,
        string? organizationId,
        string? language)
    {
        var eligible = new List<EngramStudioClassSummary>();
        var seenClassIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        Guid? tenantGuid = null;
        if (!string.IsNullOrWhiteSpace(organizationId) && Guid.TryParse(organizationId, out var parsed))
        {
            tenantGuid = parsed;
        }

        foreach (var item in rawClasses)
        {
            // Valid Class ID and Learning Path ID
            if (string.IsNullOrWhiteSpace(item.ClassId) || string.IsNullOrWhiteSpace(item.LearningPathId))
            {
                continue;
            }

            // Exclude draft, retired, deleted
            if (!string.Equals(item.PublicationStatus, "published", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // Organization scope: public or matching tenant
            if (item.OwnerTenantId.HasValue && tenantGuid.HasValue && item.OwnerTenantId.Value != tenantGuid.Value)
            {
                continue;
            }
            if (item.OwnerTenantId.HasValue && !tenantGuid.HasValue && !item.IsGlobal)
            {
                continue;
            }

            // Language filter
            if (!string.IsNullOrWhiteSpace(language) &&
                !string.IsNullOrWhiteSpace(item.Language) &&
                !string.Equals(item.Language, language, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // Deduplicate classes
            if (!seenClassIds.Add(item.ClassId))
            {
                continue;
            }

            eligible.Add(item);
        }

        return eligible;
    }

    private static List<string> CollectAllRequirements(WorkStepRequirementsAnalysisResult requirements)
    {
        var list = new List<string>();

        if (requirements.EmployeeMustBeAbleTo != null)
        {
            list.AddRange(requirements.EmployeeMustBeAbleTo);
        }

        if (requirements.RequiredSkillAreas != null)
        {
            if (requirements.RequiredSkillAreas.SoftSkills != null)
                list.AddRange(requirements.RequiredSkillAreas.SoftSkills.Select(s => s.Name));

            if (requirements.RequiredSkillAreas.BusinessSkills != null)
                list.AddRange(requirements.RequiredSkillAreas.BusinessSkills.Select(s => s.Name));

            if (requirements.RequiredSkillAreas.TechnicalSkills != null)
                list.AddRange(requirements.RequiredSkillAreas.TechnicalSkills.Select(s => s.Name));

            if (requirements.RequiredSkillAreas.DepartmentProcesses != null)
                list.AddRange(requirements.RequiredSkillAreas.DepartmentProcesses.Select(s => s.Name));

            if (requirements.RequiredSkillAreas.HrProcesses != null)
                list.AddRange(requirements.RequiredSkillAreas.HrProcesses.Select(s => s.Name));

            if (requirements.RequiredSkillAreas.AiSkills != null)
                list.AddRange(requirements.RequiredSkillAreas.AiSkills.Select(s => s.Name));
        }

        return list.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static string BuildPrompt(
        WorkStepRequirementsAnalysisResult req,
        List<EngramStudioClassSummary> eligibleClasses,
        int maxResults)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Analyze the following work-step requirements and rank the most relevant candidate Classes from Engram Studio.");
        sb.AppendLine($"Work Step: {req.WorkStep}");

        if (req.EmployeeMustBeAbleTo.Count > 0)
        {
            sb.AppendLine("\nEmployee Must Be Able To:");
            foreach (var ability in req.EmployeeMustBeAbleTo)
            {
                sb.AppendLine($"- {ability}");
            }
        }

        if (req.RequiredSkillAreas != null)
        {
            if (req.RequiredSkillAreas.SoftSkills.Count > 0)
            {
                sb.AppendLine("\nRequired Soft Skills:");
                foreach (var s in req.RequiredSkillAreas.SoftSkills)
                    sb.AppendLine($"- {s.Name}: {s.Reason}");
            }

            if (req.RequiredSkillAreas.BusinessSkills.Count > 0)
            {
                sb.AppendLine("\nRequired Business Skills:");
                foreach (var s in req.RequiredSkillAreas.BusinessSkills)
                    sb.AppendLine($"- {s.Name}: {s.Reason}");
            }

            if (req.RequiredSkillAreas.TechnicalSkills.Count > 0)
            {
                sb.AppendLine("\nRequired Technical Skills:");
                foreach (var s in req.RequiredSkillAreas.TechnicalSkills)
                    sb.AppendLine($"- {s.Name}: {s.Reason}");
            }
        }

        if (req.RequiredTools.Count > 0)
        {
            sb.AppendLine("\nRequired Tools & Working Methods:");
            foreach (var t in req.RequiredTools)
                sb.AppendLine($"- {t.Name}: {t.UseInStep}");
        }

        if (req.CompletionEvidence.Count > 0)
        {
            sb.AppendLine("\nCompletion Evidence:");
            foreach (var ev in req.CompletionEvidence)
                sb.AppendLine($"- {ev}");
        }

        sb.AppendLine("\nAvailable Published Classes from Engram Studio:");
        foreach (var c in eligibleClasses)
        {
            sb.AppendLine($"---");
            sb.AppendLine($"ClassId: {c.ClassId}");
            sb.AppendLine($"ClassName: {c.ClassName}");
            sb.AppendLine($"Parent Learning Path: {c.LearningPathName} (ID: {c.LearningPathId})");
            sb.AppendLine($"Description: {c.ClassDescription}");
            if (c.CapabilityOrTopicTags.Count > 0)
            {
                sb.AppendLine($"Tags: {string.Join(", ", c.CapabilityOrTopicTags)}");
            }
        }

        sb.AppendLine($"\nSelect up to {maxResults} candidate Classes that genuinely prepare the employee for these requirements. If a Class's description does NOT prepare for any requirement, do NOT select it.");
        return sb.ToString();
    }
}
