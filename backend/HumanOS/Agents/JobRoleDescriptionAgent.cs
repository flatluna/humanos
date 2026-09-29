using System.ComponentModel;
using System.Text.Json;
using Azure.AI.OpenAI;
using Azure.Identity;
using HumanOS.Agents.Studio;
using HumanOS.Data;
using Microsoft.Agents.AI;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using OpenAI.Chat;

namespace HumanOS.Agents;

/// <summary>
/// Which situation <see cref="JobRoleDescriptionAgent"/> is being asked to help with —
/// selects an additional guidance block injected into the prompt. This is the same
/// "Runtime-selected static guidance text" Skill pattern already used by
/// <see cref="HumanOS.Agents.Runtime.TutorSkillLibrary"/> — deliberately NOT Harness's
/// experimental <c>AgentSkillsProvider</c>/<c>AgentSkillsSource</c> mechanism (see
/// <see cref="HumanOS.Agents.Runtime.TutorAgent"/>'s doc comment for why that mechanism
/// is avoided in this codebase: it enables several general-purpose, autonomous-dev-agent
/// capabilities by default that have nothing to do with this task).
/// </summary>
public enum JobRoleDescriptionMode
{
    /// <summary>The job role has little or no content yet — write a complete description
    /// from nothing but the title/department/company context.</summary>
    CreateFromScratch,

    /// <summary>The job role already has some fields filled in by an HRAdmin — fill
    /// genuine gaps and tighten what already exists, never contradict or discard good
    /// existing content.</summary>
    ImproveExisting
}

/// <summary>The actual guidance text for each <see cref="JobRoleDescriptionMode"/> — kept
/// as plain static strings, same rationale as <c>TutorSkillLibrary</c> (a Runtime-owned
/// source, not SKILL.md files distributed across the system).</summary>
internal static class JobRoleDescriptionSkillLibrary
{
    public static string InstructionsFor(JobRoleDescriptionMode mode) => mode switch
    {
        JobRoleDescriptionMode.CreateFromScratch =>
            """
            MODE: CREATE FROM SCRATCH. The organization has NOT written a job description for
            this role yet — you must propose a complete, realistic, professional description for
            every field, grounded ONLY in the job title, department, and company context you were
            given (plus whatever the search_similar_job_roles tool tells you about how this
            organization already describes other roles). Do not leave a field empty unless it
            genuinely does not apply to this kind of role — check similar roles first.
            """,
        JobRoleDescriptionMode.ImproveExisting =>
            """
            MODE: IMPROVE EXISTING. The organization ALREADY wrote some of this job description —
            it is shown to you below as "EXISTING FIELDS". Preserve the substance of every
            existing field that is already reasonable — you may tighten phrasing, but NEVER invent
            a contradictory replacement for content that already makes sense. Only propose
            substantially new content for fields that are empty, clearly incomplete, or genuinely
            too thin to be useful. When you do propose a field that already had content, your
            proposal must be a strict improvement — a reviewer will compare your proposal against
            the original side by side, so it must be visibly better, not just different.
            """,
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown JobRoleDescriptionMode.")
    };
}

/// <summary>The full job-description field set the agent proposes. Every field is a
/// PROPOSAL for an HRAdmin to review and accept/reject individually (see
/// GenerateJobRoleDescriptionFunction and the RoleDetailPage before/after review UI) —
/// nothing here is ever written directly to <see cref="HumanOS.Models.OrgStructure.JobRole"/>.</summary>
public sealed class JobRoleDescriptionProposal
{
    public string? LevelLabel { get; set; }

    public string? Purpose { get; set; }

    public string? Summary { get; set; }

    public List<string> Responsibilities { get; set; } = [];

    public string? RequiredExperience { get; set; }

    public string? RequiredEducation { get; set; }

    public List<string> RequiredCertifications { get; set; } = [];

    public List<string> RequiredLanguages { get; set; } = [];

    public List<string> TechnicalSkills { get; set; } = [];

    public List<string> SoftSkills { get; set; } = [];

    public List<string> Tools { get; set; } = [];

    public List<string> DailyActivities { get; set; } = [];

    public List<string> PerformanceIndicators { get; set; } = [];

    public List<string> ExpectedOutcomes { get; set; } = [];

    public string? WorkSchedule { get; set; }

    public string? WorkModality { get; set; }

    public bool RequiresTravel { get; set; }

    public string? WorkLocation { get; set; }

    public string? AvailabilityNotes { get; set; }

    public string? EmploymentType { get; set; }

    public decimal? SalaryRangeMin { get; set; }

    public decimal? SalaryRangeMax { get; set; }

    public string? SalaryCurrency { get; set; }

    public List<string> Benefits { get; set; } = [];
}

/// <summary>Everything <see cref="JobRoleDescriptionAgent"/> needs for one generation
/// call — the real organizational context plus the editor's current draft field values
/// (used verbatim in <see cref="JobRoleDescriptionMode.ImproveExisting"/> mode).</summary>
public sealed class JobRoleDescriptionGenerationContext
{
    public string LanguageCode { get; init; } = "es";

    public required Guid TenantId { get; init; }

    public required JobRoleDescriptionMode Mode { get; init; }

    /// <summary>The JobRoleId being edited (null when creating a brand-new role) —
    /// excluded from the agent's own search_similar_job_roles tool so it never
    /// "benchmarks" a role against its own current draft.</summary>
    public Guid? ExcludeJobRoleId { get; init; }

    public required string CompanyName { get; init; }

    public string? CompanyDescription { get; init; }

    public required string DepartmentName { get; init; }

    public required string Title { get; init; }

    public string? LevelLabel { get; init; }

    public string? Purpose { get; init; }

    public string? Summary { get; init; }

    public IReadOnlyList<string> Responsibilities { get; init; } = [];

    public string? RequiredExperience { get; init; }

    public string? RequiredEducation { get; init; }

    public IReadOnlyList<string> RequiredCertifications { get; init; } = [];

    public IReadOnlyList<string> RequiredLanguages { get; init; } = [];

    public IReadOnlyList<string> TechnicalSkills { get; init; } = [];

    public IReadOnlyList<string> SoftSkills { get; init; } = [];

    public IReadOnlyList<string> Tools { get; init; } = [];

    public IReadOnlyList<string> DailyActivities { get; init; } = [];

    public IReadOnlyList<string> PerformanceIndicators { get; init; } = [];

    public IReadOnlyList<string> ExpectedOutcomes { get; init; } = [];

    public string? WorkSchedule { get; init; }

    public string? WorkModality { get; init; }

    public bool RequiresTravel { get; init; }

    public string? WorkLocation { get; init; }

    public string? AvailabilityNotes { get; init; }

    public string? EmploymentType { get; init; }

    public decimal? SalaryRangeMin { get; init; }

    public decimal? SalaryRangeMax { get; init; }

    public string? SalaryCurrency { get; init; }

    public IReadOnlyList<string> Benefits { get; init; } = [];
}

public sealed class OrganizationCatalogSnapshot
{
    public IReadOnlyList<string> TechnicalSkills { get; set; } = [];
    public IReadOnlyList<string> SoftSkills { get; set; } = [];
    public IReadOnlyList<string> Activities { get; set; } = [];
    public IReadOnlyList<string> Responsibilities { get; set; } = [];
    public IReadOnlyList<string> ExperienceRequirements { get; set; } = [];
    public IReadOnlyList<string> Certifications { get; set; } = [];
    public IReadOnlyList<string> Tools { get; set; } = [];
}

/// <summary>Result of a generation call: the proposal plus the token usage of the call
/// that produced it (observability only) — same shape as CuradorAgent.CurationResult.</summary>
public sealed class JobRoleDescriptionGenerationResult
{
    public JobRoleDescriptionProposal Proposal { get; set; } = null!;

    public AgentTokenUsage TokenUsage { get; set; } = null!;
}

/// <summary>
/// Drafts or improves a JobRole's full job description ("puesto") from real company/
/// department/title context, via a real LLM using Microsoft Agent Framework
/// (https://learn.microsoft.com/en-us/agent-framework/) — same
/// <c>Microsoft.Agents.AI.OpenAI</c> Azure OpenAI Chat Completion provider as every other
/// agent in this codebase (see <see cref="JobDescriptionExtractionAgent"/>).
///
/// Unlike the other plain ChatClientAgents in this codebase, this agent is given a real
/// TOOL (<see cref="Microsoft.Extensions.AI.AIFunctionFactory"/> function calling, NOT
/// Harness — Harness/its Skills provider remain reserved for the runtime Tutor only, see
/// <see cref="HumanOS.Agents.Runtime.TutorAgent"/>): <c>search_similar_job_roles</c> lets
/// it query THIS organization's own already-authored job roles for consistency (tone,
/// typical benefits, salary currency, work modality) instead of generating generic
/// industry boilerplate. MCP (Model Context Protocol) was deliberately NOT used here —
/// no MCP client/server infrastructure exists anywhere in this codebase yet, and the only
/// external context this task needs (this tenant's own JobRoles) is already reachable
/// with a plain EF Core query exposed as a tool; standing up an MCP server for a single
/// in-process, same-database tool would be pure ceremony with no real benefit.
///
/// Singleton, same rationale as every other Studio agent (holds no per-request state):
/// the underlying <see cref="AzureOpenAIClient"/> is built once, but the lightweight
/// <see cref="AIAgent"/> wrapper — and its tools, which close over the CALLER's
/// tenantId/excluded-role-id — is (re)built fresh inside <see cref="GenerateAsync"/> for
/// every request.
///
/// TODO: Set the "AzureOpenAIEndpoint" and "AzureOpenAIDeploymentName" application
/// settings once real credentials are provided — see <see cref="JobDescriptionExtractionAgent"/>.
/// </summary>
public sealed class JobRoleDescriptionAgent
{
    private const string Instructions = """
        You are the Job Description agent inside Human OS's "Roles & Positions" editor. An
        HRAdmin is looking at ONE job role (a position) inside their organization and asked
        you to help write or improve its full job description — every field of a real job
        description document: level, purpose, summary, responsibilities,
        required experience/education/certifications/languages, technical and soft skills,
        tools used, performance indicators, expected outcomes, work schedule/modality/
        location/travel, employment type, salary range, and benefits.

        LANGUAGE REQUIREMENT:
        - The caller's prompt will include an explicit "OUTPUT LANGUAGE: English" or "OUTPUT
          LANGUAGE: Spanish" instruction — you MUST generate every proposal field strictly in
          THAT language, never defaulting to English regardless of the job title's own language.
        - Every responsibility, skill name, tool, certification, summary, and requirement must be
          in clean, professional prose in that same output language.

        FORMAL JOB SPECIFICATION FORMAT (NOT A RECRUITMENT AD):
        - This document is a formal, internal ORGANIZATIONAL JOB DESCRIPTION / POSITION SPECIFICATION,
          used for job architecture, competency modeling, compensation grading, and performance evaluation.
        - DO NOT write a job advertisement, job flyer, recruitment posting, or vacancy announcement.
        - FORBIDDEN PHRASES: NEVER use hiring/recruiting marketing phrases such as:
          * "We are seeking..."
          * "We are looking for..."
          * "Seeking a motivated / passionate..."
          * "Join our team during our MVP phase..."
          * "The ideal candidate will..."
          * "Are you ready to take your career..."
        - REQUIRED TONE: Use objective, formal, third-person descriptive job architecture language:
          * Purpose format: State the formal organizational mandate and raison d'être (e.g., "Responsible for architecting, validating, and scaling machine learning pipelines and predictive analytics to support data-driven decision making across products and operations.").
          * Summary format: Provide a comprehensive, formal breakdown of the position's organizational scope, areas of accountability, operational domain, and internal/external stakeholder interactions (e.g., "The Senior Data Scientist is responsible for the end-to-end design, execution, and monitoring of data science and machine learning initiatives across the organization. This position leads model development, experiment tracking, feature engineering, and cross-functional collaboration with engineering and product teams to integrate predictive capabilities into enterprise workflows.").

        WEB GROUNDING & INDUSTRY BENCHMARKS (BING SEARCH):
        - You have access to the `search_web_for_job_benchmarks` tool. Call it with the job title
          and role keywords (e.g. "Senior Data Scientist job description responsibilities skills")
          to search the web for real-world market job profiles, standard duties, technical requirements,
          and modern industry expectations.
        - Ground your proposal in these real-world findings to ensure the job description is exhaustive,
          up-to-date, and reflects modern industry excellence.

        EXHAUSTIVE DEPTH & COMPLETENESS:
        - Do NOT write brief, superficial, or generic single-paragraph placeholders.
        - Purpose: Articulate a clear, compelling mission explaining WHY this position exists and its strategic business value.
        - Summary: Provide a comprehensive, rich, professional overview detailing the role's scope, core domains of ownership, key interactions, and overall operational impact.
        - Responsibilities: Detail concrete, actionable, and thorough responsibilities covering both daily execution and broader strategic ownership.
        - Required Experience & Education: Clearly specify practical experience expectations and relevant fields of study.
        - Skills & Tools: Provide a complete breakdown of modern industry tools, technologies, and competencies.

        GROUNDING & MASTER CATALOG SELECTION RULES:
        - Ground everything in the REAL company name, department, and job title you were
          given — never invent an unrelated industry or business model for the company.
        - MASTER CATALOG SELECTION & REUSE: The organization maintains master reference
          catalogs for Technical Skills, Soft Skills, Key Responsibilities, Experience
          Requirements, Certifications, and Tools. These are provided in the prompt under
          "ORGANIZATION MASTER CATALOGS".
          * REUSE EXISTING CATALOG ITEMS: Whenever a skill, responsibility, certification, tool,
            or requirement needed for this role is already in the catalog, SELECT and use that
            exact catalog item verbatim.
          * PROPOSE NEW ITEMS IN CLEAN ENGLISH WHEN NEEDED: If the role genuinely requires a skill, certification,
            tool, responsibility, or qualification that is NOT present in the catalog, output the clean item name
            directly (e.g. "Apache Spark", "MLflow", "PyTorch"). DO NOT prefix the item name with text like
            "Proposed new tool:" or "Proposed new technical skill:". The system automatically detects and tags new items.
          * AVOID DUPLICATE SYNONYMS: Do not invent slight variations or synonyms for items that
            already exist in the catalog (e.g., if "React" is in the catalog, use "React", do not
            create "ReactJS" or "React.js").
        - Use the search_similar_job_roles tool to see how THIS organization already
          describes other roles (tone, typical benefits, salary currency, work modality) —
          prefer consistency with the organization's own existing patterns over generic
          industry boilerplate. Call it at least once — even with no arguments — before
          proposing benefits, salary, work modality, or work schedule.
        - Never invent implausibly specific facts (exact salary figures, named tools or
          certifications) that don't plausibly fit the given title/department/company —
          reasonable, industry-typical proposals are expected and desired, but they must
          stay plausible for this specific role.
        - List fields (responsibilities, skills, etc.) should have realistic, distinct
          items — no filler, no near-duplicate entries, no placeholder text like "TBD" or
          "N/A". Leave a list EMPTY (not a placeholder) if it genuinely doesn't apply.
        - Only propose a salary range/currency if it is plausible to state one for this
          kind of role at this kind of organization (check similar roles first) —
          otherwise leave the salary fields null.
        - requiresTravel is a real yes/no judgment based on the role, never a default.
        """;

    private readonly AzureOpenAIClient? _client;
    private readonly string? _deploymentName;
    private readonly IDbContextFactory<HumanOsDbContext> _dbContextFactory;
    private readonly WebGroundingService? _webGroundingService;

    public JobRoleDescriptionAgent(
        IConfiguration configuration,
        IDbContextFactory<HumanOsDbContext> dbContextFactory,
        WebGroundingService? webGroundingService = null)
    {
        _dbContextFactory = dbContextFactory;
        _webGroundingService = webGroundingService;

        var endpoint = configuration["AzureOpenAIEndpoint"];
        // Main tier (not economy): this is open-ended generation/reasoning about a whole
        // organization's job description, not bounded extraction — quality matters more
        // than cost here, unlike JobDescriptionExtractionAgent/CuradorAgent.
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

    public async Task<JobRoleDescriptionGenerationResult> GenerateAsync(
        JobRoleDescriptionGenerationContext context,
        CancellationToken cancellationToken = default)
    {
        if (_client is null || _deploymentName is null)
        {
            throw new InvalidOperationException(
                "The Job Role Description agent is not configured. Set the 'AzureOpenAIEndpoint' " +
                "and 'AzureOpenAIDeploymentName' application settings once credentials are provided.");
        }

        var tools = new List<AITool>
        {
            AIFunctionFactory.Create(
                ([Description("Department name (or part of the name) to search for. Optional.")] string? department,
                 [Description("Keyword from the job title to search for. Optional.")] string? titleKeyword) =>
                    SearchSimilarJobRolesAsync(context.TenantId, context.ExcludeJobRoleId, department, titleKeyword, cancellationToken),
                "search_similar_job_roles",
                "Searches existing job roles in THIS organization (same department and/or similar title) " +
                "to maintain consistency in tone, seniority level, benefits, work modality, and salary range. " +
                "Returns up to 5 recent roles in JSON. Both parameters are optional; call without parameters " +
                "to see the organization's recent roles."),
        };

        if (_webGroundingService is not null && _webGroundingService.IsConfigured)
        {
            tools.Add(AIFunctionFactory.Create(
                async ([Description("Job title and search terms to search real-world job postings on the web for responsibilities, skills, and tools.")] string query) =>
                {
                    try
                    {
                        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(25));
                        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, cts.Token);

                        var result = await _webGroundingService.SearchAsync(
                            query,
                            $"Standard job description benchmark for '{context.Title}' in '{context.DepartmentName}' at '{context.CompanyName}'",
                            linkedCts.Token);
                        return string.IsNullOrWhiteSpace(result.Text) ? "No web search results found." : result.Text;
                    }
                    catch (Exception ex)
                    {
                        return $"Web benchmark summary: Standard market responsibilities, qualifications, and toolsets apply for '{query}' (live search note: {ex.Message}).";
                    }
                },
                "search_web_for_job_benchmarks",
                "Searches the web via Bing in real time to fetch standard industry job descriptions, market requirements, responsibilities, technical requirements, tools, and certifications for this role. Call this tool to ground and enrich your proposal with current industry benchmarks."));
        }

        var agent = _client
            .GetChatClient(_deploymentName)
            .AsAIAgent(instructions: Instructions, name: "JobRoleDescriptionAgent", tools: tools);

        var catalogs = await LoadOrganizationCatalogsAsync(context.TenantId, cancellationToken);
        var prompt = BuildPrompt(context, catalogs);

        var response = await agent.RunAsync<JobRoleDescriptionProposal>(prompt, cancellationToken: cancellationToken);

        var usage = response.Usage;
        var tokenUsage = new AgentTokenUsage
        {
            AgentName = "JobRoleDescriptionAgent",
            ModelName = _deploymentName,
            InputTokens = (int)(usage?.InputTokenCount ?? 0),
            OutputTokens = (int)(usage?.OutputTokenCount ?? 0),
            CachedInputTokens = (int)(usage?.CachedInputTokenCount ?? 0),
        };

        return new JobRoleDescriptionGenerationResult { Proposal = response.Result, TokenUsage = tokenUsage };
    }

    /// <summary>The agent's only tool — a genuine, read-only EF Core query over this
    /// tenant's own JobRoles. Uses <see cref="IDbContextFactory{TContext}"/> (not a
    /// scoped DbContext) because this class is a Singleton and each tool call needs its
    /// own short-lived context, same pattern as SqlRuntimeCheckpointStore/
    /// CapabilityCreationOrchestrator.</summary>
    private async Task<string> SearchSimilarJobRolesAsync(
        Guid tenantId,
        Guid? excludeJobRoleId,
        string? department,
        string? titleKeyword,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var query = dbContext.JobRoles
            .AsNoTracking()
            .Where(r => r.TenantId == tenantId && r.IsActive);

        if (excludeJobRoleId is Guid excludeId)
        {
            query = query.Where(r => r.JobRoleId != excludeId);
        }

        if (!string.IsNullOrWhiteSpace(department))
        {
            query = query.Where(r => r.Department.Name.Contains(department));
        }

        if (!string.IsNullOrWhiteSpace(titleKeyword))
        {
            query = query.Where(r => r.Title.Contains(titleKeyword));
        }

        var roles = await query
            .OrderByDescending(r => r.UpdatedDate)
            .Take(5)
            .Select(r => new
            {
                r.Title,
                DepartmentName = r.Department.Name,
                r.LevelLabel,
                r.Summary,
                r.WorkModality,
                r.WorkSchedule,
                r.EmploymentType,
                r.SalaryCurrency,
                r.SalaryRangeMin,
                r.SalaryRangeMax,
                BenefitsJson = r.BenefitsJson,
            })
            .ToListAsync(cancellationToken);

        return JsonSerializer.Serialize(roles);
    }

    private async Task<OrganizationCatalogSnapshot> LoadOrganizationCatalogsAsync(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var skills = await dbContext.Skills
            .AsNoTracking()
            .Where(s => s.TenantId == tenantId && s.IsActive)
            .Select(s => new { s.Category, s.Name })
            .ToListAsync(cancellationToken);

        var activities = await dbContext.Activities
            .AsNoTracking()
            .Where(a => a.TenantId == tenantId && a.IsActive)
            .Select(a => a.Name)
            .ToListAsync(cancellationToken);

        var responsibilities = await dbContext.Responsibilities
            .AsNoTracking()
            .Where(r => r.TenantId == tenantId && r.IsActive)
            .Select(r => r.Name)
            .ToListAsync(cancellationToken);

        var experiences = await dbContext.ExperienceRequirements
            .AsNoTracking()
            .Where(e => e.TenantId == tenantId && e.IsActive)
            .Select(e => e.Name)
            .ToListAsync(cancellationToken);

        var certifications = await dbContext.Certifications
            .AsNoTracking()
            .Where(c => c.TenantId == tenantId && c.IsActive)
            .Select(c => c.Issuer != null ? $"{c.Name} ({c.Issuer})" : c.Name)
            .ToListAsync(cancellationToken);

        var tools = await dbContext.Tools
            .AsNoTracking()
            .Where(t => t.TenantId == tenantId && t.IsActive)
            .Select(t => t.Name)
            .ToListAsync(cancellationToken);

        return new OrganizationCatalogSnapshot
        {
            TechnicalSkills = skills.Where(s => s.Category == "job-skills").Select(s => s.Name).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToList(),
            SoftSkills = skills.Where(s => s.Category == "soft-skills").Select(s => s.Name).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToList(),
            Activities = activities.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToList(),
            Responsibilities = responsibilities.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToList(),
            ExperienceRequirements = experiences.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToList(),
            Certifications = certifications.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToList(),
            Tools = tools.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToList(),
        };
    }

    private static string BuildPrompt(JobRoleDescriptionGenerationContext context, OrganizationCatalogSnapshot catalogs)
    {
        var lines = new List<string>
        {
            JobRoleDescriptionSkillLibrary.InstructionsFor(context.Mode),
            string.Empty,
            $"OUTPUT LANGUAGE: {(context.LanguageCode.Equals("en", StringComparison.OrdinalIgnoreCase) ? "English" : "Spanish")}. Write every proposed field value in this language. Do not translate the output into another language.",
            $"COMPANY: {context.CompanyName}",
        };

        if (!string.IsNullOrWhiteSpace(context.CompanyDescription))
        {
            lines.Add($"COMPANY DESCRIPTION: {context.CompanyDescription}");
        }

        lines.Add($"DEPARTMENT: {context.DepartmentName}");
        lines.Add($"JOB TITLE: {context.Title}");

        if (!string.IsNullOrWhiteSpace(context.LevelLabel))
        {
            lines.Add($"LEVEL: {context.LevelLabel}");
        }

        // Add master catalogs to the prompt
        lines.Add(string.Empty);
        lines.Add("ORGANIZATION MASTER CATALOGS (select and match from these existing items whenever applicable; if none match, propose clean new ones):");
        AppendCatalogList(lines, "Technical Skills Catalog", catalogs.TechnicalSkills);
        AppendCatalogList(lines, "Soft Skills Catalog", catalogs.SoftSkills);
        AppendCatalogList(lines, "Daily/Periodic Activities Catalog", catalogs.Activities);
        AppendCatalogList(lines, "Key Responsibilities Catalog", catalogs.Responsibilities);
        AppendCatalogList(lines, "Experience Requirements Catalog", catalogs.ExperienceRequirements);
        AppendCatalogList(lines, "Certifications Catalog", catalogs.Certifications);
        AppendCatalogList(lines, "Tools / Technologies Catalog", catalogs.Tools);

        if (context.Mode == JobRoleDescriptionMode.ImproveExisting)
        {
            lines.Add(string.Empty);
            lines.Add("EXISTING FIELDS (preserve what's already reasonable, only propose genuine improvements):");
            AppendIfAny(lines, "Purpose", context.Purpose);
            AppendIfAny(lines, "Summary", context.Summary);
            AppendListIfAny(lines, "Responsibilities", context.Responsibilities);
            AppendIfAny(lines, "RequiredExperience", context.RequiredExperience);
            AppendIfAny(lines, "RequiredEducation", context.RequiredEducation);
            AppendListIfAny(lines, "RequiredCertifications", context.RequiredCertifications);
            AppendListIfAny(lines, "RequiredLanguages", context.RequiredLanguages);
            AppendListIfAny(lines, "TechnicalSkills", context.TechnicalSkills);
            AppendListIfAny(lines, "SoftSkills", context.SoftSkills);
            AppendListIfAny(lines, "Tools", context.Tools);
            AppendListIfAny(lines, "DailyActivities", context.DailyActivities);
            AppendListIfAny(lines, "PerformanceIndicators", context.PerformanceIndicators);
            AppendListIfAny(lines, "ExpectedOutcomes", context.ExpectedOutcomes);
            AppendIfAny(lines, "WorkSchedule", context.WorkSchedule);
            AppendIfAny(lines, "WorkModality", context.WorkModality);
            lines.Add($"RequiresTravel: {context.RequiresTravel}");
            AppendIfAny(lines, "WorkLocation", context.WorkLocation);
            AppendIfAny(lines, "AvailabilityNotes", context.AvailabilityNotes);
            AppendIfAny(lines, "EmploymentType", context.EmploymentType);
            if (context.SalaryRangeMin is not null || context.SalaryRangeMax is not null)
            {
                lines.Add($"SalaryRange: {context.SalaryRangeMin} - {context.SalaryRangeMax} {context.SalaryCurrency}");
            }
            AppendListIfAny(lines, "Benefits", context.Benefits);
        }

        lines.Add(string.Empty);
        lines.Add(
            "Propose the full JobRoleDescriptionProposal now. Call search_similar_job_roles first " +
            "to check this organization's own conventions.");

        return string.Join("\n", lines);
    }

    private static void AppendCatalogList(List<string> lines, string label, IReadOnlyList<string> values)
    {
        if (values.Count > 0)
        {
            lines.Add($"- {label}: {string.Join(" | ", values)}");
        }
        else
        {
            lines.Add($"- {label}: (Catalog is currently empty — propose standard, high-quality items for new catalog creation)");
        }
    }

    private static void AppendIfAny(List<string> lines, string label, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            lines.Add($"{label}: {value}");
        }
    }

    private static void AppendListIfAny(List<string> lines, string label, IReadOnlyList<string> values)
    {
        if (values.Count > 0)
        {
            lines.Add($"{label}: {string.Join(" | ", values)}");
        }
    }
}
