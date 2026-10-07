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
    private readonly StructuredLearningEngramUsageService _usageService;

    public StructuredLearningRecallEngine(StructuredLearningRecallAgent agent, StructuredLearningEngramUsageService usageService)
    {
        _agent = agent;
        _usageService = usageService;
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
    /// grade yet, just fetches Q1) — required otherwise. <paramref
    /// name="personId"/> is null only for capabilitystudio's author/
    /// reviewer preview (shared Step-level state); the real student
    /// runtime always passes it so each student's live question/attempts
    /// stay separate.
    /// </summary>
    public async Task<RecallTurnResult> SubmitAttemptAsync(
        HumanOsDbContext dbContext, Guid sectionId, string? studentResponse, Guid? personId = null, Guid? engramSessionId = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        var step = await dbContext.StructuredLearningSectionSteps
            .FirstOrDefaultAsync(s => s.StructuredLearningDocumentSectionId == sectionId && s.StepType == StructuredLearningStepType.Recall, cancellationToken)
            ?? throw new InvalidOperationException($"Section {sectionId} has no Recall step.");

        var documentId = await ResolveDocumentIdAsync(dbContext, sectionId, cancellationToken);
        var personState = personId.HasValue ? await GetOrCreatePersonStateAsync(dbContext, personId.Value, sectionId, cancellationToken) : null;

        var context = await LoadSectionContextAsync(dbContext, sectionId, cancellationToken);
        var itemsRequired = await ResolveItemsRequiredAsync(dbContext, sectionId, cancellationToken);
        var staticConfig = await LoadStaticConfigAsync(dbContext, sectionId, cancellationToken);

        var priorAttempts = await dbContext.StructuredLearningRecallAttempts
            .Where(a => a.StructuredLearningDocumentSectionId == sectionId && a.PersonId == personId)
            .OrderBy(a => a.CreatedDate)
            .ToListAsync(cancellationToken);

        var effectiveQuestion = personState is not null ? personState.CurrentRecallPrompt : step.CurrentRecallPrompt;

        if (string.IsNullOrWhiteSpace(effectiveQuestion))
        {
            // Bootstrap: no question exists yet — nothing to grade, just fetch Q1.
            // Static mode (2026-10-06): item 0 was already generated at
            // course-creation time — read it instead of calling the LLM.
            var staticFirstItem = PickStaticItem(staticConfig.StaticItems, 0);
            string bootstrapMessage;
            if (staticConfig.Mode == StructuredLearningGenerationMode.Static && staticFirstItem is not null)
            {
                bootstrapMessage = staticFirstItem;
            }
            else
            {
                var bootstrapPrompt = BuildTurnPrompt(context, alreadyAsked: [], currentQuestion: null, studentResponse: null);
                var bootstrapAgentResult = await _agent.RunTurnAsync(bootstrapPrompt, cancellationToken);
                await _usageService.RecordTextUsageAsync(
                    personId, documentId, sectionId, engramSessionId,
                    bootstrapAgentResult.ModelName, bootstrapAgentResult.InputTokens, bootstrapAgentResult.OutputTokens, cancellationToken, stepType: "Recall");
                bootstrapMessage = bootstrapAgentResult.Turn.Message;
            }

            if (personState is not null) personState.CurrentRecallPrompt = bootstrapMessage;
            else step.CurrentRecallPrompt = bootstrapMessage;
            await dbContext.SaveChangesAsync(cancellationToken);

            return new RecallTurnResult
            {
                Started = true,
                NextQuestion = bootstrapMessage,
                ItemsRequired = itemsRequired
            };
        }

        if (string.IsNullOrWhiteSpace(studentResponse))
        {
            throw new InvalidOperationException("StudentResponse is required once a Recall question has already been asked.");
        }

        var alreadyAskedPrompts = priorAttempts.Select(a => a.TutorPromptShown).ToList();
        var prompt = BuildTurnPrompt(context, alreadyAskedPrompts, effectiveQuestion, studentResponse);
        var agentResult = await _agent.RunTurnAsync(prompt, cancellationToken);
        var turn = agentResult.Turn;
        await _usageService.RecordTextUsageAsync(
            personId, documentId, sectionId, engramSessionId,
            agentResult.ModelName, agentResult.InputTokens, agentResult.OutputTokens, cancellationToken, stepType: "Recall");

        if (turn.RecallScore is null)
        {
            // Retry once — mirrors TutorService's exact "cheap, resolves the vast majority of these" pattern.
            agentResult = await _agent.RunTurnAsync(prompt, cancellationToken);
            turn = agentResult.Turn;
            await _usageService.RecordTextUsageAsync(
                personId, documentId, sectionId, engramSessionId,
                agentResult.ModelName, agentResult.InputTokens, agentResult.OutputTokens, cancellationToken, stepType: "Recall");
        }

        var score = turn.RecallScore
            ?? throw new InvalidOperationException("StructuredLearningRecallAgent did not return a RecallScore for a graded turn (after retry).");

        var newAttempt = new StructuredLearningRecallAttempt
        {
            StructuredLearningDocumentSectionId = sectionId,
            PersonId = personId,
            StructuredLearningEngramSessionId = engramSessionId,
            TutorPromptShown = effectiveQuestion,
            StudentResponse = studentResponse,
            RecallScore = score
        };
        dbContext.StructuredLearningRecallAttempts.Add(newAttempt);

        var priorScores = priorAttempts.Select(a => a.RecallScore).ToList();
        var verdict = RecallLoopGate.Evaluate(priorScores, score, itemsRequired);

        // Static mode (2026-10-06): the AGENT still grades the student's own
        // free-text answer live (grading is inherently per-student), but the
        // NEXT QUESTION text comes from the persisted bank — not whatever
        // the agent invented — so every student sees the exact same
        // question for a given item. itemsMasteredSoFar is the 0-based
        // index of whichever item the student is on NEXT (unchanged while
        // retrying the current item, since mastery only increments once
        // they pass it).
        string nextMessage;
        if (verdict.ItemFailed)
        {
            // Regress to Teaching (2026-10-06): the student exhausted every
            // attempt on this item without mastering it. RecallLoopGate's
            // own contract says the step must NOT silently advance to a
            // "next item" here (there may not even be one left) — instead
            // wipe this student's Recall progress for the section so a
            // future visit (after reviewing Learn again) starts completely
            // fresh. Mirrors TutorService.RegressToTeachingAsync's intent
            // for the older Capability runtime, which this newer
            // Structured Learning engine never had an equivalent for.
            dbContext.StructuredLearningRecallAttempts.Remove(newAttempt);
            dbContext.StructuredLearningRecallAttempts.RemoveRange(priorAttempts);
            nextMessage = string.Empty;
            if (personState is not null) personState.CurrentRecallPrompt = null;
            else step.CurrentRecallPrompt = null;
        }
        else
        {
            nextMessage = staticConfig.Mode == StructuredLearningGenerationMode.Static
                ? PickStaticItem(staticConfig.StaticItems, verdict.ItemsMasteredSoFar) ?? turn.Message
                : turn.Message;
            if (personState is not null) personState.CurrentRecallPrompt = nextMessage;
            else step.CurrentRecallPrompt = nextMessage;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return new RecallTurnResult
        {
            NextQuestion = nextMessage,
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
    public async Task<RecallTurnResult> GetCurrentStateAsync(HumanOsDbContext dbContext, Guid sectionId, Guid? personId = null, Guid? engramSessionId = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        var step = await dbContext.StructuredLearningSectionSteps
            .FirstOrDefaultAsync(s => s.StructuredLearningDocumentSectionId == sectionId && s.StepType == StructuredLearningStepType.Recall, cancellationToken)
            ?? throw new InvalidOperationException($"Section {sectionId} has no Recall step.");

        var personState = personId.HasValue ? await GetOrCreatePersonStateAsync(dbContext, personId.Value, sectionId, cancellationToken) : null;

        var itemsRequired = await ResolveItemsRequiredAsync(dbContext, sectionId, cancellationToken);

        var effectiveQuestion = personState is not null ? personState.CurrentRecallPrompt : step.CurrentRecallPrompt;

        if (string.IsNullOrWhiteSpace(effectiveQuestion))
        {
            var staticConfig = await LoadStaticConfigAsync(dbContext, sectionId, cancellationToken);
            var staticFirstItem = PickStaticItem(staticConfig.StaticItems, 0);
            string bootstrapMessage;
            if (staticConfig.Mode == StructuredLearningGenerationMode.Static && staticFirstItem is not null)
            {
                bootstrapMessage = staticFirstItem;
            }
            else
            {
                var context = await LoadSectionContextAsync(dbContext, sectionId, cancellationToken);
                var bootstrapPrompt = BuildTurnPrompt(context, alreadyAsked: [], currentQuestion: null, studentResponse: null);
                var bootstrapAgentResult = await _agent.RunTurnAsync(bootstrapPrompt, cancellationToken);
                var documentId = await ResolveDocumentIdAsync(dbContext, sectionId, cancellationToken);
                await _usageService.RecordTextUsageAsync(
                    personId, documentId, sectionId, engramSessionId,
                    bootstrapAgentResult.ModelName, bootstrapAgentResult.InputTokens, bootstrapAgentResult.OutputTokens, cancellationToken, stepType: "Recall");
                bootstrapMessage = bootstrapAgentResult.Turn.Message;
            }

            if (personState is not null) personState.CurrentRecallPrompt = bootstrapMessage;
            else step.CurrentRecallPrompt = bootstrapMessage;
            await dbContext.SaveChangesAsync(cancellationToken);

            return new RecallTurnResult { Started = true, NextQuestion = bootstrapMessage, ItemsRequired = itemsRequired };
        }

        var priorAttempts = await dbContext.StructuredLearningRecallAttempts
            .Where(a => a.StructuredLearningDocumentSectionId == sectionId && a.PersonId == personId)
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
            NextQuestion = personState is not null ? personState.CurrentRecallPrompt ?? string.Empty : step.CurrentRecallPrompt ?? string.Empty,
            AttemptsUsedForItem = attemptsUsedForItem,
            ItemsMastered = itemsMastered,
            ItemsRequired = itemsRequired,
            Complete = itemsMastered >= itemsRequired
        };
    }

    private static async Task<Models.Capabilities.Studio.StructuredLearningPersonSectionState> GetOrCreatePersonStateAsync(
        HumanOsDbContext dbContext, Guid personId, Guid sectionId, CancellationToken cancellationToken)
    {
        var existing = await dbContext.StructuredLearningPersonSectionStates
            .FirstOrDefaultAsync(x => x.PersonId == personId && x.StructuredLearningDocumentSectionId == sectionId, cancellationToken);
        if (existing is not null) return existing;

        var created = new Models.Capabilities.Studio.StructuredLearningPersonSectionState
        {
            PersonId = personId,
            StructuredLearningDocumentSectionId = sectionId
        };
        dbContext.StructuredLearningPersonSectionStates.Add(created);
        return created;
    }

    private static async Task<Guid?> ResolveDocumentIdAsync(HumanOsDbContext dbContext, Guid sectionId, CancellationToken cancellationToken)
    {
        return await dbContext.StructuredLearningDocumentSections
            .AsNoTracking()
            .Where(s => s.StructuredLearningDocumentSectionId == sectionId)
            .Select(s => (Guid?)s.StructuredLearningDocumentId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private sealed record RecallStaticConfig(StructuredLearningGenerationMode Mode, List<string> StaticItems);

    private static async Task<RecallStaticConfig> LoadStaticConfigAsync(HumanOsDbContext dbContext, Guid sectionId, CancellationToken cancellationToken)
    {
        var row = await dbContext.StructuredLearningDocumentSections
            .AsNoTracking()
            .Where(s => s.StructuredLearningDocumentSectionId == sectionId)
            .Select(s => new { s.Document.RecallGenerationMode, s.StaticRecallItemsJson })
            .FirstOrDefaultAsync(cancellationToken);

        if (row is null || row.RecallGenerationMode != StructuredLearningGenerationMode.Static)
        {
            return new RecallStaticConfig(StructuredLearningGenerationMode.Dynamic, []);
        }

        var items = string.IsNullOrWhiteSpace(row.StaticRecallItemsJson)
            ? []
            : System.Text.Json.JsonSerializer.Deserialize<List<string>>(row.StaticRecallItemsJson) ?? [];
        return new RecallStaticConfig(StructuredLearningGenerationMode.Static, items);
    }

    /// <summary>Picks the question text for the item the student is
    /// CURRENTLY on (0-based <paramref name="itemIndex"/> = how many items
    /// they've already mastered this section) from the persisted Static
    /// bank — null once they've gone past the last persisted item (caller
    /// falls back to Dynamic generation or treats the step as complete).</summary>
    private static string? PickStaticItem(IReadOnlyList<string> items, int itemIndex) =>
        itemIndex >= 0 && itemIndex < items.Count ? items[itemIndex] : null;

    /// <summary>Eager Static-mode generation (2026-10-06) — called from
    /// <see cref="StructuredLearningPedagogyService"/> right after a
    /// section's pedagogy finishes, so up-to-3 standalone Recall questions
    /// are persisted in SQL BEFORE any student opens the course. Grading of
    /// each student's own answer still happens live via
    /// <see cref="_agent"/> at runtime (free-text grading is inherently
    /// per-student) — only the QUESTION TEXT is fixed/shared. No-op for
    /// Dynamic documents.</summary>
    public async Task EnsureStaticItemsAsync(HumanOsDbContext dbContext, Guid sectionId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        var row = await dbContext.StructuredLearningDocumentSections
            .AsNoTracking()
            .Where(s => s.StructuredLearningDocumentSectionId == sectionId)
            .Select(s => new { s.Document.RecallGenerationMode, s.Document.RecallItemsRequired })
            .FirstOrDefaultAsync(cancellationToken);

        if (row is null || row.RecallGenerationMode != StructuredLearningGenerationMode.Static)
        {
            return;
        }

        var count = Math.Clamp(row.RecallItemsRequired ?? RecallLoopGate.DefaultItemsRequiredToAdvance, 1, 3);
        var context = await LoadSectionContextAsync(dbContext, sectionId, cancellationToken);
        var documentId = await ResolveDocumentIdAsync(dbContext, sectionId, cancellationToken);

        var items = new List<string>();
        for (var i = 0; i < count; i++)
        {
            var prompt = BuildTurnPrompt(context, alreadyAsked: items, currentQuestion: null, studentResponse: null);
            var agentResult = await _agent.RunTurnAsync(prompt, cancellationToken);
            await _usageService.RecordTextUsageAsync(
                null, documentId, sectionId, null,
                agentResult.ModelName, agentResult.InputTokens, agentResult.OutputTokens, cancellationToken, stepType: "Recall");
            items.Add(agentResult.Turn.Message);
        }

        await dbContext.StructuredLearningDocumentSections
            .Where(s => s.StructuredLearningDocumentSectionId == sectionId)
            .ExecuteUpdateAsync(
                s => s.SetProperty(x => x.StaticRecallItemsJson, System.Text.Json.JsonSerializer.Serialize(items)),
                cancellationToken);
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
