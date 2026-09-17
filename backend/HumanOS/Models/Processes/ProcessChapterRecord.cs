namespace HumanOS.Models.Processes;

/// <summary>
/// A chapter, section, or major phase of a process manual.
/// </summary>
public sealed class ProcessChapterRecord
{
    public Guid ProcessChapterId { get; set; }

    public Guid ProcessDocumentId { get; set; }

    public ProcessDocumentRecord? ProcessDocument { get; set; }

    public int ChapterNumber { get; set; }

    public string Title { get; set; } = null!;

    public string? Description { get; set; }

    public List<ProcessStepRecord> Steps { get; set; } = [];
}
