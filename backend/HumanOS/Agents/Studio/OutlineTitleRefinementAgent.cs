using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Configuration;
using OpenAI.Chat;

namespace HumanOS.Agents.Studio;

/// <summary>One chapter's worth of raw, mechanically-extracted titles to
/// be refined — see <see cref="OutlineTitleRefinementAgent"/>.</summary>
public sealed class OutlineTitleRefinementChapterInput
{
    /// <summary>Position in the caller's chapter list — echoed back
    /// unchanged so the caller can merge refined titles back by index
    /// without relying on title text matching.</summary>
    public int Index { get; set; }

    public string OriginalTitle { get; set; } = string.Empty;

    public List<string> OriginalNodeTitles { get; set; } = [];
}

public sealed class OutlineTitleRefinementRequest
{
    /// <summary>DocumentType name (e.g. "Law"/"Procedure"/"AcademicWithIndex")
    /// — gives the agent the right frame of reference for what a "good"
    /// title looks like for this kind of document.</summary>
    public string DocumentType { get; set; } = string.Empty;

    /// <summary>Optional free-text instruction the designer typed (e.g.
    /// "resume el contenido real del artículo, no solo acortes la frase",
    /// "usa mayúsculas solo en la primera palabra") — the agent must follow
    /// it within the same hard constraints (exact count preserved, no
    /// invented facts). Null/empty means "use your best judgment" (the
    /// default behavior described in Instructions).</summary>
    public string? CustomInstruction { get; set; }

    public List<OutlineTitleRefinementChapterInput> Chapters { get; set; } = [];
}

public sealed class OutlineTitleRefinementChapterOutput
{
    /// <summary>Must equal the matching input's Index exactly — used to
    /// merge back by index rather than trusting title-text matching.</summary>
    public int Index { get; set; }

    public string RefinedTitle { get; set; } = string.Empty;

    /// <summary>MUST have exactly the same length, in the same order, as
    /// the matching input's OriginalNodeTitles.</summary>
    public List<string> RefinedNodeTitles { get; set; } = [];
}

public sealed class OutlineTitleRefinementResponse
{
    public List<OutlineTitleRefinementChapterOutput> Chapters { get; set; } = [];
}

/// <summary>
/// Opt-in "Mejorar títulos con IA" agent (2026-08-17) — takes the raw,
/// mechanically-truncated chapter/node titles ANY detection tier produced
/// (Tier 0 SyllabusOutlineParser, Tier 0.5 DocumentTypeOutlineParser, Tier 1
/// TocExtractionAgent, Tier 2 InferOutlineAsync — this agent is agnostic to
/// which one ran) and proposes a short, human-readable title for each,
/// in ONE single batched call regardless of how many chapters/nodes exist
/// — never one call per node, to keep this genuinely cheap even for a
/// 200-article law. Never touches StartMarker/content — purely cosmetic,
/// same "advisory only, the designer stays in control" discipline as the
/// Document Type Detection Engine (see DocumentClassificationEngine).
/// </summary>
public sealed class OutlineTitleRefinementAgent
{
    /// <summary>Phrases that indicate the model gave up and returned a
    /// generic placeholder instead of real, condensed content (2026-08-17
    /// bug fix) — any refined title/node title matching one of these is
    /// rejected and the ORIGINAL raw title is kept for that entry instead
    /// of silently accepting a useless placeholder.</summary>
    private static readonly string[] ForbiddenGenericPhrases =
    [
        "contenido no especificado", "unspecified content", "sin título",
        "título pendiente", "no title", "not specified", "no especificado",
        "contenido pendiente", "pending content"
    ];

    private const string Instructions = """
        You clean up mechanically-extracted section titles from a source
        document so a human designer can scan them quickly, WITHOUT reading
        the original document again.

        You receive, for a given DocumentType (e.g. Law, Contract, Procedure,
        AcademicWithIndex, AcademicNarrative, ResearchPaper, Policy), a list
        of chapters, each with:
        - OriginalTitle: the CURRENT title text for that chapter — this is
          ALL the text you have for it, whether it's a raw mechanical
          heading (e.g. "CAPITULO I Disposiciones generales") or already a
          shorter title from a PREVIOUS refinement round (e.g. "Artículo
          1o -"). Treat it as the complete, only input — there is no
          longer/original version hidden elsewhere for you to recall or
          reconstruct from your own training knowledge of similar
          documents. Transform EXACTLY this string, never a version you
          imagine might have existed before it was already shortened.
        - OriginalNodeTitles: same rule, per node under that chapter (e.g.
          "Artículo 1o.- Están obligadas al pago del impuesto al valor
          agregado establecido", or already-shortened text from a previous
          round).

        For EACH chapter and EACH node title, propose a short (3-10 words),
        clear, human-readable label that captures what that section is
        actually about, using ONLY the wording/content already implied by
        the raw title itself — never invent new facts, numbers, or legal
        content that isn't already present in the raw title text. Keep any
        existing numbering/label prefix that identifies the item (e.g. keep
        "Artículo 1o.-A" or "CAPITULO I" as a prefix) and add a short
        descriptive phrase after it — do not just repeat the raw
        truncated sentence, and do not strip the identifying prefix.

        NEVER output a generic/placeholder phrase instead of real content —
        e.g. "Contenido no especificado"/"Unspecified content"/"Sin
        título"/"Título pendiente" are all FORBIDDEN. Every OriginalTitle/
        OriginalNodeTitle you receive already contains real, specific
        wording — your job is to condense/clean THAT wording, never to
        replace it with a vague filler phrase because it looked hard to
        summarize. If you are ever unsure how to shorten a title well,
        prefer returning it closer to its original wording over inventing
        a generic placeholder.

        Preserve the exact same number of chapters and, within each
        chapter, the exact same number of node titles as you received —
        never add, remove, split, or merge any entry. Echo each chapter's
        Index field back unchanged so the caller can merge your titles back
        by position.

        If a CustomInstruction is provided, follow it as closely as
        possible for HOW you phrase every title (e.g. "summarize the real
        content, don't just shorten the sentence", "keep them under 6
        words", "write them as questions") — but NEVER let it override the
        hard constraints above (exact chapter/node count, no invented
        facts, keep the identifying prefix).
        """;

    private readonly AIAgent? _agent;

    public OutlineTitleRefinementAgent(IConfiguration configuration)
    {
        var endpoint = configuration["AzureOpenAIEndpoint"];
        // Economy tier: rewriting a short title is mechanical enough for a
        // cheaper model — same tier choice as TocExtractionAgent.ExtractAsync.
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
            .AsAIAgent(instructions: Instructions, name: "OutlineTitleRefinementAgent");
    }

    public bool IsConfigured => _agent is not null;

    public async Task<OutlineTitleRefinementResponse> RefineAsync(
        OutlineTitleRefinementRequest request, CancellationToken cancellationToken = default)
    {
        if (_agent is null)
        {
            throw new InvalidOperationException(
                "OutlineTitleRefinementAgent is not configured. Set the 'AzureOpenAIEndpoint' and " +
                "'AzureOpenAIDeploymentName'/'AzureOpenAIEconomyDeploymentName' application settings.");
        }

        var prompt = $"DocumentType: {request.DocumentType}\n" +
            (string.IsNullOrWhiteSpace(request.CustomInstruction) ? string.Empty : $"CustomInstruction: {request.CustomInstruction}\n") +
            "\n" +
            System.Text.Json.JsonSerializer.Serialize(request.Chapters);

        var response = await _agent.RunAsync<OutlineTitleRefinementResponse>(prompt, cancellationToken: cancellationToken);

        // Structural validation only (no content-quality LLM eval needed for
        // a purely cosmetic rewrite) — a count mismatch means the model
        // dropped/merged an entry, which the caller must never accept
        // silently since it would desync Index-based merging.
        if (response.Result.Chapters.Count != request.Chapters.Count)
        {
            throw new InvalidOperationException(
                $"OutlineTitleRefinementAgent returned {response.Result.Chapters.Count} chapters, expected {request.Chapters.Count}.");
        }

        foreach (var chapter in response.Result.Chapters)
        {
            var original = request.Chapters.FirstOrDefault(c => c.Index == chapter.Index)
                ?? throw new InvalidOperationException($"OutlineTitleRefinementAgent returned an unknown chapter Index {chapter.Index}.");
            if (chapter.RefinedNodeTitles.Count != original.OriginalNodeTitles.Count)
            {
                throw new InvalidOperationException(
                    $"OutlineTitleRefinementAgent returned {chapter.RefinedNodeTitles.Count} node titles for chapter {chapter.Index}, expected {original.OriginalNodeTitles.Count}.");
            }

            // Reject any generic placeholder the model returned instead of
            // real condensed content — fall back to the original raw title
            // for that ONE entry rather than discarding the whole batch.
            if (IsGenericPlaceholder(chapter.RefinedTitle))
            {
                chapter.RefinedTitle = original.OriginalTitle;
            }
            for (var i = 0; i < chapter.RefinedNodeTitles.Count; i++)
            {
                if (IsGenericPlaceholder(chapter.RefinedNodeTitles[i]))
                {
                    chapter.RefinedNodeTitles[i] = original.OriginalNodeTitles[i];
                }
            }
        }

        return response.Result;
    }

    private static bool IsGenericPlaceholder(string title) =>
        string.IsNullOrWhiteSpace(title) ||
        ForbiddenGenericPhrases.Any(phrase => title.Contains(phrase, StringComparison.OrdinalIgnoreCase));
}
