using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;

namespace HumanOS.Agents;

/// <summary>One Department/Área/Team box or label detected in the org
/// chart image.</summary>
public sealed class OrgChartDepartmentNode
{
    public string Name { get; set; } = string.Empty;

    /// <summary>The exact Name of the broader department/area this one
    /// visually nests under in the image (matching another entry's Name
    /// exactly). Null for a top-level department.</summary>
    public string? ParentDepartmentName { get; set; }
}

/// <summary>One person/role box detected in the org chart image.</summary>
public sealed class OrgChartPersonNode
{
    public string Name { get; set; } = string.Empty;

    public string Position { get; set; } = string.Empty;

    /// <summary>The exact Name of the Department (see
    /// <see cref="OrgChartExtractionResult.Departments"/>) this person is
    /// grouped under in the image. Null if the image shows no department
    /// grouping at all.</summary>
    public string? DepartmentName { get; set; }

    /// <summary>The exact Name of this person's manager, as it appears
    /// elsewhere in <see cref="OrgChartExtractionResult.People"/>. Empty
    /// for the top of the chart (e.g. the CEO), who reports to no one
    /// shown in the image.</summary>
    public string? ReportsTo { get; set; }

    /// <summary>Hierarchy depth: 0 for the top of the chart (CEO/root),
    /// 1 for people who report directly to the top, 2 for the next level
    /// down, and so on.</summary>
    public int Level { get; set; }
}

/// <summary>The organization structure the extraction agent proposes from
/// an uploaded org chart image — a proposal for an HRAdmin to review and
/// edit on the "Importar organigrama" screen. Nothing here is written to
/// <see cref="HumanOS.Models.OrgStructure.Department"/>/<see cref="HumanOS.Models.OrgStructure.JobRole"/>
/// until explicitly confirmed via ApplyOrgChartFunction — and no
/// <see cref="HumanOS.Models.People.Person"/> record is ever created from
/// this (a Person requires a real, already-authenticated Azure AD
/// identity, which an image can never provide).</summary>
public sealed class OrgChartExtractionResult
{
    public List<OrgChartDepartmentNode> Departments { get; set; } = [];

    public List<OrgChartPersonNode> People { get; set; } = [];
}

/// <summary>
/// Reads an uploaded org chart image (e.g. a photo or screenshot of a
/// company's organizational chart) and asks a real, vision-capable LLM,
/// via Microsoft Agent Framework
/// (https://learn.microsoft.com/en-us/agent-framework/), to extract the
/// departments/areas and the people/positions/reporting lines shown in
/// it, so an HRAdmin can review and confirm the structure instead of
/// re-typing the whole org chart by hand — see OrganizationPage.tsx's
/// "Importar organigrama (IA)" button and OrgChartImportPage.tsx.
///
/// Same multimodal configuration/DI pattern as
/// <see cref="Studio.PdfImageDescriptionAgent"/>: a <see cref="ChatMessage"/>
/// with both a <see cref="TextContent"/> instruction and a
/// <see cref="DataContent"/> image part, requiring the extra
/// <c>.AsIChatClient()</c> step before <c>.AsAIAgent</c> — plain
/// <c>ChatClient.AsAIAgent(...)</c> does not exist on the raw OpenAI SDK
/// type. Uses the same vision-capable deployment
/// ('AzureOpenAIVisionDeploymentName').
/// </summary>
public sealed class OrgChartExtractionAgent
{
    private const string ExtractionInstructions = """
        You extract the organization structure from an org chart image (a
        diagram of boxes/labels showing departments and/or people
        connected by lines showing who reports to whom, or grouped under
        department headings).

        First, list every DEPARTMENT/AREA/TEAM box or label you can see
        (e.g. "Engineering", "Retail", "Marketing"). For each one:
        - Name: exactly as written in the image.
        - ParentDepartmentName: the exact Name of the broader
          department/area it visually nests under, if any (matching
          another Department's Name exactly). Leave empty for a
          top-level department.

        Then, for every PERSON/role box you can see, extract:
        - Name: the person's name as written in the box. If only a
          position/title is shown with no name, leave Name empty.
        - Position: the job title/role as written in the box.
        - DepartmentName: the exact Name of the Department this person is
          grouped under (matching a Department's Name exactly), if the
          image groups people by department. Leave empty if the image
          shows no department grouping at all.
        - ReportsTo: the exact Name of the person directly above them in
          the hierarchy (their manager), matching another entry's Name
          exactly. Leave empty for the top of the chart (e.g. the CEO),
          who reports to no one shown in the image.
        - Level: 0 for the top/root of the chart, 1 for boxes that report
          directly to the top, 2 for the next level down, and so on,
          based on the visual hierarchy in the image.

        Only extract departments/people/positions/reporting lines that are
        explicitly visible in the image. Never invent anything that isn't
        shown. This is a provisional extraction for a human to review and
        confirm — it is not an authoritative record.
        """;

    private readonly AIAgent? _agent;

    public OrgChartExtractionAgent(IConfiguration configuration)
    {
        var endpoint = configuration["AzureOpenAIEndpoint"];
        var deploymentName = configuration["AzureOpenAIVisionDeploymentName"] ?? configuration["AzureOpenAIDeploymentName"];
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
            .AsIChatClient()
            .AsAIAgent(instructions: ExtractionInstructions, name: "OrgChartExtractionAgent");
    }

    public bool IsConfigured => _agent is not null;

    /// <summary>The deployment/model name used for extraction, for
    /// diagnostics purposes.</summary>
    public string? DeploymentName { get; }

    public async Task<OrgChartExtractionResult> ExtractAsync(
        byte[] imageBytes,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        if (_agent is null)
        {
            throw new InvalidOperationException(
                "The Org Chart extraction agent is not configured. Set the 'AzureOpenAIEndpoint' and " +
                "'AzureOpenAIVisionDeploymentName' (or 'AzureOpenAIDeploymentName') application settings.");
        }

        var imageContent = new DataContent(imageBytes, contentType)
        {
            // "high" (not the "low" used by PdfImageDescriptionAgent): org
            // charts routinely pack many small names/titles into one
            // image, so the extra resolution is worth the token cost for
            // this one-off, explicit HRAdmin action (unlike that agent's
            // per-page pipeline cost concern).
            AdditionalProperties = new() { ["detail"] = "high" },
        };

        var message = new ChatMessage(ChatRole.User,
        [
            new TextContent("Extract the full department structure and organization hierarchy from this org chart image."),
            imageContent,
        ]);

        var response = await _agent.RunAsync<OrgChartExtractionResult>(message, cancellationToken: cancellationToken);

        return response.Result;
    }
}
