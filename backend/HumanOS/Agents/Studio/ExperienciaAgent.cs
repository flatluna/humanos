using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Configuration;
using OpenAI.Chat;

namespace HumanOS.Agents.Studio;

public sealed class CompletedModule
{
    public ModuleSkeleton Module { get; set; } = null!;

    public ModuleScript Script { get; set; } = null!;

    public ModuleMetricAssignment Metrics { get; set; } = null!;

    /// <summary>
    /// The module's real processing outcome (fixed Paso 5, 2026-07-14 —
    /// see HUMAN-OS-STUDIO.md §14), set by <see cref="CompletedModuleValidator"/>
    /// inside <c>MetricoExecutor.cs</c> right after Métrico runs. Having
    /// run through the Instructor and Métrico does NOT mean the module is
    /// done — only <see cref="ModuleProcessingStatus.Verified"/> counts.
    /// </summary>
    public ModuleProcessingStatus Status { get; set; } = ModuleProcessingStatus.Pending;

    /// <summary>
    /// Set only when <see cref="Status"/> is <see cref="ModuleProcessingStatus.Failed"/>
    /// — the technical/contract-violation message that caused it. Null
    /// otherwise (a <see cref="ModuleProcessingStatus.RequiresRevision"/>
    /// module's explanation lives in <see cref="Metrics"/>'s Rationale/
    /// Verification instead, since that is a pedagogical outcome, not an
    /// error).
    /// </summary>
    public string? FailureReason { get; set; }
}

public sealed class CapabilityPackage
{
    public Guid PackageId { get; set; } = Guid.NewGuid();

    public Guid BlueprintId { get; set; }

    public string CapabilityName { get; set; } = string.Empty;

    /// <summary>
    /// Set by PublishExecutor once the Capability row is actually
    /// persisted — null until then. This is the id the frontend needs to
    /// deep-link to /courses/{capabilityId} (or call GET /capabilities/{code})
    /// after publication completes.
    /// </summary>
    public Guid? CapabilityId { get; set; }

    /// <summary>
    /// Consolidated knowledge-base text for the runtime Agente-Tutor's RAG.
    /// TODO (Point 2/4 — data model + tutor): actually chunk and embed this
    /// into the Azure SQL VECTOR column instead of carrying it as a single
    /// blob. This field is a prototype placeholder for that future
    /// indexing step, not implemented yet.
    /// </summary>
    public string TutorKnowledgeBase { get; set; } = string.Empty;

    public List<CompletedModule> Modules { get; set; } = [];

    /// <summary>Per-agent-call token usage across the ENTIRE run (Curador,
    /// Arquitecto, one entry per Instructor/Métrico call, and Experiencia
    /// itself) — observability only, see <see cref="AgentTokenUsage"/>.
    /// Populated by <c>ExperienciaExecutor</c> from the accumulated
    /// <c>PipelineState.TokenUsage</c>.</summary>
    public List<AgentTokenUsage> TokenUsage { get; set; } = [];
}

/// <summary>Structured-output shape the Experiencia agent itself returns —
/// it only authors the knowledge-base text; the rest of
/// <see cref="CapabilityPackage"/> is assembled in code from data the
/// pipeline already has.</summary>
internal sealed class TutorKnowledgeBaseResult
{
    public string TutorKnowledgeBase { get; set; } = string.Empty;
}

/// <summary>
/// Agente Experiencia — final step of the Human OS Studio pipeline.
/// Assembles the completed modules into a <see cref="CapabilityPackage"/>
/// and authors the consolidated knowledge-base text the runtime
/// Agente-Tutor will use for RAG during the interactive experience. Its
/// output is the subject of GATE 2 (human review before publishing).
/// </summary>
public sealed class ExperienciaAgent
{
    private const string Instructions = """
        You are the Experiencia agent in Human OS Studio, the final step of
        the capability-creation pipeline. Given a capability's full set of
        completed modules (title, description, type, script, metrics),
        write a consolidated knowledge-base summary that an embedded
        agent-tutor will use to answer learner questions about this
        capability during an interactive video experience — including
        being able to explain a concept a different way if the learner
        says they did not understand it. The summary must cover every
        module's key content, without inventing facts beyond what the
        modules contain.
        """;

    private readonly AIAgent? _agent;

    public ExperienciaAgent(IConfiguration configuration)
    {
        var endpoint = configuration["AzureOpenAIEndpoint"];
        var deploymentName = configuration["AzureOpenAIDeploymentName"];
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
            .AsAIAgent(instructions: Instructions, name: "ExperienciaAgent");
    }

    public bool IsConfigured => _agent is not null;

    public async Task<CapabilityPackage> AssembleAsync(
        CapabilityBlueprint blueprint,
        List<CompletedModule> completedModules,
        CancellationToken cancellationToken = default)
    {
        if (_agent is null)
        {
            throw new InvalidOperationException(
                "The Experiencia agent is not configured. Set the 'AzureOpenAIEndpoint' and " +
                "'AzureOpenAIDeploymentName' application settings.");
        }

        var modulesText = string.Join(
            "\n\n---\n\n",
            completedModules.Select(m =>
                $"Module: {m.Module.Title} ({m.Module.Type})\n" +
                $"Description: {m.Module.Description}\n" +
                $"Metrics: {string.Join(", ", m.Metrics.Metrics)}\n" +
                $"Script:\n{m.Script.Script}"));

        var prompt = $"Capability: {blueprint.CapabilityName}\nGoal: {blueprint.Goal}\n\n{modulesText}";

        var response = await _agent.RunAsync<TutorKnowledgeBaseResult>(prompt);

        var usage = response.Usage;
        var tokenUsage = new AgentTokenUsage
        {
            AgentName = "Experiencia",
            InputTokens = (int)(usage?.InputTokenCount ?? 0),
            OutputTokens = (int)(usage?.OutputTokenCount ?? 0),
            CachedInputTokens = (int)(usage?.CachedInputTokenCount ?? 0)
        };

        return new CapabilityPackage
        {
            BlueprintId = blueprint.BlueprintId,
            CapabilityName = blueprint.CapabilityName,
            TutorKnowledgeBase = response.Result.TutorKnowledgeBase,
            Modules = completedModules,
            TokenUsage = [tokenUsage]
        };
    }
}
