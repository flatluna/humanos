namespace HumanOS.Contracts.Studio;

/// <summary>Quick counts shown on a Structured Learning course's detail
/// page (2026-10-01) — "engrams" = <c>StructuredLearningDocumentSection</c>
/// rows (see that entity's doc comment), "chapters" = distinct
/// <c>ChapterTitle</c> values across those sections, "facts" =
/// <c>StructuredLearningKnowledgeFact</c> rows linked to this document's
/// engrams.</summary>
public sealed class StructuredLearningCourseSummaryResponse
{
    public Guid StructuredLearningDocumentId { get; set; }

    public int EngramsCount { get; set; }

    public int ChaptersCount { get; set; }

    public int FactsCount { get; set; }
}
