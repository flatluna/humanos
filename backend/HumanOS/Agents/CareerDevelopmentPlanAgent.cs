using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Configuration;
using OpenAI.Chat;

namespace HumanOS.Agents;

/// <summary>One ordered step in a track-specific roadmap (technical OR
/// soft skills) — an AI-suggested training topic that closes one (or a
/// tightly related group of) gap skill(s). Saved and shown to HR even
/// when no matching course exists yet in the catalog, since Programs are
/// expected to be created/uploaded incrementally afterward.</summary>
public sealed class CareerPlanRoadmapStep
{
    /// <summary>1-based sequence position within its track — foundational
    /// steps first, building up to more advanced ones.</summary>
    public int Order { get; set; }

    /// <summary>The specific required skill (from RequiredTechnicalSkills
    /// or RequiredSoftSkills) this step targets — copied/closely matched
    /// from the input, never invented.</summary>
    public string Skill { get; set; } = string.Empty;

    /// <summary>Short, concrete training/course title recommended for this
    /// step. If a real Program from RealPrograms is a good match, use ITS
    /// exact name here; otherwise propose an aspirational course title
    /// (e.g. "Fundamentos de PCB Design") for HR to create later.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>1-2 sentences: what this training should cover and why it
    /// matters for closing this specific gap.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Must be EXACTLY one of the ProgramId values from the
    /// provided RealPrograms, copied verbatim, ONLY when that Program
    /// genuinely covers this step's skill. Leave null when no real
    /// Program covers it yet — never invent an id.</summary>
    public Guid? MatchedProgramId { get; set; }
}

/// <summary>The agent's proposed career development plan for one person —
/// a skill gap analysis plus a two-track (technical + soft skills)
/// AI-recommended training roadmap, each an ordered sequence of steps.</summary>
public sealed class CareerPlanProposal
{
    /// <summary>The role's required skills (technical + soft, combined)
    /// that the person's current skills don't clearly cover yet.</summary>
    public List<string> GapSkills { get; set; } = [];

    /// <summary>Ordered training roadmap covering the REQUIRED TECHNICAL
    /// skill gaps only — empty if the person has no technical gaps.</summary>
    public List<CareerPlanRoadmapStep> TechnicalSkillsRoadmap { get; set; } = [];

    /// <summary>Ordered training roadmap covering the REQUIRED SOFT skill
    /// gaps only — empty if the person has no soft-skill gaps.</summary>
    public List<CareerPlanRoadmapStep> SoftSkillsRoadmap { get; set; } = [];

    /// <summary>Ordered training roadmap covering the REQUIRED DEPARTMENT
    /// PROCESS gaps only — empty if the person has no gaps in this track or
    /// the role has no required department processes listed.</summary>
    public List<CareerPlanRoadmapStep> DepartmentProcessesRoadmap { get; set; } = [];

    /// <summary>Ordered training roadmap covering the REQUIRED HR PROCESS
    /// gaps only (compliance/policy training, e.g. "Prevención de Acoso
    /// Sexual Laboral") — empty if the person has no gaps in this track or
    /// the role has no required HR processes listed.</summary>
    public List<CareerPlanRoadmapStep> HrProcessesRoadmap { get; set; } = [];

    /// <summary>Short, personalized narrative explaining the plan as a
    /// whole (uses the person's name and role, same language as the input).</summary>
    public string OverallRationale { get; set; } = string.Empty;
}

/// <summary>Everything the agent needs to produce one career plan.</summary>
public sealed class CareerPlanRequestContext
{
    public string PersonName { get; set; } = string.Empty;

    public string JobRoleTitle { get; set; } = string.Empty;

    public List<string> RequiredTechnicalSkills { get; set; } = [];

    public List<string> RequiredSoftSkills { get; set; } = [];

    /// <summary>The role's required department-specific processes
    /// (JobRole.DepartmentProcessesJson) — may be empty.</summary>
    public List<string> RequiredDepartmentProcesses { get; set; } = [];

    /// <summary>The role's required HR/compliance processes and policies
    /// (JobRole.HrProcessesJson) — may be empty.</summary>
    public List<string> RequiredHrProcesses { get; set; } = [];

    /// <summary>The person's own declared skills (from their résumé) —
    /// may be empty if they haven't filled in a résumé yet, in which case
    /// the agent should treat every required skill as a gap.</summary>
    public List<string> CurrentSkills { get; set; } = [];

    /// <summary>Real, already-existing, active Program rows the agent can
    /// recommend — the only Programs it's allowed to reference.</summary>
    public List<RealProgramOption> RealPrograms { get; set; } = [];
}

/// <summary>
/// Enterprise console "Rutas de Carrera" — per-employee AI career
/// development plan: compares a person's current skills (self-reported in
/// their résumé) against their assigned JobRole's required technical/soft
/// skills, identifies the gap, and produces a two-track (technical + soft
/// skills) ordered training roadmap, matching real catalog Programs where
/// one already exists. Reuses <see cref="RealProgramOption"/> from
/// <see cref="GrowthPathRecommenderAgent"/> since both agents share the
/// exact same "match against real, existing Programs" concept.
///
/// Plain ChatClientAgent with structured output, no tools — same
/// rationale as <see cref="ResumeExtractionAgent"/>/<see cref="GrowthPathRecommenderAgent"/>.
/// Singleton: holds no per-request state, only a configured AIAgent
/// instance.
/// </summary>
public sealed class CareerDevelopmentPlanAgent
{
    private const string Instructions = """
        You are the Career Development Plan agent for Human OS's HR enterprise
        console. Given one employee's current job role (with its required
        technical skills, soft skills, department processes, and HR/compliance
        processes, listed SEPARATELY) and the skills that employee has
        actually declared in their résumé, you:

        1. Identify the GAP: which of the role's required skills/processes are
           NOT clearly covered by the employee's current skills. Be reasonable
           about synonyms/close matches (e.g. "Excel avanzado" covers
           "Microsoft Excel"), but do not assume a skill is covered just
           because it seems related — only mark it covered when the
           employee's own skills clearly demonstrate it. Department processes
           and HR/compliance processes are almost NEVER already covered by a
           résumé (they are role/company-specific, not general skills) —
           treat every one of them as a gap unless the employee's résumé
           explicitly says they already completed that exact process/policy
           training. If the employee has no declared skills at all, treat
           every required skill/process as a gap. Return the combined list
           (technical + soft + department processes + HR processes) as
           GapSkills.

        2. Build FOUR separate ordered training roadmaps to close those gaps:
           - TechnicalSkillsRoadmap: one step per gap TECHNICAL skill (or a small
             group of closely related ones combined into a single step).
           - SoftSkillsRoadmap: one step per gap SOFT skill (same grouping rule).
           - DepartmentProcessesRoadmap: one step per gap DEPARTMENT PROCESS.
           - HrProcessesRoadmap: one step per gap HR/COMPLIANCE PROCESS.
           Order steps within each track from foundational to advanced. For each
           step, set Skill (the specific required skill/process it targets), a
           short Title, and a 1-2 sentence Description of what the training
           should cover and why it matters for this role. If any of the
           provided real, existing Programs is a genuinely good match for a
           step, set MatchedProgramId to its EXACT ProgramId (copied verbatim)
           and use ITS real Name as the Title — never invent a Program or
           ProgramId. If no real Program covers a step yet, leave
           MatchedProgramId null and still propose a concrete, aspirational
           Title/Description for HR to create that course later — a step MUST
           NEVER be skipped just because no matching course exists yet. If a
           track has no gaps (or the role listed nothing for it), its roadmap
           is an empty list.

        3. Write a short OverallRationale (2-4 sentences) summarizing the plan for
           an HR admin: what the gap is, why it matters for this role, and how the
           roadmaps address it. Use the employee's name and role naturally.
           Write in the same language as the role/skills data you were given
           (Spanish data → Spanish output, English → English output).

        Never invent skills, roles, or Programs beyond what was explicitly provided.
        """;

    private readonly AIAgent? _agent;

    public CareerDevelopmentPlanAgent(IConfiguration configuration)
    {
        var endpoint = configuration["AzureOpenAIEndpoint"];
        var deploymentName = configuration["AzureOpenAIDeploymentName"];
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
            .AsAIAgent(instructions: Instructions, name: "CareerDevelopmentPlanAgent");
    }

    public bool IsConfigured => _agent is not null;

    /// <summary>The deployment/model used (e.g. "gpt-5-mini"), recorded on
    /// <see cref="HumanOS.Models.People.PersonCareerPlan.GeneratedModel"/>
    /// for audit purposes.</summary>
    public string? DeploymentName { get; }

    public async Task<CareerPlanProposal> GenerateAsync(
        CareerPlanRequestContext context,
        CancellationToken cancellationToken = default)
    {
        if (_agent is null)
        {
            throw new InvalidOperationException(
                "The Career Development Plan agent is not configured. Set the 'AzureOpenAIEndpoint' " +
                "and 'AzureOpenAIDeploymentName' application settings once credentials are provided.");
        }

        var prompt = $"""
            Employee: {context.PersonName}
            Job role: {context.JobRoleTitle}

            Required technical skills:
            {(context.RequiredTechnicalSkills.Count == 0 ? "(none listed)" : string.Join('\n', context.RequiredTechnicalSkills.Select(s => "- " + s)))}

            Required soft skills:
            {(context.RequiredSoftSkills.Count == 0 ? "(none listed)" : string.Join('\n', context.RequiredSoftSkills.Select(s => "- " + s)))}

            Required department processes:
            {(context.RequiredDepartmentProcesses.Count == 0 ? "(none listed)" : string.Join('\n', context.RequiredDepartmentProcesses.Select(s => "- " + s)))}

            Required HR/compliance processes:
            {(context.RequiredHrProcesses.Count == 0 ? "(none listed)" : string.Join('\n', context.RequiredHrProcesses.Select(s => "- " + s)))}

            Employee's current declared skills:
            {(context.CurrentSkills.Count == 0 ? "(none declared yet)" : string.Join('\n', context.CurrentSkills.Select(s => "- " + s)))}

            Real, existing Programs available to recommend (ProgramId | Name | Description):
            {(context.RealPrograms.Count == 0 ? "(no active Programs available)" : string.Join('\n', context.RealPrograms.Select(p => $"- {p.ProgramId} | {p.Name} | {p.Description}")))}
            """;

        var response = await _agent.RunAsync<CareerPlanProposal>(prompt, cancellationToken: cancellationToken);

        return response.Result;
    }
}
