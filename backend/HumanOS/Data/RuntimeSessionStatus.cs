namespace HumanOS.Data;

/// <summary>
/// Minimal technical status pointer for the Interactive Learning Runtime
/// (fixed Paso 9, 2026-07-15) — records ONLY whether a session has reached
/// a terminal <c>RuntimeStage</c> (Completed/RequiresRevision), so an API
/// call can answer "is this session done?" WITHOUT ever calling
/// <c>InProcessExecution.ResumeStreamingAsync</c> again.
/// </summary>
/// <remarks>
/// REAL BUG this closes (found via live testing, 2026-07-15): resuming a
/// Workflow run from a checkpoint captured AT the terminal
/// <c>WorkflowOutputEvent</c> does NOT re-surface that output the way a
/// paused <c>RequestInfoEvent</c> checkpoint does — <c>WatchStreamAsync</c>
/// simply hangs waiting for an event that will never come again, since
/// there is nothing left pending and no more supersteps to run. Every
/// terminal session MUST be short-circuited before any resume is
/// attempted. Same "Data/, not Models/, deliberately domain-free"
/// convention as <see cref="RuntimeWorkflowCheckpoint"/> — this is
/// infrastructure bookkeeping for the API layer, not a
/// StudentEvidence/Assessment/Progression persistence decision (that
/// remains explicitly deferred).
/// </remarks>
public sealed class RuntimeSessionStatus
{
    /// <summary>Matches the Workflow engine's own session id — same value
    /// as <see cref="RuntimeWorkflowCheckpoint.SessionId"/> for the same
    /// session (both derived from <c>RuntimeSessionId.ToString("N")</c>).</summary>
    public string SessionId { get; set; } = null!;

    public bool IsTerminal { get; set; }

    /// <summary>The terminal <c>RuntimeStage</c> reached (<c>Completed</c>
    /// or <c>RequiresRevision</c>), as a string — never re-derived from the
    /// opaque checkpoint payload.</summary>
    public string? FinalStage { get; set; }

    public DateTime UpdatedDate { get; set; }
}
