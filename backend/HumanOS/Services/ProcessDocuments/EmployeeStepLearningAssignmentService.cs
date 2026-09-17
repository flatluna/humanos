using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.AI.OpenAI;
using Azure.Identity;
using HumanOS.Contracts.ProcessDocuments;
using HumanOS.Data;
using HumanOS.Models.Learning;
using HumanOS.Storage;
using Microsoft.Agents.AI;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;

namespace HumanOS.Services.ProcessDocuments;

public sealed class PersonalizationLlmResponse
{
    [JsonPropertyName("reportedExperienceSummary")]
    public List<string> ReportedExperienceSummary { get; set; } = [];

    [JsonPropertyName("engramEvaluations")]
    public List<PersonalizedEngramEvaluationItem> EngramEvaluations { get; set; } = [];

    [JsonPropertyName("assumptions")]
    public List<string> Assumptions { get; set; } = [];

    [JsonPropertyName("warnings")]
    public List<string> Warnings { get; set; } = [];
}

public sealed class PersonalizedEngramEvaluationItem
{
    [JsonPropertyName("engramId")]
    public string EngramId { get; set; } = string.Empty;

    [JsonPropertyName("relevantReportedExperience")]
    public string RelevantReportedExperience { get; set; } = string.Empty;

    [JsonPropertyName("hasReportedExperience")]
    public bool HasReportedExperience { get; set; }

    [JsonPropertyName("recommendedStatus")]
    public string RecommendedStatus { get; set; } = "required"; // "required" | "recommended" | "reference_only"

    [JsonPropertyName("assignmentReason")]
    public string AssignmentReason { get; set; } = string.Empty;
}

public sealed class EmployeeStepLearningAssignmentService : IEmployeeStepLearningAssignmentService
{
    private const string Instructions = """
        You are an expert workforce capability and personalization agent in Human OS.
        Your goal is to connect a standard Step Learning Assignment to a specific employee, comparing their reported experience (from résumé and profile) with the work-step requirements.

        CRITICAL CONSTRAINTS:
        1. Résumé information represents REPORTED experience only. It must NEVER be treated as proof of mastery or demonstrated capability.
        2. Do NOT evaluate real work performance.
        3. Do NOT remove or mark as optional any CRITICAL Engrams simply because similar experience appears in the résumé. Critical Engrams remain required when there is only reported/résumé experience.
        4. For supporting Engrams, reported experience may guide whether an Engram is "required", "recommended", or "reference_only".
        5. Summarize relevant reported experience concisely in "reportedExperienceSummary".
        6. For each Engram:
           - "relevantReportedExperience": brief note of what the résumé indicates (or empty if none).
           - "hasReportedExperience": true if résumé shows relevant exposure.
           - "recommendedStatus": "required", "recommended", or "reference_only".
           - "assignmentReason": concise justification explaining why this status was assigned given the work step requirements and the employee's background.
        """;

    private readonly AIAgent? _agent;
    private readonly IDbContextFactory<HumanOsDbContext>? _dbContextFactory;
    private readonly IProcessDocumentStorageService _storageService;
    private readonly IWorkStepRequirementsAnalyzer _stepAnalyzer;
    private readonly IEngramStudioClassCandidateFinder _candidateFinder;
    private readonly IEngramStudioEngramSelector _engramSelector;
    private readonly ILearningContentCoverageAnalyzer _coverageAnalyzer;
    private readonly ILearningContentGapDraftGenerator _draftGenerator;
    private readonly IStepLearningAssignmentGenerator _assignmentGenerator;
    private readonly ILogger<EmployeeStepLearningAssignmentService>? _logger;

    private static readonly ConcurrentDictionary<string, EmployeeStepLearningAssignmentResponse> AssignmentCache = new();

    public EmployeeStepLearningAssignmentService(
        IConfiguration configuration,
        IProcessDocumentStorageService storageService,
        IWorkStepRequirementsAnalyzer stepAnalyzer,
        IEngramStudioClassCandidateFinder candidateFinder,
        IEngramStudioEngramSelector engramSelector,
        ILearningContentCoverageAnalyzer coverageAnalyzer,
        ILearningContentGapDraftGenerator draftGenerator,
        IStepLearningAssignmentGenerator assignmentGenerator,
        IDbContextFactory<HumanOsDbContext>? dbContextFactory = null,
        ILogger<EmployeeStepLearningAssignmentService>? logger = null)
    {
        _storageService = storageService;
        _stepAnalyzer = stepAnalyzer;
        _candidateFinder = candidateFinder;
        _engramSelector = engramSelector;
        _coverageAnalyzer = coverageAnalyzer;
        _draftGenerator = draftGenerator;
        _assignmentGenerator = assignmentGenerator;
        _dbContextFactory = dbContextFactory;
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
            .AsAIAgent(instructions: Instructions, name: "EmployeeStepLearningAssignmentService");
    }

    public bool IsConfigured => _agent is not null;

    public string? DeploymentName { get; }

    public async Task<EmployeeStepLearningAssignmentResponse> CreateEmployeeAssignmentAsync(
        Guid employeeId,
        string documentId,
        string stepId,
        string organizationId,
        string? language = null,
        CancellationToken cancellationToken = default)
    {
        if (employeeId == Guid.Empty) throw new ArgumentException("Employee ID is required.", nameof(employeeId));
        if (string.IsNullOrWhiteSpace(documentId)) throw new ArgumentException("Document ID is required.", nameof(documentId));
        if (string.IsNullOrWhiteSpace(stepId)) throw new ArgumentException("Step ID is required.", nameof(stepId));
        if (string.IsNullOrWhiteSpace(organizationId)) throw new ArgumentException("Organization ID is required.", nameof(organizationId));

        var stopwatch = Stopwatch.StartNew();
        var normalizedOrgId = organizationId.Trim().ToLowerInvariant();

        using var dbContext = _dbContextFactory?.CreateDbContext();
        if (dbContext is null)
        {
            throw new InvalidOperationException("DbContext is not configured.");
        }

        // 1. Retrieve & Validate Employee
        var person = await dbContext.People
            .AsNoTracking()
            .Include(p => p.JobRole)
            .Include(p => p.Department)
            .SingleOrDefaultAsync(p => p.PersonId == employeeId, cancellationToken);

        if (person is null)
        {
            throw new KeyNotFoundException($"Employee with ID '{employeeId}' was not found.");
        }

        // Enforce organization isolation
        if (Guid.TryParse(organizationId, out var orgGuid))
        {
            if (person.TenantId.HasValue && person.TenantId.Value != orgGuid)
            {
                throw new UnauthorizedAccessException($"Employee '{employeeId}' belongs to another organization.");
            }
        }

        // Retrieve Employee Name, Profile, Résumé
        var personProfile = await dbContext.PersonProfiles
            .AsNoTracking()
            .SingleOrDefaultAsync(pp => pp.PersonId == employeeId, cancellationToken);

        var employeeName = !string.IsNullOrWhiteSpace(personProfile?.DisplayName)
            ? personProfile.DisplayName.Trim()
            : (!string.IsNullOrWhiteSpace(personProfile?.FirstName) ? $"{personProfile.FirstName} {personProfile.LastName}".Trim() : (person.Email ?? "Employee"));

        var resume = await dbContext.PersonResumes
            .AsNoTracking()
            .SingleOrDefaultAsync(pr => pr.PersonId == employeeId, cancellationToken);

        var warnings = new List<string>();
        var assumptions = new List<string>();

        if (resume is null || string.IsNullOrWhiteSpace(resume.RawResumeText))
        {
            warnings.Add("Employee résumé was unavailable; personalization was limited to job role and unassisted baselines.");
        }

        // Retrieve completed engrams from Human OS
        var completedNodeIds = await dbContext.LearningSessionNodes
            .AsNoTracking()
            .Where(lsn => lsn.LearningSession != null && lsn.LearningSession.PersonId == employeeId && lsn.Status == LearningSessionNodeStatus.Completed)
            .Select(lsn => lsn.CapabilityGraphNodeId.ToString())
            .ToListAsync(cancellationToken);

        var reviewCompletedEngramIds = await dbContext.EngramReviewCompletions
            .AsNoTracking()
            .Where(erc => erc.PersonId == employeeId)
            .Select(erc => erc.CapabilityGraphNodeId.ToString())
            .ToListAsync(cancellationToken);

        var completedEngramIds = new HashSet<string>(completedNodeIds.Concat(reviewCompletedEngramIds), StringComparer.OrdinalIgnoreCase);

        // Retrieve demonstrated evidence (if any)
        var demonstratedEngramIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // 2. Retrieve Process Document and Work Step
        var processDoc = await _storageService.GetStructuredResultAsync(normalizedOrgId, documentId, cancellationToken);
        if (processDoc is null)
        {
            throw new KeyNotFoundException($"Process document '{documentId}' was not found in organization '{normalizedOrgId}'.");
        }

        ProcessStep? targetStep = null;
        string chapterTitle = string.Empty;
        foreach (var ch in processDoc.Chapters)
        {
            var match = ch.Steps.FirstOrDefault(s =>
                string.Equals(s.StepId, stepId, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(s.StepNumber, stepId, StringComparison.OrdinalIgnoreCase));

            if (match != null)
            {
                targetStep = match;
                chapterTitle = ch.Title;
                break;
            }
        }

        if (targetStep is null)
        {
            throw new KeyNotFoundException($"Work step '{stepId}' was not found in document '{documentId}'.");
        }

        // Relevant tables
        var relevantTables = processDoc.Tables
            .Where(t => targetStep.SourcePages.Contains(t.SourcePage))
            .ToList();

        // 3. Chain Steps 2 through 7 trusted results
        var step2Result = await _stepAnalyzer.AnalyzeStepRequirementsAsync(
            documentId,
            processDoc.ProcessName,
            chapterTitle,
            targetStep,
            relevantTables,
            previouslyIdentifiedSkills: null,
            cancellationToken: cancellationToken);

        var step3Result = await _candidateFinder.FindCandidateClassesAsync(
            step2Result,
            normalizedOrgId,
            language,
            maxResults: 5,
            cancellationToken: cancellationToken);

        var step4Result = await _engramSelector.SelectRelevantEngramsAsync(
            step2Result,
            step3Result.CandidateClasses,
            normalizedOrgId,
            language,
            cancellationToken);

        var step5Result = await _coverageAnalyzer.AnalyzeCoverageAndGapsAsync(
            step4Result,
            step2Result,
            normalizedOrgId,
            cancellationToken);

        var step6Result = await _draftGenerator.GenerateContentGapDraftAsync(
            step5Result,
            step2Result,
            normalizedOrgId,
            language,
            cancellationToken);

        var step7Result = await _assignmentGenerator.GenerateAssignmentAsync(
            step2Result,
            step4Result,
            step5Result,
            step6Result,
            normalizedOrgId,
            cancellationToken);

        if (step7Result.Warnings != null) warnings.AddRange(step7Result.Warnings);
        if (step7Result.Assumptions != null) assumptions.AddRange(step7Result.Assumptions);

        // 4. Semantic Comparison with GPT-5 mini
        PersonalizationLlmResponse? llmPersonalization = null;
        if (_agent is not null)
        {
            var prompt = BuildPersonalizationPrompt(employeeName, person, personProfile, resume, step2Result, step4Result);
            try
            {
                var agentResponse = await _agent.RunAsync<PersonalizationLlmResponse>(prompt, cancellationToken: cancellationToken);
                llmPersonalization = agentResponse.Result;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger?.LogError(ex, "Failed to call LLM for employee personalization; falling back to deterministic baseline rules.");
            }
        }

        var llmEngramEvals = (llmPersonalization?.EngramEvaluations ?? [])
            .ToDictionary(e => e.EngramId, StringComparer.OrdinalIgnoreCase);

        var reportedSummary = (llmPersonalization?.ReportedExperienceSummary ?? []).Distinct().ToList();
        if (reportedSummary.Count == 0 && resume != null)
        {
            if (!string.IsNullOrWhiteSpace(resume.ProfessionalSummary)) reportedSummary.Add(resume.ProfessionalSummary.Trim());
            try
            {
                var skills = JsonSerializer.Deserialize<List<string>>(resume.SkillsJson ?? "[]");
                if (skills != null && skills.Count > 0)
                {
                    reportedSummary.Add($"Reported skills: {string.Join(", ", skills.Take(5))}");
                }
            }
            catch { }
        }

        // 5. Deterministic Assignment of Classes and Engrams
        // Rules:
        // - Critical Engrams: remain required when only reported/unknown evidence exists.
        // - Completed learning: if already completed, mark 'previously_completed', level 'completed'.
        // - Demonstrated capability: mark 'reference_only', level 'demonstrated'.
        // - Supporting engrams: can be 'required', 'recommended', or 'reference_only' based on background.
        var assignedClasses = new List<AssignedClassDto>();
        var draftRefs = new HashSet<string>(step7Result.ContentGapDraftReferences, StringComparer.OrdinalIgnoreCase);

        // Lookup engram criticality from Step 4
        var engramCriticalityMap = step4Result.SelectedClasses
            .SelectMany(c => c.SelectedEngrams)
            .ToDictionary(e => e.EngramId, e => e.Criticality, StringComparer.OrdinalIgnoreCase);

        foreach (var sc in step4Result.SelectedClasses)
        {
            // Filter out draft classes or empty classes
            if (draftRefs.Contains(sc.ClassId)) continue;

            var assignedEngrams = new List<AssignedEngramDto>();

            foreach (var se in sc.SelectedEngrams)
            {
                if (draftRefs.Contains(se.EngramId)) continue;

                var isCompleted = completedEngramIds.Contains(se.EngramId);
                var isDemonstrated = demonstratedEngramIds.Contains(se.EngramId);
                var isCritical = engramCriticalityMap.TryGetValue(se.EngramId, out var crit) && string.Equals(crit, "critical", StringComparison.OrdinalIgnoreCase);

                llmEngramEvals.TryGetValue(se.EngramId, out var llmEval);
                var hasReportedExp = (llmEval != null && llmEval.HasReportedExperience) || (resume != null && !string.IsNullOrWhiteSpace(resume.RawResumeText) && resume.RawResumeText.Contains(se.EngramName, StringComparison.OrdinalIgnoreCase));

                string assignmentStatus;
                string evidenceLevel;
                string assignmentReason;
                string evidenceSummary = llmEval?.RelevantReportedExperience ?? string.Empty;

                if (isDemonstrated)
                {
                    assignmentStatus = "reference_only";
                    evidenceLevel = "demonstrated";
                    assignmentReason = "Demonstrated in verified work performance. Available for reference.";
                }
                else if (isCompleted)
                {
                    assignmentStatus = "previously_completed";
                    evidenceLevel = "completed";
                    assignmentReason = "Previously completed in Human OS. Kept for review and operational readiness.";
                }
                else
                {
                    evidenceLevel = hasReportedExp ? "reported" : "unknown";

                    if (isCritical)
                    {
                        // Critical engrams MUST remain required under reported/unknown evidence (Rules 1 & 4)
                        assignmentStatus = "required";
                        assignmentReason = hasReportedExp
                            ? "Critical capability required for work step. Prior reported experience noted in résumé, but required to ensure operational rigor."
                            : "Critical capability required for work step. No prior demonstrated evidence.";
                    }
                    else
                    {
                        // Supporting engram: recommended or reference_only if strong reported exposure
                        if (hasReportedExp && (llmEval?.RecommendedStatus == "reference_only"))
                        {
                            assignmentStatus = "reference_only";
                            assignmentReason = "Supporting capability with substantial reported background. Kept as optional reference.";
                        }
                        else
                        {
                            assignmentStatus = "recommended";
                            assignmentReason = hasReportedExp
                                ? "Supporting capability recommended to enhance execution quality."
                                : "Supporting capability recommended for the work step.";
                        }
                    }
                }

                assignedEngrams.Add(new AssignedEngramDto
                {
                    EngramId = se.EngramId,
                    EngramName = se.EngramName,
                    AssignmentStatus = assignmentStatus,
                    AssignmentReason = assignmentReason,
                    EmployeeEvidenceLevel = evidenceLevel,
                    EmployeeEvidenceSummary = evidenceSummary
                });
            }

            if (assignedEngrams.Count > 0)
            {
                var classStatus = assignedEngrams.Any(e => e.AssignmentStatus == "required")
                    ? "required"
                    : (assignedEngrams.Any(e => e.AssignmentStatus == "recommended") ? "recommended" : "reference_only");

                assignedClasses.Add(new AssignedClassDto
                {
                    LearningPathId = sc.LearningPathId,
                    LearningPathName = sc.LearningPathName,
                    ClassId = sc.ClassId,
                    ClassName = sc.ClassName,
                    ClassAssignmentStatus = classStatus,
                    AssignedEngrams = assignedEngrams
                });
            }
        }

        // 6. Content Gaps
        var contentGaps = new List<AssignmentContentGapDto>();
        foreach (var gap in step5Result.ContentGaps)
        {
            var matchingDraft = step6Result.EngramDrafts.FirstOrDefault(d => d.WorkStepRequirementSupported == gap.Requirement)?.DraftReference
                ?? step6Result.ClassDrafts.FirstOrDefault(d => d.WorkStepRequirementsSupported.Contains(gap.Requirement))?.DraftReference
                ?? string.Empty;

            contentGaps.Add(new AssignmentContentGapDto
            {
                MissingCapability = gap.Requirement,
                ContentRequired = gap.RecommendedGapType,
                DraftReference = matchingDraft,
                Status = "needs_expert_review",
                Blocking = gap.Criticality == "critical"
            });
        }

        // 7. Overall Employee Assignment Status
        string employeeAssignmentStatus;
        if (step7Result.AssignmentStatus == "ready")
        {
            employeeAssignmentStatus = "assigned";
        }
        else if (step7Result.AssignmentStatus == "ready_with_content_gaps")
        {
            employeeAssignmentStatus = "assigned_with_content_gaps";
        }
        else
        {
            employeeAssignmentStatus = "blocked_by_content_gap";
        }

        // 8. Generate Employee-facing Markdown Output
        var markdown = RenderEmployeeMarkdown(
            step2Result,
            employeeName,
            assignedClasses,
            contentGaps,
            step7Result.AssignmentStatus);

        var assignmentId = Guid.NewGuid().ToString("D").ToLowerInvariant();

        var response = new EmployeeStepLearningAssignmentResponse
        {
            AssignmentId = assignmentId,
            EmployeeId = employeeId.ToString("D"),
            DocumentId = documentId,
            StepId = targetStep.StepId,
            StepNumber = targetStep.StepNumber,
            WorkStep = targetStep.StepName,
            EmployeeAssignmentStatus = employeeAssignmentStatus,
            EmployeeMustBeAbleTo = step2Result.EmployeeMustBeAbleTo,
            ReportedExperienceSummary = reportedSummary,
            AssignedClasses = assignedClasses,
            ExpectedWorkEvidence = step2Result.CompletionEvidence,
            ContentGaps = contentGaps,
            AssignmentMarkdown = markdown,
            Assumptions = assumptions.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            Warnings = warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            CreatedAt = DateTime.UtcNow
        };

        // 9. Save Assignment (Persistence in Data Lake and Cache)
        await _storageService.SaveEmployeeAssignmentAsync(normalizedOrgId, assignmentId, response, cancellationToken);
        AssignmentCache[assignmentId] = response;

        stopwatch.Stop();
        _logger?.LogInformation(
            "Created employee step learning assignment {AssignmentId} for employee {EmployeeId} (Status: {Status}, Duration: {DurationMs}ms)",
            assignmentId,
            employeeId,
            employeeAssignmentStatus,
            stopwatch.ElapsedMilliseconds);

        return response;
    }

    public async Task<EmployeeStepLearningAssignmentResponse?> GetAssignmentByIdAsync(
        string assignmentId,
        string? organizationId = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(assignmentId)) return null;

        if (AssignmentCache.TryGetValue(assignmentId, out var cached))
        {
            if (string.IsNullOrWhiteSpace(organizationId) || string.Equals(cached.DocumentId, organizationId, StringComparison.OrdinalIgnoreCase))
            {
                return cached;
            }
        }

        if (!string.IsNullOrWhiteSpace(organizationId))
        {
            var loaded = await _storageService.GetEmployeeAssignmentAsync(organizationId.Trim().ToLowerInvariant(), assignmentId, cancellationToken);
            if (loaded != null)
            {
                AssignmentCache[assignmentId] = loaded;
                return loaded;
            }
        }

        return cached;
    }

    private static string RenderEmployeeMarkdown(
        WorkStepRequirementsAnalysisResult step2,
        string employeeDisplayName,
        List<AssignedClassDto> assignedClasses,
        List<AssignmentContentGapDto> contentGaps,
        string step7Status)
    {
        var sb = new StringBuilder();

        // Title & Employee
        var stepNum = !string.IsNullOrWhiteSpace(step2.StepNumber) ? step2.StepNumber : "1";
        sb.AppendLine($"# Step {stepNum} Learning Assignment");
        sb.AppendLine();
        sb.AppendLine("## Employee");
        sb.AppendLine();
        sb.AppendLine($"**{employeeDisplayName}**");
        sb.AppendLine();

        // Work Step
        sb.AppendLine("## Work Step");
        sb.AppendLine();
        sb.AppendLine($"**{step2.WorkStep}**");
        sb.AppendLine();

        // Employee Must Be Able To
        sb.AppendLine("## Employee Must Be Able To");
        sb.AppendLine();
        foreach (var ability in step2.EmployeeMustBeAbleTo)
        {
            sb.AppendLine($"- {ability}");
        }
        sb.AppendLine();

        // Assigned Classes and Engrams
        sb.AppendLine("## Assigned Classes and Engrams");
        sb.AppendLine();

        for (var i = 0; i < assignedClasses.Count; i++)
        {
            var ac = assignedClasses[i];
            sb.AppendLine($"### Class {i + 1}: {ac.ClassName}");
            sb.AppendLine();
            sb.AppendLine($"**Learning Path:** {ac.LearningPathName}");
            sb.AppendLine();

            var required = ac.AssignedEngrams.Where(e => e.AssignmentStatus == "required").ToList();
            if (required.Count > 0)
            {
                sb.AppendLine("**Required Engrams:**");
                sb.AppendLine();
                for (var r = 0; r < required.Count; r++)
                {
                    sb.AppendLine($"{r + 1}. {required[r].EngramName}");
                }
                sb.AppendLine();
            }

            var recommended = ac.AssignedEngrams.Where(e => e.AssignmentStatus == "recommended").ToList();
            if (recommended.Count > 0)
            {
                sb.AppendLine("**Recommended Engrams:**");
                sb.AppendLine();
                for (var r = 0; r < recommended.Count; r++)
                {
                    sb.AppendLine($"{r + 1}. {recommended[r].EngramName}");
                }
                sb.AppendLine();
            }

            var prevOrRef = ac.AssignedEngrams.Where(e => e.AssignmentStatus == "previously_completed" || e.AssignmentStatus == "reference_only").ToList();
            if (prevOrRef.Count > 0)
            {
                sb.AppendLine("**Previously Completed or Reference:**");
                sb.AppendLine();
                for (var r = 0; r < prevOrRef.Count; r++)
                {
                    sb.AppendLine($"{r + 1}. {prevOrRef[r].EngramName}");
                }
                sb.AppendLine();
            }
        }

        // How We Will Know
        sb.AppendLine("## How We Will Know the Employee Completed the Work Step");
        sb.AppendLine();
        sb.AppendLine("The employee must provide or produce:");
        sb.AppendLine();
        foreach (var ev in step2.CompletionEvidence)
        {
            sb.AppendLine($"- {ev}");
        }
        sb.AppendLine();

        // Learning Content Gaps
        if (contentGaps.Count > 0)
        {
            sb.AppendLine("## Learning Content Gaps");
            sb.AppendLine();
            foreach (var gap in contentGaps)
            {
                var impact = gap.Blocking ? "Complete preparation is blocked" : "Assignment may proceed";
                sb.AppendLine($"- **Missing capability:** {gap.MissingCapability}");
                sb.AppendLine("- **Status:** Needs Expert Review");
                sb.AppendLine($"- **Impact:** {impact}");
                sb.AppendLine();
            }
        }

        // Assignment Status
        var statusLabel = step7Status switch
        {
            "ready" => "Ready",
            "ready_with_content_gaps" => "Ready with content gaps",
            _ => "Blocked by content gap"
        };
        sb.AppendLine("## Assignment Status");
        sb.AppendLine();
        sb.AppendLine($"**{statusLabel}**");
        sb.AppendLine();

        // Work Performance (prepared, uncompleted template)
        sb.AppendLine("## Work Performance");
        sb.AppendLine();
        sb.AppendLine("To be completed after the employee performs the work step.");
        sb.AppendLine();
        sb.AppendLine("### How the Employee Performed");
        sb.AppendLine();
        sb.AppendLine("- [ ] Completed independently");
        sb.AppendLine("- [ ] Completed with limited guidance");
        sb.AppendLine("- [ ] Completed with significant assistance");
        sb.AppendLine("- [ ] Not completed");
        sb.AppendLine();
        sb.AppendLine("### Reviewer Notes");
        sb.AppendLine();
        sb.AppendLine("**What the employee did well:**");
        sb.AppendLine();
        sb.AppendLine("[To be completed]");
        sb.AppendLine();
        sb.AppendLine("**What the employee missed:**");
        sb.AppendLine();
        sb.AppendLine("[To be completed]");
        sb.AppendLine();
        sb.AppendLine("**What assistance was required:**");
        sb.AppendLine();
        sb.AppendLine("[To be completed]");
        sb.AppendLine();
        sb.AppendLine("**What should improve next time:**");
        sb.AppendLine();
        sb.AppendLine("[To be completed]");
        sb.AppendLine();

        sb.AppendLine("### Engram Application");
        sb.AppendLine();
        foreach (var ac in assignedClasses)
        {
            foreach (var ae in ac.AssignedEngrams)
            {
                sb.AppendLine($"- **{ae.EngramName}:** Demonstrated / Partially Demonstrated / Not Demonstrated");
            }
        }
        sb.AppendLine();

        sb.AppendLine("### Final Result");
        sb.AppendLine();
        sb.AppendLine("- [ ] Ready to perform this step independently");
        sb.AppendLine("- [ ] Ready to perform this step with guidance");
        sb.AppendLine("- [ ] Needs additional preparation");
        sb.AppendLine("- [ ] Needs to repeat the step");

        return sb.ToString().TrimEnd();
    }

    private static string BuildPersonalizationPrompt(
        string employeeName,
        HumanOS.Models.People.Person person,
        HumanOS.Models.People.PersonProfile? profile,
        HumanOS.Models.People.PersonResume? resume,
        WorkStepRequirementsAnalysisResult step2,
        RelevantEngramsResponse step4)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Analyze the following employee profile and match reported experience against the work-step requirements and selected Engrams.");
        sb.AppendLine($"Employee: {employeeName}");
        sb.AppendLine($"Job Role: {person.JobRole?.Title ?? profile?.Occupation ?? "Unassigned"}");
        sb.AppendLine($"Department: {person.Department?.Name ?? "General"}");

        if (resume != null)
        {
            sb.AppendLine("\nEmployee Résumé Details:");
            if (!string.IsNullOrWhiteSpace(resume.ProfessionalObjective))
                sb.AppendLine($"Objective: {resume.ProfessionalObjective}");
            if (!string.IsNullOrWhiteSpace(resume.ProfessionalSummary))
                sb.AppendLine($"Summary: {resume.ProfessionalSummary}");
            if (!string.IsNullOrWhiteSpace(resume.SkillsJson))
                sb.AppendLine($"Skills: {resume.SkillsJson}");
            if (!string.IsNullOrWhiteSpace(resume.WorkExperienceJson))
                sb.AppendLine($"Experience: {resume.WorkExperienceJson}");
        }
        else
        {
            sb.AppendLine("Résumé: None available.");
        }

        sb.AppendLine($"\nWork Step: {step2.WorkStep}");
        sb.AppendLine("Work Step Abilities:");
        foreach (var a in step2.EmployeeMustBeAbleTo) sb.AppendLine($"- {a}");

        sb.AppendLine("\nSelected Engrams from Step 4:");
        foreach (var sc in step4.SelectedClasses)
        {
            foreach (var se in sc.SelectedEngrams)
            {
                sb.AppendLine($"- EngramId: {se.EngramId} | Name: {se.EngramName} | Criticality: {se.Criticality} | Desc: {se.EngramDescription}");
            }
        }

        sb.AppendLine("\nEvaluate reported experience and provide relevantReportedExperience, hasReportedExperience, recommendedStatus ('required', 'recommended', 'reference_only'), and assignmentReason for each Engram. Provide reportedExperienceSummary.");
        return sb.ToString();
    }
}
