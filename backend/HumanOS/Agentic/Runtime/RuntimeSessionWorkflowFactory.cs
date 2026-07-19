using HumanOS.Agents.Runtime;
using HumanOS.Agents.Studio;
using Microsoft.Agents.AI.Workflows;

namespace HumanOS.Agentic.Runtime;

/// <summary>
/// Builds the Interactive Learning Runtime's deterministic state machine
/// for ONE module session (fixed Paso 2, extended Paso 8 2026-07-14,
/// extended 2026-07-16 with a real ModuleStarted introduction, REORDERED
/// 2026-07-16 per explicit user correction, extended again 2026-07-16 with
/// an iterative Recall retry loop — see below):
///
///   ModuleStarted -&gt; [pause: Introduction-Ack]
///     -&gt; Instruction -&gt; [pause: Instruction-Ack]
///     -&gt; RecallRequired -&gt; [pause: Evidence-Recall]
///     -&gt; RecallCheck (lightweight completeness check, Tutor Agent)
///       -&gt; [Sufficient OR retries exhausted] -&gt; PredictionRequired
///       -&gt; [Insufficient, retries remain] -&gt; RecallRetry (bounded by
///          <see cref="MaxRecallRetries"/>) -&gt; [pause: Evidence-Recall-Retry]
///          -&gt; RecallCheck again
///     -&gt; PredictionRequired -&gt; [pause: Evidence-Prediction]
///     -&gt; LearnerProduction -&gt; [pause: Evidence-Production]
///     -&gt; Assessment (REAL: Tutor Agent verdict + deterministic validator, Paso 6)
///       -&gt; [Verified] -&gt; Reflection -&gt; [pause: Evidence-Reflection] -&gt; Completed
///       -&gt; [NotVerified/Failed, retries remain] -&gt; LearnerProduction (retry loop,
///          bounded by <see cref="MaxRetries"/>) -&gt; [pause: Evidence-Production-Retry]
///          -&gt; Assessment again
///       -&gt; [NotVerified/Failed, retries exhausted] -&gt; RequiresRevision (terminal)
///
/// RECALL RETRY LOOP (fixed 2026-07-16, explicit user request: "quiero que
/// el usuario pueda repasar o aprender con varias iteraciones de preguntas
/// y respuestas", grounded in "The Memory Paradox" (Oakley et al., 2025)):
/// a single one-shot Recall submission that silently advances regardless
/// of completeness wastes the retrieval-practice opportunity. RecallCheck
/// is a LIGHTWEIGHT check (see <see cref="RecallCheckResult"/>) — distinct
/// from the formal Assessment stage — that never claims Recall-as-metric
/// is verified; it only decides whether to give the learner one more
/// bounded attempt with an answer-free Socratic follow-up (prediction-
/// error style) before moving on to Prediction.
///
/// REORDERING RATIONALE (2026-07-16): the original design put Recall/
/// Prediction BEFORE Instruction (a deliberate "pretesting effect" /
/// retrieval-practice technique from learning science — attempt retrieval
/// on totally new material before ever seeing it). Real user testing
/// found this consistently confusing and rejected outright: a learner
/// asked to "recall" content they were never taught, before any teaching
/// happened, felt broken rather than pedagogically clever. Explicit
/// correction from the product owner: "primero necesitamos enseñar paso a
/// paso, preguntar que aprendio recall" — teach FIRST, then ask what they
/// learned (Recall) SECOND. This graph now reflects that: Instruction
/// comes right after the ModuleStarted welcome, and Recall/Prediction
/// test retention/hypothesis-forming AFTER the content was presented, not
/// before. RecallRequirement itself is still mandatory per module (see
/// BlueprintValidator) — only the ORDER relative to Instruction changed.
///
/// Each pause point is its own dedicated <c>RequestPort</c> (mirrors
/// Studio's 1 gate = 1 dedicated decision executor pattern, see
/// CapabilityCreationWorkflowFactory) — the Runtime (this graph), never the
/// Tutor Agent, owns every transition. The retry loop mirrors Studio's
/// bounded-revision-retry shape (<c>ModuleCompletionRouterExecutor.MaxRetries</c>)
/// exactly, including the "outcome wrapper + downstream unwrap" conditional
/// routing pattern (<see cref="AssessmentOutcome"/>).
///
/// Deliberately NOT included yet: persistence of <see cref="RuntimeAssessmentResult"/>
/// to a DB schema (mirrors the "persistencia de dominio" deferral from
/// Paso 3), and Progression ACROSS modules (RuntimeNavigator — a separate,
/// not-yet-built concept, see /memories/repo/human-os-runtime-design.md).
///
/// A fresh graph (with fresh executor instances) must be built per
/// Runtime session, same rule as Studio's capability-creation Workflow.
/// </summary>
internal static class RuntimeSessionWorkflowFactory
{
    /// <summary>Shared-state scope name for the paused
    /// <see cref="RuntimeSessionState"/>, read/written by the "asking" and
    /// "evidence received" executor pairs.</summary>
    internal const string SessionStateScope = "RuntimeSessionState";

    /// <summary>Max number of LearnerProduction retries after a
    /// non-Verified Assessment (so up to 1 + MaxRetries total attempts) —
    /// same value and rationale as Studio's
    /// <c>ModuleCompletionRouterExecutor.MaxRetries</c>.</summary>
    internal const int MaxRetries = 2;

    /// <summary>Max number of Recall retrieval-practice retries after an
    /// insufficient <see cref="RecallCheckResult"/> (fixed 2026-07-16, so
    /// up to 1 + MaxRecallRetries total attempts) — implements iterative
    /// Q&amp;A per explicit user request, bounded the same way as
    /// <see cref="MaxRetries"/> so the learner is never trapped forever.
    /// Raised from 2 to 4 (fixed 2026-07-17 — explicit user feedback after
    /// live testing: "se fue muy rápido a la siguiente pregunta... en el
    /// segundo error ya no siguió, necesitamos más flexibilidad y más
    /// intentos") — 2 was cutting the retrieval-practice loop short before
    /// the learner felt they'd genuinely gotten it. See also
    /// <see cref="EvidenceSubmission.ForceAdvance"/> for the learner's OWN
    /// escape hatch, independent of this cap.</summary>
    internal const int MaxRecallRetries = 4;

    public static Workflow Build(TutorAgent tutorAgent)
    {
        var moduleStarted = new ModuleStartedExecutor(tutorAgent);
        var introductionAckPort = RequestPort.Create<IntroductionPresentation, IntroductionAcknowledgement>("Introduction-Ack");
        var introductionAckReceived = new IntroductionAckReceivedExecutor();

        var instruction = new InstructionExecutor(tutorAgent);
        var instructionAckPort = RequestPort.Create<InstructionPresentation, InstructionAcknowledgement>("Instruction-Ack");
        var instructionAckReceived = new InstructionAckReceivedExecutor();

        // Phase-based Chapters loop (fixed 2026-07-16) — reached instead
        // of the single `instruction` turn above when the module has
        // Chapters (see HasChapters/NoChapters below). Mirrors the bounded
        // self-loop shape already proven by the Recall retry loop, except
        // the loop bound is the DYNAMIC chapter count rather than a fixed
        // MaxRecallRetries constant.
        var chapterTeaching = new ChapterTeachingExecutor(tutorAgent);
        var chapterAckPort = RequestPort.Create<ChapterPresentation, ChapterAcknowledgement>("Chapter-Ack");
        var chapterAckReceived = new ChapterAckReceivedExecutor();

        var chapterPrediction = new ChapterPredictionExecutor(tutorAgent);
        var chapterPredictionEvidencePort = RequestPort.Create<EvidenceRequest, EvidenceSubmission>("Evidence-Chapter-Prediction");
        var chapterPredictionEvidenceReceived = new ChapterPredictionEvidenceReceivedExecutor();

        var chapterMiniPractice = new ChapterMiniPracticeExecutor(tutorAgent);
        var chapterMiniPracticeAckPort = RequestPort.Create<ChapterMiniPracticePresentation, ChapterMiniPracticeAcknowledgement>("Chapter-MiniPractice-Ack");
        var chapterMiniPracticeAckReceived = new ChapterMiniPracticeAckReceivedExecutor();

        var chapterRecall = new ChapterRecallExecutor(tutorAgent);
        var chapterRecallEvidencePort = RequestPort.Create<EvidenceRequest, EvidenceSubmission>("Evidence-Chapter-Recall");
        var chapterRecallEvidenceReceived = new ChapterRecallEvidenceReceivedExecutor();
        var chapterRecallCheck = new ChapterRecallCheckExecutor(tutorAgent);
        var chapterRecallRetry = new ChapterRecallRetryExecutor();
        var chapterRecallRetryEvidencePort = RequestPort.Create<EvidenceRequest, EvidenceSubmission>("Evidence-Chapter-Recall-Retry");
        var chapterRecallRetryEvidenceReceived = new ChapterRecallRetryEvidenceReceivedExecutor();

        var chapterAdvance = new ChapterAdvanceExecutor();

        var recallRequired = new RecallRequiredExecutor(tutorAgent);
        var recallEvidencePort = RequestPort.Create<EvidenceRequest, EvidenceSubmission>("Evidence-Recall");
        var recallEvidenceReceived = new RecallEvidenceReceivedExecutor();
        var recallCheck = new RecallCheckExecutor(tutorAgent);
        var recallRetry = new RecallRetryExecutor();
        var recallRetryEvidencePort = RequestPort.Create<EvidenceRequest, EvidenceSubmission>("Evidence-Recall-Retry");
        var recallRetryEvidenceReceived = new RecallRetryEvidenceReceivedExecutor();

        var predictionRequired = new PredictionRequiredExecutor(tutorAgent);
        var predictionEvidencePort = RequestPort.Create<EvidenceRequest, EvidenceSubmission>("Evidence-Prediction");
        var predictionEvidenceReceived = new PredictionEvidenceReceivedExecutor();

        var learnerProduction = new LearnerProductionExecutor(tutorAgent);
        var productionEvidencePort = RequestPort.Create<EvidenceRequest, EvidenceSubmission>("Evidence-Production");
        var productionEvidenceReceived = new ProductionEvidenceReceivedExecutor();

        var assessment = new AssessmentExecutor(tutorAgent);

        var reflection = new ReflectionExecutor(tutorAgent);
        var reflectionEvidencePort = RequestPort.Create<EvidenceRequest, EvidenceSubmission>("Evidence-Reflection");
        var reflectionEvidenceReceived = new ReflectionEvidenceReceivedExecutor();

        var learnerProductionRetry = new LearnerProductionRetryExecutor(tutorAgent);
        var productionRetryEvidencePort = RequestPort.Create<EvidenceRequest, EvidenceSubmission>("Evidence-Production-Retry");
        var productionRetryEvidenceReceived = new ProductionRetryEvidenceReceivedExecutor();

        var requiresRevision = new RequiresRevisionExecutor();

        var completed = new CompletedExecutor();

        var builder = new WorkflowBuilder(moduleStarted);
        builder
            .AddEdge(moduleStarted, introductionAckPort)
            .AddEdge(introductionAckPort, introductionAckReceived)
            .AddEdge<RuntimeSessionState>(introductionAckReceived, chapterTeaching, condition: HasChapters)
            .AddEdge<RuntimeSessionState>(introductionAckReceived, instruction, condition: NoChapters)
            .AddEdge(instruction, instructionAckPort)
            .AddEdge(instructionAckPort, instructionAckReceived)
            .AddEdge(instructionAckReceived, recallRequired)
            .AddEdge(chapterTeaching, chapterAckPort)
            .AddEdge(chapterAckPort, chapterAckReceived)
            .AddEdge<RuntimeSessionState>(chapterAckReceived, chapterPrediction, condition: IsCurrentChapterPrimaryWeight)
            .AddEdge<RuntimeSessionState>(chapterAckReceived, chapterRecall, condition: IsCurrentChapterNotPrimaryWeight)
            .AddEdge(chapterPrediction, chapterPredictionEvidencePort)
            .AddEdge(chapterPredictionEvidencePort, chapterPredictionEvidenceReceived)
            .AddEdge<RuntimeSessionState>(chapterPredictionEvidenceReceived, chapterPrediction, condition: MorePredictionPartsRemain)
            .AddEdge<RuntimeSessionState>(chapterPredictionEvidenceReceived, chapterMiniPractice, condition: NoMorePredictionPartsRemain)
            .AddEdge(chapterMiniPractice, chapterMiniPracticeAckPort)
            .AddEdge(chapterMiniPracticeAckPort, chapterMiniPracticeAckReceived)
            .AddEdge(chapterMiniPracticeAckReceived, chapterRecall)
            .AddEdge(chapterRecall, chapterRecallEvidencePort)
            .AddEdge(chapterRecallEvidencePort, chapterRecallEvidenceReceived)
            .AddEdge(chapterRecallEvidenceReceived, chapterRecallCheck)
            .AddEdge<RecallCheckOutcome>(chapterRecallCheck, chapterAdvance, condition: IsChapterRecallSufficient)
            .AddEdge<RecallCheckOutcome>(chapterRecallCheck, chapterRecallRetry, condition: ChapterRecallNeedsRetry)
            .AddEdge(chapterRecallRetry, chapterRecallRetryEvidencePort)
            .AddEdge(chapterRecallRetryEvidencePort, chapterRecallRetryEvidenceReceived)
            .AddEdge(chapterRecallRetryEvidenceReceived, chapterRecallCheck)
            .AddEdge<RuntimeSessionState>(chapterAdvance, chapterTeaching, condition: MoreChaptersRemain)
            .AddEdge<RuntimeSessionState>(chapterAdvance, recallRequired, condition: NoMoreChaptersRemain)
            .AddEdge(recallRequired, recallEvidencePort)
            .AddEdge(recallEvidencePort, recallEvidenceReceived)
            .AddEdge(recallEvidenceReceived, recallCheck)
            .AddEdge<RecallCheckOutcome>(recallCheck, predictionRequired, condition: IsRecallSufficient)
            .AddEdge<RecallCheckOutcome>(recallCheck, recallRetry, condition: RecallNeedsRetry)
            .AddEdge(recallRetry, recallRetryEvidencePort)
            .AddEdge(recallRetryEvidencePort, recallRetryEvidenceReceived)
            .AddEdge(recallRetryEvidenceReceived, recallCheck)
            .AddEdge(predictionRequired, predictionEvidencePort)
            .AddEdge(predictionEvidencePort, predictionEvidenceReceived)
            .AddEdge(predictionEvidenceReceived, learnerProduction)
            .AddEdge(learnerProduction, productionEvidencePort)
            .AddEdge(productionEvidencePort, productionEvidenceReceived)
            .AddEdge<RuntimeSessionState>(productionEvidenceReceived, learnerProduction, condition: MoreProductionItemsRemain)
            .AddEdge<RuntimeSessionState>(productionEvidenceReceived, assessment, condition: NoMoreProductionItemsRemain)
            .AddEdge<AssessmentOutcome>(assessment, reflection, condition: IsVerified)
            .AddEdge<AssessmentOutcome>(assessment, learnerProductionRetry, condition: RequiresRetry)
            .AddEdge<AssessmentOutcome>(assessment, requiresRevision, condition: RetriesExhausted)
            .AddEdge(learnerProductionRetry, productionRetryEvidencePort)
            .AddEdge(productionRetryEvidencePort, productionRetryEvidenceReceived)
            .AddEdge(productionRetryEvidenceReceived, assessment)
            .AddEdge(reflection, reflectionEvidencePort)
            .AddEdge(reflectionEvidencePort, reflectionEvidenceReceived)
            .AddEdge(reflectionEvidenceReceived, completed)
            .WithOutputFrom(completed, requiresRevision);

        return builder.Build();
    }

    /// <summary>Routes to the phase-based Chapters loop when the module
    /// has any (fixed 2026-07-16) — see <see cref="NoChapters"/> for the
    /// mutually-exclusive legacy branch.</summary>
    private static bool HasChapters(RuntimeSessionState? state) =>
        state is not null && state.Session.Contract.Chapters.Count > 0;

    /// <summary>Routes to the legacy whole-script <c>Instruction</c> turn
    /// when the module has NO Chapters (published before this feature).</summary>
    private static bool NoChapters(RuntimeSessionState? state) =>
        state is not null && state.Session.Contract.Chapters.Count == 0;

    private static bool IsCurrentChapterPrimaryWeight(RuntimeSessionState? state) =>
        state is not null && state.Session.Contract.Chapters[state.CurrentChapterIndex].IsPrimaryWeight;

    private static bool IsCurrentChapterNotPrimaryWeight(RuntimeSessionState? state) =>
        state is not null && !state.Session.Contract.Chapters[state.CurrentChapterIndex].IsPrimaryWeight;

    /// <summary>Loops back to <c>ChapterTeachingExecutor</c> for the next
    /// chapter (fixed 2026-07-16) — the loop bound is the module's OWN
    /// chapter count, unlike the fixed <see cref="MaxRecallRetries"/>.</summary>
    private static bool MoreChaptersRemain(RuntimeSessionState? state) =>
        state is not null && state.CurrentChapterIndex < state.Session.Contract.Chapters.Count;

    /// <summary>Every chapter is done — proceeds to the module-wide,
    /// cumulative <see cref="RecallRequiredExecutor"/> (converges with the
    /// legacy no-Chapters path, which reaches the same executor).</summary>
    private static bool NoMoreChaptersRemain(RuntimeSessionState? state) =>
        state is not null && state.CurrentChapterIndex >= state.Session.Contract.Chapters.Count;

    private static bool IsVerified(AssessmentOutcome? outcome) =>
        outcome?.Result.Status == MetricVerificationStatus.Verified;

    private static bool RequiresRetry(AssessmentOutcome? outcome) =>
        outcome is not null &&
        outcome.Result.Status != MetricVerificationStatus.Verified &&
        outcome.State.ProductionAttempt < MaxRetries;

    private static bool RetriesExhausted(AssessmentOutcome? outcome) =>
        outcome is not null &&
        outcome.Result.Status != MetricVerificationStatus.Verified &&
        outcome.State.ProductionAttempt >= MaxRetries;

    /// <summary>Advances to Prediction when the Recall check found the
    /// attempt sufficient, OR when the retry budget is exhausted (fixed
    /// 2026-07-16) — never traps the learner in an infinite retrieval
    /// loop even if the Tutor keeps judging attempts insufficient. NEVER
    /// true for a genuine clarifying question (fixed 2026-07-17) — asking
    /// a question must always route back to <see cref="RecallRetryExecutor"/>
    /// so it gets answered, regardless of the retry budget.</summary>
    private static bool IsRecallSufficient(RecallCheckOutcome? outcome) =>
        outcome is not null &&
        outcome.Result.IsGenuineAttempt &&
        (outcome.Result.IsSufficient || outcome.State.RecallAttempt >= MaxRecallRetries);

    /// <summary>Loops back for one more bounded retrieval attempt when the
    /// Recall check found the attempt insufficient AND retries remain
    /// (fixed 2026-07-16) — OR unconditionally when the learner asked a
    /// genuine clarifying question instead of attempting recall (fixed
    /// 2026-07-17), since that never consumes the retry budget.</summary>
    private static bool RecallNeedsRetry(RecallCheckOutcome? outcome) =>
        outcome is not null &&
        (!outcome.Result.IsGenuineAttempt ||
            (!outcome.Result.IsSufficient && outcome.State.RecallAttempt < MaxRecallRetries));

    /// <summary>Advances to the next chapter (via ChapterAdvanceExecutor)
    /// when the CHAPTER-scoped Recall check found the attempt sufficient,
    /// OR when its own retry budget is exhausted (fixed 2026-07-16) —
    /// same never-trap-the-learner rationale as <see cref="IsRecallSufficient"/>,
    /// but tracked by <see cref="RuntimeSessionState.ChapterRecallAttempt"/>
    /// instead of the module-wide <see cref="RuntimeSessionState.RecallAttempt"/>.
    /// Same NEVER-true-for-a-question rule as <see cref="IsRecallSufficient"/>
    /// (fixed 2026-07-17).</summary>
    private static bool IsChapterRecallSufficient(RecallCheckOutcome? outcome) =>
        outcome is not null &&
        outcome.Result.IsGenuineAttempt &&
        (outcome.Result.IsSufficient || outcome.State.ChapterRecallAttempt >= MaxRecallRetries);

    /// <summary>Loops back for one more bounded retrieval attempt on the
    /// CURRENT chapter when its Recall check found the attempt
    /// insufficient AND retries remain (fixed 2026-07-16) — same
    /// unconditional-retry-for-a-question rule as <see cref="RecallNeedsRetry"/>
    /// (fixed 2026-07-17).</summary>
    private static bool ChapterRecallNeedsRetry(RecallCheckOutcome? outcome) =>
        outcome is not null &&
        (!outcome.Result.IsGenuineAttempt ||
            (!outcome.Result.IsSufficient && outcome.State.ChapterRecallAttempt < MaxRecallRetries));

    /// <summary>Loops back to <see cref="ChapterPredictionExecutor"/> for
    /// the next sub-question when the current chapter's PredictionPrompt
    /// was authored as a multi-part questionnaire and sub-questions
    /// remain (fixed 2026-07-17 — see <see cref="MultiPartPromptSegmenter"/>).
    /// Always <see langword="false"/> for a well-formed single-question
    /// PredictionPrompt (1 segment) — zero behavior change for the
    /// intended Studio authoring shape.</summary>
    private static bool MorePredictionPartsRemain(RuntimeSessionState? state)
    {
        if (state is null)
        {
            return false;
        }

        var chapter = state.Session.Contract.Chapters[state.CurrentChapterIndex];
        var segmentCount = MultiPartPromptSegmenter.Split(chapter.PredictionPrompt ?? string.Empty).Count;
        return state.PredictionDialogueTurn < segmentCount;
    }

    /// <summary>Every Prediction sub-question has been asked — proceeds to
    /// <see cref="ChapterMiniPracticeExecutor"/> (fixed 2026-07-17).</summary>
    private static bool NoMorePredictionPartsRemain(RuntimeSessionState? state) =>
        state is not null && !MorePredictionPartsRemain(state);

    /// <summary>Loops back to <see cref="LearnerProductionExecutor"/> for
    /// the next item when the module's LearnerTask was authored as a
    /// multi-part list and items remain (fixed 2026-07-17 — see
    /// <see cref="MultiPartPromptSegmenter"/>). Always <see langword="false"/>
    /// for a well-formed single-item task — zero behavior change for
    /// modules published before this fix (empty/single LearnerTask).</summary>
    private static bool MoreProductionItemsRemain(RuntimeSessionState? state)
    {
        if (state is null)
        {
            return false;
        }

        var segmentCount = MultiPartPromptSegmenter.Split(state.Session.Contract.LearnerTask ?? string.Empty).Count;
        return state.ProductionItemTurn < segmentCount;
    }

    /// <summary>Every LearnerTask item has been submitted — proceeds to
    /// <see cref="AssessmentExecutor"/> (fixed 2026-07-17).</summary>
    private static bool NoMoreProductionItemsRemain(RuntimeSessionState? state) =>
        state is not null && !MoreProductionItemsRemain(state);
}

