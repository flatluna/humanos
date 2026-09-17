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

public sealed class EngramSelectionLlmResponse
{
    [JsonPropertyName("selectedEngrams")]
    public List<SelectedEngramLlmItem> SelectedEngrams { get; set; } = [];

    [JsonPropertyName("requirementCoverage")]
    public List<RequirementCoverageLlmItem> RequirementCoverage { get; set; } = [];

    [JsonPropertyName("uncoveredRequirements")]
    public List<string> UncoveredRequirements { get; set; } = [];

    [JsonPropertyName("assumptions")]
    public List<string> Assumptions { get; set; } = [];

    [JsonPropertyName("warnings")]
    public List<string> Warnings { get; set; } = [];
}

public sealed class SelectedEngramLlmItem
{
    [JsonPropertyName("engramId")]
    public string EngramId { get; set; } = string.Empty;

    [JsonPropertyName("classId")]
    public string ClassId { get; set; } = string.Empty;

    [JsonPropertyName("supportedRequirements")]
    public List<string> SupportedRequirements { get; set; } = [];

    [JsonPropertyName("relevanceReason")]
    public string RelevanceReason { get; set; } = string.Empty;

    [JsonPropertyName("relevanceScore")]
    public double RelevanceScore { get; set; }

    [JsonPropertyName("criticality")]
    public string Criticality { get; set; } = "supporting"; // "critical" | "supporting"
}

public sealed class RequirementCoverageLlmItem
{
    [JsonPropertyName("requirement")]
    public string Requirement { get; set; } = string.Empty;

    [JsonPropertyName("requirementType")]
    public string RequirementType { get; set; } = "ability";

    [JsonPropertyName("coverageStatus")]
    public string CoverageStatus { get; set; } = "not_covered"; // "covered" | "partially_covered" | "not_covered"

    [JsonPropertyName("supportingEngramIds")]
    public List<string> SupportingEngramIds { get; set; } = [];

    [JsonPropertyName("notes")]
    public string Notes { get; set; } = string.Empty;
}

public sealed class EngramStudioEngramSelector : IEngramStudioEngramSelector
{
    private const string Instructions = """
        You are an expert workforce capability and learning curriculum agent in Engram Studio.
        Your goal is to inspect candidate Classes and select ONLY the specific Engrams (atomic concepts/skills) that directly prepare an employee to perform a given work step.

        CRITICAL CONSTRAINTS:
        1. DO NOT invent, suggest, or propose new Classes or Engrams.
        2. Select ONLY from the real available Engrams provided in the input prompt. Use their exact EngramId and ClassId.
        3. Do NOT select Engrams merely because they belong to a relevant Class. Do not select every Engram in a Class automatically.
        4. Select an Engram ONLY when its name or description directly supports at least one of:
           - An observable ability from Employee Must Be Able To.
           - A required soft skill.
           - A required business skill.
           - A required technical skill.
           - A required tool or working method.
           - The correct creation of a completion-evidence artifact.
           - Prevention of a relevant work-step error.
        5. Normally select between two and eight Engrams per Class. If only one is relevant, return one. If none are relevant, select zero (the class will be excluded).
        6. Avoid duplication: if two Engrams teach substantially the same atomic knowledge:
           - Prefer the one with the strongest direct relationship to the work step.
           - Prefer the one from the more relevant Class.
           - Preserve both only when they address meaningfully different contexts or applications, and explain the distinction.
        7. For each selected Engram:
           - "supportedRequirements": list of exact requirements it supports.
           - "relevanceReason": concise justification grounded in the Engram's description.
           - "relevanceScore": numeric score between 0.0 and 1.0.
           - "criticality": "critical" (employee may fail or make significant errors without this knowledge) or "supporting" (improves quality/efficiency).
        8. Requirement coverage:
           For every requirement, classify coverage as "covered", "partially_covered", or "not_covered", listing any supporting Engram IDs.
        9. Identify all requirements with no supporting Engram in "uncoveredRequirements".
        """;

    private readonly AIAgent? _agent;
    private readonly IEngramStudioCatalogService _catalogService;
    private readonly ILogger<EngramStudioEngramSelector>? _logger;

    public EngramStudioEngramSelector(
        IConfiguration configuration,
        IEngramStudioCatalogService catalogService,
        ILogger<EngramStudioEngramSelector>? logger = null)
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
            .AsAIAgent(instructions: Instructions, name: "EngramStudioEngramSelector");
    }

    public bool IsConfigured => _agent is not null;

    public string? DeploymentName { get; }

    public async Task<RelevantEngramsResponse> SelectRelevantEngramsAsync(
        WorkStepRequirementsAnalysisResult requirements,
        IReadOnlyList<CandidateClassDto> candidateClasses,
        string? organizationId = null,
        string? language = null,
        CancellationToken cancellationToken = default)
    {
        if (requirements is null) throw new ArgumentNullException(nameof(requirements));
        if (candidateClasses is null) throw new ArgumentNullException(nameof(candidateClasses));

        var stopwatch = Stopwatch.StartNew();

        // 1. Retrieve lightweight Engram metadata for candidate Classes
        var candidateClassIds = candidateClasses
            .Select(c => c.ClassId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        IReadOnlyList<EngramStudioEngramSummary> rawEngrams;
        try
        {
            rawEngrams = await _catalogService.GetEngramsByClassIdsAsync(
                candidateClassIds,
                organizationId,
                language,
                cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger?.LogError(ex, "Failed to retrieve Engrams for candidate classes");
            throw new InvalidOperationException($"Engram Studio catalog is unavailable: {ex.Message}", ex);
        }

        // 2. Deterministic filtering and catalog-integrity verification
        var eligibleEngrams = FilterEligibleEngrams(rawEngrams, candidateClasses, organizationId, language, out var integrityWarnings);

        // All requirements list
        var allRequirements = CollectAllTypedRequirements(requirements);

        // 3. Handle empty Engrams case
        if (eligibleEngrams.Count == 0)
        {
            stopwatch.Stop();
            var warnings = new List<string>(integrityWarnings)
            {
                "No eligible published Engrams found in candidate Classes."
            };
            if (requirements.Warnings != null)
            {
                warnings.AddRange(requirements.Warnings.Select(w => $"Step 2 context warning: {w}"));
            }

            return new RelevantEngramsResponse
            {
                DocumentId = requirements.DocumentId,
                StepId = requirements.StepId,
                StepNumber = requirements.StepNumber,
                WorkStep = requirements.WorkStep,
                SelectedClasses = [],
                RequirementCoverage = allRequirements.Select(r => new RequirementCoverageItem
                {
                    Requirement = r.Name,
                    RequirementType = r.Type,
                    CoverageStatus = "not_covered",
                    SupportingEngramIds = [],
                    Notes = "No candidate Engrams available."
                }).ToList(),
                UncoveredRequirements = allRequirements.Select(r => r.Name).ToList(),
                Assumptions = requirements.Assumptions != null ? [.. requirements.Assumptions] : [],
                Warnings = warnings,
                SourcePages = requirements.SourcePages != null ? [.. requirements.SourcePages] : []
            };
        }

        // 4. Ensure AI agent is configured
        if (_agent is null)
        {
            throw new InvalidOperationException(
                "EngramStudioEngramSelector is not configured. Set 'AzureOpenAIEndpoint' and 'AzureOpenAIDeploymentName' configuration keys.");
        }

        // 5. Prompt preparation & Semantic selection
        var prompt = BuildPrompt(requirements, candidateClasses, eligibleEngrams);

        try
        {
            var agentResponse = await _agent.RunAsync<EngramSelectionLlmResponse>(
                prompt,
                cancellationToken: cancellationToken);

            stopwatch.Stop();

            var llmResult = agentResponse.Result ?? new EngramSelectionLlmResponse();

            // 6. Deterministic post-processing: validate against real eligible engrams and maintain hierarchy
            var candidateClassMap = candidateClasses.ToDictionary(c => c.ClassId, StringComparer.OrdinalIgnoreCase);
            var eligibleEngramMap = eligibleEngrams.ToDictionary(e => e.EngramId, StringComparer.OrdinalIgnoreCase);

            var selectedEngramsByClass = new Dictionary<string, List<SelectedEngramDto>>(StringComparer.OrdinalIgnoreCase);
            var seenEngramIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var item in llmResult.SelectedEngrams)
            {
                if (string.IsNullOrWhiteSpace(item.EngramId)) continue;

                // Validate Engram existence in real catalog
                if (!eligibleEngramMap.TryGetValue(item.EngramId, out var realEngram))
                {
                    continue;
                }

                // Deduplicate Engram IDs
                if (!seenEngramIds.Add(realEngram.EngramId))
                {
                    continue;
                }

                // Verify parent Class relationship
                var parentClassId = realEngram.ClassId;
                if (!candidateClassMap.ContainsKey(parentClassId))
                {
                    continue;
                }

                if (!selectedEngramsByClass.TryGetValue(parentClassId, out var classEngramList))
                {
                    classEngramList = [];
                    selectedEngramsByClass[parentClassId] = classEngramList;
                }

                var criticality = string.Equals(item.Criticality, "critical", StringComparison.OrdinalIgnoreCase)
                    ? "critical"
                    : "supporting";

                classEngramList.Add(new SelectedEngramDto
                {
                    EngramId = realEngram.EngramId,
                    EngramName = realEngram.EngramName,
                    EngramDescription = realEngram.EngramDescription,
                    SupportedRequirements = item.SupportedRequirements ?? [],
                    RelevanceReason = !string.IsNullOrWhiteSpace(item.RelevanceReason) ? item.RelevanceReason : "Supports work step.",
                    RelevanceScore = Math.Round(Math.Clamp(item.RelevanceScore > 0 ? item.RelevanceScore : 0.85, 0.0, 1.0), 2),
                    Criticality = criticality
                });
            }

            // 7. Assemble selectedClasses preserving hierarchy (Learning Path -> Class -> Engrams)
            // Rule: "If no Engrams in a candidate Class are relevant, exclude that Class from the final selected-Class result."
            var selectedClasses = new List<SelectedClassDto>();
            var excludedClassesCount = 0;

            foreach (var candidate in candidateClasses)
            {
                if (selectedEngramsByClass.TryGetValue(candidate.ClassId, out var engrams) && engrams.Count > 0)
                {
                    selectedClasses.Add(new SelectedClassDto
                    {
                        LearningPathId = candidate.LearningPathId,
                        LearningPathName = candidate.LearningPathName,
                        ClassId = candidate.ClassId,
                        ClassName = candidate.ClassName,
                        ClassDescription = candidate.ClassDescription,
                        SelectedEngrams = engrams
                    });
                }
                else
                {
                    excludedClassesCount++;
                }
            }

            // 8. Compute requirement coverage deterministically and correlate with LLM insights
            var allSelectedEngramIds = seenEngramIds;
            var coverageItems = new List<RequirementCoverageItem>();
            var uncovered = new List<string>();

            var llmCoverageByReq = (llmResult.RequirementCoverage ?? [])
                .ToDictionary(c => c.Requirement.Trim(), StringComparer.OrdinalIgnoreCase);

            foreach (var req in allRequirements)
            {
                var supportingEngrams = new List<string>();
                foreach (var sc in selectedClasses)
                {
                    foreach (var se in sc.SelectedEngrams)
                    {
                        if (se.SupportedRequirements.Any(r => string.Equals(r.Trim(), req.Name.Trim(), StringComparison.OrdinalIgnoreCase)))
                        {
                            supportingEngrams.Add(se.EngramId);
                        }
                    }
                }

                string status;
                string notes = string.Empty;

                if (llmCoverageByReq.TryGetValue(req.Name, out var llmCov))
                {
                    // Validated supporting Engram IDs from LLM coverage
                    var validLlmEngrams = (llmCov.SupportingEngramIds ?? [])
                        .Where(id => allSelectedEngramIds.Contains(id))
                        .ToList();

                    foreach (var id in validLlmEngrams)
                    {
                        if (!supportingEngrams.Contains(id)) supportingEngrams.Add(id);
                    }

                    status = llmCov.CoverageStatus;
                    notes = llmCov.Notes ?? string.Empty;
                }
                else if (supportingEngrams.Count > 0)
                {
                    status = "covered";
                }
                else
                {
                    status = "not_covered";
                }

                // If no supporting engrams actually exist in selectedClasses, cannot be fully covered
                if (supportingEngrams.Count == 0)
                {
                    status = "not_covered";
                }

                if (status == "not_covered")
                {
                    uncovered.Add(req.Name);
                }

                coverageItems.Add(new RequirementCoverageItem
                {
                    Requirement = req.Name,
                    RequirementType = req.Type,
                    CoverageStatus = status,
                    SupportingEngramIds = supportingEngrams,
                    Notes = notes
                });
            }

            var warnings = new List<string>(integrityWarnings);
            if (requirements.Warnings != null)
            {
                foreach (var w in requirements.Warnings)
                {
                    warnings.Add($"Step 2 context warning: {w}");
                }
            }

            if (selectedClasses.Count < candidateClasses.Count)
            {
                var excludedNames = candidateClasses
                    .Where(c => !selectedClasses.Any(sc => sc.ClassId == c.ClassId))
                    .Select(c => c.ClassName);
                warnings.Add($"Excluded candidate Classes with no directly relevant Engrams: {string.Join(", ", excludedNames)}.");
            }

            var response = new RelevantEngramsResponse
            {
                DocumentId = requirements.DocumentId,
                StepId = requirements.StepId,
                StepNumber = requirements.StepNumber,
                WorkStep = requirements.WorkStep,
                SelectedClasses = selectedClasses,
                RequirementCoverage = coverageItems,
                UncoveredRequirements = uncovered.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                Assumptions = requirements.Assumptions != null ? [.. requirements.Assumptions] : [],
                Warnings = warnings,
                SourcePages = requirements.SourcePages != null ? [.. requirements.SourcePages] : []
            };

            var obs = new EngramSelectionObservability
            {
                DocumentId = requirements.DocumentId,
                StepId = requirements.StepId,
                OrganizationId = organizationId ?? "global",
                CandidateClassCount = candidateClasses.Count,
                EngramCountRetrieved = rawEngrams.Count,
                EngramCountSelected = allSelectedEngramIds.Count,
                ClassesExcludedAfterEngramInspection = excludedClassesCount,
                CoveredRequirementCount = coverageItems.Count(c => c.CoverageStatus == "covered"),
                PartiallyCoveredRequirementCount = coverageItems.Count(c => c.CoverageStatus == "partially_covered"),
                UncoveredRequirementCount = uncovered.Count,
                Duration = stopwatch.Elapsed,
                ModelDeployment = DeploymentName ?? "unknown",
                Success = true,
                WarningsCount = warnings.Count
            };

            _logger?.LogInformation(
                "Selected {Engrams} Engrams across {Classes} Classes for step {StepId} in {DurationMs}ms (Covered: {Covered}, Partial: {Partial}, Uncovered: {Uncovered})",
                obs.EngramCountSelected,
                selectedClasses.Count,
                obs.StepId,
                obs.Duration.TotalMilliseconds,
                obs.CoveredRequirementCount,
                obs.PartiallyCoveredRequirementCount,
                obs.UncoveredRequirementCount);

            return response;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            stopwatch.Stop();
            _logger?.LogError(ex, "Failed to select relevant Engrams for step {StepId}", requirements.StepId);
            throw;
        }
    }

    private static List<EngramStudioEngramSummary> FilterEligibleEngrams(
        IEnumerable<EngramStudioEngramSummary> rawEngrams,
        IReadOnlyList<CandidateClassDto> candidateClasses,
        string? organizationId,
        string? language,
        out List<string> integrityWarnings)
    {
        integrityWarnings = [];
        var eligible = new List<EngramStudioEngramSummary>();
        var seenEngramIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var validClassIds = candidateClasses
            .Select(c => c.ClassId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Guid? tenantGuid = null;
        if (!string.IsNullOrWhiteSpace(organizationId) && Guid.TryParse(organizationId, out var parsed))
        {
            tenantGuid = parsed;
        }

        foreach (var engram in rawEngrams)
        {
            // Valid Engram ID
            if (string.IsNullOrWhiteSpace(engram.EngramId)) continue;

            // Reject Engram referencing non-existent parent Class
            if (!validClassIds.Contains(engram.ClassId))
            {
                integrityWarnings.Add($"Catalog-integrity warning: Engram '{engram.EngramId}' references invalid parent Class '{engram.ClassId}'.");
                continue;
            }

            // Exclude draft, retired, deleted
            if (!string.Equals(engram.PublicationStatus, "published", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // Organization isolation
            if (engram.OwnerTenantId.HasValue && tenantGuid.HasValue && engram.OwnerTenantId.Value != tenantGuid.Value)
            {
                continue;
            }
            if (engram.OwnerTenantId.HasValue && !tenantGuid.HasValue && !engram.IsGlobal)
            {
                continue;
            }

            // Language filter
            if (!string.IsNullOrWhiteSpace(language) &&
                !string.IsNullOrWhiteSpace(engram.Language) &&
                !string.Equals(engram.Language, language, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // Deduplicate Engrams
            if (!seenEngramIds.Add(engram.EngramId))
            {
                continue;
            }

            eligible.Add(engram);
        }

        return eligible;
    }

    private static List<(string Name, string Type)> CollectAllTypedRequirements(WorkStepRequirementsAnalysisResult requirements)
    {
        var list = new List<(string Name, string Type)>();

        // In Human OS, learning content matches and gap evaluation operate primarily at SKILL level
        // (the 4 canonical categories: soft-skills, job-skills, department-processes, hr-processes + ai-skills).
        if (requirements.RequiredSkillAreas != null)
        {
            if (requirements.RequiredSkillAreas.SoftSkills != null)
                list.AddRange(requirements.RequiredSkillAreas.SoftSkills.Select(s => (s.Name, "soft_skill")));

            if (requirements.RequiredSkillAreas.TechnicalSkills != null)
                list.AddRange(requirements.RequiredSkillAreas.TechnicalSkills.Select(s => (s.Name, "technical_skill")));

            if (requirements.RequiredSkillAreas.BusinessSkills != null)
                list.AddRange(requirements.RequiredSkillAreas.BusinessSkills.Select(s => (s.Name, "business_skill")));

            if (requirements.RequiredSkillAreas.DepartmentProcesses != null)
                list.AddRange(requirements.RequiredSkillAreas.DepartmentProcesses.Select(s => (s.Name, "department_process")));

            if (requirements.RequiredSkillAreas.HrProcesses != null)
                list.AddRange(requirements.RequiredSkillAreas.HrProcesses.Select(s => (s.Name, "hr_process")));

            if (requirements.RequiredSkillAreas.AiSkills != null)
                list.AddRange(requirements.RequiredSkillAreas.AiSkills.Select(s => (s.Name, "ai_skill")));
        }

        // Only fallback to abilities if no explicit skills were identified
        if (list.Count == 0 && requirements.EmployeeMustBeAbleTo != null)
        {
            list.AddRange(requirements.EmployeeMustBeAbleTo.Select(a => (a, "ability")));
        }

        return list;
    }

    private static string BuildPrompt(
        WorkStepRequirementsAnalysisResult req,
        IReadOnlyList<CandidateClassDto> candidateClasses,
        List<EngramStudioEngramSummary> engrams)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Analyze the following work-step requirements and select the relevant Engrams from the candidate Classes.");
        sb.AppendLine($"Work Step: {req.WorkStep}");

        if (req.EmployeeMustBeAbleTo.Count > 0)
        {
            sb.AppendLine("\nEmployee Must Be Able To:");
            foreach (var a in req.EmployeeMustBeAbleTo) sb.AppendLine($"- {a}");
        }

        if (req.RequiredSkillAreas != null)
        {
            if (req.RequiredSkillAreas.SoftSkills.Count > 0)
            {
                sb.AppendLine("\nRequired Soft Skills:");
                foreach (var s in req.RequiredSkillAreas.SoftSkills) sb.AppendLine($"- {s.Name}: {s.Reason}");
            }
            if (req.RequiredSkillAreas.BusinessSkills.Count > 0)
            {
                sb.AppendLine("\nRequired Business Skills:");
                foreach (var s in req.RequiredSkillAreas.BusinessSkills) sb.AppendLine($"- {s.Name}: {s.Reason}");
            }
            if (req.RequiredSkillAreas.TechnicalSkills.Count > 0)
            {
                sb.AppendLine("\nRequired Technical Skills:");
                foreach (var s in req.RequiredSkillAreas.TechnicalSkills) sb.AppendLine($"- {s.Name}: {s.Reason}");
            }
        }

        if (req.RequiredTools.Count > 0)
        {
            sb.AppendLine("\nRequired Tools & Working Methods:");
            foreach (var t in req.RequiredTools) sb.AppendLine($"- {t.Name}: {t.UseInStep}");
        }

        if (req.CompletionEvidence.Count > 0)
        {
            sb.AppendLine("\nCompletion Evidence:");
            foreach (var ev in req.CompletionEvidence) sb.AppendLine($"- {ev}");
        }

        sb.AppendLine("\nCandidate Classes and their available Engrams:");
        var engramsByClass = engrams.GroupBy(e => e.ClassId);

        foreach (var group in engramsByClass)
        {
            var parentClass = candidateClasses.FirstOrDefault(c => string.Equals(c.ClassId, group.Key, StringComparison.OrdinalIgnoreCase));
            var className = parentClass != null ? parentClass.ClassName : group.Key;
            sb.AppendLine($"\n=== Class: {className} (ClassId: {group.Key}) ===");
            foreach (var e in group)
            {
                sb.AppendLine($"  - EngramId: {e.EngramId}");
                sb.AppendLine($"    Name: {e.EngramName}");
                sb.AppendLine($"    Description: {e.EngramDescription}");
            }
        }

        sb.AppendLine("\nSelect only the Engrams that directly support these specific requirements (normally 2 to 8 per class).");
        return sb.ToString();
    }
}
