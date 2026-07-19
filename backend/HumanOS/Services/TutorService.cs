using HumanOS.Agentic.Runtime;
using HumanOS.Agents.Runtime;
using HumanOS.Data;
using HumanOS.Models.Capabilities.Graph;
using HumanOS.Models.Learning;
using Microsoft.Agents.AI.Workflows;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.Services;

/// <summary>
/// Orchestrates on-demand TutorAgentV2 turns: loads the DB context a
/// student needs help with (blueprint step Content + conversation history
/// reconstructed from LearningEvidence), runs
/// <see cref="TutorWorkflowFactory"/>'s Workflow in-process, and returns
/// the result.
///
/// <see cref="AskAsync"/> (Teaching/Production/AssessmentFeedback) persists
/// each free-form Q&amp;A exchange itself, right after running the turn, as
/// its own LearningEvidence row (StudentResponse = the student's question,
/// TutorPrompt = the Tutor's reply, TutorScore = null since nothing here is
/// graded) — so the log is complete even if the student never follows up
/// with a graded submission (product decision, 2026-07-19). This is
/// separate from — and in addition to — a LATER graded submission for the
/// same step also persisting its own row via
/// <see cref="InstructorRuntimeOrchestrator.SubmitResponseAsync"/>'s
/// <c>tutorPrompt</c>/<c>tutorScore</c> parameters.
///
/// <see cref="SubmitRecallAttemptAsync"/> (Recall only) is the one
/// exception: since a Recall turn always scores an attempt the student
/// ALREADY made, it persists the LearningEvidence row itself AND applies
/// <see cref="RecallLoopGate"/>'s deterministic mastery/attempt-cap rule to
/// decide whether to advance the step — in the same call, so the frontend
/// never has to orchestrate that sequencing itself.
///
/// Same "service calls the agent's Workflow, workflow never touches EF
/// Core itself" split used across the Studio pipeline
/// (CapabilityCreationOrchestrator loads/persists, the Workflow only
/// transforms in-memory messages).
/// </summary>
public sealed class TutorService
{
    private readonly TutorAgentV2 _tutorAgent;
    private readonly InstructorRuntimeOrchestrator _orchestrator;

    public TutorService(TutorAgentV2 tutorAgent, InstructorRuntimeOrchestrator orchestrator)
    {
        _tutorAgent = tutorAgent;
        _orchestrator = orchestrator;
    }

    public bool IsConfigured => _tutorAgent.IsConfigured;

    /// <summary>Result of one persisted Recall attempt: the Tutor's turn,
    /// the LearningEvidence row it produced, and the deterministic
    /// mastery/advance verdict computed by <see cref="RecallLoopGate"/>.</summary>
    public sealed class RecallAttemptOutcome
    {
        public TutorTurnResult TutorTurn { get; set; } = null!;

        public Guid LearningEvidenceId { get; set; }

        /// <summary>Attempts used on the CURRENT item (resets to 0 after
        /// each mastered item, and after a regression-to-Teaching cycle).</summary>
        public int AttemptsUsedForItem { get; set; }

        /// <summary>How many of the <see cref="RecallLoopGate.ItemsRequiredToAdvance"/>
        /// distinct items the student has mastered so far on this step.</summary>
        public int ItemsMastered { get; set; }

        public int ItemsRequired { get; set; } = RecallLoopGate.ItemsRequiredToAdvance;

        /// <summary>True if THIS specific attempt was scored as mastery of
        /// its item (RecallScore &gt;= MasteryThreshold).</summary>
        public bool Mastered { get; set; }

        /// <summary>True only when all <see cref="ItemsRequired"/> items are
        /// now mastered and the step advanced to Production — <see cref="NextStep"/>
        /// is set in that case.</summary>
        public bool Advanced { get; set; }

        /// <summary>True when the student exhausted their attempt budget on
        /// one item without mastering it, and the whole node regressed back
        /// to Teaching to review — <see cref="NextStep"/> is set (the
        /// reactivated Teaching step) in that case.</summary>
        public bool RegressedToTeaching { get; set; }

        public InstructorRuntimeOrchestrator.CurrentStepResult? NextStep { get; set; }
    }

    /// <param name="dbContext">DbContext to read with (caller owns its lifetime).</param>
    /// <param name="learningSessionStepId">The step the student is currently on.</param>
    /// <param name="mode">Which pedagogical situation this turn is. Use
    /// <see cref="SubmitRecallAttemptAsync"/> instead of this method for
    /// <see cref="TutorInteractionMode.Recall"/> — it also persists and
    /// applies the attempt-cap/mastery rule.</param>
    /// <param name="studentMessage">What the student just said/submitted this turn.</param>
    /// <param name="rawAssessmentFeedback">Only required when
    /// <paramref name="mode"/> is <see cref="TutorInteractionMode.AssessmentFeedback"/> —
    /// the raw AssessmentEvaluatorAgent feedback text to translate.</param>
    public async Task<TutorTurnResult> AskAsync(
        HumanOsDbContext dbContext,
        Guid learningSessionStepId,
        TutorInteractionMode mode,
        string studentMessage,
        string? rawAssessmentFeedback = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        var step = await LoadStepAsync(dbContext, learningSessionStepId, cancellationToken);
        var request = await BuildRequestAsync(dbContext, step, mode, studentMessage, rawAssessmentFeedback, cancellationToken);

        var turn = await RunWorkflowAsync(request, cancellationToken);

        // Persist this free-form Q&A exchange too (product decision,
        // 2026-07-19 — the log should be complete, not just the
        // graded Recall/Production submissions). Direction is reversed
        // from a normal LearningEvidence row (student ASKS, tutor
        // ANSWERS) — StudentResponse holds the student's question,
        // TutorPrompt holds the Tutor's reply, TutorScore stays null
        // since nothing here is graded. This also means it naturally
        // shows up in BuildRequestAsync's History reconstruction (and in
        // any future step-review screen) alongside graded evidence,
        // ordered purely by CreatedDate.
        await _orchestrator.SubmitResponseAsync(
            dbContext,
            learningSessionStepId,
            studentMessage,
            cancellationToken,
            tutorPrompt: turn.Response.Message);

        return turn;
    }

    /// <summary>
    /// Scores one Recall attempt the student already made, persists it as
    /// LearningEvidence (<paramref name="tutorPromptShown"/> becomes this
    /// row's TutorPrompt, the freshly-computed score becomes its
    /// TutorScore), and — per <see cref="RecallLoopGate"/> — either: (a)
    /// keeps the loop going on the current or next item, (b) advances the
    /// step to Production once <see cref="RecallLoopGate.ItemsRequiredToAdvance"/>
    /// distinct items have been mastered, or (c) regresses the whole node
    /// back to Teaching if the student exhausted
    /// <see cref="RecallLoopGate.MaxAttemptsPerItem"/> attempts on one item
    /// without mastering it (confirmed product decision, 2026-07-19 — a
    /// failed item now sends the student back to review, it no longer
    /// silently advances with a low score).
    /// </summary>
    /// <param name="tutorPromptShown">The hint/question the Tutor showed
    /// the student right before THIS attempt (the previous call's
    /// <see cref="TutorTurnResponse.Message"/>) — null for the student's
    /// very first attempt on this step, which responds to the blueprint's
    /// own Recall content instead.</param>
    public async Task<RecallAttemptOutcome> SubmitRecallAttemptAsync(
        HumanOsDbContext dbContext,
        Guid learningSessionStepId,
        string studentResponse,
        string? tutorPromptShown,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        var step = await LoadStepAsync(dbContext, learningSessionStepId, cancellationToken);

        if (step.StepType != ExperienceStepType.Recall)
        {
            throw new InvalidOperationException(
                $"LearningSessionStep {learningSessionStepId} is not a Recall step (StepType={step.StepType}) — use AskAsync instead.");
        }

        // Only count attempts scored since this step's CURRENT activation —
        // if a prior Recall pass ended in a regression back to Teaching,
        // AdvanceToNextStepAsync bumps StartedDate when the step is later
        // reactivated, which naturally resets the item/attempt bookkeeping
        // without needing to touch or delete the older LearningEvidence rows.
        var priorScoresThisActivation = step.Evidence
            .Where(e => e.TutorScore.HasValue && (step.StartedDate == null || e.CreatedDate >= step.StartedDate))
            .OrderBy(e => e.CreatedDate)
            .Select(e => e.TutorScore!.Value)
            .ToList();

        var request = await BuildRequestAsync(dbContext, step, TutorInteractionMode.Recall, studentResponse, rawAssessmentFeedback: null, cancellationToken);
        var turn = await RunWorkflowAsync(request, cancellationToken);

        var score = turn.Response.RecallScore
            ?? throw new InvalidOperationException("TutorAgentV2 did not return a RecallScore for a Recall-mode turn.");

        var evidenceId = await _orchestrator.SubmitResponseAsync(
            dbContext,
            learningSessionStepId,
            studentResponse,
            cancellationToken,
            tutorPrompt: tutorPromptShown,
            tutorScore: score);

        var verdict = RecallLoopGate.Evaluate(priorScoresThisActivation, score);

        InstructorRuntimeOrchestrator.CurrentStepResult? nextStep = null;
        if (verdict.StepComplete)
        {
            nextStep = await _orchestrator.AdvanceToNextStepAsync(dbContext, step.LearningSessionNodeId, cancellationToken);
        }
        else if (verdict.ItemFailed)
        {
            nextStep = await _orchestrator.RegressToTeachingAsync(dbContext, step.LearningSessionNodeId, cancellationToken);
        }

        return new RecallAttemptOutcome
        {
            TutorTurn = turn,
            LearningEvidenceId = evidenceId,
            AttemptsUsedForItem = verdict.AttemptsUsedForCurrentItem,
            ItemsMastered = verdict.ItemsMasteredSoFar,
            Mastered = verdict.ItemMasteredThisAttempt,
            Advanced = verdict.StepComplete,
            RegressedToTeaching = verdict.ItemFailed,
            NextStep = nextStep
        };
    }

    private static async Task<LearningSessionStep> LoadStepAsync(
        HumanOsDbContext dbContext,
        Guid learningSessionStepId,
        CancellationToken cancellationToken)
    {
        var step = await dbContext.LearningSessionSteps
            .Include(s => s.Evidence)
            .Include(s => s.LearningSessionNode)
            .FirstOrDefaultAsync(s => s.LearningSessionStepId == learningSessionStepId, cancellationToken);

        if (step is null)
        {
            throw new InvalidOperationException($"LearningSessionStep {learningSessionStepId} not found.");
        }

        if (step.LearningSessionNode is null)
        {
            throw new InvalidOperationException(
                $"LearningSessionStep {learningSessionStepId} has no parent LearningSessionNode — data inconsistency.");
        }

        return step;
    }

    private static async Task<TutorTurnRequest> BuildRequestAsync(
        HumanOsDbContext dbContext,
        LearningSessionStep step,
        TutorInteractionMode mode,
        string studentMessage,
        string? rawAssessmentFeedback,
        CancellationToken cancellationToken)
    {
        string stepContent = string.Empty;
        var illustrations = new List<TutorIllustrationRef>();
        if (mode != TutorInteractionMode.AssessmentFeedback)
        {
            var nodeExperienceBlueprintId = step.LearningSessionNode!.NodeExperienceBlueprintId;

            var blueprintStep = await dbContext.NodeExperienceBlueprintSteps
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.NodeExperienceBlueprintId == nodeExperienceBlueprintId && s.StepType == step.StepType, cancellationToken)
                ?? throw new InvalidOperationException(
                    $"Blueprint {nodeExperienceBlueprintId} has no {step.StepType} content — data inconsistency.");

            stepContent = blueprintStep.Content;

            // Same resolution as InstructorRuntimeOrchestrator.BuildCurrentStepResultAsync
            // (illustrations never live in the Blueprint itself, only references to
            // rows in CapabilityGraphNodeIllustration) — the full ref (including
            // StoragePath) is kept so the caller/frontend can actually render the
            // image, not just the caption text fed to the LLM.
            if (!string.IsNullOrWhiteSpace(blueprintStep.ReferencedIllustrationIdsJson))
            {
                var illustrationIds = System.Text.Json.JsonSerializer.Deserialize<List<Guid>>(blueprintStep.ReferencedIllustrationIdsJson) ?? [];
                if (illustrationIds.Count > 0)
                {
                    illustrations = await dbContext.CapabilityGraphNodeIllustrations
                        .AsNoTracking()
                        .Where(i => illustrationIds.Contains(i.CapabilityGraphNodeIllustrationId))
                        .Select(i => new TutorIllustrationRef
                        {
                            CapabilityGraphNodeIllustrationId = i.CapabilityGraphNodeIllustrationId,
                            StoragePath = i.StoragePath,
                            Caption = i.Caption
                        })
                        .ToListAsync(cancellationToken);
                }
            }
        }

        var history = step.Evidence
            .OrderBy(e => e.CreatedDate)
            .Select(e => new TutorTurnHistoryEntry
            {
                TutorPrompt = e.TutorPrompt,
                StudentResponse = e.StudentResponse
            })
            .ToList();

        return new TutorTurnRequest
        {
            Mode = mode,
            StepContent = stepContent,
            Illustrations = illustrations,
            History = history,
            StudentMessage = studentMessage,
            RawAssessmentFeedback = rawAssessmentFeedback
        };
    }

    private async Task<TutorTurnResult> RunWorkflowAsync(TutorTurnRequest request, CancellationToken cancellationToken)
    {
        var workflow = TutorWorkflowFactory.Build(_tutorAgent);

        await using var run = await InProcessExecution.RunStreamingAsync(
            workflow,
            request,
            cancellationToken: cancellationToken);

        await foreach (var evt in run.WatchStreamAsync(cancellationToken))
        {
            if (evt is WorkflowOutputEvent output && output.Data is TutorTurnResult result)
            {
                return result;
            }
        }

        throw new InvalidOperationException("TutorWorkflowFactory's workflow ended without yielding a TutorTurnResult output.");
    }
}
