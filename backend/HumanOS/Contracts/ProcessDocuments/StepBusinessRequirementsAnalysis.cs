using System.Text.Json.Serialization;

namespace HumanOS.Contracts.ProcessDocuments;

/// <summary>
/// PHASE B — output schema for the deep business-requirements analysis of ONE process step, run lazily
/// (one step at a time) using only that step's own <see cref="ProcessStep.SourceContent"/> captured during
/// the fast Phase A index extraction. This is distinct from <see cref="WorkStepRequirementsAnalysisResult"/>,
/// which derives employee skills/tools/training needs; this result derives business-process attributes
/// (role, systems, trigger, business rules, exceptions) for process documentation purposes.
/// </summary>
public sealed class StepBusinessRequirementsAnalysisResult
{
    [JsonPropertyName("documentId")]
    public string DocumentId { get; set; } = string.Empty;

    [JsonPropertyName("stepId")]
    public string StepId { get; set; } = string.Empty;

    [JsonPropertyName("stepNumber")]
    public string StepNumber { get; set; } = string.Empty;

    [JsonPropertyName("primaryResponsibleRole")]
    public string PrimaryResponsibleRole { get; set; } = string.Empty;

    [JsonPropertyName("supportingRoles")]
    public List<string> SupportingRoles { get; set; } = [];

    [JsonPropertyName("systemsAndTools")]
    public List<string> SystemsAndTools { get; set; } = [];

    [JsonPropertyName("trigger")]
    public string Trigger { get; set; } = string.Empty;

    [JsonPropertyName("requiredInputs")]
    public List<string> RequiredInputs { get; set; } = [];

    [JsonPropertyName("decisionsAndHumanJudgment")]
    public List<string> DecisionsAndHumanJudgment { get; set; } = [];

    [JsonPropertyName("businessRulesAndControls")]
    public List<string> BusinessRulesAndControls { get; set; } = [];

    [JsonPropertyName("exceptionsAndEscalations")]
    public List<string> ExceptionsAndEscalations { get; set; } = [];

    [JsonPropertyName("evidenceProducedOrRequired")]
    public List<string> EvidenceProducedOrRequired { get; set; } = [];

    [JsonPropertyName("completionCondition")]
    public string CompletionCondition { get; set; } = string.Empty;

    [JsonPropertyName("missingInformation")]
    public List<string> MissingInformation { get; set; } = [];

    /// <summary>Individual Activities identified inside this step's sourceContent (Process &#8594; Step &#8594; Activity).</summary>
    [JsonPropertyName("activities")]
    public List<StepActivityAnalysisDto> Activities { get; set; } = [];

    // ===== Wizard Step 3 "Clasificar Proceso" categories =====

    /// <summary>The role accountable for this step, as stated in the source text.</summary>
    [JsonPropertyName("ownerRole")]
    public string OwnerRole { get; set; } = string.Empty;

    /// <summary>True only if the source text explicitly names the responsible role; false if it must be
    /// confirmed later by a human (ownerRole should then read "Role to be identified").</summary>
    [JsonPropertyName("ownerIdentified")]
    public bool OwnerIdentified { get; set; }

    [JsonPropertyName("inputs")]
    public List<ClassifiedItemDto> Inputs { get; set; } = [];

    [JsonPropertyName("systems")]
    public List<ClassifiedItemDto> Systems { get; set; } = [];

    [JsonPropertyName("dependencies")]
    public List<ClassifiedItemDto> Dependencies { get; set; } = [];

    [JsonPropertyName("businessRules")]
    public List<ClassifiedItemDto> BusinessRules { get; set; } = [];

    [JsonPropertyName("exceptions")]
    public List<ClassifiedItemDto> Exceptions { get; set; } = [];

    [JsonPropertyName("outputs")]
    public List<ClassifiedItemDto> Outputs { get; set; } = [];
}

/// <summary>One classified item as returned by the AI, before the endpoint assigns its stable per-category ID.</summary>
public sealed class ClassifiedItemDto
{
    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;
}

/// <summary>One Activity derived from a step's sourceContent during Phase B analysis. ActivityId is assigned
/// by the caller (endpoint), not the AI, to guarantee the stable [ProcessCode]-[StepCode]-A[NN] format.</summary>
public sealed class StepActivityAnalysisDto
{
    /// <summary>Controls visible ordering only.</summary>
    [JsonPropertyName("activitySequence")]
    public int ActivitySequence { get; set; }

    [JsonPropertyName("activityName")]
    public string ActivityName { get; set; } = string.Empty;

    [JsonPropertyName("activityDescription")]
    public string ActivityDescription { get; set; } = string.Empty;

    /// <summary>One of: HumanKnowledge, HumanJudgment, HumanBehavior, HumanDecision, HumanCommunication,
    /// HumanSystemOperation, HumanVerificationOfAI, SystemAutomated, ExternalSystemOperation.</summary>
    [JsonPropertyName("activityType")]
    public string ActivityType { get; set; } = string.Empty;

    [JsonPropertyName("primaryResponsibleRole")]
    public string PrimaryResponsibleRole { get; set; } = string.Empty;

    [JsonPropertyName("systemsAndTools")]
    public List<string> SystemsAndTools { get; set; } = [];

    [JsonPropertyName("requiredInputs")]
    public List<string> RequiredInputs { get; set; } = [];

    [JsonPropertyName("outputs")]
    public List<string> Outputs { get; set; } = [];

    [JsonPropertyName("businessRules")]
    public List<string> BusinessRules { get; set; } = [];

    [JsonPropertyName("humanJudgmentRequired")]
    public bool HumanJudgmentRequired { get; set; }

    /// <summary>Short summary of what AI generates/recommends/summarizes/predicts/retrieves; empty if AI does not participate.</summary>
    [JsonPropertyName("aiContribution")]
    public string AiContribution { get; set; } = string.Empty;

    [JsonPropertyName("humanVerificationRequired")]
    public bool HumanVerificationRequired { get; set; }

    [JsonPropertyName("evidenceRequired")]
    public List<string> EvidenceRequired { get; set; } = [];

    /// <summary>Capabilities/behaviors needed, written as Verb + object + performance context.</summary>
    [JsonPropertyName("requiredCapabilities")]
    public List<string> RequiredCapabilities { get; set; } = [];

    [JsonPropertyName("learningRelevance")]
    public string LearningRelevance { get; set; } = string.Empty;

    /// <summary>One of: Explanation, GuidedWalkthrough, WorkedExample, Scenario, Simulation, Recall, Assessment,
    /// PracticeAndRetry, ExternalProductTraining, CompanyWorkInstruction.</summary>
    [JsonPropertyName("suggestedLearningMethod")]
    public string SuggestedLearningMethod { get; set; } = string.Empty;

    [JsonPropertyName("readinessCriteria")]
    public List<string> ReadinessCriteria { get; set; } = [];

    [JsonPropertyName("sourcePage")]
    public int SourcePage { get; set; }

    [JsonPropertyName("sourceText")]
    public string SourceText { get; set; } = string.Empty;

    [JsonPropertyName("missingInformation")]
    public List<string> MissingInformation { get; set; } = [];
}
