namespace HumanOS.Models.Capabilities.Studio;

/// <summary>
/// One generated pedagogical step (Hypothesis/Teaching/DeepDive/Recall/
/// Production/Assessment) for one <see cref="StructuredLearningDocumentSection"/> —
/// Step 3 output. Written by the future "Diseñador de Sección" agent.
/// </summary>
public sealed class StructuredLearningSectionStep
{
    public Guid StructuredLearningSectionStepId { get; set; }

    public Guid StructuredLearningDocumentSectionId { get; set; }

    public Guid TenantId { get; set; }

    public StructuredLearningStepType StepType { get; set; }

    public int SortOrder { get; set; }

    public string Content { get; set; } = string.Empty;

    /// <summary>Structured sub-items for this step, as JSON — shape
    /// depends on <see cref="StepType"/>: Hypothesis → array of MCQ
    /// questions (question + options + correct index) generated from the
    /// same source excerpt; DeepDive → array of example cards
    /// (Title/Text/Emoji); Recall → array of distinct recall item
    /// strings; Assessment → array of questions (text + options/correct
    /// index when MultipleChoice). Teaching/Production usually only need
    /// <see cref="Content"/> unless they also carry an illustration id.
    /// Same lightweight "metadata-as-JSON-blob" convention as Graph's
    /// <see cref="HumanOS.Models.Capabilities.Graph.ExampleCard"/> (no
    /// separate DB table per sub-item type).</summary>
    public string? StepDataJson { get; set; }

    /// <summary>Only meaningful for StepType=Recall (2026-08-10) — the most
    /// recent question <see cref="Agents.Studio.StructuredLearningRecallAgent"/>
    /// generated, persisted here so a page reload can resume mid-practice
    /// instead of losing context. Mirrors
    /// <see cref="HumanOS.Models.Learning.LearningSessionStep.CurrentRecallPrompt"/>.
    /// Null until the student's first Recall turn.</summary>
    public string? CurrentRecallPrompt { get; set; }

    /// <summary>Only meaningful for StepType=DeepDive (2026-08-10) — 0-based
    /// index of the ExampleCard (in <see cref="StepDataJson"/>) the student
    /// is currently being quizzed on. Mirrors
    /// <see cref="HumanOS.Models.Learning.LearningSessionStep.CurrentDeepDiveCardIndex"/>.
    /// Every card must be mastered in order (see
    /// <see cref="HumanOS.Agentic.Runtime.DeepDiveLoopGate"/>) — no attempt
    /// cap, no regression path.</summary>
    public int CurrentDeepDiveCardIndex { get; set; }

    /// <summary>Only meaningful for StepType=DeepDive (2026-08-10) — the most
    /// recent follow-up question <see cref="Agents.Studio.StructuredLearningDeepDiveAgent"/>
    /// asked about the CURRENT card, persisted so a page reload can resume
    /// mid-card. Mirrors
    /// <see cref="HumanOS.Models.Learning.LearningSessionStep.CurrentDeepDiveQuestion"/>.
    /// Null on a brand-new card (falls back to the card's own Title/Text).</summary>
    public string? CurrentDeepDivePrompt { get; set; }

    /// <summary>Prompt/contract version that produced this step.</summary>
    public string? PromptVersion { get; set; }

    /// <summary>Process version used for this step's generation workflow.</summary>
    public string? ProcessVersion { get; set; }

    /// <summary>Model name used to generate this step.</summary>
    public string? ModelName { get; set; }

    /// <summary>Hash or snapshot of the prompt/config for this step.</summary>
    public string? PromptHash { get; set; }

    /// <summary>JSON snapshot of the generation config used for this step.</summary>
    public string? ConfigSnapshotJson { get; set; }

    /// <summary>Human sign-off (2026-08-11) — set when a reviewer directly
    /// edits this step's Content via the manual raw-edit tool and
    /// explicitly confirms they read it and take responsibility for it
    /// (the AI is not perfect and can alter or invent content). Reset back
    /// to false whenever the AI touches this step's Content again (via
    /// "Edit with AI" or a fresh regeneration) — a new AI output always
    /// needs its own fresh human sign-off.</summary>
    public bool IsHumanApproved { get; set; }

    public DateTime? HumanApprovedAtUtc { get; set; }

    /// <summary>Only meaningful for StepType=Teaching (2026-08-22) — an
    /// author-defined presentation order combining this step's narration
    /// text (auto-split into paragraphs from <see cref="Content"/>) and its
    /// generated videos into one sequence, as JSON:
    /// <c>[{"Type":"Text","ParagraphIndex":0},{"Type":"Video","IllustrationId":"..."},...]</c>.
    /// Null means "no custom order yet" — the student-facing view falls
    /// back to the old all-at-once layout (full text + all videos shown
    /// together). When set, the Production/student view renders this as a
    /// step-by-step wizard (one item at a time, Next/Previous) instead.</summary>
    public string? TimelineJson { get; set; }

    public DateTime CreatedDate { get; set; }

    public StructuredLearningDocumentSection Section { get; set; } = null!;
}
