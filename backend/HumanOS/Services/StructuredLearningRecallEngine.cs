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
    /// <summary>Same wording as ExampleCardIllustrationService/
    /// StructuredLearningEngramCardService's own KidsIllustrationStyle —
    /// kept duplicated locally on purpose (see those services' own doc
    /// comments: avoids coupling otherwise-independent illustration
    /// pipelines together). Applied to EVERY Static Recall card regardless
    /// of audience (2026-10-26) — unlike the Engram Card pipeline, Static
    /// Recall cards are offered to Standard/adult documents too.</summary>
    private const string KidsCardIllustrationStyle =
        "A simple, warm, child-friendly educational illustration for a K-12 learning app. " +
        "Flat design, soft cheerful colors, single clear focal scene, friendly and age-appropriate, " +
        "professional children's book / cartoon illustration style. No text, no letters, no numbers, no words in the image.";

    private const string RealisticCardIllustrationStyle =
        "A realistic, professional educational illustration/photo for an adult learning app. " +
        "Photorealistic or clean diagrammatic style as appropriate to the subject, natural lighting when a real " +
        "scene is depicted, single clear focal composition. No text, no letters, no numbers, no words in the image.";

    private readonly StructuredLearningRecallAgent _agent;
    private readonly StructuredLearningEngramUsageService _usageService;
    private readonly GraphIllustrationImageService _imageService;
    private readonly HumanOS.Storage.CapabilityGraphIllustrationStorageService _illustrationStorage;
    private readonly RecallCardWatermarkService _watermarkService;

    public StructuredLearningRecallEngine(
        StructuredLearningRecallAgent agent,
        StructuredLearningEngramUsageService usageService,
        GraphIllustrationImageService imageService,
        HumanOS.Storage.CapabilityGraphIllustrationStorageService illustrationStorage,
        RecallCardWatermarkService watermarkService)
    {
        _agent = agent;
        _usageService = usageService;
        _imageService = imageService;
        _illustrationStorage = illustrationStorage;
        _watermarkService = watermarkService;
    }

    public sealed class RecallTurnResult
    {
        /// <summary>True when this was the bootstrap "fetch the very first
        /// question" call — nothing was graded (there was no prior
        /// question yet).</summary>
        public bool Started { get; set; }

        public string NextQuestion { get; set; } = string.Empty;

        /// <summary>Concrete description of the objects/scene the current
        /// question refers to, for generating a supporting illustration —
        /// see <see cref="HumanOS.Agents.Studio.StructuredLearningRecallTurnResponse.VisualObjectsDescription"/>.
        /// Null when the question has nothing concrete/visual to draw.</summary>
        public string? VisualObjectsDescription { get; set; }

        /// <summary>The pre-generated Static-mode card's illustration id
        /// (2026-10-26) — fetch its image from
        /// <c>GET /illustrations/structured-learning/{id}/image</c>, same
        /// endpoint every other Structured Learning illustration uses. Null
        /// in Dynamic mode (no illustration is ever generated live) or when
        /// a Static item's own image generation failed/was skipped.</summary>
        public Guid? IllustrationId { get; set; }

        /// <summary>Short, literal correct answer to <see cref="NextQuestion"/>
        /// (2026-10-26) — when present (Static-mode bank items only, so
        /// far), lets the caller (the Realtime voice Agent's session
        /// instructions, in particular) grade/react INSTANTLY itself
        /// instead of waiting on a server round-trip. Null in Dynamic mode
        /// and for legacy Static items persisted before this field
        /// existed.</summary>
        public string? CorrectAnswer { get; set; }

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
    /// <param name="isCorrectOverride">Voice Recall verdict bypass
    /// (2026-10-27, product decision after the Realtime voice Agent's own
    /// `answerText` tool argument proved repeatedly unreliable — it would
    /// substitute its own filler reaction text, e.g. "Thank you.", instead
    /// of a real transcript of what the student said). The voice Agent
    /// ALREADY hears the student's actual audio AND is already told the
    /// correct answer so it can react with a true verdict out loud before
    /// ever calling this — so instead of re-deriving correctness from a
    /// (frequently garbled) transcript, the voice path now has the Agent
    /// report that SAME judgment directly as this boolean. When set, skips
    /// the independent text-grading LLM call entirely (there is no reliable
    /// text to grade) and scores 100/0 from the boolean alone;
    /// <paramref name="studentResponse"/> is still stored for
    /// display/record purposes only in that case, never used for grading.
    /// Null (typed-answer path, unchanged) keeps grading
    /// <paramref name="studentResponse"/>'s literal text as before.</param>
    public async Task<RecallTurnResult> SubmitAttemptAsync(
        HumanOsDbContext dbContext, Guid sectionId, string? studentResponse, Guid? personId = null, Guid? engramSessionId = null, bool? isCorrectOverride = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        var step = await dbContext.StructuredLearningSectionSteps
            .FirstOrDefaultAsync(s => s.StructuredLearningDocumentSectionId == sectionId && s.StepType == StructuredLearningStepType.Recall, cancellationToken)
            ?? throw new InvalidOperationException($"Section {sectionId} has no Recall step.");

        var documentId = await ResolveDocumentIdAsync(dbContext, sectionId, cancellationToken);
        var personState = personId.HasValue ? await GetOrCreatePersonStateAsync(dbContext, personId.Value, sectionId, cancellationToken) : null;

        var context = await LoadSectionContextAsync(dbContext, sectionId, cancellationToken);
        var itemsRequired = await ResolveItemsRequiredAsync(dbContext, sectionId, cancellationToken);
        var maxAttemptsPerItem = await ResolveMaxAttemptsPerItemAsync(dbContext, sectionId, cancellationToken);
        var staticConfig = await LoadStaticConfigAsync(dbContext, sectionId, cancellationToken);

        var priorAttempts = await dbContext.StructuredLearningRecallAttempts
            .Where(a => a.StructuredLearningDocumentSectionId == sectionId && a.PersonId == personId)
            .OrderBy(a => a.CreatedDate)
            .ToListAsync(cancellationToken);

        var effectiveQuestion = personState is not null ? personState.CurrentRecallPrompt : step.CurrentRecallPrompt;

        // Same completion-bug guard as GetCurrentStateAsync (2026-10-27):
        // an empty stored prompt can ALSO mean "already fully complete"
        // (see SubmitAttemptAsync's own ItemFailed-free branch below,
        // which intentionally stores an empty prompt once there's no next
        // item left) — never re-bootstrap back to item 0 in that case.
        if (string.IsNullOrWhiteSpace(effectiveQuestion) && priorAttempts.Count(a => RecallLoopGate.IsMastered(a.RecallScore)) >= itemsRequired)
        {
            return new RecallTurnResult
            {
                NextQuestion = string.Empty,
                ItemsMastered = priorAttempts.Count(a => RecallLoopGate.IsMastered(a.RecallScore)),
                ItemsRequired = itemsRequired,
                Complete = true
            };
        }

        if (string.IsNullOrWhiteSpace(effectiveQuestion))
        {
            // Bootstrap: no question exists yet — nothing to grade, just fetch Q1.
            // Static mode (2026-10-06): item 0 was already generated at
            // course-creation time — read it instead of calling the LLM.
            var staticFirstItem = PickStaticItem(staticConfig.StaticItems, 0);
            string bootstrapMessage;
            string? bootstrapVisualDescription = null;
            Guid? bootstrapIllustrationId = null;
            if (staticConfig.Mode == StructuredLearningGenerationMode.Static && staticFirstItem is not null)
            {
                bootstrapMessage = staticFirstItem.Question;
                bootstrapIllustrationId = staticFirstItem.IllustrationId;
                bootstrapVisualDescription = staticFirstItem.VisualObjectsDescription;
            }
            else
            {
                var bootstrapPrompt = BuildTurnPrompt(context, alreadyAsked: [], currentQuestion: null, studentResponse: null);
                var bootstrapAgentResult = await _agent.RunTurnAsync(bootstrapPrompt, cancellationToken);
                await _usageService.RecordTextUsageAsync(
                    personId, documentId, sectionId, engramSessionId,
                    bootstrapAgentResult.ModelName, bootstrapAgentResult.InputTokens, bootstrapAgentResult.OutputTokens, cancellationToken, stepType: "Recall");
                bootstrapMessage = bootstrapAgentResult.Turn.Message;
                bootstrapVisualDescription = bootstrapAgentResult.Turn.VisualObjectsDescription;
            }

            if (personState is not null)
            {
                personState.CurrentRecallPrompt = bootstrapMessage;
                personState.CurrentRecallVisualObjectsDescription = bootstrapVisualDescription;
            }
            else
            {
                step.CurrentRecallPrompt = bootstrapMessage;
                step.CurrentRecallVisualObjectsDescription = bootstrapVisualDescription;
            }
            await dbContext.SaveChangesAsync(cancellationToken);

            return new RecallTurnResult
            {
                Started = true,
                NextQuestion = bootstrapMessage,
                VisualObjectsDescription = bootstrapVisualDescription,
                IllustrationId = bootstrapIllustrationId,
                CorrectAnswer = staticFirstItem?.CorrectAnswer,
                ItemsRequired = itemsRequired
            };
        }

        if (string.IsNullOrWhiteSpace(studentResponse))
        {
            throw new InvalidOperationException("StudentResponse is required once a Recall question has already been asked.");
        }

        var effectiveVisualDescription = personState is not null ? personState.CurrentRecallVisualObjectsDescription : step.CurrentRecallVisualObjectsDescription;
        var alreadyAskedPrompts = priorAttempts.Select(a => a.TutorPromptShown).ToList();

        // The item currently being graded (2026-10-26) — its index is the
        // same "items mastered so far" count RecallLoopGate itself derives
        // from priorAttempts (mastery only ever increments, so this is
        // stable to recompute here ahead of time). Looking this item back
        // up lets us grade against its OWN persisted ground truth even
        // though the question/scene text sent to the student isn't stored
        // anywhere else per-attempt.
        var currentItemIndex = priorAttempts.Count(a => RecallLoopGate.IsMastered(a.RecallScore));
        var currentStaticItem = staticConfig.Mode == StructuredLearningGenerationMode.Static
            ? PickStaticItem(staticConfig.StaticItems, currentItemIndex)
            : null;

        int score;
        StructuredLearningRecallTurnResponse? turn = null;
        if (isCorrectOverride.HasValue)
        {
            // Voice Recall verdict bypass (2026-10-27) — see this method's
            // doc comment. No LLM grading call at all: the voice Agent
            // already judged this out loud.
            score = isCorrectOverride.Value ? 100 : 0;
        }
        else
        {
            var prompt = BuildTurnPrompt(context, alreadyAskedPrompts, effectiveQuestion, studentResponse, currentStaticItem?.VisualObjectsDescription ?? effectiveVisualDescription, currentStaticItem?.CorrectAnswer);

            // Static mode (2026-10-26, latency fix): the NEXT question always
            // comes from the persisted bank regardless of what the agent says,
            // so asking it to ALSO invent a brand-new question + scene + full
            // scratch-work every single grading call (previous behavior) was
            // pure wasted generation time — the #1 cause of the multi-second
            // "dead air" the voice Agent went silent for after every spoken
            // answer. RunGradeOnlyTurnAsync asks for (and outputs) only the
            // score + verification work, nothing else, so it finishes much
            // faster — but ONLY correctness-wise safe when a persisted
            // ground-truth VisualObjectsDescription exists for this item (see
            // StaticRecallItem); older items persisted before that field
            // existed still fall back to the full (slower) call so grading
            // never regresses to "blind".
            var canGradeOnly = staticConfig.Mode == StructuredLearningGenerationMode.Static && !string.IsNullOrWhiteSpace(currentStaticItem?.VisualObjectsDescription ?? effectiveVisualDescription);

            if (canGradeOnly)
            {
                var gradeResult = await _agent.RunGradeOnlyTurnAsync(prompt, cancellationToken);
                await _usageService.RecordTextUsageAsync(
                    personId, documentId, sectionId, engramSessionId,
                    gradeResult.ModelName, gradeResult.InputTokens, gradeResult.OutputTokens, cancellationToken, stepType: "Recall");

                if (gradeResult.Turn.RecallScore is null)
                {
                    // Retry once — mirrors TutorService's exact "cheap, resolves the vast majority of these" pattern.
                    gradeResult = await _agent.RunGradeOnlyTurnAsync(prompt, cancellationToken);
                    await _usageService.RecordTextUsageAsync(
                        personId, documentId, sectionId, engramSessionId,
                        gradeResult.ModelName, gradeResult.InputTokens, gradeResult.OutputTokens, cancellationToken, stepType: "Recall");
                }

                turn = gradeResult.Turn;
            }
            else
            {
                var agentResult = await _agent.RunTurnAsync(prompt, cancellationToken);
                turn = agentResult.Turn;
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
            }

            score = turn.RecallScore
                ?? throw new InvalidOperationException("StructuredLearningRecallAgent did not return a RecallScore for a graded turn (after retry).");
        }

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
        var verdict = RecallLoopGate.Evaluate(priorScores, score, itemsRequired, maxAttemptsPerItem);

        // Static mode (2026-10-06): the AGENT still grades the student's own
        // free-text answer live (grading is inherently per-student), but the
        // NEXT QUESTION text comes from the persisted bank — not whatever
        // the agent invented — so every student sees the exact same
        // question for a given item. itemsMasteredSoFar is the 0-based
        // index of whichever item the student is on NEXT (unchanged while
        // retrying the current item, since mastery only increments once
        // they pass it).
        string nextMessage;
        string? nextVisualDescription;
        Guid? nextIllustrationId = null;
        string? nextCorrectAnswer = null;
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
            nextVisualDescription = null;
            if (personState is not null)
            {
                personState.CurrentRecallPrompt = null;
                personState.CurrentRecallVisualObjectsDescription = null;
            }
            else
            {
                step.CurrentRecallPrompt = null;
                step.CurrentRecallVisualObjectsDescription = null;
            }
        }
        else
        {
            var usedStaticItem = staticConfig.Mode == StructuredLearningGenerationMode.Static
                ? PickStaticItem(staticConfig.StaticItems, verdict.ItemsMasteredSoFar)
                : null;
            nextMessage = usedStaticItem?.Question ?? turn?.Message ?? string.Empty;
            nextIllustrationId = usedStaticItem?.IllustrationId;
            // Static bank items now persist their own ground-truth scene
            // description (2026-10-26) — pass it along so the NEXT grading
            // call for this item can use the fast grade-only path too
            // (falls back to null only for items persisted before this
            // field existed, which still grade correctly, just slower).
            nextVisualDescription = usedStaticItem?.VisualObjectsDescription ?? (usedStaticItem is null ? turn?.VisualObjectsDescription : null);
            nextCorrectAnswer = usedStaticItem?.CorrectAnswer ?? (usedStaticItem is null ? turn?.CorrectAnswer : null);
            if (personState is not null)
            {
                personState.CurrentRecallPrompt = nextMessage;
                personState.CurrentRecallVisualObjectsDescription = nextVisualDescription;
            }
            else
            {
                step.CurrentRecallPrompt = nextMessage;
                step.CurrentRecallVisualObjectsDescription = nextVisualDescription;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return new RecallTurnResult
        {
            NextQuestion = nextMessage,
            VisualObjectsDescription = nextVisualDescription,
            IllustrationId = nextIllustrationId,
            CorrectAnswer = nextCorrectAnswer,
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

        // Completion bug fix (2026-10-27): SubmitAttemptAsync intentionally
        // stores an EMPTY CurrentRecallPrompt once the step is fully
        // complete (there's no "next item" left to ask) — but this method
        // used to treat ANY empty prompt as "never started yet" and
        // silently re-bootstrapped all the way back to item 0's question,
        // discarding the completion entirely the very next time anything
        // re-fetched state (a page reload, or the next voice session
        // asking "what's the current question" before the frontend had a
        // chance to react to the Complete flag from the attempt that just
        // finished). Check real mastery from the persisted attempts FIRST,
        // before ever touching/overwriting the stored prompt.
        var masteredScoresSoFar = await dbContext.StructuredLearningRecallAttempts
            .Where(a => a.StructuredLearningDocumentSectionId == sectionId && a.PersonId == personId)
            .Select(a => a.RecallScore)
            .ToListAsync(cancellationToken);
        var alreadyMasteredCount = masteredScoresSoFar.Count(RecallLoopGate.IsMastered);
        if (string.IsNullOrWhiteSpace(effectiveQuestion) && alreadyMasteredCount >= itemsRequired)
        {
            return new RecallTurnResult
            {
                NextQuestion = string.Empty,
                ItemsMastered = alreadyMasteredCount,
                ItemsRequired = itemsRequired,
                Complete = true
            };
        }

        if (string.IsNullOrWhiteSpace(effectiveQuestion))
        {
            var staticConfig = await LoadStaticConfigAsync(dbContext, sectionId, cancellationToken);
            var staticFirstItem = PickStaticItem(staticConfig.StaticItems, 0);
            string bootstrapMessage;
            string? bootstrapVisualDescription = null;
            Guid? bootstrapIllustrationId = null;
            if (staticConfig.Mode == StructuredLearningGenerationMode.Static && staticFirstItem is not null)
            {
                bootstrapMessage = staticFirstItem.Question;
                bootstrapIllustrationId = staticFirstItem.IllustrationId;
                bootstrapVisualDescription = staticFirstItem.VisualObjectsDescription;
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
                bootstrapVisualDescription = bootstrapAgentResult.Turn.VisualObjectsDescription;
            }

            if (personState is not null)
            {
                personState.CurrentRecallPrompt = bootstrapMessage;
                personState.CurrentRecallVisualObjectsDescription = bootstrapVisualDescription;
            }
            else
            {
                step.CurrentRecallPrompt = bootstrapMessage;
                step.CurrentRecallVisualObjectsDescription = bootstrapVisualDescription;
            }
            await dbContext.SaveChangesAsync(cancellationToken);

            return new RecallTurnResult
            {
                Started = true,
                NextQuestion = bootstrapMessage,
                VisualObjectsDescription = bootstrapVisualDescription,
                IllustrationId = bootstrapIllustrationId,
                CorrectAnswer = staticFirstItem?.CorrectAnswer,
                ItemsRequired = itemsRequired
            };
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

        // Static mode (2026-10-26): the currently-shown question's
        // illustration isn't persisted on step/personState (only the
        // question TEXT is) — it's fully determined by itemsMastered, so
        // re-derive it the same way SubmitAttemptAsync does rather than
        // adding a redundant column.
        Guid? currentIllustrationId = null;
        string? currentCorrectAnswer = null;
        if (await IsStaticModeAsync(dbContext, sectionId, cancellationToken))
        {
            var staticConfig = await LoadStaticConfigAsync(dbContext, sectionId, cancellationToken);
            var currentStaticItem = PickStaticItem(staticConfig.StaticItems, itemsMastered);
            currentIllustrationId = currentStaticItem?.IllustrationId;
            currentCorrectAnswer = currentStaticItem?.CorrectAnswer;
        }

        return new RecallTurnResult
        {
            NextQuestion = personState is not null ? personState.CurrentRecallPrompt ?? string.Empty : step.CurrentRecallPrompt ?? string.Empty,
            VisualObjectsDescription = personState is not null ? personState.CurrentRecallVisualObjectsDescription : step.CurrentRecallVisualObjectsDescription,
            IllustrationId = currentIllustrationId,
            CorrectAnswer = currentCorrectAnswer,
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

    /// <summary>One Static-mode Recall item: fixed question text plus its
    /// pre-generated card illustration (2026-10-26). <see cref="IllustrationId"/>
    /// is null when image generation isn't configured, the item has no
    /// visual scene to draw, or generation failed (best-effort — the
    /// question itself always still works without a picture).</summary>
    private sealed record StaticRecallItem(string Question, Guid? IllustrationId, string? VisualObjectsDescription = null, string? CorrectAnswer = null);

    /// <summary>Public, read-only shape of a Static Recall item for callers
    /// outside this engine (2026-10-27) — introduced for the voice Recall
    /// "whole-session" redesign (see <see cref="GetStaticItemsForVoiceSessionAsync"/>):
    /// the Realtime voice session now needs ALL of a section's items (and
    /// their correct answers) up front, to work through them one by one in
    /// a single continuous conversation instead of reconnecting per
    /// question.</summary>
    public sealed record VoiceRecallItem(string Question, Guid? IllustrationId, string? CorrectAnswer);

    /// <summary>Static-mode only (returns empty for Dynamic documents) —
    /// the full ordered list of this section's pre-generated Recall items,
    /// for the voice Agent to be told about ALL of them at session start.
    /// Order matches exactly what <see cref="PickStaticItem"/> uses during
    /// grading (0-based mastery count), so the Agent working through this
    /// list in order always matches what the real engine grades next.</summary>
    public async Task<IReadOnlyList<VoiceRecallItem>> GetStaticItemsForVoiceSessionAsync(
        HumanOsDbContext dbContext, Guid sectionId, CancellationToken cancellationToken)
    {
        var config = await LoadStaticConfigAsync(dbContext, sectionId, cancellationToken);
        return config.StaticItems.Select(i => new VoiceRecallItem(i.Question, i.IllustrationId, i.CorrectAnswer)).ToList();
    }

    private sealed record RecallStaticConfig(StructuredLearningGenerationMode Mode, List<StaticRecallItem> StaticItems);

    private static async Task<bool> IsStaticModeAsync(HumanOsDbContext dbContext, Guid sectionId, CancellationToken cancellationToken)
    {
        var mode = await dbContext.StructuredLearningDocumentSections
            .AsNoTracking()
            .Where(s => s.StructuredLearningDocumentSectionId == sectionId)
            .Select(s => s.Document.RecallGenerationMode)
            .FirstOrDefaultAsync(cancellationToken);
        return mode == StructuredLearningGenerationMode.Static;
    }

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

        var items = DeserializeStaticItems(row.StaticRecallItemsJson);
        return new RecallStaticConfig(StructuredLearningGenerationMode.Static, items);
    }

    /// <summary>Deserializes <see cref="StructuredLearningDocumentSection.StaticRecallItemsJson"/>.
    /// Supports BOTH shapes for backward compatibility: the current
    /// <c>[{"Question":"...","IllustrationId":"..."}]</c> object array
    /// (2026-10-26 onward), and the OLD plain <c>["...","..."]</c> string
    /// array persisted before cards existed — old sections keep working
    /// with no illustration until Recall is regenerated for them.</summary>
    private static List<StaticRecallItem> DeserializeStaticItems(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            var objectItems = System.Text.Json.JsonSerializer.Deserialize<List<StaticRecallItemJson>>(json);
            if (objectItems is not null)
            {
                return objectItems.Select(i => new StaticRecallItem(i.Question, i.IllustrationId, i.VisualObjectsDescription, i.CorrectAnswer)).ToList();
            }
        }
        catch (System.Text.Json.JsonException)
        {
            // Fall through to the legacy plain-string-array shape below.
        }

        var legacyItems = System.Text.Json.JsonSerializer.Deserialize<List<string>>(json) ?? [];
        return legacyItems.Select(q => new StaticRecallItem(q, null)).ToList();
    }

    private sealed class StaticRecallItemJson
    {
        public string Question { get; set; } = string.Empty;
        public Guid? IllustrationId { get; set; }

        /// <summary>Ground-truth scene description used to grade student
        /// answers against this static item (2026-10-26) — previously
        /// never persisted, which forced the grading agent to re-derive the
        /// scene from scratch (or grade blind) on every single attempt.
        /// Null for items persisted before this field existed; grading
        /// falls back to the older blind behavior for those.</summary>
        public string? VisualObjectsDescription { get; set; }

        /// <summary>Short literal correct answer (e.g. "8") persisted
        /// alongside the scene description (2026-10-26) — enables INSTANT
        /// deterministic grading (no LLM call at all) for both typed
        /// answers and the Realtime voice Agent's own immediate reaction.
        /// Null for items persisted before this field existed.</summary>
        public string? CorrectAnswer { get; set; }
    }

    /// <summary>Picks the item the student is CURRENTLY on (0-based
    /// <paramref name="itemIndex"/> = how many items they've already
    /// mastered this section) from the persisted Static bank — null once
    /// they've gone past the last persisted item (caller falls back to
    /// Dynamic generation or treats the step as complete).</summary>
    private static StaticRecallItem? PickStaticItem(IReadOnlyList<StaticRecallItem> items, int itemIndex) =>
        itemIndex >= 0 && itemIndex < items.Count ? items[itemIndex] : null;

    /// <summary>Eager Static-mode generation (2026-10-06, cards added
    /// 2026-10-26) — called from <see cref="StructuredLearningPedagogyService"/>
    /// right after a section's pedagogy finishes (i.e. during the SAME
    /// final Engram-generation pass that produces everything else for the
    /// section), so up-to-3 standalone Recall questions — EACH with its own
    /// real pre-generated card illustration, exactly like every other
    /// Structured Learning illustration (Hypothesis/Teaching/DeepDive) —
    /// are persisted in SQL BEFORE any student opens the course. Applies to
    /// every document regardless of PedagogicalAudience/age (unlike the
    /// YoungLearner-only Engram Card pipeline): the realtime agent then just
    /// shows the student this fixed card + asks the fixed question tied to
    /// it, instead of generating anything live. Grading of each student's
    /// own answer still happens live via <see cref="_agent"/> at runtime
    /// (free-text grading is inherently per-student) — only the
    /// QUESTION+CARD are fixed/shared. No-op for Dynamic documents.
    /// Best-effort per item: a failed image generation never blocks the
    /// question itself (falls back to no illustration).</summary>
    public async Task EnsureStaticItemsAsync(
        HumanOsDbContext dbContext,
        Guid sectionId,
        CancellationToken cancellationToken = default,
        byte[]? referenceImageBytes = null,
        string? referenceImageUsageInstruction = null)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        var row = await dbContext.StructuredLearningDocumentSections
            .AsNoTracking()
            .Where(s => s.StructuredLearningDocumentSectionId == sectionId)
            .Select(s => new
            {
                s.Document.RecallGenerationMode,
                s.Document.RecallItemsRequired,
                s.Document.PedagogicalAudience,
                s.TenantId,
                s.StructuredLearningDocumentId
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (row is null || row.RecallGenerationMode != StructuredLearningGenerationMode.Static)
        {
            return;
        }

        var count = Math.Clamp(row.RecallItemsRequired ?? RecallLoopGate.DefaultItemsRequiredToAdvance, 1, 3);
        var context = await LoadSectionContextAsync(dbContext, sectionId, cancellationToken);
        var documentId = await ResolveDocumentIdAsync(dbContext, sectionId, cancellationToken);
        var styleGuidance = row.PedagogicalAudience == "YoungLearner" ? KidsCardIllustrationStyle : RealisticCardIllustrationStyle;

        var questionsSoFar = new List<string>();
        var items = new List<StaticRecallItem>();
        for (var i = 0; i < count; i++)
        {
            var prompt = BuildTurnPrompt(context, alreadyAsked: questionsSoFar, currentQuestion: null, studentResponse: null);
            var agentResult = await _agent.RunTurnAsync(prompt, cancellationToken);
            await _usageService.RecordTextUsageAsync(
                null, documentId, sectionId, null,
                agentResult.ModelName, agentResult.InputTokens, agentResult.OutputTokens, cancellationToken, stepType: "Recall");
            var question = agentResult.Turn.Message;
            questionsSoFar.Add(question);

            Guid? illustrationId = null;
            var visualDescription = agentResult.Turn.VisualObjectsDescription;
            var correctAnswer = agentResult.Turn.CorrectAnswer;
            if (!string.IsNullOrWhiteSpace(visualDescription) && _imageService.IsConfigured)
            {
                try
                {
                    var fullPrompt = $"{styleGuidance} Scene: {visualDescription} " +
                        "If this scene involves counting discrete objects, you MUST draw the actual objects " +
                        "described above (e.g. real frogs, real blocks, real apples — whatever the scene says) " +
                        "with their real appearance and detail. Do NOT simplify or substitute them with plain " +
                        "colored squares, dots, icons, or abstract shapes, even to make counting easier. Render " +
                        "EXACTLY the stated number of these real objects — no more, no fewer — arranged in a " +
                        "single clear row or a simple non-overlapping grid, evenly spaced, never stacked, piled, " +
                        "or in a pyramid/triangle shape, so every single object can be individually counted at a glance.";
                    if (referenceImageBytes is { Length: > 0 })
                    {
                        // Teacher-character reference (2026-10-27, explicit user request): the
                        // uploaded reference isn't a background logo here — it's the course's
                        // recurring teacher/mascot character, and it must visibly appear IN the
                        // scene (e.g. standing beside the objects, pointing at them while asking
                        // the question), not just be vaguely "used as guidance" and dropped, which
                        // is what the generic reference instruction was doing before this fix.
                        fullPrompt += " The attached reference image shows this course's teacher character. " +
                            "Draw that exact character (same appearance, colors, and outfit as the reference) " +
                            "actually present in this scene — for example standing or kneeling next to the " +
                            "objects being counted, pointing at them as if asking this very question aloud — " +
                            "while still drawing every single countable object fully visible, clearly separated, " +
                            "and never obscured, covered, or replaced by the character.";
                        if (!string.IsNullOrWhiteSpace(referenceImageUsageInstruction))
                        {
                            fullPrompt += $" {referenceImageUsageInstruction}";
                        }
                    }

                    // Designer-uploaded reference image (e.g. a company logo) for this
                    // document's restart/regeneration (2026-10-27) — previously ONLY the
                    // Teaching/DeepDive illustration paths honored this upload; Static
                    // Recall cards always ignored it and generated from plain text alone,
                    // which is why a designer's "use this exact logo" instruction never
                    // showed up on Recall cards. Mirrors GenerateFromReferenceAsync's use
                    // elsewhere in StructuredLearningPedagogyService.
                    var generatedImage = referenceImageBytes is { Length: > 0 }
                        ? await _imageService.GenerateFromReferenceAsync(fullPrompt, referenceImageBytes, cancellationToken)
                        : await _imageService.GenerateAsync(fullPrompt, null, cancellationToken);

                    // Ground truth must match what was ACTUALLY drawn, not just what the
                    // generation prompt asked for (2026-10-26) — image generators frequently
                    // miss the exact requested count/arrangement, which silently made
                    // CorrectAnswer wrong even when the question/picture themselves looked
                    // fine. Look at the real rendered image and use IT to resolve the answer.
                    // Verified against the UN-watermarked image, before the brand mark below
                    // is stamped into the corner, so the object count the agent "sees" always
                    // matches the scene the prompt asked for.
                    var verifiedAnswer = await _agent.VerifyCorrectAnswerFromImageAsync(question, generatedImage.ImageBytes, "image/png", cancellationToken);
                    if (!string.IsNullOrWhiteSpace(verifiedAnswer))
                    {
                        correctAnswer = verifiedAnswer;
                    }

                    // Every Static Recall card is stamped with the EngramsIQ brand mark
                    // (2026-10-28) — same mark shown at the end of the Family
                    // course-creation wizard — before being persisted to storage.
                    var watermarkedImageBytes = _watermarkService.ApplyWatermark(generatedImage.ImageBytes);
                    using var imageStream = watermarkedImageBytes.ToStream();
                    var imagePath = await _illustrationStorage.UploadCardIllustrationAsync(
                        row.TenantId,
                        row.StructuredLearningDocumentId,
                        sectionId,
                        i,
                        imageStream,
                        cancellationToken: cancellationToken);

                    var illustrationEntity = new Models.Capabilities.Studio.StructuredLearningIllustration
                    {
                        StructuredLearningIllustrationId = Guid.NewGuid(),
                        StructuredLearningDocumentSectionId = sectionId,
                        StoragePath = imagePath,
                        Prompt = fullPrompt,
                        Caption = question,
                        Purpose = Models.Capabilities.Studio.StructuredLearningIllustrationPurpose.RecallCard,
                        ImageModel = generatedImage.ImageModel,
                        Width = generatedImage.Width,
                        Height = generatedImage.Height,
                        CreatedDate = DateTime.UtcNow
                    };
                    dbContext.StructuredLearningIllustrations.Add(illustrationEntity);
                    illustrationId = illustrationEntity.StructuredLearningIllustrationId;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // Best-effort — see method doc comment. Leave
                    // illustrationId null; the question still works with no
                    // picture, same as a document with image generation
                    // unconfigured entirely.
                }
            }

            items.Add(new StaticRecallItem(question, illustrationId, visualDescription, correctAnswer));
        }

        var itemsJson = System.Text.Json.JsonSerializer.Serialize(
            items.Select(i => new StaticRecallItemJson { Question = i.Question, IllustrationId = i.IllustrationId, VisualObjectsDescription = i.VisualObjectsDescription, CorrectAnswer = i.CorrectAnswer }).ToList());

        await dbContext.StructuredLearningDocumentSections
            .Where(s => s.StructuredLearningDocumentSectionId == sectionId)
            .ExecuteUpdateAsync(
                s => s.SetProperty(x => x.StaticRecallItemsJson, itemsJson),
                cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
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

    /// <summary>Product decision (2026-10-27, explicit user request): young
    /// children must never be cut off / regressed to Teaching for
    /// "exhausting attempts" on a single Recall item — they just keep
    /// retrying the same item, however many tries it takes, until they
    /// master it. Returns <see cref="int.MaxValue"/> for
    /// PedagogicalAudience=YoungLearner so <see cref="RecallLoopGate.Evaluate"/>
    /// can never mark the item as failed; every other audience keeps the
    /// normal <see cref="RecallLoopGate.MaxAttemptsPerItem"/> cap.</summary>
    private static async Task<int> ResolveMaxAttemptsPerItemAsync(HumanOsDbContext dbContext, Guid sectionId, CancellationToken cancellationToken)
    {
        var audience = await dbContext.StructuredLearningDocumentSections
            .AsNoTracking()
            .Where(s => s.StructuredLearningDocumentSectionId == sectionId)
            .Select(s => s.Document.PedagogicalAudience)
            .FirstOrDefaultAsync(cancellationToken);

        return audience == "YoungLearner" ? int.MaxValue : RecallLoopGate.MaxAttemptsPerItem;
    }

    private sealed class SectionContext
    {
        public string ChapterTitle { get; set; } = string.Empty;
        public string SubchapterTitle { get; set; } = string.Empty;
        public string TeachingContent { get; set; } = string.Empty;
        public string? PedagogicalAudience { get; set; }
    }

    private static async Task<SectionContext> LoadSectionContextAsync(HumanOsDbContext dbContext, Guid sectionId, CancellationToken cancellationToken)
    {
        var section = await dbContext.StructuredLearningDocumentSections
            .AsNoTracking()
            .Include(s => s.Steps)
            .Include(s => s.Document)
            .FirstOrDefaultAsync(s => s.StructuredLearningDocumentSectionId == sectionId, cancellationToken)
            ?? throw new InvalidOperationException($"StructuredLearningDocumentSection {sectionId} not found.");

        var teachingStep = section.Steps.FirstOrDefault(s => s.StepType == StructuredLearningStepType.Teaching);

        return new SectionContext
        {
            ChapterTitle = section.ChapterTitle,
            SubchapterTitle = section.SubchapterTitle,
            TeachingContent = BuildFullTeachingContent(teachingStep) ?? teachingStep?.Content ?? section.Text,
            PedagogicalAudience = section.Document?.PedagogicalAudience
        };
    }

    /// <summary>Recall questions were being written off the short "LEARN" summary
    /// paragraph (<see cref="StructuredLearningSectionStep.Content"/>) alone, which
    /// doesn't carry the professor's actual verbatim narration — the agent would then
    /// hallucinate ungrounded details (e.g. assuming a fixed left/right layout of two
    /// formulas that was never stated anywhere) instead of asking about real content
    /// (2026-11-07, "pasarle al agente todo el texto del video"). When the Teaching
    /// step has a per-scene "Scenes" array (StepDataJson), concatenates every scene's
    /// verbatim Text (in order) and appends it after the short summary, so the Recall
    /// agent can ground questions in anything actually said in the lecture, not just
    /// the compressed synopsis. Returns null (falls back to the short summary) if
    /// there's no StepDataJson or no Scenes — e.g. non-video-sourced sections.</summary>
    private static string? BuildFullTeachingContent(StructuredLearningSectionStep? teachingStep)
    {
        if (teachingStep is null || string.IsNullOrWhiteSpace(teachingStep.StepDataJson))
        {
            return null;
        }

        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(teachingStep.StepDataJson);
            if (!doc.RootElement.TryGetProperty("Scenes", out var scenesProp) || scenesProp.ValueKind != System.Text.Json.JsonValueKind.Array)
            {
                return null;
            }

            var sceneTexts = new List<string>();
            foreach (var sceneEl in scenesProp.EnumerateArray())
            {
                if (sceneEl.TryGetProperty("Text", out var textProp) && textProp.ValueKind == System.Text.Json.JsonValueKind.String)
                {
                    var text = textProp.GetString();
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        sceneTexts.Add(text);
                    }
                }
            }

            if (sceneTexts.Count == 0)
            {
                return null;
            }

            var builder = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(teachingStep.Content))
            {
                builder.AppendLine(teachingStep.Content);
                builder.AppendLine();
            }
            builder.AppendLine("FULL LECTURE NARRATION (verbatim, in order — use this as the real source of truth, " +
                "not just the summary above; ask about ANY part of it, chosen RANDOMLY, not only the summary or " +
                "only the beginning):");
            foreach (var text in sceneTexts)
            {
                builder.AppendLine($"- {text}");
            }

            return builder.ToString();
        }
        catch
        {
            // Malformed/unexpected StepDataJson shape — fall back to the short summary
            // rather than failing Recall question generation outright.
            return null;
        }
    }

    private static string BuildTurnPrompt(
        SectionContext context, IReadOnlyList<string> alreadyAsked, string? currentQuestion, string? studentResponse, string? currentVisualDescription = null, string? currentCorrectAnswer = null)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Chapter: {context.ChapterTitle}");
        builder.AppendLine($"Section: {context.SubchapterTitle}");
        builder.AppendLine("SECTION CONTENT:");
        builder.AppendLine(context.TeachingContent);
        builder.AppendLine();

        // The base Instructions always mandate a picture-grounded question
        // (written for young, non-reading children) — wrong for adult/
        // professional (Standard) audiences, where it was causing the agent
        // to hallucinate ungrounded spatial details about a picture that was
        // never actually described (e.g. "f(t) on the left, df/dt on the
        // right" for a math lecture with no such layout stated anywhere).
        // Override that default here per-audience (2026-11-07, "no asumir
        // que tenemos una imagen... pasarle todo el texto del video").
        if (!string.Equals(context.PedagogicalAudience, "YoungLearner", StringComparison.OrdinalIgnoreCase))
        {
            builder.AppendLine(
                "AUDIENCE OVERRIDE — this is an adult/professional learner, NOT a young child: " +
                "ignore the base instructions' picture-grounding requirement entirely. Do NOT invent any picture/" +
                "scene, do NOT fill VisualObjectsDescription (leave it null), and never mention \"the picture\", " +
                "\"left\", \"right\", \"shown\", or any other visual-layout wording. Instead, ask a plain, " +
                "direct, text-only retrieval question grounded ONLY in facts explicitly stated in the FULL " +
                "LECTURE NARRATION above — pick a DIFFERENT, RANDOM part of that narration each turn (not just " +
                "the opening sentence or the short summary), so questions vary across the whole lecture.");
            builder.AppendLine();
        }

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
            if (!string.IsNullOrWhiteSpace(currentVisualDescription))
            {
                // This question referred the student to a generated picture
                // you cannot see yourself — this is the GROUND TRUTH of
                // exactly what that picture shows (counts, colors,
                // relative sizes, etc.). Grade the student's answer against
                // THESE facts, never guess/assume anything about the image
                // beyond what is written here.
                builder.AppendLine();
                builder.AppendLine("GROUND-TRUTH DESCRIPTION OF THE PICTURE SHOWN FOR THIS QUESTION (grade against these exact facts):");
                builder.AppendLine(currentVisualDescription);
            }
            if (!string.IsNullOrWhiteSpace(currentCorrectAnswer))
            {
                builder.AppendLine();
                builder.AppendLine($"KNOWN CORRECT ANSWER (for reference — still verify from the ground truth above): {currentCorrectAnswer}");
            }
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
