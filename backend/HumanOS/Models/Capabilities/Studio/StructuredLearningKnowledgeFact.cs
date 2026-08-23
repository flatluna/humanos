namespace HumanOS.Models.Capabilities.Studio;

/// <summary>
/// One short, verifiable pedagogical statement derived from an Engram's text
/// and Knowledge Graph. Graph-path provenance is intentionally added in a
/// later step; this row stores only the primary fact record.
/// </summary>
public sealed class StructuredLearningKnowledgeFact
{
    public Guid KnowledgeFactId { get; set; }

    public Guid EngramId { get; set; }

    public string Statement { get; set; } = string.Empty;

    public string FactType { get; set; } = string.Empty;

    public string Importance { get; set; } = string.Empty;

    public string DerivationType { get; set; } = string.Empty;

    public string EpistemicStatus { get; set; } = string.Empty;

    public double Confidence { get; set; }

    public string Language { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public StructuredLearningDocumentSection Engram { get; set; } = null!;

    public ICollection<StructuredLearningKnowledgeFactGraphPathStep> GraphPathSteps { get; set; } = [];
}
