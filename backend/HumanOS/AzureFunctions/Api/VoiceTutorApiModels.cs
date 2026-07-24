namespace HumanOS.AzureFunctions.Api;

/// <summary>
/// Request/response DTOs for the voice Tutor's ephemeral-session endpoint
/// (see VoiceTutorSessionFunction.cs and Services/RealtimeVoiceSessionService.cs).
/// Kept in their own file, matching this codebase's one-*ApiModels.cs-per-
/// feature-area convention (see TutorApiModels.cs).
/// </summary>
public sealed class VoiceTutorSessionRequest
{
    /// <summary>Live-session path: an existing LearningSessionStep the
    /// student is actually on (or has already completed). Leave
    /// Guid.Empty when using the blueprint-only path below instead.</summary>
    public Guid LearningSessionStepId { get; set; }

    /// <summary>Blueprint-only path (2026-07-22) — Capability Studio's demo
    /// "read-only peek" lets a reviewer jump to ANY of the 5 steps with no
    /// LearningSession progression gating, so a peeked step (e.g. Teaching,
    /// peeked before the live session has actually reached it) may have NO
    /// LearningSessionStep row yet. When LearningSessionStepId is empty,
    /// both CapabilityGraphNodeId and StepType must be set instead — the
    /// voice Agent then reads straight from the node's most recent
    /// NodeExperienceBlueprintStep.Content, with no session context at all
    /// (never grades, same as the live path). Only Hypothesis/Teaching are
    /// supported here (mirrors this endpoint's overall step-type scope).</summary>
    public Guid? CapabilityGraphNodeId { get; set; }

    /// <summary>One of "Hypothesis"/"Teaching" — required alongside <see cref="CapabilityGraphNodeId"/> when not using LearningSessionStepId.</summary>
    public string? StepType { get; set; }

    /// <summary>Optional. For Recall steps, the prompt currently rotates
    /// per attempt (TutorAgentV2/RecallLoopGate), so the caller passes the
    /// EXACT text presently shown on screen here. Ignored for other step
    /// types (which read the blueprint's fixed Content instead). Never
    /// used for anything other than telling the voice Agent what question
    /// to read aloud — it grants no grading authority.</summary>
    public string? CurrentPromptText { get; set; }
}

public sealed class VoiceTutorSessionResponseDto
{
    /// <summary>Short-lived Azure OpenAI Realtime ephemeral token — safe to
    /// send to the browser, NEVER the real API key. The browser uses this
    /// directly (Bearer auth) against <see cref="RealtimeCallsUrl"/> to
    /// negotiate a WebRTC session straight with Azure.</summary>
    public string ClientSecret { get; set; } = string.Empty;

    public string RealtimeCallsUrl { get; set; } = string.Empty;

    public string Model { get; set; } = string.Empty;

    public string Voice { get; set; } = string.Empty;

    public long? ExpiresAtUnixSeconds { get; set; }
}
