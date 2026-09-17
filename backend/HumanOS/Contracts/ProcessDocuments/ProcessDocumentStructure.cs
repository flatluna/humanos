using System.Text.Json.Serialization;

namespace HumanOS.Contracts.ProcessDocuments;

/// <summary>
/// Root schema for the structured process extraction JSON stored in Azure Data Lake Storage.
/// </summary>
public sealed class ProcessDocumentStructure
{
    [JsonPropertyName("document_id")]
    public string DocumentId { get; set; } = string.Empty;

    [JsonPropertyName("process_name")]
    public string ProcessName { get; set; } = string.Empty;

    [JsonPropertyName("source_file")]
    public SourceFileInfo SourceFile { get; set; } = new();

    [JsonPropertyName("document_title")]
    public string DocumentTitle { get; set; } = string.Empty;

    [JsonPropertyName("chapters")]
    public List<ProcessChapter> Chapters { get; set; } = [];

    [JsonPropertyName("tables")]
    public List<ProcessTable> Tables { get; set; } = [];

    [JsonPropertyName("appendices")]
    public List<ProcessAppendix> Appendices { get; set; } = [];

    [JsonPropertyName("warnings")]
    public List<string> Warnings { get; set; } = [];

    [JsonPropertyName("processing_status")]
    public string ProcessingStatus { get; set; } = "completed";
}

public sealed class SourceFileInfo
{
    [JsonPropertyName("file_name")]
    public string FileName { get; set; } = string.Empty;

    [JsonPropertyName("content_type")]
    public string ContentType { get; set; } = "application/pdf";

    [JsonPropertyName("storage_path")]
    public string StoragePath { get; set; } = string.Empty;
}

public sealed class ProcessChapter
{
    [JsonPropertyName("chapter_id")]
    public string ChapterId { get; set; } = string.Empty;

    [JsonPropertyName("chapter_number")]
    public string ChapterNumber { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("modules")]
    public List<ProcessModule> Modules { get; set; } = [];

    [JsonPropertyName("steps")]
    public List<ProcessStep> Steps { get; set; } = [];
}

public sealed class ProcessModule
{
    [JsonPropertyName("module_id")]
    public string ModuleId { get; set; } = string.Empty;

    [JsonPropertyName("module_number")]
    public string ModuleNumber { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("phases")]
    public List<ProcessPhase> Phases { get; set; } = [];
}

public sealed class ProcessPhase
{
    [JsonPropertyName("phase_id")]
    public string PhaseId { get; set; } = string.Empty;

    [JsonPropertyName("phase_number")]
    public string PhaseNumber { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;
}

public sealed class ProcessStep
{
    [JsonPropertyName("step_id")]
    public string StepId { get; set; } = string.Empty;

    [JsonPropertyName("step_number")]
    public string StepNumber { get; set; } = string.Empty;

    [JsonPropertyName("step_name")]
    public string StepName { get; set; } = string.Empty;

    [JsonPropertyName("objective")]
    public string Objective { get; set; } = string.Empty;

    [JsonPropertyName("required_activities")]
    public List<string> RequiredActivities { get; set; } = [];

    [JsonPropertyName("required_skills")]
    public List<string> RequiredSkills { get; set; } = [];

    [JsonPropertyName("deliverables")]
    public List<string> Deliverables { get; set; } = [];

    [JsonPropertyName("completion_condition")]
    public string CompletionCondition { get; set; } = string.Empty;

    [JsonPropertyName("substeps")]
    public List<ProcessSubstep> Substeps { get; set; } = [];

    [JsonPropertyName("checklists")]
    public List<string> Checklists { get; set; } = [];

    [JsonPropertyName("source_pages")]
    public List<int> SourcePages { get; set; } = [];
}

public sealed class ProcessSubstep
{
    [JsonPropertyName("substep_id")]
    public string SubstepId { get; set; } = string.Empty;

    [JsonPropertyName("substep_number")]
    public string SubstepNumber { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;
}

public sealed class ProcessTable
{
    [JsonPropertyName("table_id")]
    public string TableId { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("headers")]
    public List<string> Headers { get; set; } = [];

    [JsonPropertyName("rows")]
    public List<List<string>> Rows { get; set; } = [];

    [JsonPropertyName("source_page")]
    public int SourcePage { get; set; }
}

public sealed class ProcessAppendix
{
    [JsonPropertyName("appendix_id")]
    public string AppendixId { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;

    [JsonPropertyName("source_pages")]
    public List<int> SourcePages { get; set; } = [];
}

/// <summary>
/// Raw Document Intelligence / PDF layout extraction result, preserved separately
/// from the normalized process structure.
/// </summary>
public sealed class RawDocumentExtractionResult
{
    [JsonPropertyName("document_id")]
    public string DocumentId { get; set; } = string.Empty;

    [JsonPropertyName("page_count")]
    public int PageCount { get; set; }

    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;

    [JsonPropertyName("paragraphs")]
    public List<RawParagraph> Paragraphs { get; set; } = [];

    [JsonPropertyName("headings")]
    public List<RawHeading> Headings { get; set; } = [];

    [JsonPropertyName("sections")]
    public List<RawSection> Sections { get; set; } = [];

    [JsonPropertyName("tables")]
    public List<RawTable> Tables { get; set; } = [];

    [JsonPropertyName("lists")]
    public List<RawList> Lists { get; set; } = [];

    [JsonPropertyName("selection_marks")]
    public List<RawSelectionMark> SelectionMarks { get; set; } = [];
}

public sealed class RawParagraph
{
    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;

    [JsonPropertyName("role")]
    public string? Role { get; set; } // "title", "sectionHeading", "pageHeader", "pageFooter", null

    [JsonPropertyName("page_number")]
    public int PageNumber { get; set; }
}

public sealed class RawHeading
{
    [JsonPropertyName("level")]
    public int Level { get; set; }

    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;

    [JsonPropertyName("page_number")]
    public int PageNumber { get; set; }
}

public sealed class RawSection
{
    [JsonPropertyName("heading")]
    public string Heading { get; set; } = string.Empty;

    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;

    [JsonPropertyName("page_number")]
    public int PageNumber { get; set; }
}

public sealed class RawTable
{
    [JsonPropertyName("row_count")]
    public int RowCount { get; set; }

    [JsonPropertyName("column_count")]
    public int ColumnCount { get; set; }

    [JsonPropertyName("page_number")]
    public int PageNumber { get; set; }

    [JsonPropertyName("headers")]
    public List<string> Headers { get; set; } = [];

    [JsonPropertyName("rows")]
    public List<List<string>> Rows { get; set; } = [];
}

public sealed class RawList
{
    [JsonPropertyName("items")]
    public List<string> Items { get; set; } = [];

    [JsonPropertyName("page_number")]
    public int PageNumber { get; set; }
}

public sealed class RawSelectionMark
{
    [JsonPropertyName("state")]
    public string State { get; set; } = "unselected"; // "selected", "unselected"

    [JsonPropertyName("page_number")]
    public int PageNumber { get; set; }

    [JsonPropertyName("label")]
    public string? Label { get; set; }
}

/// <summary>
/// Response returned to the UX upon uploading and processing a process document.
/// </summary>
public sealed class ProcessDocumentUploadResponse
{
    [JsonPropertyName("document_id")]
    public string DocumentId { get; set; } = string.Empty;

    [JsonPropertyName("file_name")]
    public string FileName { get; set; } = string.Empty;

    [JsonPropertyName("process_name")]
    public string ProcessName { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = "completed"; // "completed" or "accepted"

    [JsonPropertyName("source_storage_path")]
    public string SourceStoragePath { get; set; } = string.Empty;

    [JsonPropertyName("structured_result_path")]
    public string StructuredResultPath { get; set; } = string.Empty;

    [JsonPropertyName("chapters_identified")]
    public int ChaptersIdentified { get; set; }

    [JsonPropertyName("steps_identified")]
    public int StepsIdentified { get; set; }

    [JsonPropertyName("tables_extracted")]
    public int TablesExtracted { get; set; }

    [JsonPropertyName("warnings")]
    public List<string> Warnings { get; set; } = [];

    [JsonPropertyName("status_url")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? StatusUrl { get; set; }
}
