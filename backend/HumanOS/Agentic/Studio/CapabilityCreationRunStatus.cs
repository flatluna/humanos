namespace HumanOS.Agentic.Studio;

using System.Text.Json.Serialization;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CapabilityCreationRunStage
{
    /// <summary>The run is actively executing (Curador/Arquitecto, or the
    /// Instructor/Métrico per-module loop, or Publish) — poll
    /// GET .../status again shortly. <see cref="CapabilityCreationRunStatus.Progress"/>
    /// may be populated with the latest known progress snapshot.</summary>
    Running,

    /// <summary>The run is paused, waiting for a human decision at Gate 1 or Gate 2.</summary>
    PendingGate,

    /// <summary>The run finished — either published (approved) or ended with a rejection message.</summary>
    Completed,

    /// <summary>The run failed with an unrecoverable error (executor/workflow error).</summary>
    Failed,

    /// <summary>The event stream ended without a pending gate or an output (unexpected — usually a sign of a workflow-graph bug).</summary>
    Idle
}

/// <summary>Live progress snapshot while <see cref="CapabilityCreationRunStatus.Stage"/>
/// is <see cref="CapabilityCreationRunStage.Running"/>, built from the
/// custom progress events in StudioProgressEvents.cs (module generation
/// and/or publish sub-tasks). Both sections are optional and populated
/// only for the phase currently in flight.</summary>
public sealed class CapabilityCreationRunProgress
{
    // --- Module generation phase (Instructor/Métrico loop) ---
    public int? TotalModules { get; init; }

    public int? CompletedModules { get; init; }

    public string? CurrentModuleTitle { get; init; }

    // --- Publish phase ---
    public IReadOnlyList<PublishTaskStatus>? PublishTasks { get; init; }
}

public sealed class PublishTaskStatus
{
    public required string TaskKey { get; init; }

    public required string Status { get; init; }
}

/// <summary>Snapshot of a capability-creation run returned to the caller
/// after starting a run, polling its status, or responding to a gate.</summary>
public sealed class CapabilityCreationRunStatus
{
    public Guid RunId { get; init; }

    public CapabilityCreationRunStage Stage { get; init; }

    /// <summary>Set when <see cref="Stage"/> is <see cref="CapabilityCreationRunStage.PendingGate"/> —
    /// pass this back as <c>subjectId</c> when responding to the gate.</summary>
    public Guid? PendingSubjectId { get; init; }

    /// <summary>The gate's pending payload (a CapabilityBlueprint or
    /// CapabilityPackage) when pending, or the final output when completed.</summary>
    public object? Payload { get; init; }

    /// <summary>Populated when <see cref="Stage"/> is <see cref="CapabilityCreationRunStage.Running"/>.</summary>
    public CapabilityCreationRunProgress? Progress { get; init; }

    /// <summary>Populated when <see cref="Stage"/> is <see cref="CapabilityCreationRunStage.Failed"/>.</summary>
    public string? ErrorMessage { get; init; }

    public static CapabilityCreationRunStatus Running(Guid runId, CapabilityCreationRunProgress? progress = null) =>
        new() { RunId = runId, Stage = CapabilityCreationRunStage.Running, Progress = progress };

    public static CapabilityCreationRunStatus PendingGate(Guid runId, Guid subjectId, object? payload) =>
        new()
        {
            RunId = runId,
            Stage = CapabilityCreationRunStage.PendingGate,
            PendingSubjectId = subjectId,
            Payload = payload
        };

    public static CapabilityCreationRunStatus Completed(Guid runId, object? payload) =>
        new() { RunId = runId, Stage = CapabilityCreationRunStage.Completed, Payload = payload };

    public static CapabilityCreationRunStatus Failed(Guid runId, string errorMessage) =>
        new() { RunId = runId, Stage = CapabilityCreationRunStage.Failed, ErrorMessage = errorMessage };

    public static CapabilityCreationRunStatus Idle(Guid runId) =>
        new() { RunId = runId, Stage = CapabilityCreationRunStage.Idle };
}

