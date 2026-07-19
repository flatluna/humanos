using HumanOS.Agents.Runtime;
using HumanOS.Agents.Studio;
using Microsoft.Agents.AI.Workflows;

namespace HumanOS.Agentic.Runtime;

/// <summary>
/// Shared logic for the 4 "asking" executors (Recall/Prediction/
/// LearnerProduction/Reflection) — each sets the session's current
/// <see cref="RuntimeStage"/>, records an auditable transition, persists
/// the full <see cref="RuntimeSessionState"/> so it can be recovered when
/// the paused run resumes, and builds the stub <see cref="EvidenceRequest"/>
/// that pauses the Workflow until the learner responds.
/// </summary>
internal static class RuntimeStageTransitionHelper
{
    public static async ValueTask<EvidenceRequest> RequestEvidenceAsync(
        RuntimeSessionState state,
        RuntimeStage stage,
        string prompt,
        IWorkflowContext context,
        CancellationToken cancellationToken,
        int? attemptNumber = null,
        int? totalAttempts = null,
        int? lastAccuracyPercentage = null)
    {
        state.Session.Stage = stage;
        state.History.Add(new RuntimeStageTransition { Stage = stage });

        await context.QueueStateUpdateAsync(
            state.Session.RuntimeSessionId.ToString(),
            state,
            scopeName: RuntimeSessionWorkflowFactory.SessionStateScope);

        // Fixed 2026-07-16: chapter progress is populated ONLY for the two
        // stages that pause WITHIN the Chapters loop (ChapterRecall/
        // ChapterPrediction) — every other evidence-pausing stage (module-
        // wide RecallRequired/PredictionRequired/LearnerProduction/
        // Reflection) has no single "current chapter" to report.
        var isChapterStage = stage is RuntimeStage.ChapterRecall or RuntimeStage.ChapterPrediction;
        var contract = state.Session.Contract;

        return new EvidenceRequest
        {
            RuntimeSessionId = state.Session.RuntimeSessionId,
            CapabilityModuleId = state.Session.CapabilityModuleId,
            Stage = stage,
            Prompt = prompt,
            CapabilityTitle = contract.CapabilityTitle,
            CapabilityCode = contract.CapabilityCode,
            CapabilityId = contract.CapabilityId,
            AllChapterTitles = [.. contract.Chapters.Select(c => c.Title)],
            ChapterIndex = isChapterStage ? state.CurrentChapterIndex : null,
            TotalChapters = isChapterStage ? contract.Chapters.Count : null,
            ChapterTitle = isChapterStage ? contract.Chapters[state.CurrentChapterIndex].Title : null,
            AttemptNumber = attemptNumber,
            TotalAttempts = totalAttempts,
            LastAccuracyPercentage = lastAccuracyPercentage
        };
    }

    /// <summary>Prepends a queued <see cref="RuntimeSessionState.PendingRecallReveal"/>
    /// to a turn's message and clears it (fixed 2026-07-17) — called by
    /// whichever executor presents the FIRST turn after a Recall retry
    /// budget was exhausted without success
    /// (<c>PredictionRequiredExecutor</c>/<c>ChapterTeachingExecutor</c>/
    /// <c>RecallRequiredExecutor</c>). A no-op (returns
    /// <paramref name="message"/> unchanged) when nothing is pending.</summary>
    public static string ConsumePendingRecallReveal(RuntimeSessionState state, string message)
    {
        if (state.PendingRecallReveal is not { } reveal)
        {
            return message;
        }

        state.PendingRecallReveal = null;
        return $"{reveal}\n\n{message}";
    }
}

/// <summary>
/// Shared logic for the 4 "evidence received" executors — recovers the
/// <see cref="RuntimeSessionState"/> persisted right before the pause (keyed
/// by <see cref="EvidenceSubmission.RuntimeSessionId"/>) and appends the
/// submitted <see cref="StudentEvidence"/> to the session's evidence list.
/// </summary>
internal static class RuntimeEvidenceReceiver
{
    public static async ValueTask<RuntimeSessionState> ReceiveAsync(
        EvidenceSubmission submission,
        IWorkflowContext context,
        CancellationToken cancellationToken)
    {
        var state = await context.ReadStateAsync<RuntimeSessionState>(
            submission.RuntimeSessionId.ToString(),
            scopeName: RuntimeSessionWorkflowFactory.SessionStateScope);

        if (state is null)
        {
            throw new ArgumentException(
                $"No pending Runtime session found for id '{submission.RuntimeSessionId}'.");
        }

        state.Session.Evidence.Add(submission.Evidence);
        state.PendingForceAdvance = submission.ForceAdvance;
        return state;
    }
}

/// <summary>
/// Entry point of the Runtime Workflow graph — initializes
/// <see cref="RuntimeSessionState"/> from the freshly-created
/// <see cref="RuntimeSession"/> (already carrying its Studio-approved
/// <see cref="RuntimePedagogicalContract"/>), then PAUSES (via the
/// dedicated <c>Introduction-Ack</c> port, fixed 2026-07-16) with the
/// Tutor Agent's warm, grounded introduction to the module — BEFORE any
/// Recall attempt. Closes a real pedagogical gap: a total beginner has
/// nothing to unassisted-retrieve on their very first module, so this
/// turn orients them (what the module covers) and reassures them that not
/// knowing yet is expected, rather than opening cold with "recall what
/// you know". Never produces <see cref="StudentEvidence"/> — pure
/// presentation, same rationale as <see cref="InstructionExecutor"/>.
/// </summary>
internal sealed class ModuleStartedExecutor : Executor<RuntimeSession, IntroductionPresentation>
{
    private readonly TutorAgent _tutorAgent;

    public ModuleStartedExecutor(TutorAgent tutorAgent) : base(nameof(ModuleStartedExecutor))
    {
        _tutorAgent = tutorAgent;
    }

    public override async ValueTask<IntroductionPresentation> HandleAsync(
        RuntimeSession session,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        session.Stage = RuntimeStage.ModuleStarted;

        var state = new RuntimeSessionState
        {
            Session = session,
            History = [new RuntimeStageTransition { Stage = RuntimeStage.ModuleStarted }]
        };

        var turnContext = TutorTurnContextBuilder.Build(state, RuntimeStage.ModuleStarted);
        var result = await _tutorAgent.RespondAsync(turnContext, cancellationToken);

        await context.QueueStateUpdateAsync(
            state.Session.RuntimeSessionId.ToString(),
            state,
            scopeName: RuntimeSessionWorkflowFactory.SessionStateScope);

        return new IntroductionPresentation
        {
            RuntimeSessionId = state.Session.RuntimeSessionId,
            IntroductionText = result.Response,
            CapabilityTitle = state.Session.Contract.CapabilityTitle,
            CapabilityCode = state.Session.Contract.CapabilityCode,
            CapabilityId = state.Session.Contract.CapabilityId,
            CapabilityModuleId = state.Session.CapabilityModuleId
        };
    }
}

/// <summary>
/// Recovers the <see cref="RuntimeSessionState"/> persisted right before
/// the module-introduction pause once the learner acknowledges it (fixed
/// 2026-07-16) — mirrors <see cref="InstructionAckReceivedExecutor"/> but
/// for the evidence-free introduction acknowledgement.
/// </summary>
internal sealed class IntroductionAckReceivedExecutor : Executor<IntroductionAcknowledgement, RuntimeSessionState>
{
    public IntroductionAckReceivedExecutor() : base(nameof(IntroductionAckReceivedExecutor))
    {
    }

    public override async ValueTask<RuntimeSessionState> HandleAsync(
        IntroductionAcknowledgement ack,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        var state = await context.ReadStateAsync<RuntimeSessionState>(
            ack.RuntimeSessionId.ToString(),
            scopeName: RuntimeSessionWorkflowFactory.SessionStateScope);

        if (state is null)
        {
            throw new ArgumentException(
                $"No pending Runtime session found for id '{ack.RuntimeSessionId}'.");
        }

        return state;
    }
}

/// <summary>
/// Requires the learner to attempt unaided/cued retrieval BEFORE any
/// instruction, example, or AI assistance — implements the module's
/// <see cref="RuntimePedagogicalContract.RecallRequirement"/>. The prompt
/// text is the Tutor Agent's real, adaptive phrasing (fixed Paso 9,
/// 2026-07-15) — the Tutor never decides WHETHER to ask, only HOW to say
/// it, per <see cref="TutorTurnContextBuilder"/>'s stage-gated permissions.
/// </summary>
internal sealed class RecallRequiredExecutor : Executor<RuntimeSessionState, EvidenceRequest>
{
    private readonly TutorAgent _tutorAgent;

    public RecallRequiredExecutor(TutorAgent tutorAgent) : base(nameof(RecallRequiredExecutor))
    {
        _tutorAgent = tutorAgent;
    }

    public override async ValueTask<EvidenceRequest> HandleAsync(
        RuntimeSessionState state,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        var turnContext = TutorTurnContextBuilder.Build(state, RuntimeStage.RecallRequired);
        var result = await _tutorAgent.RespondAsync(turnContext, cancellationToken);
        var message = RuntimeStageTransitionHelper.ConsumePendingRecallReveal(state, result.Response);

        return await RuntimeStageTransitionHelper.RequestEvidenceAsync(
            state,
            RuntimeStage.RecallRequired,
            message,
            context,
            cancellationToken,
            attemptNumber: state.RecallAttempt + 1,
            totalAttempts: RuntimeSessionWorkflowFactory.MaxRecallRetries + 1);
    }
}

internal sealed class RecallEvidenceReceivedExecutor : Executor<EvidenceSubmission, RuntimeSessionState>
{
    public RecallEvidenceReceivedExecutor() : base(nameof(RecallEvidenceReceivedExecutor))
    {
    }

    public override ValueTask<RuntimeSessionState> HandleAsync(
        EvidenceSubmission submission,
        IWorkflowContext context,
        CancellationToken cancellationToken = default) =>
        RuntimeEvidenceReceiver.ReceiveAsync(submission, context, cancellationToken);
}

/// <summary>
/// Lightweight completeness check for the Recall attempt just submitted
/// (fixed 2026-07-16 — implements iterative retrieval practice per
/// explicit user request, see <see cref="RecallCheckResult"/>). Calls
/// <see cref="TutorAgent.CheckRecallAsync"/> and yields a
/// <see cref="RecallCheckOutcome"/> for the conditional
/// Sufficient/Retry routing in <see cref="RuntimeSessionWorkflowFactory"/>.
/// </summary>
internal sealed class RecallCheckExecutor : Executor<RuntimeSessionState, RecallCheckOutcome>
{
    private readonly TutorAgent _tutorAgent;

    public RecallCheckExecutor(TutorAgent tutorAgent) : base(nameof(RecallCheckExecutor))
    {
        _tutorAgent = tutorAgent;
    }

    public override async ValueTask<RecallCheckOutcome> HandleAsync(
        RuntimeSessionState state,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        // Fixed 2026-07-17: the learner's own "continuar de todas formas"
        // escape hatch — bypass the Tutor check entirely, independent of
        // the MaxRecallRetries budget, when they explicitly chose to move
        // on rather than retry again.
        if (state.PendingForceAdvance)
        {
            state.PendingForceAdvance = false;
            return new RecallCheckOutcome
            {
                State = state,
                Result = new RecallCheckResult { IsSufficient = true, FollowUpPrompt = string.Empty }
            };
        }

        RecallCheckResult result;
        try
        {
            result = await _tutorAgent.CheckRecallAsync(state, cancellationToken);
        }
        catch (InvalidOperationException)
        {
            // A technical failure in this lightweight check must never
            // block the learner's progress — default to sufficient and
            // let the session continue (unlike the formal Assessment,
            // this check has no TargetMetric stakes to protect).
            result = new RecallCheckResult { IsSufficient = true, FollowUpPrompt = string.Empty };
        }

        // Fixed 2026-07-17 (explicit user request: "llegar a un punto
        // donde el agente simplemente le da la respuesta correcta cuando
        // ya llevamos un x numero de iteraciones") — the retry budget is
        // about to be exhausted WITHOUT a sufficient attempt: instead of
        // silently moving on, queue a deterministic reveal of the real
        // source content (no extra LLM call) for the NEXT turn to prepend.
        // Fixed AGAIN same day: use the Chapters' own clean TeachingContent
        // (never embeds Recall/Prediction/Restrictions instructions) when
        // available, NOT the legacy whole ModuleScript — that field is the
        // ORIGINAL full activity script (still includes its own embedded
        // "RECALL inicial/Predicción breve/Restricciones/Tarea principal"
        // structure from before Chapters existed), so revealing it
        // verbatim recreated the exact "wall of text" problem this whole
        // fix was meant to solve. ModuleScript is only used as a fallback
        // for legacy modules published before Chapters shipped.
        if (result.IsGenuineAttempt && !result.IsSufficient &&
            state.RecallAttempt >= RuntimeSessionWorkflowFactory.MaxRecallRetries)
        {
            var contract = state.Session.Contract;
            var revealSource = contract.Chapters.Count > 0
                ? string.Join("\n\n", contract.Chapters.Select(c => c.TeachingContent))
                : contract.ModuleScript;
            state.PendingRecallReveal = RecallRevealBuilder.Build(revealSource);
        }

        return new RecallCheckOutcome { State = state, Result = result };
    }
}

/// <summary>
/// Retry branch (fixed 2026-07-16) — reached when the Recall check found
/// the attempt insufficient and the retry budget
/// (<see cref="RuntimeSessionWorkflowFactory.MaxRecallRetries"/>) is not
/// yet exhausted. Increments <see cref="RuntimeSessionState.RecallAttempt"/>
/// and re-pauses at <see cref="RuntimeStage.RecallRequired"/> with the
/// check's own answer-free Socratic follow-up as the next prompt — never
/// silently advances past an incomplete retrieval attempt while retries
/// remain.
/// </summary>
internal sealed class RecallRetryExecutor : Executor<RecallCheckOutcome, EvidenceRequest>
{
    public RecallRetryExecutor() : base(nameof(RecallRetryExecutor))
    {
    }

    public override async ValueTask<EvidenceRequest> HandleAsync(
        RecallCheckOutcome outcome,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        // Fixed 2026-07-17: a genuine clarifying question never costs the
        // learner a retrieval-practice attempt — only a real (if
        // incomplete) recall attempt consumes the bounded budget.
        if (outcome.Result.IsGenuineAttempt)
        {
            outcome.State.RecallAttempt++;
        }

        return await RuntimeStageTransitionHelper.RequestEvidenceAsync(
            outcome.State,
            RuntimeStage.RecallRequired,
            outcome.Result.FollowUpPrompt,
            context,
            cancellationToken,
            attemptNumber: outcome.State.RecallAttempt + 1,
            totalAttempts: RuntimeSessionWorkflowFactory.MaxRecallRetries + 1,
            lastAccuracyPercentage: outcome.Result.IsGenuineAttempt ? outcome.Result.AccuracyPercentage : null);
    }
}

internal sealed class RecallRetryEvidenceReceivedExecutor : Executor<EvidenceSubmission, RuntimeSessionState>
{
    public RecallRetryEvidenceReceivedExecutor() : base(nameof(RecallRetryEvidenceReceivedExecutor))
    {
    }

    public override ValueTask<RuntimeSessionState> HandleAsync(
        EvidenceSubmission submission,
        IWorkflowContext context,
        CancellationToken cancellationToken = default) =>
        RuntimeEvidenceReceiver.ReceiveAsync(submission, context, cancellationToken);
}

/// <summary>
/// Requires the learner to commit to a prediction/hypothesis BEFORE seeing
/// the real content or answer (neuroscience principle P1 — prediction
/// error — already used by Studio's InstructorAgent). The prompt text is
/// the Tutor Agent's real, adaptive phrasing (fixed Paso 9, 2026-07-15).
/// Reached only via the Sufficient branch of <see cref="RecallCheckOutcome"/>
/// (fixed 2026-07-16) — unwraps <c>.State</c>, same "downstream unwraps the
/// wrapper type" pattern used for <see cref="ReflectionExecutor"/>.
/// </summary>
internal sealed class PredictionRequiredExecutor : Executor<RecallCheckOutcome, EvidenceRequest>
{
    private readonly TutorAgent _tutorAgent;

    public PredictionRequiredExecutor(TutorAgent tutorAgent) : base(nameof(PredictionRequiredExecutor))
    {
        _tutorAgent = tutorAgent;
    }

    public override async ValueTask<EvidenceRequest> HandleAsync(
        RecallCheckOutcome outcome,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        var turnContext = TutorTurnContextBuilder.Build(outcome.State, RuntimeStage.PredictionRequired);
        var result = await _tutorAgent.RespondAsync(turnContext, cancellationToken);
        var message = RuntimeStageTransitionHelper.ConsumePendingRecallReveal(outcome.State, result.Response);

        return await RuntimeStageTransitionHelper.RequestEvidenceAsync(
            outcome.State, RuntimeStage.PredictionRequired, message, context, cancellationToken);
    }
}

internal sealed class PredictionEvidenceReceivedExecutor : Executor<EvidenceSubmission, RuntimeSessionState>
{
    public PredictionEvidenceReceivedExecutor() : base(nameof(PredictionEvidenceReceivedExecutor))
    {
    }

    public override ValueTask<RuntimeSessionState> HandleAsync(
        EvidenceSubmission submission,
        IWorkflowContext context,
        CancellationToken cancellationToken = default) =>
        RuntimeEvidenceReceiver.ReceiveAsync(submission, context, cancellationToken);
}

/// <summary>
/// Presents content/explanation — REAL (fixed Paso 9, 2026-07-15): calls
/// the Tutor Agent to adaptively phrase the module's
/// <see cref="RuntimePedagogicalContract.ModuleScript"/>, then PAUSES
/// (via the dedicated <c>Instruction-Ack</c> port) until the learner
/// acknowledges having received it. Never produces <see cref="StudentEvidence"/> —
/// pure presentation is not evidence of anything by itself (Memory Paradox
/// principle: consumption is not production).
/// </summary>
internal sealed class InstructionExecutor : Executor<RuntimeSessionState, InstructionPresentation>
{
    private readonly TutorAgent _tutorAgent;

    public InstructionExecutor(TutorAgent tutorAgent) : base(nameof(InstructionExecutor))
    {
        _tutorAgent = tutorAgent;
    }

    public override async ValueTask<InstructionPresentation> HandleAsync(
        RuntimeSessionState state,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        state.Session.Stage = RuntimeStage.Instruction;
        state.History.Add(new RuntimeStageTransition { Stage = RuntimeStage.Instruction });

        var turnContext = TutorTurnContextBuilder.Build(
            state, RuntimeStage.Instruction, moduleScript: state.Session.Contract.ModuleScript);
        var result = await _tutorAgent.RespondAsync(turnContext, cancellationToken);

        await context.QueueStateUpdateAsync(
            state.Session.RuntimeSessionId.ToString(),
            state,
            scopeName: RuntimeSessionWorkflowFactory.SessionStateScope);

        return new InstructionPresentation
        {
            RuntimeSessionId = state.Session.RuntimeSessionId,
            Content = result.Response,
            CapabilityTitle = state.Session.Contract.CapabilityTitle,
            CapabilityCode = state.Session.Contract.CapabilityCode,
            CapabilityId = state.Session.Contract.CapabilityId,
            CapabilityModuleId = state.Session.CapabilityModuleId
        };
    }
}

/// <summary>
/// Recovers the <see cref="RuntimeSessionState"/> persisted right before
/// the Instruction pause once the learner acknowledges it (fixed Paso 9,
/// 2026-07-15) — mirrors <see cref="RuntimeEvidenceReceiver"/> but for the
/// evidence-free Instruction acknowledgement.
/// </summary>
internal sealed class InstructionAckReceivedExecutor : Executor<InstructionAcknowledgement, RuntimeSessionState>
{
    public InstructionAckReceivedExecutor() : base(nameof(InstructionAckReceivedExecutor))
    {
    }

    public override async ValueTask<RuntimeSessionState> HandleAsync(
        InstructionAcknowledgement ack,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        var state = await context.ReadStateAsync<RuntimeSessionState>(
            ack.RuntimeSessionId.ToString(),
            scopeName: RuntimeSessionWorkflowFactory.SessionStateScope);

        if (state is null)
        {
            throw new ArgumentException(
                $"No pending Runtime session found for id '{ack.RuntimeSessionId}'.");
        }

        return state;
    }
}

/// <summary>
/// Presents the CURRENT chapter's <c>TeachingContent</c> (fixed
/// 2026-07-16) — the phase-based replacement for
/// <see cref="InstructionExecutor"/> when the module has Chapters. Reached
/// once per chapter: first from <see cref="RuntimeStage.ModuleStarted"/>'s
/// ack (index 0), then again from <c>ChapterAdvanceExecutor</c>'s loop-back
/// edge for every subsequent chapter — same executor instance handles
/// every iteration, mirroring <see cref="RecallCheckExecutor"/>'s reuse
/// across retry loops. Never produces <see cref="StudentEvidence"/>.
/// </summary>
internal sealed class ChapterTeachingExecutor : Executor<RuntimeSessionState, ChapterPresentation>
{
    private readonly TutorAgent _tutorAgent;

    public ChapterTeachingExecutor(TutorAgent tutorAgent) : base(nameof(ChapterTeachingExecutor))
    {
        _tutorAgent = tutorAgent;
    }

    public override async ValueTask<ChapterPresentation> HandleAsync(
        RuntimeSessionState state,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        state.Session.Stage = RuntimeStage.ChapterTeaching;
        state.History.Add(new RuntimeStageTransition { Stage = RuntimeStage.ChapterTeaching });

        var chapter = state.Session.Contract.Chapters[state.CurrentChapterIndex];
        var turnContext = TutorTurnContextBuilder.Build(
            state, RuntimeStage.ChapterTeaching, chapterIndex: state.CurrentChapterIndex);
        var result = await _tutorAgent.RespondAsync(turnContext, cancellationToken);
        var message = RuntimeStageTransitionHelper.ConsumePendingRecallReveal(state, result.Response);

        await context.QueueStateUpdateAsync(
            state.Session.RuntimeSessionId.ToString(),
            state,
            scopeName: RuntimeSessionWorkflowFactory.SessionStateScope);

        return new ChapterPresentation
        {
            RuntimeSessionId = state.Session.RuntimeSessionId,
            ChapterIndex = state.CurrentChapterIndex,
            TotalChapters = state.Session.Contract.Chapters.Count,
            ChapterTitle = chapter.Title,
            TeachingContent = message,
            CapabilityTitle = state.Session.Contract.CapabilityTitle,
            CapabilityCode = state.Session.Contract.CapabilityCode,
            CapabilityId = state.Session.Contract.CapabilityId,
            CapabilityModuleId = state.Session.CapabilityModuleId,
            AllChapterTitles = [.. state.Session.Contract.Chapters.Select(c => c.Title)]
        };
    }
}

/// <summary>
/// Recovers the <see cref="RuntimeSessionState"/> persisted right before a
/// chapter's teaching pause once the learner acknowledges it (fixed
/// 2026-07-16) — mirrors <see cref="InstructionAckReceivedExecutor"/>.
/// </summary>
internal sealed class ChapterAckReceivedExecutor : Executor<ChapterAcknowledgement, RuntimeSessionState>
{
    public ChapterAckReceivedExecutor() : base(nameof(ChapterAckReceivedExecutor))
    {
    }

    public override async ValueTask<RuntimeSessionState> HandleAsync(
        ChapterAcknowledgement ack,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        var state = await context.ReadStateAsync<RuntimeSessionState>(
            ack.RuntimeSessionId.ToString(),
            scopeName: RuntimeSessionWorkflowFactory.SessionStateScope);

        if (state is null)
        {
            throw new ArgumentException(
                $"No pending Runtime session found for id '{ack.RuntimeSessionId}'.");
        }

        return state;
    }
}

/// <summary>
/// Presents the CURRENT chapter's <c>PredictionPrompt</c> (fixed
/// 2026-07-16) — reached ONLY for the module's one
/// <c>IsPrimaryWeight</c> chapter, via the conditional edge in
/// <see cref="RuntimeSessionWorkflowFactory"/>. Extended 2026-07-17 —
/// micro-dialogue fix: some stored PredictionPrompts are multi-part
/// numbered questionnaires (a real Studio content bug found live); this
/// executor presents ONLY the CURRENT sub-question
/// (<see cref="RuntimeSessionState.PredictionDialogueTurn"/>), never the
/// whole block, via <see cref="MultiPartPromptSegmenter"/>. Well-formed
/// single-question prompts behave exactly as before (1 segment, 1 turn).
/// </summary>
internal sealed class ChapterPredictionExecutor : Executor<RuntimeSessionState, EvidenceRequest>
{
    private readonly TutorAgent _tutorAgent;

    public ChapterPredictionExecutor(TutorAgent tutorAgent) : base(nameof(ChapterPredictionExecutor))
    {
        _tutorAgent = tutorAgent;
    }

    public override async ValueTask<EvidenceRequest> HandleAsync(
        RuntimeSessionState state,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        var chapter = state.Session.Contract.Chapters[state.CurrentChapterIndex];
        var segments = MultiPartPromptSegmenter.Split(chapter.PredictionPrompt ?? string.Empty);
        var turn = Math.Min(state.PredictionDialogueTurn, segments.Count - 1);

        var turnContext = TutorTurnContextBuilder.Build(
            state,
            RuntimeStage.ChapterPrediction,
            chapterIndex: state.CurrentChapterIndex,
            chapterSourceTextOverride: segments[turn],
            isMultiPartChapterDialogueTurn: segments.Count > 1);
        var result = await _tutorAgent.RespondAsync(turnContext, cancellationToken);

        return await RuntimeStageTransitionHelper.RequestEvidenceAsync(
            state, RuntimeStage.ChapterPrediction, result.Response, context, cancellationToken);
    }
}

/// <summary>
/// Records the learner's answer to ONE sub-question of the Prediction
/// dialogue and advances <see cref="RuntimeSessionState.PredictionDialogueTurn"/>
/// (fixed 2026-07-17) — the conditional edges in
/// <see cref="RuntimeSessionWorkflowFactory"/> route back to
/// <see cref="ChapterPredictionExecutor"/> for the next sub-question, or
/// on to <see cref="ChapterMiniPracticeExecutor"/> once every sub-question
/// has been asked.
/// </summary>
internal sealed class ChapterPredictionEvidenceReceivedExecutor : Executor<EvidenceSubmission, RuntimeSessionState>
{
    public ChapterPredictionEvidenceReceivedExecutor() : base(nameof(ChapterPredictionEvidenceReceivedExecutor))
    {
    }

    public override async ValueTask<RuntimeSessionState> HandleAsync(
        EvidenceSubmission submission,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        var state = await RuntimeEvidenceReceiver.ReceiveAsync(submission, context, cancellationToken);
        state.PredictionDialogueTurn++;
        return state;
    }
}

/// <summary>
/// Presents the CURRENT chapter's <c>MiniPracticePrompt</c> (fixed
/// 2026-07-16) — reached only right after <see cref="ChapterPredictionExecutor"/>,
/// on the same primary-weight chapter. Presentation-only, like
/// <see cref="ChapterTeachingExecutor"/> — never produces
/// <see cref="StudentEvidence"/> (private retrieval practice, not graded).
/// </summary>
internal sealed class ChapterMiniPracticeExecutor : Executor<RuntimeSessionState, ChapterMiniPracticePresentation>
{
    private readonly TutorAgent _tutorAgent;

    public ChapterMiniPracticeExecutor(TutorAgent tutorAgent) : base(nameof(ChapterMiniPracticeExecutor))
    {
        _tutorAgent = tutorAgent;
    }

    public override async ValueTask<ChapterMiniPracticePresentation> HandleAsync(
        RuntimeSessionState state,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        state.Session.Stage = RuntimeStage.ChapterMiniPractice;
        state.History.Add(new RuntimeStageTransition { Stage = RuntimeStage.ChapterMiniPractice });

        var chapter = state.Session.Contract.Chapters[state.CurrentChapterIndex];
        var turnContext = TutorTurnContextBuilder.Build(
            state, RuntimeStage.ChapterMiniPractice, chapterIndex: state.CurrentChapterIndex);
        var result = await _tutorAgent.RespondAsync(turnContext, cancellationToken);

        await context.QueueStateUpdateAsync(
            state.Session.RuntimeSessionId.ToString(),
            state,
            scopeName: RuntimeSessionWorkflowFactory.SessionStateScope);

        return new ChapterMiniPracticePresentation
        {
            RuntimeSessionId = state.Session.RuntimeSessionId,
            ChapterTitle = chapter.Title,
            MiniPracticeContent = result.Response,
            CapabilityTitle = state.Session.Contract.CapabilityTitle,
            CapabilityCode = state.Session.Contract.CapabilityCode,
            CapabilityId = state.Session.Contract.CapabilityId,
            CapabilityModuleId = state.Session.CapabilityModuleId
        };
    }
}

/// <summary>
/// Recovers the <see cref="RuntimeSessionState"/> persisted right before
/// the mini-practice pause once the learner acknowledges it (fixed
/// 2026-07-16) — mirrors <see cref="ChapterAckReceivedExecutor"/>.
/// </summary>
internal sealed class ChapterMiniPracticeAckReceivedExecutor : Executor<ChapterMiniPracticeAcknowledgement, RuntimeSessionState>
{
    public ChapterMiniPracticeAckReceivedExecutor() : base(nameof(ChapterMiniPracticeAckReceivedExecutor))
    {
    }

    public override async ValueTask<RuntimeSessionState> HandleAsync(
        ChapterMiniPracticeAcknowledgement ack,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        var state = await context.ReadStateAsync<RuntimeSessionState>(
            ack.RuntimeSessionId.ToString(),
            scopeName: RuntimeSessionWorkflowFactory.SessionStateScope);

        if (state is null)
        {
            throw new ArgumentException(
                $"No pending Runtime session found for id '{ack.RuntimeSessionId}'.");
        }

        return state;
    }
}

/// <summary>
/// Presents the CURRENT chapter's own <c>RecallPrompt</c> (fixed
/// 2026-07-16) — reached for EVERY chapter (directly after its teaching
/// ack for non-primary chapters, or after its mini-practice ack for the
/// primary-weight one). Lighter than the module-wide
/// <see cref="RecallRequiredExecutor"/>, which still runs once at the end
/// after every chapter is done.
/// </summary>
internal sealed class ChapterRecallExecutor : Executor<RuntimeSessionState, EvidenceRequest>
{
    private readonly TutorAgent _tutorAgent;

    public ChapterRecallExecutor(TutorAgent tutorAgent) : base(nameof(ChapterRecallExecutor))
    {
        _tutorAgent = tutorAgent;
    }

    public override async ValueTask<EvidenceRequest> HandleAsync(
        RuntimeSessionState state,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        var turnContext = TutorTurnContextBuilder.Build(
            state, RuntimeStage.ChapterRecall, chapterIndex: state.CurrentChapterIndex);
        var result = await _tutorAgent.RespondAsync(turnContext, cancellationToken);

        return await RuntimeStageTransitionHelper.RequestEvidenceAsync(
            state,
            RuntimeStage.ChapterRecall,
            result.Response,
            context,
            cancellationToken,
            attemptNumber: state.ChapterRecallAttempt + 1,
            totalAttempts: RuntimeSessionWorkflowFactory.MaxRecallRetries + 1);
    }
}

internal sealed class ChapterRecallEvidenceReceivedExecutor : Executor<EvidenceSubmission, RuntimeSessionState>
{
    public ChapterRecallEvidenceReceivedExecutor() : base(nameof(ChapterRecallEvidenceReceivedExecutor))
    {
    }

    public override ValueTask<RuntimeSessionState> HandleAsync(
        EvidenceSubmission submission,
        IWorkflowContext context,
        CancellationToken cancellationToken = default) =>
        RuntimeEvidenceReceiver.ReceiveAsync(submission, context, cancellationToken);
}

/// <summary>
/// Lightweight completeness check for the CURRENT chapter's just-submitted
/// Recall attempt (fixed 2026-07-16 — closes a real pedagogical gap the
/// product owner flagged: a chapter's Recall answer was silently advancing
/// to the next chapter with ZERO feedback on whether it was right or
/// wrong). Mirrors <see cref="RecallCheckExecutor"/> exactly, but scoped
/// to ONE chapter's own <c>TeachingContent</c> via
/// <see cref="TutorAgent.CheckChapterRecallAsync"/> instead of the whole
/// module script — the conditional edges in
/// <see cref="RuntimeSessionWorkflowFactory"/> route its output to
/// <see cref="ChapterAdvanceExecutor"/> (sufficient, or retries exhausted)
/// or <see cref="ChapterRecallRetryExecutor"/> (insufficient, retries
/// remain).
/// </summary>
internal sealed class ChapterRecallCheckExecutor : Executor<RuntimeSessionState, RecallCheckOutcome>
{
    private readonly TutorAgent _tutorAgent;

    public ChapterRecallCheckExecutor(TutorAgent tutorAgent) : base(nameof(ChapterRecallCheckExecutor))
    {
        _tutorAgent = tutorAgent;
    }

    public override async ValueTask<RecallCheckOutcome> HandleAsync(
        RuntimeSessionState state,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        // Fixed 2026-07-17: same learner-controlled "continuar de todas
        // formas" escape hatch as RecallCheckExecutor.
        if (state.PendingForceAdvance)
        {
            state.PendingForceAdvance = false;
            return new RecallCheckOutcome
            {
                State = state,
                Result = new RecallCheckResult { IsSufficient = true, FollowUpPrompt = string.Empty }
            };
        }

        RecallCheckResult result;
        try
        {
            result = await _tutorAgent.CheckChapterRecallAsync(state, state.CurrentChapterIndex, cancellationToken);
        }
        catch (InvalidOperationException)
        {
            // Same fail-open rationale as RecallCheckExecutor — a
            // technical failure here must never block the learner's
            // progress through the module.
            result = new RecallCheckResult { IsSufficient = true, FollowUpPrompt = string.Empty };
        }

        // Fixed 2026-07-17: same "reveal instead of silently giving up"
        // fix as RecallCheckExecutor, scoped to THIS chapter's own
        // TeachingContent.
        if (result.IsGenuineAttempt && !result.IsSufficient &&
            state.ChapterRecallAttempt >= RuntimeSessionWorkflowFactory.MaxRecallRetries)
        {
            var chapter = state.Session.Contract.Chapters[state.CurrentChapterIndex];
            state.PendingRecallReveal = RecallRevealBuilder.Build(chapter.TeachingContent);
        }

        return new RecallCheckOutcome { State = state, Result = result };
    }
}

/// <summary>
/// Retry branch for a chapter's Recall (fixed 2026-07-16) — reached when
/// <see cref="ChapterRecallCheckExecutor"/> found the attempt insufficient
/// and the retry budget (<see cref="RuntimeSessionWorkflowFactory.MaxRecallRetries"/>)
/// is not yet exhausted. Increments
/// <see cref="RuntimeSessionState.ChapterRecallAttempt"/> and re-pauses at
/// <see cref="RuntimeStage.ChapterRecall"/> with the check's own
/// answer-free Socratic follow-up as the next prompt — mirrors
/// <see cref="RecallRetryExecutor"/> exactly.
/// </summary>
internal sealed class ChapterRecallRetryExecutor : Executor<RecallCheckOutcome, EvidenceRequest>
{
    public ChapterRecallRetryExecutor() : base(nameof(ChapterRecallRetryExecutor))
    {
    }

    public override async ValueTask<EvidenceRequest> HandleAsync(
        RecallCheckOutcome outcome,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        // Fixed 2026-07-17: same "a question never costs a retry" fix as
        // RecallRetryExecutor.
        if (outcome.Result.IsGenuineAttempt)
        {
            outcome.State.ChapterRecallAttempt++;
        }

        return await RuntimeStageTransitionHelper.RequestEvidenceAsync(
            outcome.State,
            RuntimeStage.ChapterRecall,
            outcome.Result.FollowUpPrompt,
            context,
            cancellationToken,
            attemptNumber: outcome.State.ChapterRecallAttempt + 1,
            totalAttempts: RuntimeSessionWorkflowFactory.MaxRecallRetries + 1,
            lastAccuracyPercentage: outcome.Result.IsGenuineAttempt ? outcome.Result.AccuracyPercentage : null);
    }
}

internal sealed class ChapterRecallRetryEvidenceReceivedExecutor : Executor<EvidenceSubmission, RuntimeSessionState>
{
    public ChapterRecallRetryEvidenceReceivedExecutor() : base(nameof(ChapterRecallRetryEvidenceReceivedExecutor))
    {
    }

    public override ValueTask<RuntimeSessionState> HandleAsync(
        EvidenceSubmission submission,
        IWorkflowContext context,
        CancellationToken cancellationToken = default) =>
        RuntimeEvidenceReceiver.ReceiveAsync(submission, context, cancellationToken);
}

/// <summary>
/// Advances <see cref="RuntimeSessionState.CurrentChapterIndex"/> after a
/// chapter's Recall check found the attempt sufficient (or the retry
/// budget was exhausted) — fixed 2026-07-16, now reached from
/// <see cref="ChapterRecallCheckExecutor"/>'s <see cref="RecallCheckOutcome"/>
/// (previously reached directly from raw evidence with no check at all).
/// The conditional edges in <see cref="RuntimeSessionWorkflowFactory"/>
/// route its output back to <see cref="ChapterTeachingExecutor"/> (more
/// chapters remain) or on to the existing <see cref="RecallRequiredExecutor"/>
/// (every chapter is done — converges with the legacy no-Chapters path).
/// </summary>
internal sealed class ChapterAdvanceExecutor : Executor<RecallCheckOutcome, RuntimeSessionState>
{
    public ChapterAdvanceExecutor() : base(nameof(ChapterAdvanceExecutor))
    {
    }

    public override ValueTask<RuntimeSessionState> HandleAsync(
        RecallCheckOutcome outcome,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        outcome.State.CurrentChapterIndex++;
        outcome.State.ChapterRecallAttempt = 0;
        return ValueTask.FromResult(outcome.State);
    }
}

/// <summary>
/// Requires the learner to produce the concrete, observable artifact
/// demanded by <see cref="RuntimePedagogicalContract.LearnerProduction"/>.
/// The AI must never produce this evidence on the learner's behalf. The
/// prompt text is the Tutor Agent's real, adaptive phrasing (fixed Paso 9,
/// 2026-07-15). Extended 2026-07-17 — micro-dialogue fix: when
/// <see cref="RuntimePedagogicalContract.LearnerTask"/> was authored as a
/// multi-part numbered list (e.g. "5 expresiones"), presents ONLY the
/// CURRENT item (<see cref="RuntimeSessionState.ProductionItemTurn"/>),
/// never the whole list — same <see cref="MultiPartPromptSegmenter"/>
/// mechanism already proven for a chapter's PredictionPrompt. Well-formed
/// single-item tasks (or modules published before LearnerTask existed)
/// behave exactly as before (1 segment, 1 turn).
/// </summary>
internal sealed class LearnerProductionExecutor : Executor<RuntimeSessionState, EvidenceRequest>
{
    private readonly TutorAgent _tutorAgent;

    public LearnerProductionExecutor(TutorAgent tutorAgent) : base(nameof(LearnerProductionExecutor))
    {
        _tutorAgent = tutorAgent;
    }

    public override async ValueTask<EvidenceRequest> HandleAsync(
        RuntimeSessionState state,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        var segments = MultiPartPromptSegmenter.Split(state.Session.Contract.LearnerTask ?? string.Empty);
        var turn = Math.Min(state.ProductionItemTurn, segments.Count - 1);

        var turnContext = TutorTurnContextBuilder.Build(
            state,
            RuntimeStage.LearnerProduction,
            learnerTaskOverride: segments[turn],
            isMultiPartLearnerTaskTurn: segments.Count > 1);
        var result = await _tutorAgent.RespondAsync(turnContext, cancellationToken);

        return await RuntimeStageTransitionHelper.RequestEvidenceAsync(
            state, RuntimeStage.LearnerProduction, result.Response, context, cancellationToken);
    }
}

/// <summary>
/// Records the learner's evidence for ONE item of the LearnerProduction
/// task and advances <see cref="RuntimeSessionState.ProductionItemTurn"/>
/// (fixed 2026-07-17) — the conditional edges in
/// <see cref="RuntimeSessionWorkflowFactory"/> route back to
/// <see cref="LearnerProductionExecutor"/> for the next item, or on to
/// <see cref="AssessmentExecutor"/> once every item has been submitted
/// (Assessment still evaluates the FULL accumulated evidence at once —
/// only the presentation is itemized, not the grading).
/// </summary>
internal sealed class ProductionEvidenceReceivedExecutor : Executor<EvidenceSubmission, RuntimeSessionState>
{
    public ProductionEvidenceReceivedExecutor() : base(nameof(ProductionEvidenceReceivedExecutor))
    {
    }

    public override async ValueTask<RuntimeSessionState> HandleAsync(
        EvidenceSubmission submission,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        var state = await RuntimeEvidenceReceiver.ReceiveAsync(submission, context, cancellationToken);
        state.ProductionItemTurn++;
        return state;
    }
}

/// <summary>
/// Real Assessment (fixed Paso 8, 2026-07-14): calls the Tutor Agent's
/// <see cref="TutorAgent.AssessAsync"/> (LLM verdict + deterministic
/// <see cref="RuntimeAssessmentValidator"/>, see Paso 6) against the
/// evidence collected so far, records the verdict on the session state,
/// and yields an <see cref="AssessmentOutcome"/> for the conditional
/// Verified/Retry/RequiresRevision routing in
/// <see cref="RuntimeSessionWorkflowFactory"/>.
/// </summary>
internal sealed class AssessmentExecutor : Executor<RuntimeSessionState, AssessmentOutcome>
{
    private readonly TutorAgent _tutorAgent;

    public AssessmentExecutor(TutorAgent tutorAgent) : base(nameof(AssessmentExecutor))
    {
        _tutorAgent = tutorAgent;
    }

    public override async ValueTask<AssessmentOutcome> HandleAsync(
        RuntimeSessionState state,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        state.Session.Stage = RuntimeStage.Assessment;
        state.History.Add(new RuntimeStageTransition { Stage = RuntimeStage.Assessment });

        var turnContext = TutorTurnContextBuilder.Build(state, RuntimeStage.Assessment);
        RuntimeAssessmentResult result;
        try
        {
            result = await _tutorAgent.AssessAsync(turnContext, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            // Structural violation of the Assessment contract (caught by
            // RuntimeAssessmentValidator, or TutorAgent's own "not
            // configured" guard) — a real technical defect in the LLM's
            // structured output (e.g. it under/over-counted
            // SuccessCriteriaResults), NOT a pedagogical judgment. Mirrors
            // Studio's MetricoExecutor catching
            // MetricVerificationValidator's exceptions and mapping them to
            // a Failed status instead of crashing the whole run (see
            // HUMAN-OS-STUDIO.md §14.2) — Failed still flows through the
            // SAME bounded retry loop as a pedagogical NotVerified, so one
            // bad LLM response costs a retry, not the entire session.
            result = new RuntimeAssessmentResult
            {
                TargetMetric = state.Session.Contract.TargetMetric,
                Status = MetricVerificationStatus.Failed,
                SuccessCriteriaResults =
                [
                    .. state.Session.Contract.SuccessCriteria.Select(c => new SuccessCriterionAssessment
                    {
                        Criterion = c,
                        IsSatisfied = false,
                        Evidence = "N/A — technical Assessment validation failure, see Explanation."
                    })
                ],
                Explanation = $"Assessment failed structural validation and could not be trusted: {ex.Message}"
            };
        }

        state.LastAssessment = result;

        return new AssessmentOutcome { State = state, Result = result };
    }
}

/// <summary>
/// Post-task metacognitive reflection — the prompt text is the Tutor
/// Agent's real, adaptive phrasing (fixed Paso 9, 2026-07-15). Only
/// reached via the Verified branch of <see cref="AssessmentOutcome"/>
/// (fixed Paso 8, 2026-07-14) — unwraps <c>.State</c>, same "downstream
/// unwraps the wrapper type" pattern Studio established for Gate outcomes.
/// </summary>
internal sealed class ReflectionExecutor : Executor<AssessmentOutcome, EvidenceRequest>
{
    private readonly TutorAgent _tutorAgent;

    public ReflectionExecutor(TutorAgent tutorAgent) : base(nameof(ReflectionExecutor))
    {
        _tutorAgent = tutorAgent;
    }

    public override async ValueTask<EvidenceRequest> HandleAsync(
        AssessmentOutcome outcome,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        var turnContext = TutorTurnContextBuilder.Build(outcome.State, RuntimeStage.Reflection);
        var result = await _tutorAgent.RespondAsync(turnContext, cancellationToken);

        return await RuntimeStageTransitionHelper.RequestEvidenceAsync(
            outcome.State, RuntimeStage.Reflection, result.Response, context, cancellationToken);
    }
}

internal sealed class ReflectionEvidenceReceivedExecutor : Executor<EvidenceSubmission, RuntimeSessionState>
{
    public ReflectionEvidenceReceivedExecutor() : base(nameof(ReflectionEvidenceReceivedExecutor))
    {
    }

    public override ValueTask<RuntimeSessionState> HandleAsync(
        EvidenceSubmission submission,
        IWorkflowContext context,
        CancellationToken cancellationToken = default) =>
        RuntimeEvidenceReceiver.ReceiveAsync(submission, context, cancellationToken);
}

/// <summary>
/// Retry branch (fixed Paso 8, 2026-07-14) — reached when Assessment did
/// NOT verify the TargetMetric and the retry budget
/// (<see cref="RuntimeSessionWorkflowFactory.MaxRetries"/>) is not yet
/// exhausted. Increments <see cref="RuntimeSessionState.ProductionAttempt"/>,
/// routes the session back to <see cref="RuntimeStage.LearnerProduction"/>
/// to give the learner another real attempt — never silently advances
/// past a non-Verified metric. The retry prompt is the Tutor Agent's real,
/// answer-free feedback phrasing (fixed Paso 9, 2026-07-15), grounded in
/// <see cref="RuntimeSessionState.LastAssessment"/>.
/// </summary>
internal sealed class LearnerProductionRetryExecutor : Executor<AssessmentOutcome, EvidenceRequest>
{
    private readonly TutorAgent _tutorAgent;

    public LearnerProductionRetryExecutor(TutorAgent tutorAgent) : base(nameof(LearnerProductionRetryExecutor))
    {
        _tutorAgent = tutorAgent;
    }

    public override async ValueTask<EvidenceRequest> HandleAsync(
        AssessmentOutcome outcome,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        outcome.State.ProductionAttempt++;

        var turnContext = TutorTurnContextBuilder.Build(outcome.State, RuntimeStage.LearnerProduction);
        var result = await _tutorAgent.RespondAsync(turnContext, cancellationToken);

        return await RuntimeStageTransitionHelper.RequestEvidenceAsync(
            outcome.State, RuntimeStage.LearnerProduction, result.Response, context, cancellationToken);
    }
}

internal sealed class ProductionRetryEvidenceReceivedExecutor : Executor<EvidenceSubmission, RuntimeSessionState>
{
    public ProductionRetryEvidenceReceivedExecutor() : base(nameof(ProductionRetryEvidenceReceivedExecutor))
    {
    }

    public override ValueTask<RuntimeSessionState> HandleAsync(
        EvidenceSubmission submission,
        IWorkflowContext context,
        CancellationToken cancellationToken = default) =>
        RuntimeEvidenceReceiver.ReceiveAsync(submission, context, cancellationToken);
}

/// <summary>
/// Terminal branch (fixed Paso 8, 2026-07-14) — reached when Assessment
/// did not verify the TargetMetric AND the retry budget is exhausted. A
/// legitimate pedagogical outcome (see <see cref="RuntimeStage.RequiresRevision"/>'s
/// doc comment), never silently treated as Completed.
/// </summary>
internal sealed class RequiresRevisionExecutor : Executor<AssessmentOutcome, RuntimeSessionState>
{
    public RequiresRevisionExecutor() : base(nameof(RequiresRevisionExecutor))
    {
    }

    public override async ValueTask<RuntimeSessionState> HandleAsync(
        AssessmentOutcome outcome,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        outcome.State.Session.Stage = RuntimeStage.RequiresRevision;
        outcome.State.History.Add(new RuntimeStageTransition { Stage = RuntimeStage.RequiresRevision });

        await context.YieldOutputAsync(outcome.State, cancellationToken);
        return outcome.State;
    }
}

/// <summary>
/// Terminal executor — marks the session Completed. Progression to the
/// next module is a Runtime decision grounded in real Assessment evidence
/// (a later Paso), never in "every stage was visited".
/// </summary>
internal sealed class CompletedExecutor : Executor<RuntimeSessionState, RuntimeSessionState>
{
    public CompletedExecutor() : base(nameof(CompletedExecutor))
    {
    }

    public override async ValueTask<RuntimeSessionState> HandleAsync(
        RuntimeSessionState state,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        state.Session.Stage = RuntimeStage.Completed;
        state.Session.CompletedDate = DateTime.UtcNow;
        state.History.Add(new RuntimeStageTransition { Stage = RuntimeStage.Completed });

        await context.YieldOutputAsync(state, cancellationToken);
        return state;
    }
}
