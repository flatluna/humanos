using Azure.AI.OpenAI;
using Azure.Identity;
using HumanOS.Agents.Studio;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Configuration;
using OpenAI.Chat;

namespace HumanOS.Agents;

/// <summary>The structured résumé/CV fields the extraction agent proposes
/// — a pure PROPOSAL for an HRAdmin to review (see
/// ExtractPersonResumeFunction and the EmployeeDetailPage before/after
/// review UI), never written directly to
/// <see cref="HumanOS.Models.People.PersonResume"/>.</summary>
public sealed class ResumeExtractionProposal
{
    public string? Address { get; set; }

    public string? LinkedInUrl { get; set; }

    public string? ProfessionalObjective { get; set; }

    public string? ProfessionalSummary { get; set; }

    public List<string> Skills { get; set; } = [];

    public List<string> WorkExperience { get; set; } = [];

    public List<string> Education { get; set; } = [];

    public List<string> Certifications { get; set; } = [];

    public List<string> Languages { get; set; } = [];
}

/// <summary>Result of an extraction call: the proposal plus the token
/// usage of the call that produced it (observability only) — same shape
/// as JobRoleDescriptionGenerationResult.</summary>
public sealed class ResumeExtractionResult
{
    public ResumeExtractionProposal Proposal { get; set; } = null!;

    public AgentTokenUsage TokenUsage { get; set; } = null!;
}

/// <summary>
/// Extracts structured résumé/CV fields (address, LinkedIn, professional
/// objective/summary, skills, work experience, education, certifications,
/// languages) from a person's raw pasted résumé text, via Microsoft Agent
/// Framework (https://learn.microsoft.com/en-us/agent-framework/) — same
/// <c>Microsoft.Agents.AI.OpenAI</c> Azure OpenAI Chat Completion provider
/// as every other agent in this codebase.
///
/// Purely extractive, same rationale as <see cref="JobDescriptionExtractionAgent"/>:
/// bounded field extraction from already-provided text ("only extract what's
/// explicitly present, never invent") — a good candidate for the cheaper
/// "economy" deployment, not the main one. Deliberately has NO tools
/// (unlike <see cref="JobRoleDescriptionAgent"/>'s search_similar_job_roles):
/// there is no organizational data relevant to extracting one person's OWN
/// résumé, so a plain ChatClientAgent is enough.
///
/// Singleton, same rationale as every other Studio/agent-framework agent in
/// this codebase (holds no per-request state — only a configured AIAgent
/// instance).
///
/// TODO: Set the "AzureOpenAIEndpoint" and "AzureOpenAIDeploymentName"
/// application settings once real credentials are provided — see
/// <see cref="JobDescriptionExtractionAgent"/>.
/// </summary>
public sealed class ResumeExtractionAgent
{
    private const string Instructions = """
        You extract structured résumé/CV information from the raw text of a person's
        résumé, for an HR enterprise console's employee dashboard. Only extract
        information that is explicitly present in the provided text — never invent an
        address, LinkedIn URL, skill, job, degree, certification, or language that isn't
        stated or clearly implied by the text. If a field is not present, leave it empty
        (empty string or empty list) rather than guessing — this is a provisional
        extraction for an HR admin to review, edit, and confirm, not an authoritative
        record.

        Write each list item as a single, complete, human-readable line:
        - Work experience: "Empresa — Puesto (2020-2023): logro o responsabilidad clave".
        - Education: "Institución — Título (2015-2019)".
        - Skills/certifications/languages: one item per line, no filler, no near-duplicate
          entries, no placeholder text like "N/A".

        Write in the same language as the résumé text you were given (Spanish résumé →
        Spanish output, English → English output).
        """;

    private readonly AIAgent? _agent;

    public ResumeExtractionAgent(IConfiguration configuration)
    {
        var endpoint = configuration["AzureOpenAIEndpoint"];
        // Economy tier: this is bounded field extraction from an already-provided
        // text, not open-ended reasoning — same rationale as
        // JobDescriptionExtractionAgent. Falls back to the main deployment if
        // 'AzureOpenAIEconomyDeploymentName' isn't set.
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
            .AsAIAgent(instructions: Instructions, name: "ResumeExtractionAgent");
    }

    public bool IsConfigured => _agent is not null;

    /// <summary>The deployment/model used for extraction (e.g.
    /// "gpt-5-mini"), recorded on
    /// <see cref="HumanOS.Models.People.PersonResume.ExtractionModel"/> for
    /// audit purposes.</summary>
    public string? DeploymentName { get; }

    public async Task<ResumeExtractionResult> ExtractAsync(
        string resumeText,
        CancellationToken cancellationToken = default)
    {
        if (_agent is null)
        {
            throw new InvalidOperationException(
                "The Resume extraction agent is not configured. Set the 'AzureOpenAIEndpoint' " +
                "and 'AzureOpenAIDeploymentName' application settings once credentials are provided.");
        }

        var prompt = "Extract the structured résumé/CV fields from the following text:\n\n" + resumeText;

        var response = await _agent.RunAsync<ResumeExtractionProposal>(prompt, cancellationToken: cancellationToken);

        var usage = response.Usage;
        var tokenUsage = new AgentTokenUsage
        {
            AgentName = "ResumeExtractionAgent",
            ModelName = DeploymentName!,
            InputTokens = (int)(usage?.InputTokenCount ?? 0),
            OutputTokens = (int)(usage?.OutputTokenCount ?? 0),
            CachedInputTokens = (int)(usage?.CachedInputTokenCount ?? 0),
        };

        return new ResumeExtractionResult { Proposal = response.Result, TokenUsage = tokenUsage };
    }
}
