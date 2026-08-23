namespace HumanOS.Models.Capabilities.Studio;

/// <summary>
/// One ordered Knowledge Graph connection used to derive a Knowledge Fact.
/// The referenced Connection remains the single source of truth for entity,
/// relationship, evidence, and epistemic metadata.
/// </summary>
public sealed class StructuredLearningKnowledgeFactGraphPathStep
{
    public Guid KnowledgeFactGraphPathStepId { get; set; }

    public Guid KnowledgeFactId { get; set; }

    public Guid ConnectionId { get; set; }

    public int StepOrder { get; set; }

    public DateTime CreatedAt { get; set; }

    public StructuredLearningKnowledgeFact KnowledgeFact { get; set; } = null!;

    public StructuredLearningConnection Connection { get; set; } = null!;
}
