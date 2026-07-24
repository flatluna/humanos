using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Configuration;
using OpenAI.Chat;

namespace HumanOS.Agents;

/// <summary>One step of a recommended learning Program — a single,
/// bounded, teachable capability name (same granularity rule as
/// frontend/human-os-web/src/features/starting-point/subjectGapSuggestions.ts:
/// narrow enough to become ONE real Capability on its own, e.g. "Control
/// de Versiones con Git", never a mega-umbrella like "Programación").</summary>
public sealed class RecommendedProgramStep
{
    public string Name { get; set; } = string.Empty;

    /// <summary>"Beginner", "Intermediate" or "Advanced" — used by the
    /// frontend to compute a suggested entry point from the person's
    /// declared current level.</summary>
    public string Level { get; set; } = string.Empty;
}

/// <summary>A Subject the person already selected in Growth Plan Step 1
/// — the agent may ONLY ever recommend within these.</summary>
public sealed class GrowthPathSubjectOption
{
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
}

/// <summary>Everything the agent needs to produce one recommendation.</summary>
public sealed class GrowthPathRequestContext
{
    public string PersonName { get; set; } = string.Empty;

    public string GoalPrompt { get; set; } = string.Empty;

    public List<GrowthPathSubjectOption> AllowedSubjects { get; set; } = [];

    /// <summary>Broader life goals/motivations the person already stated
    /// in Growth Plan Step 2 (translated labels, not raw ids) — extra
    /// context, not a hard constraint.</summary>
    public List<string> StatedGoals { get; set; } = [];

    /// <summary>A text dump of the current "future catalog" of Programs
    /// and capability-cluster suggestions already shown as chips
    /// elsewhere in the product (see subjectGapSuggestions.ts /
    /// mockLearningPrograms.ts on the frontend) — NONE of these exist as
    /// real Capabilities yet. Use them as inspiration/building blocks;
    /// you are free to adapt one, combine several, or invent a new,
    /// similarly-scoped Program/capability list if nothing fits the
    /// stated goal well, since this entire catalog is roadmap content,
    /// not a strict retrieval source.</summary>
    public string CatalogContext { get; set; } = string.Empty;

    /// <summary>Real, already-existing Program rows (curated via
    /// ProgramService/Studio) the person could actually be enrolled in
    /// today — unlike CatalogContext, these are real DB rows with a real
    /// Id. Prefer matching one of these over inventing something new.</summary>
    public List<RealProgramOption> RealPrograms { get; set; } = [];
}

/// <summary>A real, existing Program row (see LearningProgram) the agent
/// can match a goal against and point to directly.</summary>
public sealed class RealProgramOption
{
    public Guid ProgramId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;
}

/// <summary>The agent's proposed recommendation for one goal prompt.</summary>
public sealed class GrowthPathRecommendation
{
    public bool HasRecommendation { get; set; }

    /// <summary>"Program" (a full ordered path toward the goal) or
    /// "Capabilities" (a looser set of individual capability names, when
    /// a full ordered path doesn't make sense for the goal). Only
    /// meaningful when HasRecommendation is true.</summary>
    public string RecommendationType { get; set; } = string.Empty;

    public string? ProgramName { get; set; }

    public string? ProgramDescription { get; set; }

    /// <summary>Must be EXACTLY one of the codes in
    /// GrowthPathRequestContext.AllowedSubjects — never anything else.</summary>
    public string? SubjectCode { get; set; }

    /// <summary>Ordered (Beginner -> Advanced) when RecommendationType is
    /// "Program"; order doesn't matter when it's "Capabilities".</summary>
    public List<RecommendedProgramStep> Steps { get; set; } = [];

    /// <summary>Short, personalized explanation (uses the person's name,
    /// same language as the goal prompt) of why this was recommended.</summary>
    public string? Rationale { get; set; }

    /// <summary>Set to the EXACT ProgramId (copied verbatim) of one of
    /// the provided RealPrograms, ONLY when that real Program is a good
    /// match for the goal. Null when no real Program fits and a
    /// catalog-inspired/new recommendation was made instead.</summary>
    public Guid? MatchedProgramId { get; set; }
}

/// <summary>
/// Growth Plan Step 3 ("Planeemos Juntos tu Desarrollo", merged Steps
/// 3+4, agreed 2026-07-22) — recommends a Program (an ordered bundle of
/// Capabilities toward a goal) or a set of individual Capabilities, from
/// the person's free-text goal, scoped to the Subjects they already
/// picked in Step 1. Replaces the earlier pure-frontend keyword-matching
/// mock (mockLearningPrograms.ts's recommendPath) with a real LLM call.
///
/// Plain ChatClientAgent with structured output — no Harness/Skills
/// (reserved for the runtime Agente-Tutor). Follows the same
/// configuration pattern as <see cref="JobDescriptionExtractionAgent"/>
/// and <see cref="HumanOS.Agents.Studio.CuradorAgent"/>.
/// </summary>
public sealed class GrowthPathRecommenderAgent
{
    private const string Instructions = """
        You are the Growth Path Recommender agent for Human OS. A person
        tells you, in their own words, what they want to achieve, and you
        recommend either a Program (a named, ORDERED bundle of
        Capabilities that together fulfill their goal, e.g. "Learn
        English" = Basic -> Intermediate -> Advanced -> Conversation) or a
        looser set of individual Capabilities, when a strict order doesn't
        make sense for the goal.

        HARD SCOPE RULE: you will be given a list of "allowed Subjects" —
        you may ONLY ever recommend within one of those Subjects. Set
        SubjectCode to EXACTLY one of the provided codes, copied verbatim.
        Never invent a Subject or pick one outside the allowed list. If
        nothing reasonable fits the stated goal within the allowed
        Subjects, set HasRecommendation to false and leave the rest empty
        — do not force an unrelated recommendation.

        REAL EXISTING PROGRAMS (highest priority): you will also be given
        a list of REAL Programs that already exist in the product and can
        actually be assigned to the person today. If one of these is a
        good match for the stated goal, PREFER IT over inventing anything
        new: reuse its exact Name/Description as ProgramName/
        ProgramDescription, set RecommendationType to "Program", and set
        MatchedProgramId to its EXACT ProgramId, copied verbatim. Only
        fall back to the catalog context or a fully new recommendation
        when none of the real Programs reasonably fit. Never invent a
        ProgramId — only copy one from the provided list, or leave it null.

        THE "CATALOG CONTEXT" YOU RECEIVE: a dump of Programs and
        capability-cluster suggestions the product already shows as
        placeholder chips elsewhere. NONE of these exist as real,
        buildable Capabilities yet — they are the team's roadmap/wishlist,
        at the same granularity you should use. Treat this catalog as
        inspiration and building blocks, not a rigid database: you may
        reuse an existing Program/cluster as-is, adapt or recombine
        entries from it, or invent a new, similarly-scoped Program or
        capability list of your own if nothing in the catalog fits the
        stated goal well (and no real Program matched either). Either
        way, every capability name you output must be narrow enough to
        become ONE real Capability on its own — a single, bounded,
        teachable, assessable topic (e.g. "Control de Versiones con Git",
        "Fundamentos de la Nube") — never a mega-umbrella like
        "Programación" or "Cloud" by itself.

        WHEN TO RETURN "Program" vs "Capabilities": prefer "Program" when
        the goal implies a natural progression toward a single outcome
        (a certification, fluency in a skill, a career path). Use
        "Capabilities" when the goal is broader/exploratory and a strict
        order would feel arbitrary. When RecommendationType is "Program",
        order Steps from Beginner to Advanced reflecting real learning
        progression. Each Step's Level must be exactly "Beginner",
        "Intermediate", or "Advanced".

        PERSONALIZATION: you are given the person's name and the broader
        life goals/motivations they already stated earlier in their Growth
        Plan (extra context, not a hard constraint) — use them naturally
        in a short Rationale that explains, in the SAME language the goal
        prompt was written in (Spanish or English), why this
        Program/these Capabilities fit them. Address them by name.
        """;

    private readonly AIAgent? _agent;

    public GrowthPathRecommenderAgent(IConfiguration configuration)
    {
        var endpoint = configuration["AzureOpenAIEndpoint"];
        // Economy tier: this is a single short recommendation call, not
        // deep pedagogical design — a good candidate for a cheaper model,
        // same rationale as CuradorAgent. Falls back to the main
        // deployment if 'AzureOpenAIEconomyDeploymentName' isn't set.
        var deploymentName = configuration["AzureOpenAIEconomyDeploymentName"] ?? configuration["AzureOpenAIDeploymentName"];
        var apiKey = configuration["AzureOpenAIApiKey"];

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
            .AsAIAgent(instructions: Instructions, name: "GrowthPathRecommenderAgent");
    }

    public bool IsConfigured => _agent is not null;

    public async Task<GrowthPathRecommendation> RecommendAsync(
        GrowthPathRequestContext context,
        CancellationToken cancellationToken = default)
    {
        if (_agent is null)
        {
            throw new InvalidOperationException(
                "The Growth Path recommender agent is not configured. Set the 'AzureOpenAIEndpoint' and " +
                "'AzureOpenAIDeploymentName' application settings.");
        }

        var allowedSubjectsText = string.Join(
            ", ",
            context.AllowedSubjects.Select(s => $"{s.Code} ({s.Name})"));

        var statedGoalsText = context.StatedGoals.Count > 0
            ? string.Join(", ", context.StatedGoals)
            : "(none stated)";

        var realProgramsText = context.RealPrograms.Count > 0
            ? string.Join(
                "\n",
                context.RealPrograms.Select(p => $"- Id: {p.ProgramId} | Name: {p.Name} | Description: {p.Description}"))
            : "(none available)";

        var prompt = $"""
            Person's name: {context.PersonName}
            Allowed Subjects (recommend ONLY within these): {allowedSubjectsText}
            Broader life goals already stated: {statedGoalsText}

            Real, existing Programs (prefer matching one of these — see instructions):
            {realProgramsText}

            Catalog context (placeholder Programs/capability clusters — inspiration only, see instructions):
            {context.CatalogContext}

            Person's stated goal for this recommendation:
            "{context.GoalPrompt}"

            Recommend a Program or a set of individual Capabilities for this goal.
            """;

        var response = await _agent.RunAsync<GrowthPathRecommendation>(prompt);

        return response.Result;
    }
}
