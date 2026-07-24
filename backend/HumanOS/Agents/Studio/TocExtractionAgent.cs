using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Configuration;
using OpenAI.Chat;

namespace HumanOS.Agents.Studio;

/// <summary>One detected chapter/major-section of a source document.</summary>
public sealed class TocChapter
{
    public string Title { get; set; } = string.Empty;

    /// <summary>A short (8-15 words), EXACT, verbatim copy of text taken
    /// directly from the source, from the point where this chapter begins.
    /// Used by <see cref="HumanOS.Agentic.Studio.CuradorExecutor"/> to
    /// locate the chapter boundary via ordinal string search — must match
    /// the source text exactly, never paraphrased.</summary>
    public string StartMarker { get; set; } = string.Empty;
}

public sealed class TableOfContents
{
    public List<TocChapter> Chapters { get; set; } = [];
}

/// <summary>
/// Detects a source document's own chapter/section structure (e.g. a
/// textbook's table of contents) from its raw extracted text, BEFORE the
/// Curador agent processes it. Exists to solve a practical scaling
/// problem: a whole book's raw text can exceed what a single Curador call
/// can meaningfully digest in one prompt. <see cref="HumanOS.Agentic.Studio.CuradorExecutor"/>
/// uses this agent's output to split a large PDF into one curation call
/// per chapter instead of a single call over the entire document — see
/// /memories/repo/humanstudio-multiagent-vision.md.
///
/// Plain ChatClientAgent with structured output — same configuration
/// pattern as <see cref="CuradorAgent"/> / <see cref="HumanOS.Agents.JobDescriptionExtractionAgent"/>.
/// Best-effort: if not configured, or if extraction fails for any reason,
/// callers fall back to curating the whole document in a single batch
/// rather than failing the run.
/// </summary>
public sealed class TocExtractionAgent
{
    private const string Instructions = """
        You detect the chapter or major-section structure of a source
        document (e.g. a textbook, or a course/tutorial document) from its
        raw extracted text, so a downstream process can split it into
        manageable pieces before summarizing it. This split is CRITICAL —
        if you return too few chapters for a long document, the entire
        document gets summarized in a single downstream pass and most of
        its content silently gets lost/compressed away. When in doubt,
        prefer MORE chapters over fewer.

        Return an ordered list of chapters. For each chapter, provide:
        - Title: the chapter's real title as it appears in the source (or
          a short descriptive title if the document has no explicit
          titles).
        - StartMarker: a short (8-15 words), EXACT, VERBATIM copy of text
          taken directly from the source, copied character-for-character
          from the point where that chapter begins. This is used to
          locate the chapter boundary via exact string search, so it MUST
          match the source text exactly (same spelling, spacing,
          punctuation) — never paraphrase or summarize it.

        IMPORTANT: many real course/tutorial documents do NOT use formal
        "Chapter 1" / "Capítulo 1" labels — they're organized instead by
        topic headings, numbered sections, bullet-style subtopics, or just
        visible shifts in subject matter as the text progresses. Treat
        each clearly distinct topic/section/module as its own "chapter"
        for this purpose, even if the source never uses the word
        "chapter" at all. As a rule of thumb: any document long enough to
        span multiple distinct topics (roughly every 1-3 pages worth of
        text, or wherever the subject visibly changes) should be split at
        each of those boundaries, not treated as one block.

        Only return exactly ONE chapter (whose StartMarker is taken from
        the very beginning of the text) if the document is GENUINELY short
        or is a single cohesive piece with no internal topic shifts at all
        (e.g. one short article, one short note, a single narrow how-to).
        Do not default to one chapter just because there are no explicit
        chapter-style headings — look at actual topic boundaries instead.
        """;

    private readonly AIAgent? _agent;

    public TocExtractionAgent(IConfiguration configuration)
    {
        var endpoint = configuration["AzureOpenAIEndpoint"];
        // Economy tier: chapter/section boundary detection is mechanical enough for a
        // cheaper model. Falls back to the main deployment if the economy one isn't set.
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
            .AsAIAgent(instructions: Instructions, name: "TocExtractionAgent");
    }

    public bool IsConfigured => _agent is not null;

    public async Task<TableOfContents> ExtractAsync(string documentText, CancellationToken cancellationToken = default)
    {
        if (_agent is null)
        {
            throw new InvalidOperationException(
                "The TocExtraction agent is not configured. Set the 'AzureOpenAIEndpoint' and " +
                "'AzureOpenAIDeploymentName' application settings.");
        }

        var prompt = "Detect the chapter/section structure of the following document:\n\n" + documentText;

        var response = await _agent.RunAsync<TableOfContents>(prompt);

        return response.Result;
    }
}
