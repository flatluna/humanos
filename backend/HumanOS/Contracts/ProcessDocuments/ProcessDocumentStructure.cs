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

    /// <summary>Stable short process code (e.g. "CCR" for "Customer Complaint Resolution") used as the first segment of every ActivityId.</summary>
    [JsonPropertyName("process_code")]
    public string ProcessCode { get; set; } = string.Empty;

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

    // ===== Process-level metadata (extracted once, in the Phase A index call) =====

    /// <summary>LLM-synthesized 3-5 sentence executive summary of the full process, in business language, for change-management review. Not a verbatim copy of the document's own "Purpose" line.</summary>
    [JsonPropertyName("executive_summary")]
    public string ExecutiveSummary { get; set; } = string.Empty;

    [JsonPropertyName("purpose")]
    public string Purpose { get; set; } = string.Empty;

    [JsonPropertyName("process_owner")]
    public string ProcessOwner { get; set; } = string.Empty;

    [JsonPropertyName("trigger")]
    public string Trigger { get; set; } = string.Empty;

    [JsonPropertyName("scope")]
    public string Scope { get; set; } = string.Empty;

    [JsonPropertyName("inputs")]
    public List<string> Inputs { get; set; } = [];

    [JsonPropertyName("final_output")]
    public string FinalOutput { get; set; } = string.Empty;

    [JsonPropertyName("participating_roles")]
    public List<string> ParticipatingRoles { get; set; } = [];

    [JsonPropertyName("systems_used")]
    public List<string> SystemsUsed { get; set; } = [];

    [JsonPropertyName("applicable_policies")]
    public List<string> ApplicablePolicies { get; set; } = [];

    [JsonPropertyName("metrics")]
    public List<string> Metrics { get; set; } = [];

    [JsonPropertyName("version")]
    public string Version { get; set; } = "1.0";

    [JsonPropertyName("status")]
    public string Status { get; set; } = "Draft";

    // ===== Scope: which Departments/Job Roles must follow this process (user-curated, not AI-extracted) =====

    /// <summary>When true, this process applies to the entire organization and
    /// <see cref="DepartmentIds"/>/<see cref="JobRoleIds"/> are ignored.</summary>
    [JsonPropertyName("applies_to_all_departments")]
    public bool AppliesToAllDepartments { get; set; }

    /// <summary>Department IDs (from the tenant's existing Department catalog) this process applies to.</summary>
    [JsonPropertyName("department_ids")]
    public List<Guid> DepartmentIds { get; set; } = [];

    /// <summary>Job Role IDs (from the tenant's existing JobRole catalog) this process applies to.</summary>
    [JsonPropertyName("job_role_ids")]
    public List<Guid> JobRoleIds { get; set; } = [];

    /// <summary>Last persisted whole-process (Step 4 "Skills Identification") learning recommendation,
    /// including any manually-assigned skills. Saved automatically after each LLM analysis run and after
    /// every manual skill add/remove, so it survives navigation and future sessions without re-running the LLM.</summary>
    [JsonPropertyName("process_wide_learning_recommendation")]
    public ProcessWideLearningRecommendationResult? ProcessWideLearningRecommendation { get; set; }
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

    /// <summary>Stable process-step code, e.g. "03-03", used as the second segment of every ActivityId.</summary>
    [JsonPropertyName("step_code")]
    public string StepCode { get; set; } = string.Empty;

    [JsonPropertyName("step_number")]
    public string StepNumber { get; set; } = string.Empty;

    [JsonPropertyName("step_name")]
    public string StepName { get; set; } = string.Empty;

    [JsonPropertyName("objective")]
    public string Objective { get; set; } = string.Empty;

    [JsonPropertyName("primary_responsible_role")]
    public string PrimaryResponsibleRole { get; set; } = string.Empty;

    [JsonPropertyName("supporting_roles")]
    public List<string> SupportingRoles { get; set; } = [];

    [JsonPropertyName("systems_and_tools")]
    public List<string> SystemsAndTools { get; set; } = [];

    [JsonPropertyName("trigger")]
    public string Trigger { get; set; } = string.Empty;

    [JsonPropertyName("required_inputs")]
    public List<string> RequiredInputs { get; set; } = [];

    [JsonPropertyName("required_activities")]
    public List<string> RequiredActivities { get; set; } = [];

    [JsonPropertyName("required_skills")]
    public List<string> RequiredSkills { get; set; } = [];

    [JsonPropertyName("deliverables")]
    public List<string> Deliverables { get; set; } = [];

    [JsonPropertyName("decisions_and_human_judgment")]
    public List<string> DecisionsAndHumanJudgment { get; set; } = [];

    [JsonPropertyName("business_rules_and_controls")]
    public List<string> BusinessRulesAndControls { get; set; } = [];

    [JsonPropertyName("exceptions_and_escalations")]
    public List<string> ExceptionsAndEscalations { get; set; } = [];

    [JsonPropertyName("evidence_produced_or_required")]
    public List<string> EvidenceProducedOrRequired { get; set; } = [];

    [JsonPropertyName("completion_condition")]
    public string CompletionCondition { get; set; } = string.Empty;

    [JsonPropertyName("missing_information")]
    public List<string> MissingInformation { get; set; } = [];

    /// <summary>Individual Activities belonging to this step (Process &#8594; Step &#8594; Activity hierarchy).</summary>
    [JsonPropertyName("activities")]
    public List<ProcessActivity> Activities { get; set; } = [];

    [JsonPropertyName("substeps")]
    public List<ProcessSubstep> Substeps { get; set; } = [];

    [JsonPropertyName("checklists")]
    public List<string> Checklists { get; set; } = [];

    [JsonPropertyName("source_pages")]
    public List<int> SourcePages { get; set; } = [];

    /// <summary>Verbatim (or near-verbatim) excerpt of the source document covering this step's instructions.
    /// Captured once during the fast Step-1 index extraction; used later as the ONLY input to the
    /// per-step deep analysis (role, systems, trigger, business rules, exceptions, etc.) so that analysis
    /// can run lazily, one step at a time, instead of inside the single bulk extraction call.</summary>
    [JsonPropertyName("source_content")]
    public string SourceContent { get; set; } = string.Empty;

    /// <summary>Real images embedded in the source document's page(s) covering this step (e.g. a
    /// screenshot, diagram, or form shown in the policy/manual) — extracted verbatim via
    /// <see cref="HumanOS.Storage.PdfTextExtractor.ExtractPagesWithImages"/> and attached here by
    /// matching <see cref="SourcePages"/> against each image's page number. These are the ACTUAL
    /// images from the document (not AI-generated), so a later Studio course can show the real
    /// figure to learners instead of an AI-reinterpreted illustration.</summary>
    [JsonPropertyName("step_images")]
    public List<ProcessStepImage> StepImages { get; set; } = [];

    /// <summary>True once the per-step deep business-requirements analysis (role, systems, trigger, business
    /// rules, exceptions, evidence, completion condition) has been run for this step.</summary>
    [JsonPropertyName("business_requirements_analyzed")]
    public bool BusinessRequirementsAnalyzed { get; set; }

    // ===== Wizard Step 3 "Clasificar Proceso" — categorized items with stable per-category IDs =====

    /// <summary>The role accountable for this step, as stated or inferred from the source document.</summary>
    [JsonPropertyName("owner_role")]
    public string OwnerRole { get; set; } = string.Empty;

    /// <summary>One of: Identified (role explicitly stated in the source), ToBeConfirmed (not stated — must be
    /// resolved by a human in a later wizard step).</summary>
    [JsonPropertyName("owner_status")]
    public string OwnerStatus { get; set; } = "ToBeConfirmed";

    [JsonPropertyName("classified_inputs")]
    public List<CodedListItem> ClassifiedInputs { get; set; } = [];

    [JsonPropertyName("classified_systems")]
    public List<CodedListItem> ClassifiedSystems { get; set; } = [];

    [JsonPropertyName("classified_dependencies")]
    public List<CodedListItem> ClassifiedDependencies { get; set; } = [];

    [JsonPropertyName("classified_business_rules")]
    public List<CodedListItem> ClassifiedBusinessRules { get; set; } = [];

    [JsonPropertyName("classified_exceptions")]
    public List<CodedListItem> ClassifiedExceptions { get; set; } = [];

    [JsonPropertyName("classified_outputs")]
    public List<CodedListItem> ClassifiedOutputs { get; set; } = [];

    /// <summary>True once this step's categorized classification (owner, inputs, systems, dependencies,
    /// activities, business rules, exceptions, outputs) has been derived in Wizard Step 3.</summary>
    [JsonPropertyName("process_classified")]
    public bool ProcessClassified { get; set; }
}

/// <summary>
/// A single, individually identifiable unit of work inside a Process Step.
/// ActivityId is a stable identifier (e.g. "CCR-03-03-A01") that must never be reused or changed on reorder.
/// Kept intentionally lean (no verbatim quotes or long narrative fields) so AI extraction stays fast even for processes with many steps/activities.
/// </summary>
public sealed class ProcessActivity
{
    /// <summary>Stable identifier in the form [ProcessCode]-[StepCode]-A[ActivityNumber]. Never reused, never changes on reorder.</summary>
    [JsonPropertyName("activity_id")]
    public string ActivityId { get; set; } = string.Empty;

    /// <summary>Controls visible ordering only; independent from ActivityId.</summary>
    [JsonPropertyName("activity_sequence")]
    public int ActivitySequence { get; set; }

    [JsonPropertyName("activity_name")]
    public string ActivityName { get; set; } = string.Empty;

    [JsonPropertyName("activity_description")]
    public string ActivityDescription { get; set; } = string.Empty;

    /// <summary>One of: HumanKnowledge, HumanJudgment, HumanBehavior, HumanDecision, HumanCommunication,
    /// HumanSystemOperation, ManagerApproval, PolicyOrComplianceReview, AIRecommendation,
    /// HumanVerificationOfAI, SystemAutomated, ExternalSystemOperation.</summary>
    [JsonPropertyName("activity_type")]
    public string ActivityType { get; set; } = string.Empty;

    /// <summary>One of: Draft, Active, Superseded, Retired.</summary>
    [JsonPropertyName("activity_status")]
    public string ActivityStatus { get; set; } = "Active";

    /// <summary>One of: SourceDocument, SystemGenerated, HumanAssigned.</summary>
    [JsonPropertyName("id_source")]
    public string IdSource { get; set; } = "SystemGenerated";

    [JsonPropertyName("primary_responsible_role")]
    public string PrimaryResponsibleRole { get; set; } = string.Empty;

    [JsonPropertyName("systems_and_tools")]
    public List<string> SystemsAndTools { get; set; } = [];

    [JsonPropertyName("human_judgment_required")]
    public bool HumanJudgmentRequired { get; set; }

    /// <summary>Short (one sentence) summary of what AI generates/recommends/summarizes/predicts/retrieves for this activity; empty if AI does not participate.</summary>
    [JsonPropertyName("ai_contribution")]
    public string AiContribution { get; set; } = string.Empty;

    [JsonPropertyName("human_verification_required")]
    public bool HumanVerificationRequired { get; set; }

    /// <summary>One of: Include, Exclude, IncludeHumanJudgmentOnly, ExternalProductTraining, CompanyWorkInstruction, RequiresValidation.</summary>
    [JsonPropertyName("learning_workflow_decision")]
    public string LearningWorkflowDecision { get; set; } = "Include";

    /// <summary>Capabilities written as Verb + object + performance context, e.g. "Classify a customer complaint using approved business rules."</summary>
    [JsonPropertyName("required_capabilities")]
    public List<string> RequiredCapabilities { get; set; } = [];

    /// <summary>One of: Explanation, GuidedWalkthrough, WorkedExample, Scenario, Simulation, Recall, Assessment,
    /// PracticeAndRetry, ExternalProductTraining, CompanyWorkInstruction.</summary>
    [JsonPropertyName("suggested_learning_method")]
    public string SuggestedLearningMethod { get; set; } = string.Empty;

    [JsonPropertyName("source_page")]
    public int SourcePage { get; set; }

    // ===== Rich detail fields (Phase B — derived lazily, one step at a time) =====

    [JsonPropertyName("required_inputs")]
    public List<string> RequiredInputs { get; set; } = [];

    [JsonPropertyName("outputs")]
    public List<string> Outputs { get; set; } = [];

    [JsonPropertyName("business_rules")]
    public List<string> BusinessRules { get; set; } = [];

    [JsonPropertyName("readiness_criteria")]
    public List<string> ReadinessCriteria { get; set; } = [];

    /// <summary>Verbatim (or near-verbatim) source text this activity was derived from.</summary>
    [JsonPropertyName("source_text")]
    public string SourceText { get; set; } = string.Empty;

    [JsonPropertyName("missing_information")]
    public List<string> MissingInformation { get; set; } = [];
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

/// <summary>A real image embedded in the source document, extracted verbatim (never AI-generated) and
/// attached to whichever <see cref="ProcessStep"/> covers its page — see <see cref="ProcessStep.StepImages"/>.</summary>
public sealed class ProcessStepImage
{
    /// <summary>1-based page number in the original PDF this image came from.</summary>
    [JsonPropertyName("source_page")]
    public int SourcePage { get; set; }

    /// <summary>Blob storage path (same convention as the source PDF / structured JSON) where the
    /// original image bytes are persisted, e.g. "process-documents/{documentId}/images/page-2-img-1.png".</summary>
    [JsonPropertyName("storage_path")]
    public string StoragePath { get; set; } = string.Empty;

    /// <summary>"image/png" or "image/jpeg".</summary>
    [JsonPropertyName("content_type")]
    public string ContentType { get; set; } = "image/png";

    /// <summary>Base64 data URL (e.g. "data:image/png;base64,...") embedded directly so the Review
    /// Process Steps screen can render a thumbnail immediately without a separate blob-download call.</summary>
    [JsonPropertyName("data_url")]
    public string DataUrl { get; set; } = string.Empty;
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
