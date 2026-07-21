using Azure.AI.Agents.Persistent;
using Azure.Identity;
using Microsoft.Extensions.Configuration;

namespace HumanOS.Agents.Studio;

/// <summary>One fact/finding surfaced via live web search, always carrying
/// explicit provenance (title + URL) — required both by Bing Grounding's
/// own Use and Display Requirements, and so Curador can judge credibility
/// per-source rather than treat every finding as unconditionally true.</summary>
public sealed class WebGroundingFinding
{
    public string Title { get; set; } = string.Empty;

    public string Url { get; set; } = string.Empty;
}

public sealed class WebGroundingResult
{
    /// <summary>The agent's synthesized findings, with inline
    /// "[Title](Url)" citations replacing the tool's raw annotation
    /// markers — ready to be embedded as a <see cref="RawMaterialItem"/>'s
    /// Content for Curador to ingest.</summary>
    public string Text { get; set; } = string.Empty;

    public List<WebGroundingFinding> Citations { get; set; } = [];
}

/// <summary>
/// Wraps Azure "Grounding with Bing Search" (via the AI Foundry Persistent
/// Agents service) so Curador can supplement a topic already present in
/// the user's own material with CURRENT web information — never to
/// introduce topics the user's material didn't already cover (see
/// /memories/repo/frontier-bing-grounding-design.md).
///
/// Deliberately a SEPARATE agent-construction pattern from every other
/// Studio agent (which all use the plain ChatClientAgent /
/// AzureOpenAIClient pattern, see <see cref="CuradorAgent"/>) because the
/// Bing Grounding tool is only exposed through the Assistants/Agents API,
/// not plain chat completions. Optional feature: if not configured,
/// <see cref="IsConfigured"/> is false and callers must skip web
/// enrichment entirely rather than fail the whole capability-creation run.
/// </summary>
public sealed class WebGroundingService
{
    private const string Instructions = """
        You answer questions using ONLY the Grounding with Bing Search tool
        results you receive — never your own training knowledge, since the
        whole point of this call is to find CURRENT information (things
        that may have changed since your training cutoff).

        For every distinct fact/finding you report:
        - Judge its credibility first: prefer results from reputable,
          authoritative sources (official documentation, established
          publications, recognized experts/organizations) over random
          blogs/forums. If a claim comes from a single low-authority
          source, or sources disagree, say so explicitly in plain language
          (e.g. "según una sola fuente, no confirmado en otras: ...", or
          "esto podría estar desactualizado o ser impreciso porque...").
        - Never invent or embellish beyond what the search results actually
          say. If the search results don't clearly answer the question,
          say that plainly instead of guessing.
        - Every fact must be traceable to one of the returned citations —
          do not state anything the tool's results don't support.

        Write your final answer as a short list of concrete, dated
        findings (not prose fluff) — each one self-contained enough to be
        used as a standalone reference chunk.
        """;

    private readonly PersistentAgentsClient? _client;
    private readonly string? _modelDeploymentName;
    private readonly string? _bingConnectionId;

    public WebGroundingService(IConfiguration configuration)
    {
        var projectEndpoint = configuration["AzureAIFoundryProjectEndpoint"];
        _bingConnectionId = configuration["BingGroundingConnectionId"];
        _modelDeploymentName = configuration["WebGroundingDeploymentName"];

        if (string.IsNullOrWhiteSpace(projectEndpoint)
            || string.IsNullOrWhiteSpace(_bingConnectionId)
            || string.IsNullOrWhiteSpace(_modelDeploymentName))
        {
            _client = null;
            return;
        }

        _client = new PersistentAgentsClient(projectEndpoint, new DefaultAzureCredential());
    }

    public bool IsConfigured => _client is not null;

    /// <summary>
    /// Searches the web for current information about <paramref name="topic"/>,
    /// scoped by <paramref name="context"/> (e.g. the capability's name/goal)
    /// so the search never wanders into unrelated topics.
    /// </summary>
    public async Task<WebGroundingResult> SearchAsync(
        string topic, string context, CancellationToken cancellationToken = default)
    {
        if (_client is null)
        {
            throw new InvalidOperationException(
                "WebGroundingService is not configured. Set 'AzureAIFoundryProjectEndpoint', " +
                "'BingGroundingConnectionId', and 'WebGroundingDeploymentName'.");
        }

        var bingTool = new BingGroundingToolDefinition(
            new BingGroundingSearchToolParameters(
                [new BingGroundingSearchConfiguration(_bingConnectionId)]));

        PersistentAgent agent = await _client.Administration.CreateAgentAsync(
            model: _modelDeploymentName,
            name: "HumanOS-WebGrounding",
            instructions: Instructions,
            tools: [bingTool],
            cancellationToken: cancellationToken);

        PersistentAgentThread? thread = null;
        try
        {
            thread = await _client.Threads.CreateThreadAsync(cancellationToken: cancellationToken);

            var prompt =
                $"Contexto (para que la búsqueda sea relevante, no te salgas de este tema): {context}\n\n" +
                $"Busca información ACTUAL sobre: {topic}\n\n" +
                "Reporta hallazgos concretos, fechados, con juicio de credibilidad como se indicó en tus instrucciones.";

            await _client.Messages.CreateMessageAsync(
                thread.Id, MessageRole.User, prompt, cancellationToken: cancellationToken);

            ThreadRun run = await _client.Runs.CreateRunAsync(thread.Id, agent.Id, cancellationToken: cancellationToken);

            while (run.Status == RunStatus.Queued || run.Status == RunStatus.InProgress)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(750), cancellationToken);
                run = await _client.Runs.GetRunAsync(thread.Id, run.Id, cancellationToken);
            }

            if (run.Status != RunStatus.Completed)
            {
                throw new InvalidOperationException(
                    $"Web grounding run did not complete successfully (status={run.Status}): {run.LastError?.Message}");
            }

            var result = new WebGroundingResult();
            var textParts = new List<string>();

            var messages = _client.Messages.GetMessagesAsync(
                thread.Id, order: ListSortOrder.Ascending, cancellationToken: cancellationToken);

            await foreach (PersistentThreadMessage message in messages)
            {
                if (message.Role != MessageRole.Agent)
                {
                    continue;
                }

                foreach (MessageContent contentItem in message.ContentItems)
                {
                    if (contentItem is not MessageTextContent textItem)
                    {
                        continue;
                    }

                    var text = textItem.Text;
                    if (textItem.Annotations is not null)
                    {
                        foreach (MessageTextAnnotation annotation in textItem.Annotations)
                        {
                            if (annotation is MessageTextUriCitationAnnotation urlAnnotation)
                            {
                                text = text.Replace(
                                    urlAnnotation.Text,
                                    $" [{urlAnnotation.UriCitation.Title}]({urlAnnotation.UriCitation.Uri})");

                                result.Citations.Add(new WebGroundingFinding
                                {
                                    Title = urlAnnotation.UriCitation.Title,
                                    Url = urlAnnotation.UriCitation.Uri
                                });
                            }
                        }
                    }

                    textParts.Add(text);
                }
            }

            result.Text = string.Join("\n\n", textParts);
            return result;
        }
        finally
        {
            // Agents/threads are cheap, short-lived per-call resources here
            // — clean up rather than accumulating orphaned agents/threads
            // in the AI Foundry project over many capability-creation runs.
            if (thread is not null)
            {
                await _client.Threads.DeleteThreadAsync(thread.Id, cancellationToken);
            }

            await _client.Administration.DeleteAgentAsync(agent.Id, cancellationToken);
        }
    }
}
