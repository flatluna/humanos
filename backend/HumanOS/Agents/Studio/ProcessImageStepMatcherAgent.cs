using System.Text.Json;
using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;

namespace HumanOS.Agents.Studio;

/// <summary>
/// Decides which process step(s) an image extracted from a PDF page
/// actually illustrates, when that page is shared by more than one step
/// (e.g. a short page with a step's closing paragraph and the next step's
/// opening paragraph both on it). Page-number matching alone cannot tell
/// these apart (see <see cref="HumanOS.Services.ProcessDocuments.ProcessDocumentPipelineService.AttachStepImagesAsync"/>),
/// so this agent looks at the image itself plus each candidate step's own
/// verbatim source text and picks the step(s) it genuinely belongs to.
///
/// Plain ChatClientAgent, multimodal, same "low" detail/cost posture as
/// <see cref="PdfImageDescriptionAgent"/> — this is a coarse classification
/// task, not pixel-level analysis.
/// </summary>
public sealed class ProcessImageStepMatcherAgent
{
    private const string Instructions = """
        You will be shown one image extracted from a page of a business
        process document (a manual, SOP, or policy), plus a numbered list
        of candidate process steps whose own text comes from that same
        page.

        Decide which candidate step(s) this image actually illustrates —
        based on the image's visible content (what form, screen, diagram,
        or photo it shows) and which candidate's text it most plausibly
        accompanies. A page can contain the end of one step and the start
        of another; the image usually belongs with only ONE of them, not
        all.

        If the image is purely decorative (a logo, a divider line, a
        background pattern) and does not meaningfully illustrate any
        candidate, return an empty list.

        Respond with ONLY a JSON object, no markdown, in this exact shape:
        {"matched_indices": [<int>, ...], "reason": "<one short sentence>"}

        Where each index is the 1-based number of a candidate step from the
        provided list. Return at most all the indices you are confident
        about — do not guess if you are unsure; an empty list is a valid,
        correct answer when the image does not clearly belong to any one
        step.
        """;

    private readonly AIAgent? _agent;
    private readonly string _modelName;

    public ProcessImageStepMatcherAgent(IConfiguration configuration)
    {
        var endpoint = configuration["AzureOpenAIEndpoint"];
        var deploymentName = configuration["AzureOpenAIVisionDeploymentName"] ?? configuration["AzureOpenAIDeploymentName"];
        var apiKey = configuration["AzureOpenAIApiKey"];

        _modelName = deploymentName ?? string.Empty;

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
            .AsAIAgent(instructions: Instructions, name: "ProcessImageStepMatcherAgent");
    }

    public bool IsConfigured => _agent is not null;

    public sealed class StepCandidate
    {
        public required string StepId { get; init; }
        public required string StepName { get; init; }
        public required string SourceContent { get; init; }
    }

    public sealed class MatchResult
    {
        /// <summary>StepIds (from the input candidates) this image was matched to. May be empty
        /// (decorative image / no confident match) or contain more than one (image genuinely
        /// shared by adjacent steps).</summary>
        public List<string> MatchedStepIds { get; set; } = [];

        public string Reason { get; set; } = string.Empty;
    }

    /// <summary>Picks which of <paramref name="candidates"/> (all steps mapped to the same source
    /// page as the image) this image actually illustrates. Returns all candidate StepIds
    /// (fail-open, preserving today's behavior) if the agent isn't configured or the call fails —
    /// callers should treat that as "could not refine, keep existing page-level association".</summary>
    public async Task<MatchResult> MatchAsync(
        byte[] imageBytes,
        string contentType,
        List<StepCandidate> candidates,
        CancellationToken cancellationToken = default)
    {
        if (_agent is null || candidates.Count == 0)
        {
            return new MatchResult { MatchedStepIds = candidates.Select(c => c.StepId).ToList(), Reason = "Agent not configured; kept page-level association." };
        }

        var candidateList = string.Join("\n\n", candidates.Select((c, i) =>
            $"{i + 1}. Step \"{c.StepName}\":\n{Truncate(c.SourceContent, 1500)}"));

        var promptText = $"Here are the candidate steps from this page:\n\n{candidateList}\n\nWhich step(s) does this image belong to?";

        var imageContent = new DataContent(imageBytes, contentType)
        {
            AdditionalProperties = new() { ["detail"] = "low" }
        };

        var message = new ChatMessage(ChatRole.User,
        [
            new TextContent(promptText),
            imageContent
        ]);

        try
        {
            var response = await _agent.RunAsync(message, cancellationToken: cancellationToken);
            var text = response.Text?.Trim() ?? string.Empty;

            // Strip accidental markdown code fences before parsing.
            if (text.StartsWith("```"))
            {
                var firstNewline = text.IndexOf('\n');
                var lastFence = text.LastIndexOf("```");
                if (firstNewline >= 0 && lastFence > firstNewline)
                {
                    text = text[(firstNewline + 1)..lastFence].Trim();
                }
            }

            using var doc = JsonDocument.Parse(text);
            var root = doc.RootElement;
            var indices = root.TryGetProperty("matched_indices", out var idxEl)
                ? idxEl.EnumerateArray().Select(e => e.GetInt32()).ToList()
                : [];
            var reason = root.TryGetProperty("reason", out var reasonEl) ? reasonEl.GetString() ?? string.Empty : string.Empty;

            var matchedIds = indices
                .Where(i => i >= 1 && i <= candidates.Count)
                .Select(i => candidates[i - 1].StepId)
                .Distinct()
                .ToList();

            return new MatchResult { MatchedStepIds = matchedIds, Reason = reason };
        }
        catch (Exception) when (true)
        {
            // Parsing/agent failure: fail open to today's behavior (attach to every candidate on the page)
            // rather than silently dropping the image.
            return new MatchResult { MatchedStepIds = candidates.Select(c => c.StepId).ToList(), Reason = "Agent call failed; kept page-level association." };
        }
    }

    private static string Truncate(string text, int maxLength)
        => text.Length <= maxLength ? text : text[..maxLength] + "...";
}
