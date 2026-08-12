using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Configuration;
using OpenAI.Chat;

namespace HumanOS.Agents.Studio;

/// <summary>The single reading card produced by <see cref="LessonCardAgent"/>
/// for one lesson/section — pure content, no graded interaction (unlike
/// Graph mode's ExampleCard, which the student must "examine"/answer about
/// before advancing).</summary>
public sealed class LessonCardDto
{
    public string Title { get; set; } = string.Empty;

    /// <summary>The card's real teaching content — a real explanation
    /// with concrete detail, not a thin summary. May include simple HTML
    /// (e.g. &lt;b&gt;, &lt;ul&gt;/&lt;li&gt;) for basic formatting.</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>A text prompt for generating this card's one required
    /// illustration — always non-null/non-empty, grounded 100% in the
    /// card's own text (never an invented scene).</summary>
    public string? IllustrationPrompt { get; set; }
}

/// <summary>Always exactly ONE card per lesson/section — one lesson call
/// (<see cref="LessonCardAgent.DesignAsync"/>) already corresponds to one
/// section of the source document (e.g. "1.1 Purpose of the Policy"), so
/// there is never a reason to split it further. <see cref="Cards"/> keeps
/// the list shape for schema stability, but callers should treat index 0
/// as the card (<see cref="DesignAsync"/> merges any extras defensively).</summary>
public sealed class LessonCardsResponse
{
    public List<LessonCardDto> Cards { get; set; } = [];
}

/// <summary>
/// Agente de lectura (Traditional-course pipeline, 2026-08-09): turns ONE
/// lesson's (subchapter's) curated source content into exactly ONE reading
/// card — the traditional-course equivalent of Graph mode's Teaching +
/// DeepDive steps combined, but deliberately WITHOUT any Recall/Production/
/// "examine per card" interaction (per the "Coursera-style" course design:
/// read cards, then take ONE final multiple-choice exam per chapter). One
/// section of the source document = one lesson = one card; never split a
/// single section across multiple cards. Acts as a warm teacher READING/
/// TEACHING the source material to the student — never a generic rewrite/
/// summary of it; every fact, example, number and detail in the curated
/// material must survive in the card, in the source's own tone.
///
/// Plain ChatClientAgent with structured output — same configuration
/// pattern as <see cref="CuradorAgent"/>.
/// </summary>
public sealed class LessonCardAgent
{
    private const string Instructions = """
        You are the professor teaching ONE section of a document directly to
        the students in a traditional, Coursera-style course. Convert this
        one source section into exactly ONE reading card.

        HARD CONTRACT — SOURCE-FIRST, PROFESSOR VOICE, NO INVENTION
        - Every idea in the card must be grounded in the source text. Do not
          invent facts, examples, emotions, scenarios, or conclusions that the
          source does not state.
        - Teach in the voice of a professor speaking to one or more students:
          direct, warm, clear, instructional, and natural. Start as if you are
          teaching the class, not narrating a document.
        - Keep the wording as close to the source as possible. This is not a
          loose summary or generic explanation. Preserve the source's structure,
          tone, key phrases, examples, names, rules, and numbers whenever they
          exist.
        - If the source is personal, warm, or welcoming, keep that quality.
          If it is formal, keep that register. Match the source's voice, not a
          neutralized corporate tone.
        - You may add very light, spoken clarification only when needed to
          make the source's meaning easier to follow. Do not add new facts.

        REQUIRED CARD SHAPE
        - Title = the exact lesson number/title provided in the prompt.
        - Text = the full teaching content for this section, written as a
          professor speaking to students, staying as close to the source as
          possible while preserving natural instructional flow.
        - At the end of the Text, add a separate description of the image in
          the following format, with a single horizontal line between the main
          teaching text and the image description:

          ---
          Image description: [plain-language description of the illustration
          that matches the lesson content exactly]

          The line above must be exactly "---" on its own line. There must be
          one blank line before and after that separator line. Do not add extra
          commentary before or after it.

        ILLUSTRATION PROMPT RULES
        - IllustrationPrompt is REQUIRED for every card.
        - It must depict the same thing the lesson text is teaching and the
          image description describes.
        - It must be concrete, grounded in the source, and match the same
          scene or concept described in the text.
        - Never invent a scene, object, or action that is not clearly implied
          by the source.
        - Do not generate a decorative logo, brand mark, letterform, or
          abstract icon unless the source itself is specifically about a logo.
          For onboarding, safety, or process lessons, illustrate the actual
          process or idea being taught, not a logo.

        CARD COUNT RULES
        - This lesson is exactly one section, so you must return exactly ONE
          card in Cards.
        - Never split a lesson into multiple cards.

        LANGUAGE RULES
        - Write everything in the required output language provided in the
          prompt.
        - Preserve the source's tone and coverage in that language.
        """;

    private readonly AIAgent? _agent;
    private readonly string _modelName;

    public LessonCardAgent(IConfiguration configuration)
    {
        var endpoint = configuration["AzureOpenAIEndpoint"];
        var deploymentName = configuration["AzureOpenAIDeploymentName"];
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
            .AsAIAgent(instructions: Instructions, name: "LessonCardAgent");
    }

    public bool IsConfigured => _agent is not null;

    public sealed class LessonCardsResult
    {
        public LessonCardsResponse Response { get; set; } = null!;

        public AgentTokenUsage TokenUsage { get; set; } = null!;
    }

    public static string EnsureImageDescriptionSeparator(string text, string? illustrationPrompt)
    {
        var normalizedText = text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedText))
        {
            return string.IsNullOrWhiteSpace(illustrationPrompt)
                ? string.Empty
                : $"Image description: {illustrationPrompt.Trim()}";
        }

        if (normalizedText.Contains("Image description:", StringComparison.OrdinalIgnoreCase))
        {
            return normalizedText;
        }

        var description = string.IsNullOrWhiteSpace(illustrationPrompt)
            ? "Image description: A visual that matches the lesson content and reinforces the same idea."
            : $"Image description: {illustrationPrompt.Trim()}";

        return $"{normalizedText}\n\n---\n\n{description}";
    }

    public static string NormalizeIllustrationPrompt(string? illustrationPrompt, string lessonText)
    {
        var prompt = (illustrationPrompt ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(prompt))
        {
            return "A simple illustration of the lesson's key teaching idea and process.";
        }

        var lower = prompt.ToLowerInvariant();
        var isLogoDrift = lower.Contains("logo") || lower.Contains("brand") || lower.Contains("decorative") ||
            lower.Contains("lettermark") || lower.Contains("serif") || lower.Contains("script") ||
            lower.Contains("gtx") || lower.Contains("capital letter") || lower.Contains("square logo");

        if (!isLogoDrift)
        {
            return prompt;
        }

        var clueParts = new List<string>();
        if (lessonText.Contains("application", StringComparison.OrdinalIgnoreCase))
        {
            clueParts.Add("volunteer application");
        }

        if (lessonText.Contains("background check", StringComparison.OrdinalIgnoreCase))
        {
            clueParts.Add("background check");
        }

        if (lessonText.Contains("orientation", StringComparison.OrdinalIgnoreCase))
        {
            clueParts.Add("friendly orientation");
        }

        if (lessonText.Contains("policy", StringComparison.OrdinalIgnoreCase))
        {
            clueParts.Add("policy review");
        }

        if (lessonText.Contains("agreement", StringComparison.OrdinalIgnoreCase))
        {
            clueParts.Add("agreement signing");
        }

        if (clueParts.Count == 0)
        {
            return "A simple illustration of the lesson's key teaching idea and process.";
        }

        return $"A simple illustration of the volunteer onboarding process: {string.Join(", ", clueParts)}, emphasizing safety, care, and a welcoming environment.";
    }

    public async Task<LessonCardsResult> DesignAsync(
        int chapterNumber,
        string chapterTitle,
        int lessonNumber,
        string lessonTitle,
        string curatedContent,
        string? webFindings,
        string languageCode,
        CancellationToken cancellationToken = default)
    {
        if (_agent is null)
        {
            throw new InvalidOperationException(
                "The LessonCard agent is not configured. Set the 'AzureOpenAIEndpoint' and " +
                "'AzureOpenAIDeploymentName' application settings.");
        }

        var targetLanguageName = languageCode.Trim().ToLowerInvariant() == "en" ? "English" : "Spanish";
        var prompt =
            $"Chapter {chapterNumber}: {chapterTitle}\n" +
            $"Lesson {lessonNumber}: {lessonTitle}\n" +
            $"Required output language: {targetLanguageName}\n\n" +
            $"This lesson is exactly ONE section of the source document — produce " +
            $"exactly ONE card. Title must be \"{lessonTitle}\" (the exact lesson " +
            $"number/title above). Ensure the final card text ends with an image description in the required format.\n\n" +
            $"Illustration rule: do not generate a decorative logo, brand mark, abstract letterform, or any icon unrelated to the actual lesson content. For onboarding or process lessons, depict the concrete steps being taught (for example: application, background check, orientation, policy review, agreement).\n\n" +
            $"Curated source material for this lesson:\n{curatedContent}" +
            (string.IsNullOrWhiteSpace(webFindings) ? string.Empty : $"\n\nSupplementary current web findings:\n{webFindings}");

        var response = await _agent.RunAsync<LessonCardsResponse>(prompt, cancellationToken: cancellationToken);

        var usage = response.Usage;
        var tokenUsage = new AgentTokenUsage
        {
            AgentName = "LessonCard",
            ModelName = _modelName,
            InputTokens = (int)(usage?.InputTokenCount ?? 0),
            OutputTokens = (int)(usage?.OutputTokenCount ?? 0),
            CachedInputTokens = (int)(usage?.CachedInputTokenCount ?? 0)
        };

        // Defensive: this lesson is always exactly one section, so collapse
        // into a single card even if the model still returned more than one.
        var result = response.Result;
        if (result.Cards.Count > 1)
        {
            var first = result.Cards[0];
            var mergedText = string.Join("\n\n", result.Cards.Select(c => c.Text));
            var mergedIllustration = result.Cards.Select(c => c.IllustrationPrompt)
                .FirstOrDefault(p => !string.IsNullOrWhiteSpace(p));
            var normalizedIllustration = NormalizeIllustrationPrompt(mergedIllustration, curatedContent);
            result.Cards = [new LessonCardDto { Title = first.Title, Text = EnsureImageDescriptionSeparator(mergedText, normalizedIllustration), IllustrationPrompt = normalizedIllustration }];
        }
        else if (result.Cards.Count == 1)
        {
            var card = result.Cards[0];
            card.IllustrationPrompt = NormalizeIllustrationPrompt(card.IllustrationPrompt, curatedContent);
            card.Text = EnsureImageDescriptionSeparator(card.Text, card.IllustrationPrompt);
        }

        return new LessonCardsResult { Response = result, TokenUsage = tokenUsage };
    }
}
