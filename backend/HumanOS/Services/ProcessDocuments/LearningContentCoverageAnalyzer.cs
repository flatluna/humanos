using System.Diagnostics;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Azure.AI.OpenAI;
using Azure.Identity;
using HumanOS.Contracts.ProcessDocuments;
using HumanOS.Data;
using Microsoft.Agents.AI;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;

namespace HumanOS.Services.ProcessDocuments;

public sealed class CoverageAnalysisLlmResponse
{
    [JsonPropertyName("coverageSummary")]
    public string CoverageSummary { get; set; } = string.Empty;

    [JsonPropertyName("assumptions")]
    public List<string> Assumptions { get; set; } = [];

    [JsonPropertyName("warnings")]
    public List<string> Warnings { get; set; } = [];
}

public sealed class LearningContentCoverageAnalyzer : ILearningContentCoverageAnalyzer
{
    private const string Instructions = """
        You are an expert workforce capability and corporate curriculum-gap analysis agent in Human OS.
        Your goal is to evaluate whether the employee is prepared for a work step by assessing required macro Skills against the organization's Skill Catalog and courses.

        CRITICAL CONSTRAINTS:
        1. A SKILL IS A HIGH-LEVEL TRAINING COURSE (e.g. "Prompt Engineering", "Stakeholder Management", "Discovery Process Execution").
        2. NEVER speak of "engrams", "atomic units", or micro-tasks. Speak exclusively about Skills and Courses.
        3. Provide an executive "coverageSummary" (2 to 3 sentences) explaining whether the employee has courses ready or if the organization needs to create courses or register new skills in its catalog.
        """;

    private readonly AIAgent? _agent;
    private readonly IDbContextFactory<HumanOsDbContext>? _dbContextFactory;
    private readonly ILogger<LearningContentCoverageAnalyzer>? _logger;

    public LearningContentCoverageAnalyzer(
        IConfiguration configuration,
        ILogger<LearningContentCoverageAnalyzer>? logger)
        : this(configuration, null, logger)
    {
    }

    public LearningContentCoverageAnalyzer(
        IConfiguration configuration,
        IDbContextFactory<HumanOsDbContext>? dbContextFactory = null,
        ILogger<LearningContentCoverageAnalyzer>? logger = null)
    {
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
            .AsAIAgent(instructions: Instructions, name: "LearningContentCoverageAnalyzer");
    }

    public bool IsConfigured => _agent is not null;

    public string? DeploymentName { get; }

    public async Task<LearningContentCoverageResponse> AnalyzeCoverageAndGapsAsync(
        RelevantEngramsResponse step4Result,
        WorkStepRequirementsAnalysisResult? step2Requirements = null,
        string? organizationId = null,
        CancellationToken cancellationToken = default)
    {
        if (step4Result is null)
        {
            throw new ArgumentNullException(nameof(step4Result));
        }

        var stopwatch = Stopwatch.StartNew();

        // 1. Collect required Skills for the step (high-level macro competencies: soft, job, dept, hr, ai)
        var skillsToEvaluate = CollectStepSkills(step2Requirements);

        // 2. Query tenant's real Skill Catalog (dbo.Skill) and Courses from Database
        Guid? tenantGuid = null;
        if (!string.IsNullOrWhiteSpace(organizationId) && Guid.TryParse(organizationId, out var parsedGuid))
        {
            tenantGuid = parsedGuid;
        }

        List<HumanOS.Models.OrgStructure.Skill> catalogSkills = [];
        List<(string Id, string Name, string? TargetSkill, string Category)> activeCourses = [];

        if (_dbContextFactory != null)
        {
            try
            {
                using var db = _dbContextFactory.CreateDbContext();

                // If tenantGuid is not an actual TenantId in dbo.Tenants, try resolving via AzureTenantId or People
                if (tenantGuid.HasValue && !await db.Tenants.AnyAsync(t => t.TenantId == tenantGuid.Value, cancellationToken))
                {
                    tenantGuid = null;
                }

                if (!tenantGuid.HasValue && !string.IsNullOrWhiteSpace(organizationId))
                {
                    var matchedTenant = await db.Tenants
                        .AsNoTracking()
                        .FirstOrDefaultAsync(t => t.AzureTenantId == organizationId, cancellationToken);
                    if (matchedTenant != null)
                    {
                        tenantGuid = matchedTenant.TenantId;
                    }
                    else
                    {
                        var matchedPerson = await db.People
                            .AsNoTracking()
                            .FirstOrDefaultAsync(p => (p.AzureTid == organizationId || p.AzureOid == organizationId) && p.TenantId != null, cancellationToken);
                        if (matchedPerson?.TenantId != null)
                        {
                            tenantGuid = matchedPerson.TenantId.Value;
                        }
                    }
                }

                if (tenantGuid.HasValue)
                {
                    catalogSkills = await db.Skills
                        .AsNoTracking()
                        .Where(s => s.TenantId == tenantGuid.Value && s.IsActive)
                        .ToListAsync(cancellationToken);
                }

                var docs = await db.StructuredLearningDocuments
                    .AsNoTracking()
                    .Where(d => d.IsPublished && (d.IsGlobal || d.TenantId == Guid.Empty || (tenantGuid.HasValue && d.TenantId == tenantGuid.Value)))
                    .Select(d => new { Id = d.StructuredLearningDocumentId.ToString(), d.DocumentName, d.TargetSkill, d.Category })
                    .ToListAsync(cancellationToken);

                foreach (var d in docs)
                {
                    activeCourses.Add((d.Id, d.DocumentName, d.TargetSkill, d.Category ?? "job-skills"));
                }

                var caps = await db.Capabilities
                    .AsNoTracking()
                    .Where(c => c.IsActive && (c.OwnerTenantId == null || (tenantGuid.HasValue && c.OwnerTenantId == tenantGuid.Value)))
                    .Select(c => new { Id = c.CapabilityId.ToString(), c.Name, c.TargetSkill, c.Category })
                    .ToListAsync(cancellationToken);

                foreach (var c in caps)
                {
                    activeCourses.Add((c.Id, c.Name, c.TargetSkill, c.Category ?? "job-skills"));
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to load skills/courses directly from DB in LearningContentCoverageAnalyzer; using candidate classes fallback.");
            }
        }

        // Add any candidate classes found in Step 4 to active courses
        foreach (var sc in step4Result.SelectedClasses)
        {
            if (!activeCourses.Any(c => string.Equals(c.Id, sc.ClassId, StringComparison.OrdinalIgnoreCase)))
            {
                activeCourses.Add((sc.ClassId, sc.ClassName, sc.ClassName, "job-skills"));
            }
        }

        // 3. Evaluate each Skill against the Catalog and Courses
        var coveredRequirements = new List<CoveredRequirementItem>();
        var contentGaps = new List<ContentGapItem>();

        static string Norm(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return string.Empty;
            return Regex.Replace(s.Trim().ToLowerInvariant(), @"\s+", " ");
        }

        foreach (var skill in skillsToEvaluate)
        {
            var skillNorm = Norm(skill.Name);
            if (string.IsNullOrWhiteSpace(skillNorm)) continue;

            // Check if skill exists in organization catalog
            var matchingCatalogSkill = catalogSkills.FirstOrDefault(cs =>
                string.Equals(Norm(cs.Name), skillNorm, StringComparison.OrdinalIgnoreCase) ||
                (Norm(cs.Name).Length > 4 && skillNorm.Contains(Norm(cs.Name))) ||
                (skillNorm.Length > 4 && Norm(cs.Name).Contains(skillNorm)));

            // Check if there is an active course created for this skill
            var matchedCourse = activeCourses.FirstOrDefault(c =>
                (!string.IsNullOrEmpty(c.TargetSkill) && string.Equals(Norm(c.TargetSkill), skillNorm, StringComparison.OrdinalIgnoreCase)) ||
                string.Equals(Norm(c.Name), skillNorm, StringComparison.OrdinalIgnoreCase) ||
                (!string.IsNullOrEmpty(c.TargetSkill) && (Norm(c.TargetSkill).Contains(skillNorm) || skillNorm.Contains(Norm(c.TargetSkill)))) ||
                (Norm(c.Name).Contains(skillNorm) || skillNorm.Contains(Norm(c.Name))));

            if (!string.IsNullOrEmpty(matchedCourse.Id))
            {
                // Covered: Skill is in catalog AND course exists
                coveredRequirements.Add(new CoveredRequirementItem
                {
                    Requirement = skill.Name,
                    Criticality = "critical",
                    Category = skill.Category,
                    SkillId = skill.SkillId,
                    IsAiSkill = skill.IsAiSkill,
                    MatchedCapabilityName = matchedCourse.Name,
                    SupportingEngramIds = []
                });
            }
            else if (matchingCatalogSkill != null)
            {
                // Skill exists in catalog, but Course is pending in Studio
                contentGaps.Add(new ContentGapItem
                {
                    Requirement = skill.Name,
                    RequirementType = skill.Category,
                    Category = skill.Category,
                    SkillId = skill.SkillId,
                    IsAiSkill = skill.IsAiSkill,
                    IsInCatalog = true,
                    HasCourse = false,
                    Criticality = "critical",
                    CurrentCoverage = "partially_covered",
                    GapReason = "Skill is registered in your organization catalog, but its training course has not been created yet in Studio.",
                    RecommendedGapType = "skill_in_catalog_course_pending",
                    ExistingClassId = matchingCatalogSkill.SkillId.ToString(),
                    TargetClassName = skill.Name,
                    TargetClassType = "existing_class"
                });
            }
            else
            {
                // Skill does not exist in catalog
                contentGaps.Add(new ContentGapItem
                {
                    Requirement = skill.Name,
                    RequirementType = skill.Category,
                    Category = skill.Category,
                    SkillId = skill.SkillId,
                    IsAiSkill = skill.IsAiSkill,
                    IsInCatalog = false,
                    HasCourse = false,
                    Criticality = "critical",
                    CurrentCoverage = "not_covered",
                    GapReason = "Skill is not registered in your organization's catalog.",
                    RecommendedGapType = "new_skill_required",
                    TargetClassName = skill.Name,
                    TargetClassType = "proposed_class"
                });
            }
        }

        // Fallback for tests or workflows where only Step 4 results were provided without Step 2 skills
        if (skillsToEvaluate.Count == 0 && step4Result.RequirementCoverage != null && step4Result.RequirementCoverage.Count > 0)
        {
            foreach (var req in step4Result.RequirementCoverage)
            {
                if (req.CoverageStatus == "covered" || req.SupportingEngramIds.Count > 0)
                {
                    coveredRequirements.Add(new CoveredRequirementItem
                    {
                        Requirement = req.Requirement,
                        Criticality = "critical",
                        Category = req.RequirementType,
                        SupportingEngramIds = [.. req.SupportingEngramIds]
                    });
                }
                else
                {
                    contentGaps.Add(new ContentGapItem
                    {
                        Requirement = req.Requirement,
                        RequirementType = req.RequirementType,
                        Category = req.RequirementType,
                        Criticality = "critical",
                        CurrentCoverage = "not_covered",
                        GapReason = "No existing published Engram covers this requirement.",
                        RecommendedGapType = "new_class_required"
                    });
                }
            }
        }

        // 4. Overall status determination
        string overallStatus;
        if (contentGaps.Count == 0)
        {
            overallStatus = "covered";
        }
        else if (contentGaps.All(g => g.IsInCatalog))
        {
            overallStatus = "partially_covered";
        }
        else
        {
            overallStatus = "content_gap";
        }

        // 5. LLM Executive Summary (high-level, no engrams)
        string coverageSummary;
        if (_agent is not null)
        {
            try
            {
                var prompt = BuildSkillCoveragePrompt(step4Result.WorkStep, step4Result.StepNumber, coveredRequirements, contentGaps);
                var agentResponse = await _agent.RunAsync<CoverageAnalysisLlmResponse>(prompt, cancellationToken: cancellationToken);
                coverageSummary = !string.IsNullOrWhiteSpace(agentResponse.Result?.CoverageSummary)
                    ? agentResponse.Result.CoverageSummary
                    : BuildDefaultSummary(overallStatus, coveredRequirements.Count, contentGaps.Count);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "LLM coverage summary call failed; using default summary.");
                coverageSummary = BuildDefaultSummary(overallStatus, coveredRequirements.Count, contentGaps.Count);
            }
        }
        else
        {
            coverageSummary = BuildDefaultSummary(overallStatus, coveredRequirements.Count, contentGaps.Count);
        }

        stopwatch.Stop();

        return new LearningContentCoverageResponse
        {
            DocumentId = step4Result.DocumentId,
            StepId = step4Result.StepId,
            StepNumber = step4Result.StepNumber,
            WorkStep = step4Result.WorkStep,
            OverallCoverageStatus = overallStatus,
            CoverageSummary = coverageSummary,
            CoveredRequirements = coveredRequirements,
            ContentGaps = contentGaps,
            Assumptions = (step4Result.Assumptions ?? []).Concat(step2Requirements?.Assumptions ?? []).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            Warnings = (step4Result.Warnings ?? []).Concat(step2Requirements?.Warnings ?? []).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            SourcePages = step4Result.SourcePages != null ? [.. step4Result.SourcePages] : []
        };
    }

    private static List<SkillItem> CollectStepSkills(WorkStepRequirementsAnalysisResult? step2Requirements)
    {
        var list = new List<SkillItem>();
        if (step2Requirements?.RequiredSkillAreas == null) return list;

        void AddRange(List<SkillItem>? items, string cat)
        {
            if (items == null) return;
            foreach (var item in items)
            {
                if (string.IsNullOrWhiteSpace(item.Category)) item.Category = cat;
                if (!list.Any(x => string.Equals(x.Name, item.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    list.Add(item);
                }
            }
        }

        AddRange(step2Requirements.RequiredSkillAreas.SoftSkills, "soft-skills");
        AddRange(step2Requirements.RequiredSkillAreas.TechnicalSkills, "job-skills");
        AddRange(step2Requirements.RequiredSkillAreas.BusinessSkills, "job-skills");
        AddRange(step2Requirements.RequiredSkillAreas.DepartmentProcesses, "department-processes");
        AddRange(step2Requirements.RequiredSkillAreas.HrProcesses, "hr-processes");
        AddRange(step2Requirements.RequiredSkillAreas.AiSkills, "job-skills");

        return list;
    }

    private static string BuildDefaultSummary(string overallStatus, int coveredCount, int gapCount)
    {
        return overallStatus switch
        {
            "covered" => $"All required skills ({coveredCount}) exist in your organization catalog with active training courses available in Studio.",
            "partially_covered" => $"{coveredCount} skills have courses ready. The remaining {gapCount} skills exist in your catalog, but their courses still need to be authored in Studio.",
            _ => $"Skill catalog setup needed: {gapCount} required skills are not yet registered in your organization's catalog."
        };
    }

    private static string BuildSkillCoveragePrompt(
        string workStep,
        string stepNumber,
        IReadOnlyList<CoveredRequirementItem> covered,
        IReadOnlyList<ContentGapItem> gaps)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Analyze the workforce readiness and skill coverage for the following work step:");
        sb.AppendLine($"Work Step: {workStep} (Step {stepNumber})");
        sb.AppendLine("\nCOVERED SKILLS (Existing in company catalog with active course in Studio):");
        if (covered.Count == 0) sb.AppendLine("- None");
        else foreach (var c in covered) sb.AppendLine($"- [{c.Category}] {c.Requirement} (Course: {c.MatchedCapabilityName})");

        sb.AppendLine("\nSKILLS IN CATALOG (Course pending in Studio):");
        var pendingCourses = gaps.Where(g => g.IsInCatalog).ToList();
        if (pendingCourses.Count == 0) sb.AppendLine("- None");
        else foreach (var p in pendingCourses) sb.AppendLine($"- [{p.Category}] {p.Requirement}");

        sb.AppendLine("\nNEW SKILLS REQUIRED (Not yet in company catalog):");
        var newSkills = gaps.Where(g => !g.IsInCatalog).ToList();
        if (newSkills.Count == 0) sb.AppendLine("- None");
        else foreach (var n in newSkills) sb.AppendLine($"- [{n.Category}] {n.Requirement}");

        sb.AppendLine("\nINSTRUCTIONS:");
        sb.AppendLine("Provide a professional 2-sentence executive summary explaining whether the workforce is prepared or which skills need catalog setup / course authoring.");
        sb.AppendLine("CRITICAL CONSTRAINTS: Do NOT mention 'engrams', 'atomic units', or micro-tasks! Speak strictly in terms of high-level Skills and Courses.");
        return sb.ToString();
    }
}
