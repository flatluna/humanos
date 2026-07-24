using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Configuration;
using OpenAI.Chat;

namespace HumanOS.Agents;

/// <summary>
/// The structured Job Description fields the extraction agent proposes.
/// Every field is a proposal for the employee to review — nothing here
/// is written to <see cref="HumanOS.Models.JobDescriptions.JobDescriptionRecord"/>
/// as "Confirmed" until the employee explicitly confirms it.
/// </summary>
public sealed class JobDescriptionExtractionResult
{
    public string JobTitle { get; set; } = string.Empty;

    public string? RolePurpose { get; set; }

    public string? RoleSummary { get; set; }

    public List<string> PrimaryResponsibilities { get; set; } = [];

    public List<string> ExpectedOutcomes { get; set; } = [];

    public string? RequiredExperience { get; set; }

    public List<string> ToolsMentioned { get; set; } = [];

    /// <summary>The agent's suggested professional level (e.g.
    /// "Senior") based on the responsibilities described — a proposal,
    /// never auto-applied to the employee's profile.</summary>
    public string? SuggestedProfessionalLevel { get; set; }
}

/// <summary>
/// Reads the plain text of an uploaded Job Description (see
/// <see cref="HumanOS.Storage.PdfTextExtractor"/>) and asks a real LLM,
/// via Microsoft Agent Framework
/// (https://learn.microsoft.com/en-us/agent-framework/), to extract
/// structured fields from it.
///
/// Built on the Azure OpenAI Chat Completion provider
/// (<c>Microsoft.Agents.AI.OpenAI</c>) — see
/// https://learn.microsoft.com/en-us/agent-framework/agents/providers/azure-openai.
/// The agent only extracts what is explicitly present in the source
/// text; it must never invent organizational facts.
///
/// TODO: Set the "AzureOpenAIEndpoint" and "AzureOpenAIDeploymentName"
/// (e.g. a "gpt-5-mini" deployment) application settings once real
/// credentials are provided. Until then, <see cref="IsConfigured"/> is
/// false and extraction is rejected with a clear error. Optionally set
/// "AzureOpenAIApiKey" for key-based auth; otherwise falls back to
/// <see cref="DefaultAzureCredential"/> (Managed Identity in Azure).
///
/// TODO: Enable OpenTelemetry observability for this agent once an
/// Application Insights connection string is available — see
/// https://learn.microsoft.com/en-us/agent-framework/agents/observability.
/// </summary>
public sealed class JobDescriptionExtractionAgent
{
    private const string ExtractionInstructions = """
        You extract structured information from an organization's Job
        Description document. Only extract information that is
        explicitly present in the provided text. Never invent
        responsibilities, outcomes, tools, or requirements that are not
        stated in the document. If a field is not present in the text,
        leave it empty. This is a provisional extraction for a human
        employee to review and confirm — it is not an authoritative
        record.
        """;

    private readonly AIAgent? _agent;

    public JobDescriptionExtractionAgent(IConfiguration configuration)
    {
        var endpoint = configuration["AzureOpenAIEndpoint"];
        // Economy tier: this is bounded field extraction from an already-provided
        // text ("only extract what's explicitly present, never invent"), not
        // open-ended reasoning — a good candidate for a cheaper model. Falls back
        // to the main deployment if 'AzureOpenAIEconomyDeploymentName' isn't set.
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
            .AsAIAgent(
                instructions: ExtractionInstructions,
                name: "JobDescriptionExtractionAgent");
    }

    public bool IsConfigured => _agent is not null;

    /// <summary>The deployment/model name used for extraction (e.g.
    /// "gpt-5-mini"), recorded on <see cref="HumanOS.Models.JobDescriptions.JobDescriptionRecord.ExtractionModel"/>
    /// for audit purposes.</summary>
    public string? DeploymentName { get; }

    public async Task<JobDescriptionExtractionResult> ExtractAsync(
        string jobDescriptionText,
        CancellationToken cancellationToken = default)
    {
        if (_agent is null)
        {
            throw new InvalidOperationException(
                "The Job Description extraction agent is not configured. Set the " +
                "'AzureOpenAIEndpoint' and 'AzureOpenAIDeploymentName' application settings " +
                "once credentials are provided.");
        }

        var prompt =
            "Extract the structured Job Description fields from the following document text:\n\n" +
            jobDescriptionText;

        var response = await _agent.RunAsync<JobDescriptionExtractionResult>(prompt);

        return response.Result;
    }
}
