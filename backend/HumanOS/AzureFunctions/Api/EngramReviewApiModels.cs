namespace HumanOS.AzureFunctions.Api;

/// <summary>
/// Request DTO for the "Reactivar Engrama" feature's ephemeral voice-session
/// endpoint (see EngramReviewSessionFunction.cs). Kept in its own file,
/// matching this codebase's one-*ApiModels.cs-per-feature-area convention
/// (see VoiceTutorApiModels.cs/TutorApiModels.cs). The response DTO is
/// intentionally reused from VoiceTutorApiModels.cs
/// (<see cref="VoiceTutorSessionResponseDto"/>) since the shape is
/// identical — both endpoints mint the same kind of Azure OpenAI Realtime
/// ephemeral session.
/// </summary>
public sealed class EngramReviewSessionRequest
{
    /// <summary>The student requesting the review — used to load THEIR OWN
    /// most recent completed attempt (content + past evidence/answers) via
    /// <see cref="HumanOS.Services.InstructorRuntimeOrchestrator.GetNodeSummaryAsync"/>,
    /// so the Agent can reference what they personally wrote/answered.</summary>
    public Guid PersonId { get; set; }

    /// <summary>The already-Mastered node being reviewed. Requires at least
    /// one Completed LearningSessionNode for this person+node — the same
    /// precondition as the "node summary" recap screen this feature is
    /// launched from.</summary>
    public Guid CapabilityGraphNodeId { get; set; }

    /// <summary>Optional (2026-08-02) — 0-based index into this node's Deep
    /// Dive <c>ExampleCards</c> pool. When provided (and valid), the Agent's
    /// conversation is focused specifically on explaining/telling stories
    /// about THAT ONE card instead of the whole lesson — lets a student
    /// browse the generated cards on the Brain page and ask to have one
    /// explained, rather than always starting a whole-lesson review. Out of
    /// range or omitted falls back to the original whole-lesson behavior.</summary>
    public int? ExampleCardIndex { get; set; }
}

/// <summary>
/// Request DTO for logging one completed full "Reactivar Engrama" voice
/// review (see EngramReviewCompleteFunction.cs) — sent by the frontend only
/// when the Agent itself calls the <c>complete_review</c> Realtime tool,
/// signaling it walked the student through the ENTIRE lesson from beginning
/// to end. See <see cref="HumanOS.Models.Learning.EngramReviewCompletion"/>.
/// </summary>
public sealed class EngramReviewCompleteRequest
{
    public Guid PersonId { get; set; }

    public Guid CapabilityGraphNodeId { get; set; }
}

