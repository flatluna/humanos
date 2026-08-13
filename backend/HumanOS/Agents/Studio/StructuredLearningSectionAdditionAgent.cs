using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Configuration;
using OpenAI.Chat;

namespace HumanOS.Agents.Studio;

/// <summary>Structured output: a brand-new section to insert into an
/// existing chapter of a Structured Learning document.</summary>
public sealed class StructuredLearningSectionAdditionResponse
{
    /// <summary>e.g. "3.4 Handling Difficult Situations" — follows the
    /// SAME numbering convention as the chapter's other existing
    /// subchapters (given below), placed logically after them.</summary>
    public string SubchapterTitle { get; set; } = string.Empty;

    /// <summary>One sentence naming the capability this engram belongs to.
    /// Use the chapter title as the schema/category bucket, and define the
    /// concrete capability the learner is developing in this section.</summary>
    public string Capability { get; set; } = string.Empty;

    /// <summary>Prose covering the topic, matching the SCOPE/LENGTH the
    /// professor's own instruction implies (short instruction -> short
    /// result) — plays the role a real excerpt of the source document
    /// would otherwise play (this document has no such excerpt for a
    /// professor-added section, so it must be originated here).</summary>
    public string Text { get; set; } = string.Empty;
}

/// <summary>
/// StructuredLearningSectionAdditionAgent (2026-08-11) — Structured
/// Learning's "Edit mode" add-section feature, mirrors Graph's
/// <see cref="GraphNodeAdditionAgent"/> in spirit but adapted to this
/// pipeline's own "verbatim source text" model: since there is no real
/// document excerpt for a professor-added section, this agent must
/// ORIGINATE one (same deliberate exception to the "never invent"
/// discipline already established by <see cref="IdeaToDocumentAgent"/>
/// for the no-source-document case). Its output is then treated exactly
/// like a real extracted section and flows through the SAME
/// StructuredLearningSectionDesignerAgent pipeline unchanged — see
/// Services/StructuredLearningSectionAdditionService.cs.
/// </summary>
public sealed class StructuredLearningSectionAdditionAgent
{
    private const string Instructions = """
        You are a subject-matter expert helping a professor insert ONE new section into an
        EXISTING chapter of a document. There is no real source excerpt for this new section —
        you must WRITE content covering the topic, drawing on your own expert knowledge, exactly
        as if it had been extracted verbatim from a real document.

        You are given: the document's name, the chapter title this section belongs to, the
        titles of every OTHER subchapter already in that chapter (in order), and the professor's
        free-text instruction describing what this new section should cover.

        Your job:
        1. Capability: infer the core capability this section is creating for the learner.
           Use the chapter title as the schema/category bucket and define the actual capability
           embodied by this engram in one sentence, in the format:
           "The [role/person] can [concrete action/skill], [supporting clause]."
           Example: "The volunteer can encourage a participant, help them remain engaged, and
           support their inclusion without taking over."
           This capability must match the content and chapter being created.
        2. SubchapterTitle: follow the EXACT numbering convention already used by the other
           subchapters in this chapter (e.g. if they are "3.1 ...", "3.2 ...", "3.3 ...", this
           new one continues as "3.4 ..."). Place it logically — usually appended after the
           existing ones, unless the instruction clearly asks for something that belongs
           earlier in the sequence (still pick the next unused number in that case; ordering in
           the document is handled separately from numbering).
        3. Text — LENGTH AND SCOPE DISCIPLINE (read this carefully, it is the #1 rule):
           Match the SCOPE and LENGTH the professor's own instruction implies — do not
           default to an exhaustive, textbook-length treatise. A short, specific instruction
           (one sentence, a short statement, a simple value/policy line) must produce a SHORT
           result — typically one short paragraph, not several. Only write multiple paragraphs
           when the professor's instruction itself is long, detailed, or explicitly asks for
           thorough/comprehensive coverage. NEVER invent additional sub-categories, exhaustive
           taxonomies, legal-style enumerations, or extra examples beyond what the instruction
           actually implies — e.g. if asked for "a caring statement about zero tolerance for
           abuse", do not go on to enumerate physical/emotional/sexual/financial abuse,
           neglect, digital-conduct rules, and reporting procedures unless the instruction
           actually asked for that level of detail.
           If the professor's own instruction already contains specific, well-crafted wording
           (a value statement, a stance, a memorable phrase), PRESERVE that wording closely as
           the heart of the section — you may lightly weave in a sentence of context/rationale
           around it, but never replace it with a longer, more clinical rewrite.
        3. VOICE: write the way a warm, caring instructor/mentor speaks to their students, never
           like a legal or compliance policy document. Personal, direct, warm register — never
           a dry enumeration of rules/categories/definitions. Match the tone/register of a real
           textbook section ONLY in the sense of being well-written prose; never in the sense of
           being long or exhaustive by default. Grounded in your own real knowledge of the
           subject, factually accurate, never inventing pseudo-facts. Do not reference "the
           professor asked for..." or any meta-commentary — write as if it were the document's
           own original text.
        4. Never duplicate the knowledge already covered by the chapter's OTHER subchapters
           (given to you) — this must be a genuinely new, distinct topic within the chapter.
        5. LANGUAGE: match the language the OTHER subchapter titles/document are written in.
        """;

    private readonly AIAgent? _agent;

    public StructuredLearningSectionAdditionAgent(IConfiguration configuration)
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
            .AsAIAgent(instructions: Instructions, name: "StructuredLearningSectionAdditionAgent");
    }

    public bool IsConfigured => _agent is not null;

    public async Task<StructuredLearningSectionAdditionResponse> GenerateAsync(
        string documentName,
        string chapterTitle,
        IReadOnlyList<string> existingSubchapterTitles,
        string instruction,
        string? pedagogicalAudience,
        CancellationToken cancellationToken)
    {
        if (_agent is null)
        {
            throw new InvalidOperationException("StructuredLearningSectionAdditionAgent is not configured.");
        }

        var prompt = $"""
            Document: {documentName}
            Chapter: {chapterTitle}
            Existing subchapters in this chapter (in order):
            {string.Join("\n", existingSubchapterTitles.Select(t => $"- {t}"))}
            {(string.IsNullOrWhiteSpace(pedagogicalAudience) ? "" : $"Intended audience: {pedagogicalAudience}\n")}
            Professor's instruction for the new section: {instruction}
            """;

        var response = await _agent.RunAsync<StructuredLearningSectionAdditionResponse>(prompt, cancellationToken: cancellationToken);
        return response.Result;
    }
}
