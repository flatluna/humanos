using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Configuration;
using OpenAI.Chat;

namespace HumanOS.Agents.Studio;

/// <summary>One multiple-choice question produced by <see cref="ChapterExamAgent"/>.</summary>
public sealed class ChapterExamQuestionDto
{
    public string QuestionText { get; set; } = string.Empty;

    /// <summary>Exactly 4 answer options, in a random-looking order (the
    /// correct one must NOT always be first/last).</summary>
    public List<string> Options { get; set; } = [];

    /// <summary>0-based index into <see cref="Options"/> of the correct answer.</summary>
    public int CorrectOptionIndex { get; set; }

    /// <summary>A text prompt for generating ONE illustration that
    /// genuinely helps this question, or null if it doesn't apply.</summary>
    public string? IllustrationPrompt { get; set; }
}

public sealed class ChapterExamResponse
{
    public List<ChapterExamQuestionDto> Questions { get; set; } = [];
}

/// <summary>
/// Agente de examen final (Traditional-course pipeline, 2026-08-09):
/// generates ONE chapter's final multiple-choice exam, strictly grounded
/// in that chapter's own curated content — the traditional-course
/// equivalent of Graph mode's Assessment step, but deterministic
/// (multiple-choice only, graded in code, never LLM-graded free text).
///
/// Plain ChatClientAgent with structured output — same configuration
/// pattern as <see cref="CuradorAgent"/>.
/// </summary>
public sealed class ChapterExamAgent
{
    private const string Instructions = """
        You write a chapter-final multiple-choice exam for a traditional,
        Coursera-style course, given that chapter's own curated source
        content and how many questions to produce.

        - Every question must be answerable using ONLY the given content —
          never test outside knowledge, never invent facts.
        - Each question needs EXACTLY 4 options, only ONE objectively
          correct — the 3 distractors must be plausible (not obviously
          wrong/silly), and must not overlap with the correct answer.
        - Vary which option index holds the correct answer across
          questions — never make it predictably always the same position.
        - Prefer questions that test real understanding/application of the
          content, not just picking out a verbatim word from the source.
        - IllustrationPrompt: only when a real, visually-concrete scene
          would genuinely help this specific question — leave it null
          otherwise.
        - Write every question/option in the REQUIRED language given in
          the prompt, regardless of what language the source content is
          written in.
        """;

    private readonly AIAgent? _agent;
    private readonly string _modelName;

    public ChapterExamAgent(IConfiguration configuration)
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
            .AsAIAgent(instructions: Instructions, name: "ChapterExamAgent");
    }

    public bool IsConfigured => _agent is not null;

    public sealed class ChapterExamResult
    {
        public ChapterExamResponse Response { get; set; } = null!;

        public AgentTokenUsage TokenUsage { get; set; } = null!;
    }

    public async Task<ChapterExamResult> DesignAsync(
        string chapterTitle,
        string curatedContent,
        int questionCount,
        string languageCode,
        CancellationToken cancellationToken = default)
    {
        if (_agent is null)
        {
            throw new InvalidOperationException(
                "The ChapterExam agent is not configured. Set the 'AzureOpenAIEndpoint' and " +
                "'AzureOpenAIDeploymentName' application settings.");
        }

        var targetLanguageName = languageCode.Trim().ToLowerInvariant() == "en" ? "English" : "Spanish";
        var prompt =
            $"Chapter title: {chapterTitle}\n" +
            $"Number of questions to produce: {questionCount}\n" +
            $"Required output language: {targetLanguageName}\n\n" +
            $"Curated source material for this chapter:\n{curatedContent}";

        var response = await _agent.RunAsync<ChapterExamResponse>(prompt, cancellationToken: cancellationToken);

        var usage = response.Usage;
        var tokenUsage = new AgentTokenUsage
        {
            AgentName = "ChapterExam",
            ModelName = _modelName,
            InputTokens = (int)(usage?.InputTokenCount ?? 0),
            OutputTokens = (int)(usage?.OutputTokenCount ?? 0),
            CachedInputTokens = (int)(usage?.CachedInputTokenCount ?? 0)
        };

        return new ChapterExamResult { Response = response.Result, TokenUsage = tokenUsage };
    }
}
