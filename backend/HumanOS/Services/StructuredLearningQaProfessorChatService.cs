using System.Text;
using System.Text.Json;
using HumanOS.Agents.Studio;
using HumanOS.Data;
using HumanOS.Models.Capabilities.Graph;
using HumanOS.Models.Capabilities.Studio;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.Services;

public sealed class StructuredLearningQaChatHistoryEntry
{
    /// <summary>"Student" or "Professor".</summary>
    public string Role { get; set; } = string.Empty;

    public string Text { get; set; } = string.Empty;
}

/// <summary>
/// Orchestrates one turn of the text "Q&amp;A with Professor" tab (2026-10-01)
/// — the cheap/text sibling of VoiceTutorSessionFunction's Q&amp;A mode.
/// Grounds the Agent in the EXACT SAME context the voice professor gets for
/// this section: the section's own Teaching content plus the full
/// LEARNER CONTEXT (role/course + the student's own Hypothesis answer),
/// via the shared <see cref="ILearnerContextBuilder"/> — so switching
/// between the Voice and Text tabs never changes what the professor knows.
/// Stateless — the caller resends the running conversation each turn.
/// </summary>
public sealed class StructuredLearningQaProfessorChatService
{
    private readonly HumanOsDbContext _dbContext;
    private readonly ILearnerContextBuilder _learnerContextBuilder;
    private readonly StructuredLearningQaProfessorAgent _agent;
    private readonly StructuredLearningEngramUsageService _usageService;

    public StructuredLearningQaProfessorChatService(
        HumanOsDbContext dbContext,
        ILearnerContextBuilder learnerContextBuilder,
        StructuredLearningQaProfessorAgent agent,
        StructuredLearningEngramUsageService usageService)
    {
        _dbContext = dbContext;
        _learnerContextBuilder = learnerContextBuilder;
        _agent = agent;
        _usageService = usageService;
    }

    public bool IsConfigured => _agent.IsConfigured;

    public async Task<string> AskAsync(
        Guid structuredLearningSectionStepId,
        Guid? personId,
        string message,
        IReadOnlyList<StructuredLearningQaChatHistoryEntry> history,
        CancellationToken cancellationToken,
        int? exampleCardIndex = null,
        Guid? engramSessionId = null)
    {
        var step = await _dbContext.StructuredLearningSectionSteps
            .AsNoTracking()
            .Include(s => s.Section)
            .ThenInclude(sec => sec.Document)
            .FirstOrDefaultAsync(s => s.StructuredLearningSectionStepId == structuredLearningSectionStepId, cancellationToken);

        if (step is null || step.Section is null)
        {
            throw new KeyNotFoundException("No StructuredLearningSectionStep was found with that id.");
        }

        var courseId = step.Section.Document?.StructuredLearningDocumentId;
        var engramId = step.Section.StructuredLearningDocumentSectionId.ToString();

        var learnerContext = await _learnerContextBuilder.BuildLearnerContextBlockAsync(
            personId,
            courseId,
            engramId,
            cancellationToken,
            engramTitle: step.Section.SubchapterTitle);

        var card = ResolveExampleCard(step, exampleCardIndex);
        var prompt = BuildPrompt(step, card, learnerContext, history, message);
        var answer = await _agent.AnswerAsync(prompt, cancellationToken);

        await _usageService.RecordTextUsageAsync(
            personId,
            courseId,
            step.Section.StructuredLearningDocumentSectionId,
            engramSessionId,
            answer.ModelName,
            answer.InputTokens,
            answer.OutputTokens,
            cancellationToken,
            stepType: step.StepType.ToString());

        return answer.Text;
    }

    /// <summary>DeepDive cards only (2026-10-02) — mirrors
    /// VoiceTutorSessionFunction's reviewOnly card path: grounds the text
    /// Professor in ONE ExampleCard (Title/Text) instead of the whole
    /// step's Content, same as the Voice tab does for a DeepDive card.
    /// Returns null (falls back to full step.Content) if the index/JSON is
    /// missing or invalid.</summary>
    private static ExampleCard? ResolveExampleCard(StructuredLearningSectionStep step, int? exampleCardIndex)
    {
        if (exampleCardIndex is null || string.IsNullOrWhiteSpace(step.StepDataJson)) return null;
        try
        {
            var cards = JsonSerializer.Deserialize<List<ExampleCard>>(step.StepDataJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return cards is not null && exampleCardIndex >= 0 && exampleCardIndex < cards.Count ? cards[exampleCardIndex.Value] : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string BuildPrompt(
        StructuredLearningSectionStep step,
        ExampleCard? card,
        string? learnerContext,
        IReadOnlyList<StructuredLearningQaChatHistoryEntry> history,
        string message)
    {
        var sb = new StringBuilder();

        if (!string.IsNullOrWhiteSpace(learnerContext))
        {
            sb.AppendLine(learnerContext);
            sb.AppendLine();
        }

        sb.AppendLine($"=== LESSON ({step.Section!.ChapterTitle} -> {step.Section.SubchapterTitle}) ===");
        if (!string.IsNullOrWhiteSpace(step.Section.Document?.DocumentName))
        {
            sb.AppendLine($"Course: {step.Section.Document!.DocumentName}");
        }
        if (card is not null)
        {
            sb.AppendLine($"You are discussing ONE specific example card: \"{card.Title}\".");
            sb.AppendLine("CARD CONTENT (your only source of truth):");
            sb.AppendLine(card.Text);
        }
        else
        {
            sb.AppendLine("LESSON CONTENT (your only source of truth):");
            sb.AppendLine(step.Content);
        }
        sb.AppendLine();

        if (history.Count > 0)
        {
            sb.AppendLine("=== CONVERSATION SO FAR ===");
            foreach (var turn in history)
            {
                sb.AppendLine($"{turn.Role}: {turn.Text}");
            }
            sb.AppendLine();
        }

        sb.AppendLine("=== STUDENT'S NEW QUESTION ===");
        sb.AppendLine(message);

        return sb.ToString();
    }
}
