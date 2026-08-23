namespace HumanOS.Models.Capabilities.Studio;

/// <summary>
/// One row per SUCCESSFUL Google Veo video generation call (2026-08-21) —
/// written once, right when a video finishes generating, and NEVER removed
/// when the video itself is later deleted by the reviewer. This is a pure
/// cost ledger (each call is billed ~$0.30 regardless of whether the
/// resulting video is kept), completely separate from
/// <see cref="StructuredLearningIllustration"/> rows (which DO get deleted
/// when a reviewer removes a video, freeing up a slot under the
/// max-2-saved-per-section limit).
/// </summary>
public sealed class StructuredLearningVideoGenerationLog
{
    public Guid StructuredLearningVideoGenerationLogId { get; set; } = Guid.NewGuid();

    public Guid StructuredLearningDocumentSectionId { get; set; }

    public Guid TenantId { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
}
