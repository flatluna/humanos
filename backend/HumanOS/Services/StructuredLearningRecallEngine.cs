using System.Text;
using HumanOS.Agentic.Runtime;
using HumanOS.Agents.Studio;
using HumanOS.Data;
using HumanOS.Models.Capabilities.Studio;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.Services;

/// <summary>
/// Runtime — real-time Recall engine for Structured Learning sections
/// (2026-08-10). Mirrors <see cref="TutorService"/>'s
/// <c>SubmitRecallAttemptAsync</c> + <see cref="RecallLoopGate"/> pattern:
/// ONE call each turn both grades the answer to the question asked LAST
/// turn (via <see cref="StructuredLearningRecallAgent"/>) AND generates the
/// NEXT question — replaces the OLD design where
/// StructuredLearningSectionDesignerAgent baked a fixed list of Recall
/// items once at generation time. Deterministic mastery/attempt-budget
/// decisions reuse <see cref="RecallLoopGate"/> AS-IS (it's already
/// generic/stateless, no Graph-specific dependency) — same "LLM proposes,
/// code decides" discipline as the Graph runtime.
///
/// Keyed directly by StructuredLearningDocumentSectionId — see
/// <see cref="StructuredLearningAssessmentRound"/>'s doc comment for why
/// (no per-student session concept exists yet for Structured Learning).
/// Unlike Graph's Recall, there is no "regress to Teaching" transition
/// here (Structured Learning has no step-activation/session model to
/// regress within yet) — an exhausted item is simply reported via
/// <see cref="RecallTurnResult.ItemFailed"/> for the future runtime UI to
/// react to however it wants.
/// </summary>
public sealed class StructuredLearningRecallEngine
{
    private readonly StructuredLearningRecallAgent _agent;

    public StructuredLearningRecallEngine(StructuredLearningRecallAgent agent)
    {
        _agent = agent;
    }

    public sealed class RecallTurnResult
    {
        /// <summary>True when this was the bootstrap "fetch the very first
        /// question" call — nothing was graded (there was no prior
        /// question yet).</summary>
        public bool Started { get; set; }

        public string NextQuestion { get; set; } = string.Empty;

        /// <summary>Null only when <see cref="Started"/> is true.</summary>
        public int? Score { get; set; }

        public int AttemptsUsedForItem { get; set; }
        public int ItemsMastered { get; set; }
        public int ItemsRequired { get; set; }
        public bool MasteredThisAttempt { get; set; }

        /// <summary>True once enough distinct items have been mastered.</summary>
        public bool Complete { get; set; }

        /// <summary>True when the student exhausted their attempt budget on
        /// the current item without mastering it.</summary>
        public bool ItemFailed { get; set; }
    }

    /// <summary>
    /// Grades the answer to the question asked last turn (if any) and
    /// generates the next one. Pass <paramref name="studentResponse"/> as
    /// null/empty ONLY on the very first call for a section (nothing to
    /// grade yet, just fetches Q1) — required otherwise.
    /// </summary>
    public async Task<RecallTurnResult> SubmitAttemptAsync(
        HumanOsDbContext dbContext, Guid sectionId, string? studentResponse, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        var step = await dbContext.StructuredLearningSectionSteps
            .FirstOrDefaultAsync(s => s.StructuredLearningDocumentSectionId == sectionId && s.StepType == StructuredLearningStepType.Recall, cancellationToken)
            ?? throw new InvalidOperationException($"Section {sectionId} has no Recall step.");

        var context = await LoadSectionContextAsync(dbContext, sectionId, cancellationToken);
        var itemsRequired = await ResolveItemsRequiredAsync(dbContext, sectionId, cancellationToken);

        var priorAttempts = await dbContext.StructuredLearningRecallAttempts
            .Where(a => a.StructuredLearningDocumentSectionId == sectionId)
            .OrderBy(a => a.CreatedDate)
            .ToListAsync(cancellationToken);

        var effectiveQuestion = step.CurrentRecallPrompt;

        if (string.IsNullOrWhiteSpace(effectiveQuestion))
        {
            // Bootstrap: no question exists yet — nothing to grade, just fetch Q1.
            var bootstrapPrompt = BuildTurnPrompt(context, alreadyAsked: [], currentQuestion: null, studentResponse: null);
            var bootstrapResponse = await _agent.RunTurnAsync(bootstrapPrompt, cancellationToken);

            step.CurrentRecallPrompt = bootstrapResponse.Message;
            await dbContext.SaveChangesAsync(cancellationToken);

            return new RecallTurnResult
            {
                Started = true,
                NextQuestion = bootstrapResponse.Message,
                ItemsRequired = itemsRequired
            };
        }

        if (string.IsNullOrWhiteSpace(studentResponse))
        {
            throw new InvalidOperationException("StudentResponse is required once a Recall question has already been asked.");
        }

        var alreadyAskedPrompts = priorAttempts.Select(a => a.TutorPromptShown).ToList();
        var prompt = BuildTurnPrompt(context, alreadyAskedPrompts, effectiveQuestion, studentResponse);
        var turn = await _agent.RunTurnAsync(prompt, cancellationToken);

        if (turn.RecallScore is null)
        {
            // Retry once — mirrors TutorService's exact "cheap, resolves the vast majority of these" pattern.
            turn = await _agent.RunTurnAsync(prompt, cancellationToken);
        }

        var score = turn.RecallScore
            ?? throw new InvalidOperationException("StructuredLearningRecallAgent did not return a RecallScore for a graded turn (after retry).");

        dbContext.StructuredLearningRecallAttempts.Add(new StructuredLearningRecallAttempt
        {
            StructuredLearningDocumentSectionId = sectionId,
            TutorPromptShown = effectiveQuestion,
            StudentResponse = studentResponse,
            RecallScore = score
        });

        var priorScores = priorAttempts.Select(a => a.RecallScore).ToList();
        var verdict = RecallLoopGate.Evaluate(priorScores, score, itemsRequired);

        step.CurrentRecallPrompt = turn.Message;

        await dbContext.SaveChangesAsync(cancellationToken);

        return new RecallTurnResult
        {
            NextQuestion = turn.Message,
            Score = score,
            AttemptsUsedForItem = verdict.AttemptsUsedForCurrentItem,
            ItemsMastered = verdict.ItemsMasteredSoFar,
            ItemsRequired = itemsRequired,
            MasteredThisAttempt = verdict.ItemMasteredThisAttempt,
            Complete = verdict.StepComplete,
            ItemFailed = verdict.ItemFailed
        };
    }

    /// <summary>
    /// Bootstrap-safe read of the current Recall state — NEVER grades
    /// anything, safe to call on every page mount/reload. Fixes a real bug
    /// (2026-08-10): the frontend used to always POST .../recall/attempt
    /// with no StudentResponse on mount, assuming it was always the "very
    /// first call" — but any remount AFTER the section's first question was
    /// already asked (step.CurrentRecallPrompt already set) hit
    /// SubmitAttemptAsync's "StudentResponse is required" guard and threw a
    /// 400. Bootstraps (asks Q1) only if truly never started; otherwise
    /// just reports the CURRENT question + progress with no new grading.
    /// </summary>
    public async Task<RecallTurnResult> GetCurrentStateAsync(HumanOsDbContext dbContext, Guid sectionId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        var step = await dbContext.StructuredLearningSectionSteps
            .FirstOrDefaultAsync(s => s.StructuredLearningDocumentSectionId == sectionId && s.StepType == StructuredLearningStepType.Recall, cancellationToken)
            ?? throw new InvalidOperationException($"Section {sectionId} has no Recall step.");

        var itemsRequired = await ResolveItemsRequiredAsync(dbContext, sectionId, cancellationToken);

        if (string.IsNullOrWhiteSpace(step.CurrentRecallPrompt))
        {
            var context = await LoadSectionContextAsync(dbContext, sectionId, cancellationToken);
            var bootstrapPrompt = BuildTurnPrompt(context, alreadyAsked: [], currentQuestion: null, studentResponse: null);
            var bootstrapResponse = await _agent.RunTurnAsync(bootstrapPrompt, cancellationToken);

            step.CurrentRecallPrompt = bootstrapResponse.Message;
            await dbContext.SaveChangesAsync(cancellationToken);

            return new RecallTurnResult { Started = true, NextQuestion = bootstrapResponse.Message, ItemsRequired = itemsRequired };
        }

        var priorAttempts = await dbContext.StructuredLearningRecallAttempts
            .Where(a => a.StructuredLearningDocumentSectionId == sectionId)
            .OrderBy(a => a.CreatedDate)
            .Select(a => a.RecallScore)
            .ToListAsync(cancellationToken);

        var itemsMastered = 0;
        var attemptsUsedForItem = 0;
        foreach (var score in priorAttempts)
        {
            attemptsUsedForItem++;
            if (RecallLoopGate.IsMastered(score))
            {
                itemsMastered++;
                attemptsUsedForItem = 0;
            }
        }

        return new RecallTurnResult
        {
            NextQuestion = step.CurrentRecallPrompt,
            AttemptsUsedForItem = attemptsUsedForItem,
            ItemsMastered = itemsMastered,
            ItemsRequired = itemsRequired,
            Complete = itemsMastered >= itemsRequired
        };
    }

    private static async Task<int> ResolveItemsRequiredAsync(HumanOsDbContext dbContext, Guid sectionId, CancellationToken cancellationToken)
    {
        var configuredValue = await dbContext.StructuredLearningDocumentSections
            .AsNoTracking()
            .Where(s => s.StructuredLearningDocumentSectionId == sectionId)
            .Select(s => s.Document.RecallItemsRequired)
            .FirstOrDefaultAsync(cancellationToken);

        return configuredValue.HasValue
            ? Math.Clamp(configuredValue.Value, RecallLoopGate.MinItemsRequiredToAdvance, RecallLoopGate.MaxItemsRequiredToAdvance)
            : RecallLoopGate.DefaultItemsRequiredToAdvance;
    }

    private sealed class SectionContext
    {
        public string ChapterTitle { get; set; } = string.Empty;
        public string SubchapterTitle { get; set; } = string.Empty;
        public string TeachingContent { get; set; } = string.Empty;
    }

    private static async Task<SectionContext> LoadSectionContextAsync(HumanOsDbContext dbContext, Guid sectionId, CancellationToken cancellationToken)
    {
        var section = await dbContext.StructuredLearningDocumentSections
            .AsNoTracking()
            .Include(s => s.Steps)
            .FirstOrDefaultAsync(s => s.StructuredLearningDocumentSectionId == sectionId, cancellationToken)
            ?? throw new InvalidOperationException($"StructuredLearningDocumentSection {sectionId} not found.");

        var teachingStep = section.Steps.FirstOrDefault(s => s.StepType == StructuredLearningStepType.Teaching);

        return new SectionContext
        {
            ChapterTitle = section.ChapterTitle,
            SubchapterTitle = section.SubchapterTitle,
            TeachingContent = teachingStep?.Content ?? section.Text
        };
    }

    private static string BuildTurnPrompt(SectionContext context, IReadOnlyList<string> alreadyAsked, string? currentQuestion, string? studentResponse)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Chapter: {context.ChapterTitle}");
        builder.AppendLine($"Section: {context.SubchapterTitle}");
        builder.AppendLine("SECTION CONTENT:");
        builder.AppendLine(context.TeachingContent);
        builder.AppendLine();

        if (alreadyAsked.Count > 0)
        {
            builder.AppendLine("QUESTIONS ALREADY ASKED (never repeat one of these literally or with trivial rewording):");
            foreach (var q in alreadyAsked)
            {
                builder.AppendLine($"- {q}");
            }
            builder.AppendLine();
        }

        if (currentQuestion is not null)
        {
            builder.AppendLine("THE QUESTION BEING ANSWERED RIGHT NOW (you asked this last turn — verify against THIS question's exact values):");
            builder.AppendLine(currentQuestion);
            builder.AppendLine();
            builder.AppendLine($"STUDENT ANSWER: {studentResponse}");
        }
        else
        {
            builder.AppendLine("This is the student's very first turn — there is no prior question to grade yet. Just ask the first question.");
        }

        return builder.ToString();
    }
}
