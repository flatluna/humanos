namespace HumanOS.Data;

/// <summary>
/// Technical infrastructure table backing the Interactive Learning
/// Runtime's Workflow checkpointing (fixed Paso 3, 2026-07-14). Deliberately
/// lives under <c>Data/</c>, NOT <c>Models/</c> — unlike every entity under
/// <c>Models/</c> (Capability, Evidence, Assessment, ...), this is NOT a
/// Human OS domain concept. It is an opaque store for whatever
/// <c>Microsoft.Agents.AI.Workflows</c> itself serializes at a checkpoint
/// boundary (see <c>SqlRuntimeCheckpointStore</c>, which implements the
/// framework's own <c>ICheckpointStore&lt;JsonElement&gt;</c> contract
/// against this table).
/// </summary>
/// <remarks>
/// HARD BOUNDARY (explicit user decision, 2026-07-14): this table must
/// NEVER grow domain-specific columns (no StudentEvidence, Assessment,
/// Progression fields). The full Runtime persistence model (RuntimeSession,
/// StudentEvidence, AssessmentResult, Progression, learner history, metrics)
/// is intentionally deferred until Assessment and Progression are designed
/// — this table exists ONLY so a paused Runtime session can survive a
/// process restart or a long real-world gap (the learner returning
/// tomorrow), never as a place to query domain data from.
/// </remarks>
public sealed class RuntimeWorkflowCheckpoint
{
    public Guid RuntimeWorkflowCheckpointId { get; set; }

    /// <summary>The Workflow engine's own run/session identifier — NOT
    /// necessarily equal in meaning to a domain <c>RuntimeSessionId</c>,
    /// though callers set them to the same value in practice.</summary>
    public string SessionId { get; set; } = null!;

    public string CheckpointId { get; set; } = null!;

    /// <summary>Framework-assigned parent checkpoint, if any — mirrors
    /// <c>ICheckpointStore&lt;T&gt;</c>'s own optional parent-checkpoint
    /// concept. Null for a session's first checkpoint.</summary>
    public string? ParentCheckpointId { get; set; }

    /// <summary>Opaque JSON payload — the Workflow engine's serialized
    /// internal state at this checkpoint. Never parsed/queried by Human OS
    /// application code; only the Workflow engine itself reads this back.</summary>
    public string PayloadJson { get; set; } = null!;

    public DateTime CreatedDate { get; set; }
}
