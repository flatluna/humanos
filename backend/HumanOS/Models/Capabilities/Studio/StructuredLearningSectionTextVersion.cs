namespace HumanOS.Models.Capabilities.Studio;

/// <summary>
/// One historical snapshot of a Structured Learning section's source
/// <see cref="StructuredLearningDocumentSection.Text"/> (2026-11-11) — lets
/// a designer see how the verbatim Engram source text evolved over time as
/// courses get edited/improved. VersionNumber 0 is always the ORIGINAL text
/// extracted from the source PDF/video, captured lazily the first time a
/// reviewer edits the source text (never created otherwise). Only the
/// source text itself is versioned here — the AI-generated Content/
/// illustration/audio that was derived from each version is NOT kept; only
/// the current (latest) generated artifacts exist, same as every other
/// Studio "Edit with AI" tool.
/// </summary>
public sealed class StructuredLearningSectionTextVersion
{
    public Guid StructuredLearningSectionTextVersionId { get; set; } = Guid.NewGuid();

    /// <summary>FK: the section this source-text version belongs to.</summary>
    public Guid StructuredLearningDocumentSectionId { get; set; }

    /// <summary>0 = original extracted text (never edited by a human).
    /// 1, 2, 3... = each subsequent human-reviewed edit, in order.</summary>
    public int VersionNumber { get; set; }

    /// <summary>Full verbatim text of the section as of this version.</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>The reviewer's free-text instruction that produced this
    /// version from the previous one. Null for VersionNumber 0 (the
    /// original — nobody "instructed" it, it came straight from
    /// extraction).</summary>
    public string? Instruction { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
}
