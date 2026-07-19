using HumanOS.Data;
using HumanOS.Models.Capabilities.Graph;
using HumanOS.Models.Learning;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.Services;

/// <summary>
/// Runtime Paso 3.5 — "Session Recovery Engine". The Runtime is the ONLY
/// source of truth for "where is this person right now" — never the
/// browser, never localStorage/sessionStorage, never an ID the frontend
/// happened to remember. A person can close the tab, lose connection,
/// switch devices, or come back days later; this service reconstructs
/// their exact position (Session → Node → Step) purely from what is
/// already persisted in SQL, using nothing but PersonId (+ CapabilityId).
///
/// No AI, no content generation, no graph unlocking, no HTTP endpoints —
/// those are later Pasos. This is purely a set of read queries plus the
/// existing content/illustration-resolution logic already used by
/// <see cref="InstructorRuntimeOrchestrator"/>.
///
/// Design note — "should we add CurrentStepType to LearningSessionNode?":
/// NOT added. The existing model already has a single unambiguous answer to
/// "what step is this node on right now" — the one LearningSessionStep row
/// with Status=Active for that node (the same invariant
/// GetCurrentStepAsync/AdvanceToNextStepAsync already rely on via
/// SingleOrDefault). Adding a denormalized CurrentStepType column on
/// LearningSessionNode would just be a second place that has to be kept in
/// sync with LearningSessionStep.Status on every write, with no new
/// capability gained — a pure duplication risk. If step lookups ever show
/// up as a real performance bottleneck, an index on
/// (LearningSessionNodeId, Status) would solve it without any denormalization.
/// </summary>
public sealed class SessionRecoveryEngine
{
    /// <summary>The active LearningSessionNode for a person on a Capability, with just enough to resolve its step/blueprint.</summary>
    public sealed class ActiveNodeResult
    {
        public Guid LearningSessionId { get; set; }
        public Guid LearningSessionNodeId { get; set; }
        public Guid CapabilityGraphNodeId { get; set; }
        public Guid NodeExperienceBlueprintId { get; set; }
        public LearningSessionNodeStatus Status { get; set; }
    }

    /// <summary>The active LearningSessionStep for a node.</summary>
    public sealed class ActiveStepResult
    {
        public Guid LearningSessionStepId { get; set; }
        public ExperienceStepType StepType { get; set; }
        public LearningSessionStepStatus Status { get; set; }
    }

    /// <summary>Full reconstructed position — everything the UI needs to resume rendering exactly where the person left off.</summary>
    public sealed class ResumeSessionResult
    {
        public Guid LearningSessionId { get; set; }
        public Guid LearningSessionNodeId { get; set; }
        public Guid CapabilityGraphNodeId { get; set; }
        public InstructorRuntimeOrchestrator.CurrentStepResult CurrentStep { get; set; } = null!;
    }

    /// <summary>
    /// Método 1 — ¿en qué sesión está esta persona? Returns the most
    /// recently started LearningSession with Status=Active for this person,
    /// or null if the person has none in progress right now.
    /// </summary>
    public async Task<LearningSession?> GetActiveSessionAsync(
        HumanOsDbContext dbContext,
        Guid personId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        return await dbContext.LearningSessions
            .AsNoTracking()
            .Where(s => s.PersonId == personId && s.Status == LearningSessionStatus.Active)
            .OrderByDescending(s => s.StartedDate)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// Método 2 — ¿en qué nodo se quedó? Returns the active
    /// LearningSessionNode for this person's active session on this
    /// Capability, or null if there is none. Filters through
    /// LearningSession (PersonId/CapabilityId/Status=Active) because
    /// LearningSessionNode itself does not carry PersonId/CapabilityId —
    /// those only live on its parent LearningSession.
    /// </summary>
    public async Task<ActiveNodeResult?> GetActiveNodeAsync(
        HumanOsDbContext dbContext,
        Guid personId,
        Guid capabilityId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        var node = await dbContext.LearningSessionNodes
            .AsNoTracking()
            .Where(n => n.LearningSession!.PersonId == personId
                     && n.LearningSession!.CapabilityId == capabilityId
                     && n.LearningSession!.Status == LearningSessionStatus.Active
                     && n.Status == LearningSessionNodeStatus.Active)
            .OrderByDescending(n => n.StartedDate)
            .FirstOrDefaultAsync(cancellationToken);

        if (node is null)
        {
            return null;
        }

        return new ActiveNodeResult
        {
            LearningSessionId = node.LearningSessionId,
            LearningSessionNodeId = node.LearningSessionNodeId,
            CapabilityGraphNodeId = node.CapabilityGraphNodeId,
            NodeExperienceBlueprintId = node.NodeExperienceBlueprintId,
            Status = node.Status
        };
    }

    /// <summary>
    /// Método 3 — ¿en qué paso exacto se quedó? Returns the Status=Active
    /// LearningSessionStep for this node, or null if the node has no active
    /// step (e.g. already Completed).
    /// </summary>
    public async Task<ActiveStepResult?> GetActiveStepAsync(
        HumanOsDbContext dbContext,
        Guid learningSessionNodeId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        var step = await dbContext.LearningSessionSteps
            .AsNoTracking()
            .Where(s => s.LearningSessionNodeId == learningSessionNodeId && s.Status == LearningSessionStepStatus.Active)
            .FirstOrDefaultAsync(cancellationToken);

        if (step is null)
        {
            return null;
        }

        return new ActiveStepResult
        {
            LearningSessionStepId = step.LearningSessionStepId,
            StepType = step.StepType,
            Status = step.Status
        };
    }

    /// <summary>
    /// Método 4 — reconstruye todo el contexto: sesión activa → nodo activo
    /// → paso activo → contenido + ilustraciones (sin regenerar nada, sin
    /// llamar a GPT ni a modelos de imagen — solo lee lo ya persistido).
    /// This is the one method the UI actually needs: "person just opened
    /// the app for this Capability, where do I put them?".
    /// </summary>
    public async Task<ResumeSessionResult> ResumeSessionAsync(
        HumanOsDbContext dbContext,
        Guid personId,
        Guid capabilityId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        var sessionNode = await dbContext.LearningSessionNodes
            .Include(n => n.Steps)
            .Where(n => n.LearningSession!.PersonId == personId
                     && n.LearningSession!.CapabilityId == capabilityId
                     && n.LearningSession!.Status == LearningSessionStatus.Active
                     && n.Status == LearningSessionNodeStatus.Active)
            .OrderByDescending(n => n.StartedDate)
            .FirstOrDefaultAsync(cancellationToken);

        if (sessionNode is null)
        {
            throw new InvalidOperationException(
                $"No active LearningSessionNode found for Person {personId} on Capability {capabilityId} — nothing to resume.");
        }

        var activeStep = sessionNode.Steps.SingleOrDefault(s => s.Status == LearningSessionStepStatus.Active);
        if (activeStep is null)
        {
            throw new InvalidOperationException(
                $"LearningSessionNode {sessionNode.LearningSessionNodeId} has no Active step — data inconsistency, cannot resume.");
        }

        var currentStep = await InstructorRuntimeOrchestrator.BuildCurrentStepResultAsync(
            dbContext, sessionNode, activeStep, cancellationToken);

        return new ResumeSessionResult
        {
            LearningSessionId = sessionNode.LearningSessionId,
            LearningSessionNodeId = sessionNode.LearningSessionNodeId,
            CapabilityGraphNodeId = sessionNode.CapabilityGraphNodeId,
            CurrentStep = currentStep
        };
    }
}
