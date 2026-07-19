using Microsoft.Agents.AI.Workflows;

namespace HumanOS.Agentic.Runtime;

/// <summary>
/// Custom progress events for TutorAgentV2's Workflow, raised via
/// <see cref="IWorkflowContext.AddEventAsync"/> from
/// <see cref="TutorTurnExecutor"/>. Same non-pausing, informational-only
/// pattern as Studio's <c>StudioProgressEvent</c> family
/// (StudioProgressEvents.cs) — these do NOT pause the workflow and are not
/// a HITL mechanism (TutorAgentV2 has no approval gates).
/// </summary>
public abstract class TutorPedagogicalEvent : WorkflowEvent
{
    protected TutorPedagogicalEvent(object? data) : base(data)
    {
    }
}

/// <summary>Raised right before TutorAgentV2 is called for this turn.</summary>
public sealed class TutorTurnStartedEvent : TutorPedagogicalEvent
{
    public TutorInteractionMode Mode { get; }

    public TutorTurnStartedEvent(TutorInteractionMode mode) : base(new { mode })
    {
        Mode = mode;
    }
}

/// <summary>Raised once TutorAgentV2 has produced its response for this
/// turn. RecallScore is only non-null when Mode is
/// <see cref="TutorInteractionMode.Recall"/>.</summary>
public sealed class TutorTurnCompletedEvent : TutorPedagogicalEvent
{
    public TutorInteractionMode Mode { get; }

    public int? RecallScore { get; }

    public TutorTurnCompletedEvent(TutorInteractionMode mode, int? recallScore)
        : base(new { mode, recallScore })
    {
        Mode = mode;
        RecallScore = recallScore;
    }
}
