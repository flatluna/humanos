using HumanOS.Data;
using HumanOS.Models.Capabilities.Graph;
using HumanOS.Models.Learning;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.Services;

/// <summary>
/// Runtime Paso 2 — "Instructor Runtime Orchestrator". This is the engine
/// that EXECUTES an already-validated NodeExperienceBlueprint for a real
/// person: it creates/advances a LearningSession through the fixed Memory
/// Paradox sequence (Hypothesis → Teaching → Recall → Production →
/// Assessment) and persists whatever the student produces. It never
/// generates content, never designs pedagogy, and never modifies a
/// Blueprint — those belong to Studio. No AI, no chat, no voice, no
/// automatic evaluation happens here (Assessment scoring is a LATER
/// Runtime Paso — <see cref="AssessmentEvaluator"/>).
///
/// Mental model: Node = what to learn, Blueprint = the pedagogical GPS,
/// this orchestrator = the engine that drives the GPS, LearningSession =
/// one real person's trip.
/// </summary>
public sealed class InstructorRuntimeOrchestrator
{
    private static readonly ExperienceStepType[] CanonicalStepOrder =
    [
        ExperienceStepType.Hypothesis,
        ExperienceStepType.Teaching,
        ExperienceStepType.Recall,
        ExperienceStepType.Production,
        ExperienceStepType.Assessment
    ];

    /// <summary>Used ONLY when the node's blueprint includes the optional
    /// Examples step (formal/school-track capabilities) — see
    /// NodeExperienceBlueprintPersistenceService's identically-named array
    /// for the full rationale. Examples behaves as a plain "read + continue"
    /// step, exactly like Teaching, with no special gating.</summary>
    private static readonly ExperienceStepType[] FormalEducationStepOrder =
    [
        ExperienceStepType.Hypothesis,
        ExperienceStepType.Teaching,
        ExperienceStepType.DeepDive,
        ExperienceStepType.Recall,
        ExperienceStepType.Production,
        ExperienceStepType.Assessment
    ];

    /// <summary>Used ONLY when the node's blueprint omits the optional
    /// Production step (2026-08-04, see
    /// <see cref="HumanOS.Models.Capabilities.Capability.IncludeProductionStep"/>)
    /// and has no Deep Dive step either.</summary>
    private static readonly ExperienceStepType[] NoProductionStepOrder =
    [
        ExperienceStepType.Hypothesis,
        ExperienceStepType.Teaching,
        ExperienceStepType.Recall,
        ExperienceStepType.Assessment
    ];

    /// <summary>Used ONLY when the node's blueprint omits the optional
    /// Production step but DOES have a Deep Dive step.</summary>
    private static readonly ExperienceStepType[] FormalEducationNoProductionStepOrder =
    [
        ExperienceStepType.Hypothesis,
        ExperienceStepType.Teaching,
        ExperienceStepType.DeepDive,
        ExperienceStepType.Recall,
        ExperienceStepType.Assessment
    ];

    /// <summary>One illustration resolved from Data Lake metadata (never the image bytes themselves).</summary>
    public sealed class IllustrationRef
    {
        public Guid CapabilityGraphNodeIllustrationId { get; set; }
        public string StoragePath { get; set; } = string.Empty;
        public string? Caption { get; set; }

        /// <summary>Raw generation-model tag ("deterministic-skiasharp-v1" or a
        /// generative image model name like "gpt-image-2") — surfaced to the
        /// UI so reviewers/students can see exactly which pipeline produced
        /// each image.</summary>
        public string ImageModel { get; set; } = string.Empty;
    }

    /// <summary>Result of starting a new LearningSession at its first (Hypothesis) step.</summary>
    public sealed class StartSessionResult
    {
        public Guid LearningSessionId { get; set; }
        public Guid LearningSessionNodeId { get; set; }
        public Guid CapabilityGraphNodeId { get; set; }
        public Guid NodeExperienceBlueprintId { get; set; }
    }

    /// <summary>The full picture of "where is this person right now" — what GetCurrentStep/AdvanceToNextStep return.</summary>
    public sealed class CurrentStepResult
    {
        public Guid LearningSessionId { get; set; }
        public Guid LearningSessionNodeId { get; set; }
        public Guid LearningSessionStepId { get; set; }
        public ExperienceStepType StepType { get; set; }
        public string StepContent { get; set; } = string.Empty;
        public List<IllustrationRef> Illustrations { get; set; } = [];

        /// <summary>Only populated when StepType == DeepDive.</summary>
        public List<ExampleCard>? ExampleCards { get; set; }

        /// <summary>Only populated when StepType == DeepDive: 0-based index
        /// of the card the student is currently required to master.</summary>
        public int? CurrentDeepDiveCardIndex { get; set; }

        /// <summary>Only populated when StepType == DeepDive: total number
        /// of cards in this node's Deep Dive pool (ExampleCards.Count).</summary>
        public int? DeepDiveTotalCards { get; set; }
    }

    /// <summary>One past student response, as recorded in LearningEvidence — read-only.</summary>
    public sealed class EvidenceEntryRef
    {
        public string StudentResponse { get; set; } = string.Empty;
        public string? TutorPrompt { get; set; }
        public int? TutorScore { get; set; }
        public DateTime CreatedDate { get; set; }
    }

    /// <summary>
    /// Read-only "what did I do/answer during this step" view — for a
    /// step that has already been started (Active or Completed). Never
    /// mutates anything (no reactivation, no evidence writes); purely for
    /// the student to review their own history. See
    /// <see cref="GetStepReviewAsync"/>.
    /// </summary>
    public sealed class StepReviewResult
    {
        public Guid LearningSessionNodeId { get; set; }
        public ExperienceStepType StepType { get; set; }
        public LearningSessionStepStatus Status { get; set; }
        public DateTime? StartedDate { get; set; }
        public DateTime? CompletedDate { get; set; }
        public string StepContent { get; set; } = string.Empty;
        public List<IllustrationRef> Illustrations { get; set; } = [];
        public List<EvidenceEntryRef> Evidence { get; set; } = [];

        /// <summary>Only populated when StepType == Examples.</summary>
        public List<ExampleCard>? ExampleCards { get; set; }
    }

    /// <summary>One past completed attempt at this node — read-only summary row, see <see cref="NodeSummaryResult.PastAttempts"/>.</summary>
    public sealed class NodeAttemptSummary
    {
        public Guid LearningSessionNodeId { get; set; }
        public DateTime? StartedDate { get; set; }
        public DateTime? CompletedDate { get; set; }
        public int? FinalScore { get; set; }
        public bool Passed { get; set; }
    }

    public sealed class NodeMemorySummary
    {
        public int RecallPercent { get; set; }
        public string MemoryState { get; set; } = "Strong";
        public int ReviewCount { get; set; }
        public DateTime? LastReviewDate { get; set; }
        public bool IsAutomatic { get; set; }
        public int ReviewsUntilAutomatic { get; set; }
        public DateTime? NextReviewDate { get; set; }
    }

    public sealed class NodeRewardSummary
    {
        public int PointsAwarded { get; set; }
        public bool WasSurprise { get; set; }
        public string SurpriseType { get; set; } = "NONE";
        public int PointsBase { get; set; }
        public decimal DifficultyMultiplier { get; set; }
        public decimal MemoryEffortMultiplier { get; set; }
        public decimal SurpriseMultiplier { get; set; }
        public DateTime? EarnedAt { get; set; }
    }

    public sealed class NodeImpactSummary
    {
        public int Score { get; set; }
        public string Tier { get; set; } = string.Empty;
        public string? PrimarySignal { get; set; }
        public string? Reason { get; set; }
    }

    /// <summary>
    /// Read-only "what happened the last time I did this node" view — for a
    /// node the person has already Completed at least once (i.e. Mastered
    /// on the map). Never starts a new attempt; see
    /// <see cref="GetNodeSummaryAsync"/>.
    /// </summary>
    public sealed class NodeSummaryResult
    {
        public Guid CapabilityGraphNodeId { get; set; }

        /// <summary>Which completed attempt <see cref="Steps"/> below belongs to — always the
        /// most recent one (<c>PastAttempts[0]</c>). The frontend uses this to know which
        /// row in <see cref="PastAttempts"/> is already loaded vs. which ones it still needs
        /// to fetch (via GetStepReview per step) if the student picks an older attempt.</summary>
        public Guid LearningSessionNodeId { get; set; }
        public int AttemptCount { get; set; }
        public DateTime? FirstCompletedDate { get; set; }
        public DateTime? LastCompletedDate { get; set; }
        public int? FinalScore { get; set; }
        public List<StepReviewResult> Steps { get; set; } = [];
        public List<NodeAttemptSummary> PastAttempts { get; set; } = [];
        public NodeMemorySummary? Memory { get; set; }
        public NodeRewardSummary? Reward { get; set; }
        public NodeImpactSummary? Impact { get; set; }
    }

    /// <summary>
    /// Starts a brand-new LearningSession for one person on one Capability,
    /// resolving the node's blueprint itself (the MOST RECENTLY created
    /// NodeExperienceBlueprint for that node — no mastery/recommendation
    /// logic exists yet to pick among several), and positions it at that
    /// blueprint's Hypothesis step.
    /// </summary>
    public async Task<StartSessionResult> StartSessionAsync(
        HumanOsDbContext dbContext,
        Guid personId,
        Guid capabilityId,
        Guid capabilityGraphNodeId,
        Guid? programId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        var blueprint = await dbContext.NodeExperienceBlueprints
            .Include(b => b.Steps)
            .Where(b => b.CapabilityGraphNodeId == capabilityGraphNodeId)
            .OrderByDescending(b => b.CreatedDate)
            .FirstOrDefaultAsync(cancellationToken);

        if (blueprint is null)
        {
            throw new InvalidOperationException(
                $"No NodeExperienceBlueprint exists for CapabilityGraphNode {capabilityGraphNodeId} — cannot start a LearningSession without one.");
        }

        if (!blueprint.Steps.Any(s => s.StepType == ExperienceStepType.Hypothesis))
        {
            throw new InvalidOperationException(
                $"Blueprint {blueprint.NodeExperienceBlueprintId} has no Hypothesis step — cannot start a LearningSession from it.");
        }

        var now = DateTime.UtcNow;

        var session = new LearningSession
        {
            PersonId = personId,
            CapabilityId = capabilityId,
            ProgramId = programId,
            Status = LearningSessionStatus.Active,
            StartedDate = now
        };

        var sessionNode = new LearningSessionNode
        {
            CapabilityGraphNodeId = capabilityGraphNodeId,
            NodeExperienceBlueprintId = blueprint.NodeExperienceBlueprintId,
            Status = LearningSessionNodeStatus.Active,
            StartedDate = now
        };
        session.Nodes.Add(sessionNode);

        var sessionStep = new LearningSessionStep
        {
            StepType = ExperienceStepType.Hypothesis,
            Status = LearningSessionStepStatus.Active,
            StartedDate = now
        };
        sessionNode.Steps.Add(sessionStep);

        dbContext.LearningSessions.Add(session);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new StartSessionResult
        {
            LearningSessionId = session.LearningSessionId,
            LearningSessionNodeId = sessionNode.LearningSessionNodeId,
            CapabilityGraphNodeId = capabilityGraphNodeId,
            NodeExperienceBlueprintId = blueprint.NodeExperienceBlueprintId
        };
    }

    /// <summary>
    /// Returns the step the person is currently on for this node (the one
    /// LearningSessionStep row with Status=Active — the unique-per-StepType
    /// constraint guarantees there's at most one), with its content pulled
    /// from the Blueprint and its illustrations resolved from
    /// CapabilityGraphNodeIllustration/Data Lake (illustrations never live
    /// in the Blueprint itself — only references to them).
    /// </summary>
    public async Task<CurrentStepResult> GetCurrentStepAsync(
        HumanOsDbContext dbContext,
        Guid learningSessionNodeId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        var sessionNode = await dbContext.LearningSessionNodes
            .Include(n => n.Steps)
            .FirstOrDefaultAsync(n => n.LearningSessionNodeId == learningSessionNodeId, cancellationToken);

        if (sessionNode is null)
        {
            throw new InvalidOperationException($"LearningSessionNode {learningSessionNodeId} not found.");
        }

        var activeStep = sessionNode.Steps.SingleOrDefault(s => s.Status == LearningSessionStepStatus.Active);
        if (activeStep is null)
        {
            throw new InvalidOperationException(
                $"LearningSessionNode {learningSessionNodeId} has no Active step — it may already be Completed.");
        }

        return await BuildCurrentStepResultAsync(dbContext, sessionNode, activeStep, cancellationToken);
    }

    /// <summary>
    /// Persists what the person actually did/answered during a step, as
    /// APPEND-ONLY LearningEvidence. Does NOT evaluate it in any way —
    /// evaluation (for Assessment specifically) is
    /// <see cref="AssessmentEvaluator"/>'s job, a separate Runtime Paso.
    /// </summary>
    /// <param name="tutorPrompt">Optional — the question/hint the TutorAgent
    /// gave right before this response (null when no Tutor interaction
    /// preceded it, e.g. a plain Hypothesis/Teaching/Production submission).</param>
    /// <param name="tutorScore">Optional — ephemeral 0-100 score the
    /// TutorAgent assigned to THIS attempt (only meaningful for Recall-style
    /// loops). Never written to <see cref="LearningAssessmentResult"/>.</param>
    public async Task<Guid> SubmitResponseAsync(
        HumanOsDbContext dbContext,
        Guid learningSessionStepId,
        string personResponse,
        CancellationToken cancellationToken = default,
        string? tutorPrompt = null,
        int? tutorScore = null)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        var stepExists = await dbContext.LearningSessionSteps
            .AnyAsync(s => s.LearningSessionStepId == learningSessionStepId, cancellationToken);

        if (!stepExists)
        {
            throw new InvalidOperationException($"LearningSessionStep {learningSessionStepId} not found.");
        }

        var evidence = new LearningEvidence
        {
            LearningSessionStepId = learningSessionStepId,
            StudentResponse = personResponse,
            TutorPrompt = tutorPrompt,
            TutorScore = tutorScore
        };

        dbContext.LearningEvidences.Add(evidence);
        await dbContext.SaveChangesAsync(cancellationToken);

        return evidence.LearningEvidenceId;
    }

    /// <summary>
    /// Marks the current Active step Completed and creates the next step in
    /// the FIXED Memory Paradox order (Hypothesis → Teaching → Recall →
    /// Production → Assessment) — the order is dictated entirely by
    /// <see cref="ExperienceStepType"/>'s own numeric sequence, never
    /// invented or reordered here. Throws if called while already on
    /// Assessment — call <see cref="CompleteNodeAsync"/> instead, there is
    /// no 6th step.
    /// </summary>
    public async Task<CurrentStepResult> AdvanceToNextStepAsync(
        HumanOsDbContext dbContext,
        Guid learningSessionNodeId,
        CancellationToken cancellationToken = default,
        bool bypassGatedStepGuard = false)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        var sessionNode = await dbContext.LearningSessionNodes
            .Include(n => n.Steps)
            .FirstOrDefaultAsync(n => n.LearningSessionNodeId == learningSessionNodeId, cancellationToken);

        if (sessionNode is null)
        {
            throw new InvalidOperationException($"LearningSessionNode {learningSessionNodeId} not found.");
        }

        var activeStep = sessionNode.Steps.SingleOrDefault(s => s.Status == LearningSessionStepStatus.Active);
        if (activeStep is null)
        {
            throw new InvalidOperationException(
                $"LearningSessionNode {learningSessionNodeId} has no Active step to advance from.");
        }

        if (activeStep.StepType == ExperienceStepType.Assessment)
        {
            throw new InvalidOperationException(
                "Already on the Assessment step — there is no next step. Call CompleteNodeAsync instead.");
        }

        // Recall and Deep Dive NEVER advance through the GENERIC public
        // step-advance endpoint (RuntimeAdvanceStepFunction) — they can
        // only move forward via SubmitRecallAttemptAsync/
        // SubmitDeepDiveAttemptAsync's own deterministic gates
        // (RecallLoopGate/DeepDiveLoopGate), which require the student to
        // actually demonstrate mastery first. Without this guard, calling
        // this generic endpoint while either is Active would silently
        // complete it with zero/incomplete LearningEvidence, bypassing the
        // mastery check entirely — this is exactly what happened to stale
        // test data found in the DB for one node (Recall step completed in
        // 34s with 0 evidence rows, immediately followed by Production).
        // `bypassGatedStepGuard` is set ONLY by TutorService's
        // SubmitRecallAttemptAsync/SubmitDeepDiveAttemptAsync, which call
        // this method themselves once their respective gate already
        // confirmed StepComplete — without the bypass, that legitimate
        // internal call would ALSO trip this guard (the step is still
        // StepType=Recall/DeepDive at this exact point — it only becomes
        // Completed inside this method).
        if ((activeStep.StepType == ExperienceStepType.Recall || activeStep.StepType == ExperienceStepType.DeepDive) && !bypassGatedStepGuard)
        {
            throw new InvalidOperationException(
                $"Cannot advance away from {activeStep.StepType} via the generic step-advance path — {activeStep.StepType} only advances through its own dedicated attempt endpoint once its mastery gate is satisfied.");
        }

        // Formal/school-track blueprints (Capability.IncludeExamplesStep)
        // include an OPTIONAL Examples step between Teaching and Recall —
        // detect its presence directly from THIS blueprint (not from the
        // Capability flag, which could theoretically change after the
        // blueprint was generated) and advance through the matching order.
        // Same detection approach for the OPTIONAL Production step
        // (Capability.IncludeProductionStep, 2026-08-04) — a blueprint
        // persisted with it omitted has no Production row at all, so
        // advancing must skip straight from Recall to Assessment.
        var hasExamplesStep = await dbContext.NodeExperienceBlueprintSteps
            .AsNoTracking()
            .AnyAsync(s => s.NodeExperienceBlueprintId == sessionNode.NodeExperienceBlueprintId && s.StepType == ExperienceStepType.DeepDive, cancellationToken);
        var hasProductionStep = await dbContext.NodeExperienceBlueprintSteps
            .AsNoTracking()
            .AnyAsync(s => s.NodeExperienceBlueprintId == sessionNode.NodeExperienceBlueprintId && s.StepType == ExperienceStepType.Production, cancellationToken);
        var stepOrder = (hasExamplesStep, hasProductionStep) switch
        {
            (true, true) => FormalEducationStepOrder,
            (true, false) => FormalEducationNoProductionStepOrder,
            (false, true) => CanonicalStepOrder,
            (false, false) => NoProductionStepOrder
        };

        var currentIndex = Array.IndexOf(stepOrder, activeStep.StepType);
        var nextStepType = stepOrder[currentIndex + 1];

        var now = DateTime.UtcNow;

        activeStep.Status = LearningSessionStepStatus.Completed;
        activeStep.CompletedDate = now;

        // A row for nextStepType can already exist if this node previously
        // went through a Recall regression cycle (RegressToTeachingAsync
        // resets Recall back to NotStarted without deleting its row, since
        // (LearningSessionNodeId, StepType) is unique) — reactivate that
        // existing row in place instead of inserting a duplicate, which the
        // unique index would reject.
        var existingNextStep = sessionNode.Steps.SingleOrDefault(s => s.StepType == nextStepType);

        LearningSessionStep nextStep;
        if (existingNextStep is not null)
        {
            existingNextStep.Status = LearningSessionStepStatus.Active;
            existingNextStep.StartedDate = now;
            existingNextStep.CompletedDate = null;
            // Reset the stale dynamic Recall prompt from a PRIOR
            // activation cycle — a fresh Recall pass starts from the
            // blueprint's static content again, same as priorScoresThisActivation
            // being scoped to StartedDate.
            existingNextStep.CurrentRecallPrompt = null;
            nextStep = existingNextStep;
        }
        else
        {
            nextStep = new LearningSessionStep
            {
                LearningSessionNodeId = learningSessionNodeId,
                StepType = nextStepType,
                Status = LearningSessionStepStatus.Active,
                StartedDate = now
            };

            // Added explicitly via the DbSet (not just sessionNode.Steps.Add) —
            // sessionNode here is an already-tracked (Unchanged) entity loaded
            // by a query, not the root of an Add() call. Because
            // LearningSessionStepId already has a non-default Guid value (set
            // by the model's property initializer), relying on navigation-only
            // fixup makes EF Core assume this row already exists in the
            // database (Modified instead of Added), producing an UPDATE that
            // affects 0 rows and throws DbUpdateConcurrencyException. Adding it
            // via the DbSet directly forces the correct Added state.
            dbContext.LearningSessionSteps.Add(nextStep);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return await BuildCurrentStepResultAsync(dbContext, sessionNode, nextStep, cancellationToken);
    }

    /// <summary>
    /// Sends a node back from Recall to Teaching so the student can review
    /// the concept, because they exhausted their attempt budget on one
    /// Recall item without mastering it (see
    /// <see cref="Agentic.Runtime.RecallLoopGate"/>). Reactivates the
    /// existing (already-Completed) Teaching step row in place and resets
    /// the Recall step row back to NotStarted — never deletes/creates rows,
    /// since (LearningSessionNodeId, StepType) is unique. Older
    /// LearningEvidence on the Recall step is kept for history but is
    /// excluded from future item/attempt counting once Recall's
    /// StartedDate is bumped forward by the next
    /// <see cref="AdvanceToNextStepAsync"/> call that reactivates it.
    /// </summary>
    public async Task<CurrentStepResult> RegressToTeachingAsync(
        HumanOsDbContext dbContext,
        Guid learningSessionNodeId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        var sessionNode = await dbContext.LearningSessionNodes
            .Include(n => n.Steps)
            .FirstOrDefaultAsync(n => n.LearningSessionNodeId == learningSessionNodeId, cancellationToken);

        if (sessionNode is null)
        {
            throw new InvalidOperationException($"LearningSessionNode {learningSessionNodeId} not found.");
        }

        var recallStep = sessionNode.Steps.SingleOrDefault(s => s.Status == LearningSessionStepStatus.Active);
        if (recallStep is null || recallStep.StepType != ExperienceStepType.Recall)
        {
            throw new InvalidOperationException(
                $"LearningSessionNode {learningSessionNodeId} is not currently Active on Recall — cannot regress to Teaching.");
        }

        var teachingStep = sessionNode.Steps.SingleOrDefault(s => s.StepType == ExperienceStepType.Teaching);
        if (teachingStep is null)
        {
            throw new InvalidOperationException(
                $"LearningSessionNode {learningSessionNodeId} has no Teaching step — data inconsistency.");
        }

        var now = DateTime.UtcNow;

        recallStep.Status = LearningSessionStepStatus.NotStarted;
        recallStep.StartedDate = null;
        recallStep.CompletedDate = null;
        recallStep.CurrentRecallPrompt = null;

        teachingStep.Status = LearningSessionStepStatus.Active;
        teachingStep.StartedDate = now;
        teachingStep.CompletedDate = null;

        await dbContext.SaveChangesAsync(cancellationToken);

        return await BuildCurrentStepResultAsync(dbContext, sessionNode, teachingStep, cancellationToken);
    }

    /// <summary>
    /// Closes out a node once its Assessment step is done: marks the
    /// Assessment step Completed (if not already) and the
    /// LearningSessionNode itself Completed, with a completion timestamp.
    /// Does NOT touch CapabilityGraph edges, unlock any other node, or
    /// touch LearningSession.Status — that is explicitly out of scope for
    /// this Runtime Paso.
    /// </summary>
    public async Task CompleteNodeAsync(
        HumanOsDbContext dbContext,
        Guid learningSessionNodeId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        var sessionNode = await dbContext.LearningSessionNodes
            .Include(n => n.Steps)
            .FirstOrDefaultAsync(n => n.LearningSessionNodeId == learningSessionNodeId, cancellationToken);

        if (sessionNode is null)
        {
            throw new InvalidOperationException($"LearningSessionNode {learningSessionNodeId} not found.");
        }

        var assessmentStep = sessionNode.Steps.SingleOrDefault(s => s.StepType == ExperienceStepType.Assessment);
        if (assessmentStep is null)
        {
            throw new InvalidOperationException(
                $"LearningSessionNode {learningSessionNodeId} has no Assessment step yet — cannot complete the node.");
        }

        var now = DateTime.UtcNow;

        if (assessmentStep.Status != LearningSessionStepStatus.Completed)
        {
            assessmentStep.Status = LearningSessionStepStatus.Completed;
            assessmentStep.CompletedDate = now;
        }

        sessionNode.Status = LearningSessionNodeStatus.Completed;
        sessionNode.CompletedDate = now;

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Read-only lookup of what the student saw and answered during ANY
    /// step (Active or Completed) of a node — e.g. clicking a completed
    /// (green) step in the UI's stepper to review it. Never mutates
    /// anything: no reactivation, no new evidence, no status change. Throws
    /// if the step has never been started (StartedDate is null — nothing to
    /// review yet).
    /// </summary>
    public async Task<StepReviewResult> GetStepReviewAsync(
        HumanOsDbContext dbContext,
        Guid learningSessionNodeId,
        ExperienceStepType stepType,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        var sessionNode = await dbContext.LearningSessionNodes
            .AsNoTracking()
            .Include(n => n.Steps)
            .ThenInclude(s => s.Evidence)
            .FirstOrDefaultAsync(n => n.LearningSessionNodeId == learningSessionNodeId, cancellationToken);

        if (sessionNode is null)
        {
            throw new InvalidOperationException($"LearningSessionNode {learningSessionNodeId} not found.");
        }

        var step = sessionNode.Steps.SingleOrDefault(s => s.StepType == stepType);
        if (step is null || step.StartedDate is null)
        {
            throw new InvalidOperationException(
                $"Step {stepType} has not been started yet for LearningSessionNode {learningSessionNodeId} — nothing to review.");
        }

        var current = await BuildCurrentStepResultAsync(dbContext, sessionNode, step, cancellationToken);

        return new StepReviewResult
        {
            LearningSessionNodeId = sessionNode.LearningSessionNodeId,
            StepType = step.StepType,
            Status = step.Status,
            StartedDate = step.StartedDate,
            CompletedDate = step.CompletedDate,
            StepContent = current.StepContent,
            Illustrations = current.Illustrations,
            ExampleCards = current.ExampleCards,
            Evidence = step.Evidence
                .OrderBy(e => e.CreatedDate)
                .Select(e => new EvidenceEntryRef
                {
                    StudentResponse = e.StudentResponse,
                    TutorPrompt = e.TutorPrompt,
                    TutorScore = e.TutorScore,
                    CreatedDate = e.CreatedDate
                })
                .ToList()
        };
    }

    /// <summary>
    /// Read-only "what happened the last time I completed this node"
    /// summary — for opening a node that is already Mastered on the map.
    /// Deliberately does NOT start a new LearningSession/Node (that stays an
    /// explicit, separate "practicar de nuevo" action) — this only reads
    /// the MOST RECENT Completed LearningSessionNode's full 5-step detail
    /// (reusing <see cref="GetStepReviewAsync"/> for each step) plus basic
    /// stats across every past completed attempt for this node (attempt
    /// count, first/last completion dates, best passing score). Every past
    /// attempt is preserved forever — no unique constraint stops a person
    /// from completing the same CapabilityGraphNode more than once over
    /// time, which is exactly what a future "learning graph over time /
    /// forgetting curve" view will need to read.
    /// </summary>
    public async Task<NodeSummaryResult> GetNodeSummaryAsync(
        HumanOsDbContext dbContext,
        Guid personId,
        Guid capabilityGraphNodeId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        var completedNodes = await dbContext.LearningSessionNodes
            .AsNoTracking()
            .Include(n => n.AssessmentResults)
            .Where(n => n.LearningSession!.PersonId == personId
                     && n.CapabilityGraphNodeId == capabilityGraphNodeId
                     && n.Status == LearningSessionNodeStatus.Completed)
            .OrderByDescending(n => n.CompletedDate)
            .ToListAsync(cancellationToken);

        if (completedNodes.Count == 0)
        {
            throw new InvalidOperationException(
                $"Person {personId} has no completed LearningSessionNode for CapabilityGraphNode {capabilityGraphNodeId} — nothing to summarize yet.");
        }

        var latest = completedNodes[0];

        // Formal/school-track blueprints may include the optional Examples
        // step — try every possible step type in this node's OWN blueprint
        // order (the try/catch below already tolerates any step that was
        // never started, so trying the extended 6-item order for every
        // node is safe even for the common 5-step case).
        var hasExamplesStep = await dbContext.NodeExperienceBlueprintSteps
            .AsNoTracking()
            .AnyAsync(s => s.NodeExperienceBlueprintId == dbContext.LearningSessionNodes
                .Where(n => n.LearningSessionNodeId == latest.LearningSessionNodeId)
                .Select(n => n.NodeExperienceBlueprintId)
                .FirstOrDefault()
                && s.StepType == ExperienceStepType.DeepDive, cancellationToken);
        var stepOrder = hasExamplesStep ? FormalEducationStepOrder : CanonicalStepOrder;

        var steps = new List<StepReviewResult>();
        foreach (var stepType in stepOrder)
        {
            try
            {
                steps.Add(await GetStepReviewAsync(dbContext, latest.LearningSessionNodeId, stepType, cancellationToken));
            }
            catch (InvalidOperationException)
            {
                // Defensive only — a genuinely Completed node should have started all 5 steps.
            }
        }

        static LearningAssessmentResult? BestPassed(LearningSessionNode node) => node.AssessmentResults
            .Where(a => a.Passed)
            .OrderByDescending(a => a.CreatedDate)
            .FirstOrDefault();

        // 1. MEMORY BLOCK: compute recall %, review count, automatic status, and next review date
        var passedAttemptsCount = completedNodes.Count(n => n.AssessmentResults.Any(a => a.Passed));
        var voiceReviews = await dbContext.EngramReviewCompletions
            .AsNoTracking()
            .Where(r => r.PersonId == personId && r.CapabilityGraphNodeId == capabilityGraphNodeId)
            .Select(r => r.CompletedDate)
            .ToListAsync(cancellationToken);

        int reviewCount = passedAttemptsCount + voiceReviews.Count;
        DateTime? lastReviewDate = latest.CompletedDate;
        if (voiceReviews.Count > 0)
        {
            var maxVoiceReviewDate = voiceReviews.Max();
            if (!lastReviewDate.HasValue || maxVoiceReviewDate > lastReviewDate.Value)
            {
                lastReviewDate = maxVoiceReviewDate;
            }
        }

        NodeMemorySummary? memory = null;
        if (lastReviewDate.HasValue)
        {
            var recallPercent = MemoryScienceEngine.CalculateRecallPercent(lastReviewDate.Value, reviewCount);
            var memoryState = MemoryScienceEngine.DetermineMemoryState(recallPercent);
            var isAuto = MemoryScienceEngine.IsAutomatic(reviewCount);
            var reviewsUntilAuto = Math.Max(0, 5 - reviewCount);
            var nextReviewDate = MemoryScienceEngine.CalculateNextReviewDate(lastReviewDate.Value, reviewCount);

            memory = new NodeMemorySummary
            {
                RecallPercent = recallPercent,
                MemoryState = memoryState,
                ReviewCount = reviewCount,
                LastReviewDate = lastReviewDate,
                IsAutomatic = isAuto,
                ReviewsUntilAutomatic = reviewsUntilAuto,
                NextReviewDate = nextReviewDate
            };
        }

        // 2. REWARD & IMPACT BLOCKS: query frozen ledger and role-impact evaluations
        var courseId = latest.LearningSession?.CapabilityId
            ?? await dbContext.LearningSessions
                .AsNoTracking()
                .Where(s => s.LearningSessionId == latest.LearningSessionId)
                .Select(s => s.CapabilityId)
                .FirstOrDefaultAsync(cancellationToken);

        var engramIdStr = capabilityGraphNodeId.ToString();

        NodeRewardSummary? reward = null;
        if (courseId != Guid.Empty)
        {
            var rewardEntry = await dbContext.RewardLedgerEntries
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.PersonId == personId && r.CourseId == courseId && r.EngramId == engramIdStr, cancellationToken);

            if (rewardEntry != null)
            {
                reward = new NodeRewardSummary
                {
                    PointsAwarded = rewardEntry.PointsAwarded,
                    WasSurprise = rewardEntry.WasSurprise,
                    SurpriseType = rewardEntry.SurpriseType ?? "NONE",
                    PointsBase = rewardEntry.PointsBase,
                    DifficultyMultiplier = rewardEntry.DifficultyMultiplier,
                    MemoryEffortMultiplier = rewardEntry.MemoryEffortMultiplier,
                    SurpriseMultiplier = rewardEntry.SurpriseMultiplier,
                    EarnedAt = rewardEntry.EarnedAt
                };
            }
        }

        NodeImpactSummary? impact = null;
        if (courseId != Guid.Empty)
        {
            var impactEntry = await dbContext.EngramImpactScores
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.EmployeeId == personId && i.CourseId == courseId && i.EngramId == engramIdStr, cancellationToken);

            if (impactEntry != null)
            {
                impact = new NodeImpactSummary
                {
                    Score = impactEntry.Score,
                    Tier = impactEntry.Tier ?? string.Empty,
                    PrimarySignal = impactEntry.PrimarySignal,
                    Reason = impactEntry.Reason
                };
            }
        }

        return new NodeSummaryResult
        {
            CapabilityGraphNodeId = capabilityGraphNodeId,
            LearningSessionNodeId = latest.LearningSessionNodeId,
            AttemptCount = completedNodes.Count,
            FirstCompletedDate = completedNodes.Min(n => n.CompletedDate),
            LastCompletedDate = latest.CompletedDate,
            FinalScore = BestPassed(latest)?.Score,
            Steps = steps,
            PastAttempts = completedNodes.Select(n => new NodeAttemptSummary
            {
                LearningSessionNodeId = n.LearningSessionNodeId,
                StartedDate = n.StartedDate,
                CompletedDate = n.CompletedDate,
                FinalScore = BestPassed(n)?.Score,
                Passed = n.AssessmentResults.Any(a => a.Passed)
            }).ToList(),
            Memory = memory,
            Reward = reward,
            Impact = impact
        };
    }

    /// <summary>
    /// Shared helper: loads a step's Content from its Blueprint and resolves
    /// its referenced illustrations. Internal (not private) so
    /// <see cref="SessionRecoveryEngine"/> (Runtime Paso 3.5) can reuse the
    /// exact same content/illustration-resolution logic when rebuilding the
    /// current step during a session resume — there is only ever ONE way to
    /// go from a LearningSessionStep to its displayable content.
    /// </summary>
    internal static async Task<CurrentStepResult> BuildCurrentStepResultAsync(
        HumanOsDbContext dbContext,
        LearningSessionNode sessionNode,
        LearningSessionStep step,
        CancellationToken cancellationToken)
    {
        var blueprintStep = await dbContext.NodeExperienceBlueprintSteps
            .AsNoTracking()
            .FirstOrDefaultAsync(
                s => s.NodeExperienceBlueprintId == sessionNode.NodeExperienceBlueprintId && s.StepType == step.StepType,
                cancellationToken);

        if (blueprintStep is null)
        {
            throw new InvalidOperationException(
                $"Blueprint {sessionNode.NodeExperienceBlueprintId} has no {step.StepType} step — data inconsistency.");
        }

        var illustrations = new List<IllustrationRef>();
        if (!string.IsNullOrWhiteSpace(blueprintStep.ReferencedIllustrationIdsJson))
        {
            var illustrationIds = System.Text.Json.JsonSerializer.Deserialize<List<Guid>>(blueprintStep.ReferencedIllustrationIdsJson) ?? [];
            if (illustrationIds.Count > 0)
            {
                illustrations = await dbContext.CapabilityGraphNodeIllustrations
                    .AsNoTracking()
                    .Where(i => illustrationIds.Contains(i.CapabilityGraphNodeIllustrationId))
                    .Select(i => new IllustrationRef
                    {
                        CapabilityGraphNodeIllustrationId = i.CapabilityGraphNodeIllustrationId,
                        StoragePath = i.StoragePath,
                        Caption = i.Caption,
                        ImageModel = i.ImageModel
                    })
                    .ToListAsync(cancellationToken);
            }
        }

        // For Recall specifically, prefer the most recently generated
        // dynamic question (persisted on the step itself) over the static
        // blueprint text — otherwise a page reload/resume mid-Recall would
        // show the ORIGINAL generic Recall prompt instead of the actual
        // question the student was just asked, desyncing the UI from the
        // conversation history the TutorAgent sees. Same rationale for
        // Deep Dive's CurrentDeepDiveQuestion.
        var stepContent = step.StepType == ExperienceStepType.Recall && !string.IsNullOrWhiteSpace(step.CurrentRecallPrompt)
            ? step.CurrentRecallPrompt
            : step.StepType == ExperienceStepType.DeepDive && !string.IsNullOrWhiteSpace(step.CurrentDeepDiveQuestion)
                ? step.CurrentDeepDiveQuestion
                : blueprintStep.Content;

        var exampleCards = step.StepType == ExperienceStepType.DeepDive && !string.IsNullOrWhiteSpace(blueprintStep.ExampleCardsJson)
            ? System.Text.Json.JsonSerializer.Deserialize<List<ExampleCard>>(blueprintStep.ExampleCardsJson)
            : null;

        return new CurrentStepResult
        {
            LearningSessionId = sessionNode.LearningSessionId,
            LearningSessionNodeId = sessionNode.LearningSessionNodeId,
            LearningSessionStepId = step.LearningSessionStepId,
            StepType = step.StepType,
            StepContent = stepContent,
            Illustrations = illustrations,
            ExampleCards = exampleCards,
            CurrentDeepDiveCardIndex = step.StepType == ExperienceStepType.DeepDive ? step.CurrentDeepDiveCardIndex : null,
            DeepDiveTotalCards = step.StepType == ExperienceStepType.DeepDive ? exampleCards?.Count : null
        };
    }
}
