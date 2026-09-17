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

public sealed class ContentGapDraftLlmResponse
{
    [JsonPropertyName("proposedEngrams")]
    public List<ProposedEngramLlmItem> ProposedEngrams { get; set; } = [];

    [JsonPropertyName("proposedClasses")]
    public List<ProposedClassLlmItem> ProposedClasses { get; set; } = [];

    [JsonPropertyName("assumptions")]
    public List<string> Assumptions { get; set; } = [];

    [JsonPropertyName("warnings")]
    public List<string> Warnings { get; set; } = [];
}

public sealed class ProposedEngramLlmItem
{
    [JsonPropertyName("workStepRequirement")]
    public string WorkStepRequirement { get; set; } = string.Empty;

    [JsonPropertyName("proposedEngramTitle")]
    public string ProposedEngramTitle { get; set; } = string.Empty;

    [JsonPropertyName("proposedEngramDescription")]
    public string ProposedEngramDescription { get; set; } = string.Empty;

    [JsonPropertyName("capabilitySupported")]
    public string CapabilitySupported { get; set; } = string.Empty;

    [JsonPropertyName("reasonNeeded")]
    public string ReasonNeeded { get; set; } = string.Empty;

    [JsonPropertyName("parentClassId")]
    public string ParentClassId { get; set; } = string.Empty;
}

public sealed class ProposedClassLlmItem
{
    [JsonPropertyName("proposedClassTitle")]
    public string ProposedClassTitle { get; set; } = string.Empty;

    [JsonPropertyName("proposedClassDescription")]
    public string ProposedClassDescription { get; set; } = string.Empty;

    [JsonPropertyName("capabilityDeveloped")]
    public string CapabilityDeveloped { get; set; } = string.Empty;

    [JsonPropertyName("workStepRequirementsSupported")]
    public List<string> WorkStepRequirementsSupported { get; set; } = [];

    [JsonPropertyName("recommendedExistingLearningPathId")]
    public string? RecommendedExistingLearningPathId { get; set; }

    [JsonPropertyName("proposedLearningPathName")]
    public string? ProposedLearningPathName { get; set; }

    [JsonPropertyName("reasonNewClassIsRequired")]
    public string ReasonNewClassIsRequired { get; set; } = string.Empty;

    [JsonPropertyName("proposedEngramIndex")]
    public List<ProposedEngramIndexItemDto> ProposedEngramIndex { get; set; } = [];
}

public sealed class LearningContentGapDraftGenerator : ILearningContentGapDraftGenerator
{
    private const string Instructions = """
        You are an expert learning curriculum architect in Engram Studio.
        Your goal is to propose reviewable content-gap drafts ONLY for missing learning requirements identified in a work step.

        CRITICAL CONSTRAINTS:
        1. DO NOT develop complete lessons, assessments, recall questions, practice activities, media, or full class content.
        2. Propose content ONLY for requirements that are missing or partially covered.
        3. For "new_engram_in_existing_class":
           - Propose ONLY the missing atomic knowledge required by the work step inside the verified existing Class.
           - Provide: proposedEngramTitle, proposedEngramDescription (one clear sentence), capabilitySupported, reasonNeeded.
           - DO NOT duplicate any existing Engrams in that Class.
        4. For "new_class_required":
           - Propose a coherent new Class that develops the missing capability.
           - If an existing Learning Path is appropriate from the available list, recommend its ID; if none fits, propose a new proposedLearningPathName.
           - Provide: proposedClassTitle, proposedClassDescription, capabilityDeveloped, workStepRequirementsSupported, reasonNewClassIsRequired.
           - Provide a proposedEngramIndex: a coherent, ordered list of atomic Engrams (normally 4 to 12) covering the capability. Each engram in the index must have proposedEngramTitle, proposedEngramDescription, capabilitySupported, workStepRequirementSupported, suggestedOrder.
        5. Every proposal is strictly a draft for expert review.
        """;

    private readonly AIAgent? _agent;
    private readonly IEngramStudioCatalogService _catalogService;
    private readonly ILogger<LearningContentGapDraftGenerator>? _logger;

    public LearningContentGapDraftGenerator(
        IConfiguration configuration,
        IEngramStudioCatalogService catalogService,
        ILogger<LearningContentGapDraftGenerator>? logger = null)
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
            .AsAIAgent(instructions: Instructions, name: "LearningContentGapDraftGenerator");
    }

    public bool IsConfigured => _agent is not null;

    public string? DeploymentName { get; }

    public async Task<LearningContentGapDraftResponse> GenerateContentGapDraftAsync(
        LearningContentCoverageResponse step5Result,
        WorkStepRequirementsAnalysisResult? step2Requirements = null,
        string? organizationId = null,
        string? language = null,
        CancellationToken cancellationToken = default)
    {
        if (step5Result is null) throw new ArgumentNullException(nameof(step5Result));

        var stopwatch = Stopwatch.StartNew();
        var warnings = new List<string>(step5Result.Warnings ?? []);
        var assumptions = new List<string>(step5Result.Assumptions ?? []);
        var catalogItemsChecked = new List<CatalogItemCheckedDto>();

        // 1. Outcome 1: Covered -> No draft created
        if (string.Equals(step5Result.OverallCoverageStatus, "covered", StringComparison.OrdinalIgnoreCase) ||
            step5Result.ContentGaps.Count == 0)
        {
            stopwatch.Stop();
            warnings.Add("Existing catalog content provides sufficient coverage for all critical work-step requirements. No content draft is required.");

            return new LearningContentGapDraftResponse
            {
                DocumentId = step5Result.DocumentId,
                StepId = step5Result.StepId,
                StepNumber = step5Result.StepNumber,
                WorkStep = step5Result.WorkStep,
                OverallCoverageStatus = "covered",
                DraftCreated = false,
                EngramDrafts = [],
                ClassDrafts = [],
                CatalogItemsChecked = [],
                Assumptions = assumptions,
                Warnings = warnings,
                SourcePages = step5Result.SourcePages != null ? [.. step5Result.SourcePages] : []
            };
        }

        // 2. Retrieve catalog metadata for verification
        var availableClasses = await _catalogService.GetAvailableClassesAsync(organizationId, language, cancellationToken)
            ?? Array.Empty<EngramStudioClassSummary>();
        var availableLearningPaths = await _catalogService.GetAvailableLearningPathsAsync(organizationId, cancellationToken)
            ?? Array.Empty<EngramStudioLearningPathSummary>();

        var classMap = availableClasses.ToDictionary(c => c.ClassId, StringComparer.OrdinalIgnoreCase);
        var lpMap = availableLearningPaths.ToDictionary(lp => lp.LearningPathId, StringComparer.OrdinalIgnoreCase);

        foreach (var cls in availableClasses)
        {
            catalogItemsChecked.Add(new CatalogItemCheckedDto
            {
                ItemType = "class",
                ItemId = cls.ClassId,
                ItemName = cls.ClassName
            });
        }
        foreach (var lp in availableLearningPaths)
        {
            catalogItemsChecked.Add(new CatalogItemCheckedDto
            {
                ItemType = "learning_path",
                ItemId = lp.LearningPathId,
                ItemName = lp.LearningPathName
            });
        }

        // 3. Partition gaps
        var engramGaps = step5Result.ContentGaps
            .Where(g => g.RecommendedGapType == "new_engram_in_existing_class")
            .ToList();

        var classGaps = step5Result.ContentGaps
            .Where(g => g.RecommendedGapType == "new_class_required" || g.RecommendedGapType != "new_engram_in_existing_class")
            .ToList();

        // 4. Verify existing classes for engram gaps and retrieve their existing engrams
        var verifiedEngramGaps = new List<ContentGapItem>();
        var existingEngramsByClass = new Dictionary<string, List<EngramStudioEngramSummary>>(StringComparer.OrdinalIgnoreCase);

        var targetClassIds = engramGaps
            .Select(g => g.ExistingClassId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList()!;

        if (targetClassIds.Count > 0)
        {
            var engrams = await _catalogService.GetEngramsByClassIdsAsync(targetClassIds!, organizationId, language, cancellationToken)
                ?? Array.Empty<EngramStudioEngramSummary>();

            foreach (var eng in engrams)
            {
                catalogItemsChecked.Add(new CatalogItemCheckedDto
                {
                    ItemType = "engram",
                    ItemId = eng.EngramId,
                    ItemName = eng.EngramName
                });

                if (!existingEngramsByClass.TryGetValue(eng.ClassId, out var list))
                {
                    list = [];
                    existingEngramsByClass[eng.ClassId] = list;
                }
                list.Add(eng);
            }
        }

        foreach (var gap in engramGaps)
        {
            if (string.IsNullOrWhiteSpace(gap.ExistingClassId) || !classMap.TryGetValue(gap.ExistingClassId, out var existingClass))
            {
                warnings.Add($"Catalog-integrity warning: Existing Class ID '{gap.ExistingClassId}' for requirement '{gap.Requirement}' was not found in authorized catalog.");
                continue;
            }

            // Check if existing Learning Path matches
            if (!string.IsNullOrWhiteSpace(gap.ExistingLearningPathId) && !lpMap.ContainsKey(gap.ExistingLearningPathId))
            {
                warnings.Add($"Catalog-integrity warning: Existing Learning Path ID '{gap.ExistingLearningPathId}' does not exist in catalog.");
            }

            // Check if an existing Engram in this Class already covers the requirement (duplicate check)
            if (existingEngramsByClass.TryGetValue(gap.ExistingClassId, out var classEngrams))
            {
                var duplicateMatch = classEngrams.FirstOrDefault(e =>
                    string.Equals(e.EngramName.Trim(), gap.Requirement.Trim(), StringComparison.OrdinalIgnoreCase) ||
                    (gap.Requirement.Length > 10 && e.EngramDescription.Contains(gap.Requirement, StringComparison.OrdinalIgnoreCase)));

                if (duplicateMatch != null)
                {
                    warnings.Add($"Existing catalog Engram '{duplicateMatch.EngramName}' ({duplicateMatch.EngramId}) already covers requirement '{gap.Requirement}'. Avoided generating duplicate Engram draft.");
                    continue;
                }
            }

            verifiedEngramGaps.Add(gap);
        }

        // 5. Call LLM to propose Engrams and Classes
        ContentGapDraftLlmResponse? llmResult = null;
        if (_agent is not null && (verifiedEngramGaps.Count > 0 || classGaps.Count > 0))
        {
            var prompt = BuildPrompt(step5Result, verifiedEngramGaps, classGaps, classMap, lpMap, existingEngramsByClass);
            try
            {
                var agentResponse = await _agent.RunAsync<ContentGapDraftLlmResponse>(
                    prompt,
                    cancellationToken: cancellationToken);

                llmResult = agentResponse.Result;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger?.LogError(ex, "LLM call for content gap draft generation failed; generating deterministic draft proposals.");
            }
        }

        // 6. Build Engram Drafts
        var engramDrafts = new List<EngramDraftDto>();
        var draftEngramCounter = 1;

        foreach (var gap in verifiedEngramGaps)
        {
            var existingClass = classMap[gap.ExistingClassId!];
            var lpName = lpMap.TryGetValue(existingClass.LearningPathId, out var lp) ? lp.LearningPathName : existingClass.LearningPathName;

            var proposedLlmEng = llmResult?.ProposedEngrams.FirstOrDefault(e =>
                string.Equals(e.WorkStepRequirement.Trim(), gap.Requirement.Trim(), StringComparison.OrdinalIgnoreCase));

            var proposedTitle = !string.IsNullOrWhiteSpace(proposedLlmEng?.ProposedEngramTitle)
                ? proposedLlmEng.ProposedEngramTitle
                : $"{gap.Requirement}";

            var proposedDesc = !string.IsNullOrWhiteSpace(proposedLlmEng?.ProposedEngramDescription)
                ? proposedLlmEng.ProposedEngramDescription
                : $"Teaches foundational principles and application of {gap.Requirement}.";

            var capability = !string.IsNullOrWhiteSpace(proposedLlmEng?.CapabilitySupported)
                ? proposedLlmEng.CapabilitySupported
                : $"{gap.Requirement}";

            var reason = !string.IsNullOrWhiteSpace(proposedLlmEng?.ReasonNeeded)
                ? proposedLlmEng.ReasonNeeded
                : gap.GapReason;

            engramDrafts.Add(new EngramDraftDto
            {
                DraftReference = $"draft-eng-{draftEngramCounter++}",
                ProposedEngramTitle = proposedTitle,
                ProposedEngramDescription = proposedDesc,
                CapabilitySupported = capability,
                WorkStepRequirementSupported = gap.Requirement,
                ReasonNeeded = reason,
                ParentLearningPathId = existingClass.LearningPathId,
                ParentLearningPathName = lpName,
                ParentClassId = existingClass.ClassId,
                ParentClassName = existingClass.ClassName,
                Status = "needs_expert_review"
            });
        }

        // 7. Build Class Drafts
        var classDrafts = new List<ClassDraftDto>();
        var draftClassCounter = 1;

        if (classGaps.Count > 0)
        {
            if (llmResult?.ProposedClasses != null && llmResult.ProposedClasses.Count > 0)
            {
                foreach (var proposedLlmClass in llmResult.ProposedClasses)
                {
                    var reqNames = proposedLlmClass.WorkStepRequirementsSupported != null && proposedLlmClass.WorkStepRequirementsSupported.Count > 0
                        ? proposedLlmClass.WorkStepRequirementsSupported
                        : classGaps.Select(g => g.Requirement).ToList();

                    var proposedClassTitle = !string.IsNullOrWhiteSpace(proposedLlmClass.ProposedClassTitle)
                        ? proposedLlmClass.ProposedClassTitle
                        : $"{step5Result.WorkStep}: Core Capabilities";

                    var proposedClassDesc = !string.IsNullOrWhiteSpace(proposedLlmClass.ProposedClassDescription)
                        ? proposedLlmClass.ProposedClassDescription
                        : $"Provides structured training for the unserved capabilities required to perform {step5Result.WorkStep}.";

                    var capabilityDeveloped = !string.IsNullOrWhiteSpace(proposedLlmClass.CapabilityDeveloped)
                        ? proposedLlmClass.CapabilityDeveloped
                        : $"{step5Result.WorkStep} Mastery";

                    var reasonNewClass = !string.IsNullOrWhiteSpace(proposedLlmClass.ReasonNewClassIsRequired)
                        ? proposedLlmClass.ReasonNewClassIsRequired
                        : "No existing catalog Class provides an appropriate conceptual foundation for these capabilities.";

                    // Evaluate Recommended Learning Path
                    string? recommendedLpId = proposedLlmClass.RecommendedExistingLearningPathId;
                    string? recommendedLpName = null;
                    string? proposedLpName = proposedLlmClass.ProposedLearningPathName;

                    if (!string.IsNullOrWhiteSpace(recommendedLpId) && lpMap.TryGetValue(recommendedLpId, out var existingLp))
                    {
                        recommendedLpName = existingLp.LearningPathName;
                        proposedLpName = null;
                    }
                    else if (availableLearningPaths.Count > 0)
                    {
                        var fallbackLp = availableLearningPaths.First();
                        recommendedLpId = fallbackLp.LearningPathId;
                        recommendedLpName = fallbackLp.LearningPathName;
                        proposedLpName = null;
                    }
                    else
                    {
                        recommendedLpId = null;
                        recommendedLpName = null;
                        if (string.IsNullOrWhiteSpace(proposedLpName))
                        {
                            proposedLpName = "Operations & Process Execution Program";
                        }
                    }

                    // Engram Index
                    var engramIndex = new List<ProposedEngramIndexItemDto>();
                    if (proposedLlmClass.ProposedEngramIndex != null && proposedLlmClass.ProposedEngramIndex.Count > 0)
                    {
                        var order = 1;
                        foreach (var idxItem in proposedLlmClass.ProposedEngramIndex)
                        {
                            engramIndex.Add(new ProposedEngramIndexItemDto
                            {
                                ProposedEngramTitle = idxItem.ProposedEngramTitle,
                                ProposedEngramDescription = idxItem.ProposedEngramDescription,
                                CapabilitySupported = idxItem.CapabilitySupported,
                                WorkStepRequirementSupported = idxItem.WorkStepRequirementSupported,
                                SuggestedOrder = idxItem.SuggestedOrder > 0 ? idxItem.SuggestedOrder : order++
                            });
                        }
                    }
                    else
                    {
                        var order = 1;
                        var matchingGaps = classGaps.Where(g => reqNames.Contains(g.Requirement, StringComparer.OrdinalIgnoreCase)).ToList();
                        if (matchingGaps.Count == 0) matchingGaps = classGaps;

                        foreach (var g in matchingGaps)
                        {
                            var engTitle = !string.IsNullOrWhiteSpace(g.ProposedEngramTitle) ? g.ProposedEngramTitle : g.Requirement;
                            engramIndex.Add(new ProposedEngramIndexItemDto
                            {
                                ProposedEngramTitle = engTitle,
                                ProposedEngramDescription = $"Core principles, methods, and practical application of {engTitle}.",
                                CapabilitySupported = engTitle,
                                WorkStepRequirementSupported = g.Requirement,
                                SuggestedOrder = order++
                            });
                        }
                    }

                    classDrafts.Add(new ClassDraftDto
                    {
                        DraftReference = $"draft-cls-{draftClassCounter++}",
                        ProposedClassTitle = proposedClassTitle,
                        ProposedClassDescription = proposedClassDesc,
                        CapabilityDeveloped = capabilityDeveloped,
                        WorkStepRequirementsSupported = reqNames,
                        RecommendedExistingLearningPathId = recommendedLpId,
                        RecommendedExistingLearningPathName = recommendedLpName,
                        ProposedLearningPathName = proposedLpName,
                        ReasonNewClassIsRequired = reasonNewClass,
                        ProposedEngramIndex = engramIndex,
                        Status = "needs_expert_review"
                    });
                }
            }
            else
            {
                // Fallback deterministic class draft grouping: group by TargetClassName
                var classGroups = classGaps
                    .GroupBy(g => !string.IsNullOrWhiteSpace(g.TargetClassName) ? g.TargetClassName : $"{step5Result.WorkStep}: Core Capabilities")
                    .ToList();

                foreach (var group in classGroups)
                {
                    var groupGaps = group.ToList();
                    var proposedClassTitle = group.Key;
                    var proposedClassDesc = $"Provides structured training for {proposedClassTitle} required to perform {step5Result.WorkStep}.";
                    var capabilityDeveloped = proposedClassTitle;
                    var reasonNewClass = "No existing catalog Class provides an appropriate conceptual foundation for these capabilities.";

                    string? recommendedLpId = null;
                    string? recommendedLpName = null;
                    string? proposedLpName = "Operations & Process Execution Program";

                    if (availableLearningPaths.Count > 0)
                    {
                        var fallbackLp = availableLearningPaths.First();
                        recommendedLpId = fallbackLp.LearningPathId;
                        recommendedLpName = fallbackLp.LearningPathName;
                        proposedLpName = null;
                    }

                    var engramIndex = new List<ProposedEngramIndexItemDto>();
                    var order = 1;
                    foreach (var g in groupGaps)
                    {
                        var engTitle = !string.IsNullOrWhiteSpace(g.ProposedEngramTitle) ? g.ProposedEngramTitle : g.Requirement;
                        engramIndex.Add(new ProposedEngramIndexItemDto
                        {
                            ProposedEngramTitle = engTitle,
                            ProposedEngramDescription = $"Core principles, methods, and practical application of {engTitle}.",
                            CapabilitySupported = engTitle,
                            WorkStepRequirementSupported = g.Requirement,
                            SuggestedOrder = order++
                        });
                    }

                    classDrafts.Add(new ClassDraftDto
                    {
                        DraftReference = $"draft-cls-{draftClassCounter++}",
                        ProposedClassTitle = proposedClassTitle,
                        ProposedClassDescription = proposedClassDesc,
                        CapabilityDeveloped = capabilityDeveloped,
                        WorkStepRequirementsSupported = groupGaps.Select(g => g.Requirement).ToList(),
                        RecommendedExistingLearningPathId = recommendedLpId,
                        RecommendedExistingLearningPathName = recommendedLpName,
                        ProposedLearningPathName = proposedLpName,
                        ReasonNewClassIsRequired = reasonNewClass,
                        ProposedEngramIndex = engramIndex,
                        Status = "needs_expert_review"
                    });
                }
            }
        }

        if (llmResult?.Warnings != null)
        {
            foreach (var w in llmResult.Warnings)
            {
                if (!warnings.Contains(w, StringComparer.OrdinalIgnoreCase)) warnings.Add(w);
            }
        }
        if (llmResult?.Assumptions != null)
        {
            foreach (var a in llmResult.Assumptions)
            {
                if (!assumptions.Contains(a, StringComparer.OrdinalIgnoreCase)) assumptions.Add(a);
            }
        }

        stopwatch.Stop();
        var draftCreated = engramDrafts.Count > 0 || classDrafts.Count > 0;

        var response = new LearningContentGapDraftResponse
        {
            DocumentId = step5Result.DocumentId,
            StepId = step5Result.StepId,
            StepNumber = step5Result.StepNumber,
            WorkStep = step5Result.WorkStep,
            OverallCoverageStatus = step5Result.OverallCoverageStatus,
            DraftCreated = draftCreated,
            EngramDrafts = engramDrafts,
            ClassDrafts = classDrafts,
            CatalogItemsChecked = catalogItemsChecked,
            Assumptions = assumptions.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            Warnings = warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            SourcePages = step5Result.SourcePages != null ? [.. step5Result.SourcePages] : []
        };

        var obs = new ContentGapDraftObservability
        {
            DocumentId = step5Result.DocumentId,
            StepId = step5Result.StepId,
            OrganizationId = organizationId ?? "global",
            DraftCreated = draftCreated,
            EngramDraftsCount = engramDrafts.Count,
            ClassDraftsCount = classDrafts.Count,
            CatalogItemsCheckedCount = catalogItemsChecked.Count,
            Duration = stopwatch.Elapsed,
            ModelDeployment = DeploymentName ?? "deterministic",
            Success = true,
            WarningsCount = response.Warnings.Count
        };

        _logger?.LogInformation(
            "Generated content gap drafts for step {StepId}: DraftCreated={DraftCreated}, Engrams={Engrams}, Classes={Classes} in {DurationMs}ms",
            obs.StepId,
            obs.DraftCreated,
            obs.EngramDraftsCount,
            obs.ClassDraftsCount,
            obs.Duration.TotalMilliseconds);

        return response;
    }

    private static string BuildPrompt(
        LearningContentCoverageResponse step5Result,
        List<ContentGapItem> engramGaps,
        List<ContentGapItem> classGaps,
        Dictionary<string, EngramStudioClassSummary> classMap,
        Dictionary<string, EngramStudioLearningPathSummary> lpMap,
        Dictionary<string, List<EngramStudioEngramSummary>> existingEngramsByClass)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Generate reviewable content gap proposals for the following work step gaps.");
        sb.AppendLine($"Work Step: {step5Result.WorkStep}");
        sb.AppendLine($"Overall Coverage Status: {step5Result.OverallCoverageStatus}");

        if (engramGaps.Count > 0)
        {
            sb.AppendLine("\nGaps requiring NEW ENGRAM in an EXISTING CLASS:");
            foreach (var g in engramGaps)
            {
                var existingClass = classMap.TryGetValue(g.ExistingClassId ?? string.Empty, out var ec) ? ec : null;
                sb.AppendLine($"- Requirement: {g.Requirement} (Type: {g.RequirementType}, Gap Reason: {g.GapReason})");
                if (existingClass != null)
                {
                    sb.AppendLine($"  Target ClassId: {existingClass.ClassId} ({existingClass.ClassName})");
                    sb.AppendLine($"  Target Class Description: {existingClass.ClassDescription}");
                    if (existingEngramsByClass.TryGetValue(existingClass.ClassId, out var engs))
                    {
                        sb.AppendLine($"  Existing Engrams in Class: {string.Join(", ", engs.Select(e => e.EngramName))}");
                    }
                }
            }
        }

        if (classGaps.Count > 0)
        {
            sb.AppendLine("\nGaps requiring a NEW CLASS (Group into 1 to 4 coherent, logical Classes):");
            foreach (var g in classGaps)
            {
                var groupTag = !string.IsNullOrWhiteSpace(g.TargetClassName) ? $" [Suggested Group: {g.TargetClassName}]" : "";
                var engramTag = !string.IsNullOrWhiteSpace(g.ProposedEngramTitle) ? $" [Engram: {g.ProposedEngramTitle}]" : "";
                sb.AppendLine($"- Requirement: {g.Requirement} (Type: {g.RequirementType}, Gap Reason: {g.GapReason}){groupTag}{engramTag}");
            }

            sb.AppendLine("\nAvailable Existing Learning Paths:");
            foreach (var lp in lpMap.Values)
            {
                sb.AppendLine($"- LearningPathId: {lp.LearningPathId} | {lp.LearningPathName}");
            }
        }

        sb.AppendLine("\nCRITICAL CURRICULUM ARCHITECTURE RULES:");
        sb.AppendLine("1. Each missing requirement represents an atomic ENGRAM (not a standalone Class).");
        sb.AppendLine("2. For engram gaps in existing classes, propose proposedEngramTitle, proposedEngramDescription, capabilitySupported, and reasonNeeded.");
        sb.AppendLine("3. For new class gaps, group all related missing requirements into 1 to 4 coherent, logical Classes (e.g. 'Process Decision & Business Rules Architecture', 'Discovery Knowledge & Evidence Governance'). DO NOT invent a separate Class for every individual requirement!");
        sb.AppendLine("4. For EACH proposed Class, provide proposedClassTitle, proposedClassDescription, capabilityDeveloped, workStepRequirementsSupported (which missing requirements belong to this class), recommendedExistingLearningPathId (or proposedLearningPathName), reasonNewClassIsRequired, and a coherent proposedEngramIndex listing all the constituent engrams.");
        return sb.ToString();
    }
}
