using System.Text;
using System.Text.Json;
using HumanOS.Agentic.Runtime;
using HumanOS.Agents.Studio;
using HumanOS.Data;
using HumanOS.Models.Capabilities.Studio;
using HumanOS.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;

namespace HumanOS.Services;

/// <summary>
/// Runtime — real-time Deep Dive engine for Structured Learning sections
/// (2026-08-10). Mirrors <see cref="TutorService"/>'s
/// <c>SubmitDeepDiveAttemptAsync</c> + <see cref="DeepDiveLoopGate"/>
/// pattern exactly (same "LLM proposes, code decides" discipline): the
/// student must master EVERY <see cref="StructuredLearningExampleCardDto"/>
/// in the section's Deep Dive pool, strictly in order — no attempt cap, no
/// regression path, just keep retrying the SAME card until cleared.
///
/// Keyed directly by StructuredLearningDocumentSectionId (no per-student
/// session model exists yet for Structured Learning — same reasoning as
/// <see cref="StructuredLearningRecallEngine"/>) — progress
/// (CurrentDeepDiveCardIndex/CurrentDeepDivePrompt) is persisted straight
/// on the <see cref="StructuredLearningSectionStep"/> row itself, exactly
/// like Graph's own <c>LearningSessionStep.CurrentDeepDiveCardIndex/
/// CurrentDeepDiveQuestion</c> — no separate attempts table needed (Graph's
/// own Deep Dive doesn't keep one either).
/// </summary>
public sealed class StructuredLearningDeepDiveEngine
{
    private readonly StructuredLearningDeepDiveAgent _agent;
    private readonly CapabilityGraphIllustrationStorageService _illustrationStorage;

    public StructuredLearningDeepDiveEngine(StructuredLearningDeepDiveAgent agent, CapabilityGraphIllustrationStorageService illustrationStorage)
    {
        _agent = agent;
        _illustrationStorage = illustrationStorage;
    }

    public sealed class DeepDiveTurnResult
    {
        public string Message { get; set; } = string.Empty;
        public int CardIndex { get; set; }
        public int TotalCards { get; set; }
        /// <summary>True if THIS attempt's score cleared mastery for the current card.</summary>
        public bool Mastered { get; set; }
        /// <summary>Index of the card to ask about next (unchanged if not yet mastered).</summary>
        public int NextCardIndex { get; set; }
        /// <summary>True once every card (0..TotalCards-1) has been mastered in order.</summary>
        public bool Complete { get; set; }
    }

    /// <summary>Bootstrap-safe read of the current Deep Dive state — never
    /// grades anything, safe to call on every page mount/reload (mirrors
    /// the GET .../recall/state fix for the same "always re-bootstrapping"
    /// bug). Returns the current card's own opening prompt (null — the
    /// frontend falls back to the card's Title/Text) the very first time,
    /// or whatever follow-up question the last graded attempt produced.</summary>
    public async Task<DeepDiveTurnResult> GetCurrentStateAsync(HumanOsDbContext dbContext, Guid sectionId, CancellationToken cancellationToken = default)
    {
        var step = await LoadStepAsync(dbContext, sectionId, cancellationToken);
        var cards = LoadCards(step);

        var currentCardIndex = Math.Min(step.CurrentDeepDiveCardIndex, cards.Count - 1);

        return new DeepDiveTurnResult
        {
            Message = step.CurrentDeepDivePrompt ?? string.Empty,
            CardIndex = currentCardIndex,
            TotalCards = cards.Count,
            NextCardIndex = currentCardIndex,
            Complete = step.CurrentDeepDiveCardIndex >= cards.Count
        };
    }

    /// <summary>
    /// Scores one Deep Dive attempt on the CURRENT card and — per
    /// <see cref="DeepDiveLoopGate"/> — either keeps the loop going on the
    /// same card (not yet mastered), moves on to the NEXT card (mastered,
    /// more cards remain), or marks the whole step Complete once EVERY
    /// card has been mastered in order.
    /// </summary>
    /// <param name="tutorPromptShown">The question shown to the student
    /// right before THIS attempt — null for a card's very first attempt
    /// (falls back to an implicit question from the card's own title,
    /// same self-healing fallback as Graph's TutorService).</param>
    public async Task<DeepDiveTurnResult> SubmitAttemptAsync(
        HumanOsDbContext dbContext, Guid sectionId, string studentResponse, string? tutorPromptShown, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(studentResponse);

        var step = await LoadStepAsync(dbContext, sectionId, cancellationToken);
        var cards = LoadCards(step);

        if (step.CurrentDeepDiveCardIndex >= cards.Count)
        {
            // Already complete — nothing left to grade.
            return new DeepDiveTurnResult
            {
                Message = string.Empty,
                CardIndex = cards.Count,
                TotalCards = cards.Count,
                NextCardIndex = cards.Count,
                Complete = true
            };
        }

        var currentCardIndex = step.CurrentDeepDiveCardIndex;
        var currentCard = cards[currentCardIndex];

        // Same self-healing fallback as TutorService.SubmitDeepDiveAttemptAsync
        // — a card's very first attempt has no preceding tutor question yet.
        var effectiveTutorPromptShown = string.IsNullOrWhiteSpace(tutorPromptShown)
            ? step.CurrentDeepDivePrompt
            : tutorPromptShown;
        if (string.IsNullOrWhiteSpace(effectiveTutorPromptShown))
        {
            effectiveTutorPromptShown = $"Explain what you just learned about \"{currentCard.Title}\".";
        }

        var context = await LoadSectionContextAsync(dbContext, sectionId, cancellationToken);
        var prompt = BuildTurnPrompt(context, currentCard, effectiveTutorPromptShown, studentResponse);
        var sessionKey = $"{step.StructuredLearningSectionStepId}:{currentCardIndex}";
        var isFirstTurnOnCard = _agent.IsFirstTurnOnCard(sessionKey);
        var message = await BuildTurnMessageAsync(dbContext, currentCard, prompt, isFirstTurnOnCard, cancellationToken);
        var turn = await _agent.RunTurnAsync(sessionKey, message, cancellationToken);

        for (var attempt = 0; attempt < 2 && turn.DeepDiveScore is null; attempt++)
        {
            turn = await _agent.RunTurnAsync(sessionKey, message, cancellationToken);
        }

        var score = turn.DeepDiveScore
            ?? throw new InvalidOperationException("StructuredLearningDeepDiveAgent did not return a DeepDiveScore (after retry).");

        var verdict = DeepDiveLoopGate.Evaluate(currentCardIndex, cards.Count, score);

        // Null out the persisted prompt on a fresh card so its own first
        // attempt falls back to the implicit title-based question above,
        // never the PREVIOUS card's last question — same rule as Graph.
        step.CurrentDeepDivePrompt = verdict.CardMasteredThisAttempt && !verdict.StepComplete ? null : turn.Message;
        step.CurrentDeepDiveCardIndex = verdict.NextCardIndex;

        await dbContext.SaveChangesAsync(cancellationToken);

        return new DeepDiveTurnResult
        {
            Message = turn.Message,
            CardIndex = currentCardIndex,
            TotalCards = cards.Count,
            Mastered = verdict.CardMasteredThisAttempt,
            NextCardIndex = verdict.NextCardIndex,
            Complete = verdict.StepComplete
        };
    }

    private static async Task<StructuredLearningSectionStep> LoadStepAsync(HumanOsDbContext dbContext, Guid sectionId, CancellationToken cancellationToken)
    {
        return await dbContext.StructuredLearningSectionSteps
            .FirstOrDefaultAsync(s => s.StructuredLearningDocumentSectionId == sectionId && s.StepType == StructuredLearningStepType.DeepDive, cancellationToken)
            ?? throw new InvalidOperationException($"Section {sectionId} has no Deep Dive step.");
    }

    private static List<StructuredLearningExampleCardDto> LoadCards(StructuredLearningSectionStep step)
    {
        var cards = string.IsNullOrWhiteSpace(step.StepDataJson)
            ? []
            : JsonSerializer.Deserialize<List<StructuredLearningExampleCardDto>>(step.StepDataJson) ?? [];

        if (cards.Count == 0)
        {
            throw new InvalidOperationException($"Deep Dive step {step.StructuredLearningSectionStepId} has no example cards — nothing to quiz.");
        }

        return cards;
    }

    private sealed class SectionContext
    {
        public string ChapterTitle { get; set; } = string.Empty;
        public string SubchapterTitle { get; set; } = string.Empty;
    }

    private static async Task<SectionContext> LoadSectionContextAsync(HumanOsDbContext dbContext, Guid sectionId, CancellationToken cancellationToken)
    {
        var section = await dbContext.StructuredLearningDocumentSections
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.StructuredLearningDocumentSectionId == sectionId, cancellationToken)
            ?? throw new InvalidOperationException($"StructuredLearningDocumentSection {sectionId} not found.");

        return new SectionContext { ChapterTitle = section.ChapterTitle, SubchapterTitle = section.SubchapterTitle };
    }

    private static string BuildTurnPrompt(SectionContext context, StructuredLearningExampleCardDto card, string currentQuestion, string studentResponse)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Chapter: {context.ChapterTitle}");
        builder.AppendLine($"Section: {context.SubchapterTitle}");
        builder.AppendLine($"CARD TITLE: {card.Title}");
        builder.AppendLine("CARD CONTENT:");
        builder.AppendLine(card.Text);

        // The prompt originally used to generate the card's own illustration
        // (2026-09-05) — the image itself is only attached to the chat on the
        // card's first turn (see BuildTurnMessageAsync), but this text
        // description is cheap to include on EVERY turn so the agent always
        // knows what the picture actually shows, even mid-conversation.
        if (card.IllustrationPrompts is { Count: > 0 } stepPrompts)
        {
            builder.AppendLine();
            builder.AppendLine("IMAGE DESCRIPTION (what the accompanying illustration(s) show, step by step):");
            for (var i = 0; i < stepPrompts.Count; i++)
            {
                builder.AppendLine($"  Step {i + 1}: {stepPrompts[i]}");
            }
        }
        else if (!string.IsNullOrWhiteSpace(card.IllustrationPrompt))
        {
            builder.AppendLine();
            builder.AppendLine($"IMAGE DESCRIPTION (what the accompanying illustration shows): {card.IllustrationPrompt}");
        }

        builder.AppendLine();
        builder.AppendLine("THE QUESTION BEING ANSWERED RIGHT NOW (you asked this last turn — verify against it):");
        builder.AppendLine(currentQuestion);
        builder.AppendLine();
        builder.AppendLine($"STUDENT ANSWER: {studentResponse}");

        return builder.ToString();
    }

    /// <summary>Builds the actual chat turn sent to the agent — on a
    /// card's first turn this attaches the card's own generated
    /// illustration(s) alongside the text prompt (so the agent can
    /// literally see and explain what the student is looking at); on
    /// every later turn for the SAME card only the text is sent, since the
    /// session (see <see cref="StructuredLearningDeepDiveAgent"/>) already
    /// remembers the image from the first turn.</summary>
    private async Task<ChatMessage> BuildTurnMessageAsync(
        HumanOsDbContext dbContext, StructuredLearningExampleCardDto card, string prompt, bool isFirstTurnOnCard, CancellationToken cancellationToken)
    {
        var contents = new List<AIContent> { new TextContent(prompt) };

        if (isFirstTurnOnCard && _illustrationStorage.IsConfigured)
        {
            var illustrationIds = new List<Guid>();
            if (card.IllustrationIds is { Count: > 0 } ids)
            {
                illustrationIds.AddRange(ids);
            }
            else if (card.IllustrationId is { } singleId)
            {
                illustrationIds.Add(singleId);
            }

            foreach (var illustrationId in illustrationIds.Distinct())
            {
                var illustration = await dbContext.StructuredLearningIllustrations
                    .AsNoTracking()
                    .Where(i => i.StructuredLearningIllustrationId == illustrationId)
                    .Select(i => new { i.StoragePath, i.MediaType })
                    .FirstOrDefaultAsync(cancellationToken);

                if (illustration is null || illustration.MediaType == "Video")
                {
                    continue;
                }

                try
                {
                    var bytes = await _illustrationStorage.DownloadIllustrationBytesAsync(illustration.StoragePath, cancellationToken);
                    contents.Add(new DataContent(bytes.ToMemory(), "image/png"));
                }
                catch
                {
                    // Best-effort — never block the teaching turn on a missing/unreachable image blob.
                }
            }
        }

        return new ChatMessage(ChatRole.User, contents);
    }
}
