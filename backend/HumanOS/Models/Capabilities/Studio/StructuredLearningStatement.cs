namespace HumanOS.Models.Capabilities.Studio;

/// <summary>
/// A section-scoped business rule or norm inferred from a source excerpt before
/// ontology generation. This is intentionally lightweight and not tied to a fixed
/// domain ontology shape yet; it preserves the source text and the rule's logical
/// properties so later higher-level reasoning can lift them into entities and edges.
/// </summary>
public sealed class StructuredLearningStatement
{
    public Guid StatementId { get; set; }

    public Guid SectionId { get; set; }

    public string StatementType { get; set; } = string.Empty;

    public string Statement { get; set; } = string.Empty;

    public string? SourceText { get; set; }

    public string? ConditionEntity { get; set; }

    public string? ConditionAttribute { get; set; }

    public string? ConditionOperator { get; set; }

    public string? ConditionValue { get; set; }

    public string? ConditionUnit { get; set; }

    public string? ActorEntity { get; set; }

    public string? ActorType { get; set; }

    public string? Action { get; set; }

    public string? TargetEntity { get; set; }

    public string? Modality { get; set; }

    public List<string> PossibleOutcomes { get; set; } = [];

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
}
