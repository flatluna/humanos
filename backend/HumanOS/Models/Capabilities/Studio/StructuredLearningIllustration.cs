namespace HumanOS.Models.Capabilities.Studio;

/// <summary>
/// Metadata for one real generated illustration for a Structured Learning
/// Document's DeepDive example card — the actual image binary lives in
/// Azure Data Lake (same "capability-graphs" container/service as every
/// other pipeline's illustrations, see CapabilityGraphIllustrationStorageService,
/// reused as-is since it only needs opaque GUIDs to build the blob path).
/// This table only stores the pointer/metadata, same convention as
/// <see cref="HumanOS.Models.Capabilities.Traditional.CapabilityTraditionalIllustration"/>.
///
/// DeepDive cards themselves are never their own DB rows (they live inside
/// StructuredLearningSectionStep.StepDataJson as a JSON blob) — each card
/// just embeds this row's Id as a plain <c>IllustrationId</c> value in that
/// JSON, with no enforced FK from the card side.
/// </summary>
public sealed class StructuredLearningIllustration
{
    public Guid StructuredLearningIllustrationId { get; set; } = Guid.NewGuid();

    /// <summary>FK: the section this illustration was generated for.</summary>
    public Guid StructuredLearningDocumentSectionId { get; set; }

    public string StoragePath { get; set; } = string.Empty;

    public string Prompt { get; set; } = string.Empty;

    /// <summary>Prompt/contract version that produced this illustration.</summary>
    public string? PromptVersion { get; set; }

    /// <summary>Process version used for the illustration-generation workflow.</summary>
    public string? ProcessVersion { get; set; }

    /// <summary>Hash or snapshot of the prompt/config for this illustration.</summary>
    public string? PromptHash { get; set; }

    /// <summary>JSON snapshot of the generation config used for this illustration.</summary>
    public string? ConfigSnapshotJson { get; set; }

    public string? Caption { get; set; }

    public StructuredLearningIllustrationPurpose Purpose { get; set; }

    public string ImageModel { get; set; } = string.Empty;

    public int Width { get; set; }

    public int Height { get; set; }

    /// <summary>"Image" (default) or "Video" (2026-08-21 — Google Veo
    /// video generation for physical/gestural Learn content). Video rows
    /// reuse StoragePath (points to an .mp4 blob instead of .png) and
    /// ImageModel (holds the Veo model name, e.g.
    /// "veo-3.1-fast-generate-preview") — no separate video-only columns,
    /// to avoid a wider migration for what is otherwise the exact same
    /// "one generated media file for one section" shape.</summary>
    public string MediaType { get; set; } = "Image";

    /// <summary>Whether students see this video in real-mode (2026-08-26) —
    /// edit mode always shows every video regardless of this flag; only the
    /// learner-facing runtime filters by it. Lets a reviewer keep an unused
    /// clip around (not deleted) without exposing it to students. Only
    /// meaningful for MediaType="Video"; images are unaffected.</summary>
    public bool IsPublished { get; set; } = true;

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
}
