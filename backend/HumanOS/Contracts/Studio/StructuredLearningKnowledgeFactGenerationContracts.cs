namespace HumanOS.Contracts.Studio;

/// <summary>
/// Input contract for the future Knowledge Fact generator. It contains only
/// the Engram text and safe, serializable Knowledge Graph projections.
/// </summary>
public sealed class StructuredLearningKnowledgeFactGenerationRequest
{
    public Guid EngramId { get; set; }

    public string EngramName { get; set; } = string.Empty;

    public string Language { get; set; } = string.Empty;

    public string SourceText { get; set; } = string.Empty;

    public List<StructuredLearningKnowledgeFactEntityInput> Entities { get; set; } = [];

    public List<StructuredLearningKnowledgeFactConnectionInput> Connections { get; set; } = [];
}

/// <summary>Safe entity projection for a future Knowledge Fact generator.</summary>
public sealed class StructuredLearningKnowledgeFactEntityInput
{
    public Guid EntityId { get; set; }

    public string CanonicalName { get; set; } = string.Empty;

    public string EntityType { get; set; } = string.Empty;

    public string? Description { get; set; }

    public List<string> Aliases { get; set; } = [];
}

/// <summary>Safe connection projection for a future Knowledge Fact generator.
/// Connection remains the source of truth for Graph metadata and evidence.</summary>
public sealed class StructuredLearningKnowledgeFactConnectionInput
{
    public Guid ConnectionId { get; set; }

    public Guid SourceEntityId { get; set; }

    public string SourceEntityName { get; set; } = string.Empty;

    public Guid TargetEntityId { get; set; }

    public string TargetEntityName { get; set; } = string.Empty;

    public string RelationshipCategory { get; set; } = string.Empty;

    public string RelationshipLabel { get; set; } = string.Empty;

    public string? Evidence { get; set; }

    public string EpistemicStatus { get; set; } = "explicit";

    public double? Confidence { get; set; }
}

/// <summary>Output contract for the future Knowledge Fact generator.</summary>
public sealed class StructuredLearningKnowledgeFactGenerationResponse
{
    public Guid EngramId { get; set; }

    public string EngramName { get; set; } = string.Empty;

    public string Language { get; set; } = string.Empty;

    public List<StructuredLearningGeneratedKnowledgeFact> KnowledgeFacts { get; set; } = [];

    public List<StructuredLearningMissingGraphFact> MissingGraphFacts { get; set; } = [];
}

/// <summary>One proposed fact. KnowledgeFactId is intentionally absent because
/// it is assigned only after validation and persistence.</summary>
public sealed class StructuredLearningGeneratedKnowledgeFact
{
    public string Statement { get; set; } = string.Empty;

    public string FactType { get; set; } = string.Empty;

    public string Importance { get; set; } = string.Empty;

    public string DerivationType { get; set; } = string.Empty;

    public List<Guid> SourceEntityIds { get; set; } = [];

    public List<Guid> SourceConnectionIds { get; set; } = [];

    public List<StructuredLearningKnowledgeFactGraphPathStepOutput> GraphPath { get; set; } = [];

    public List<string> Evidence { get; set; } = [];

    public string EpistemicStatus { get; set; } = "explicit";

    public double Confidence { get; set; }
}

/// <summary>One ordered connection reference in a proposed Graph Path.</summary>
public sealed class StructuredLearningKnowledgeFactGraphPathStepOutput
{
    public Guid ConnectionId { get; set; }

    public int StepOrder { get; set; }
}

/// <summary>An important text idea that lacks sufficient support in the
/// available Knowledge Graph. It is a proposal for later review, not a fact.</summary>
public sealed class StructuredLearningMissingGraphFact
{
    public string Statement { get; set; } = string.Empty;

    public string? Reason { get; set; }

    public string EpistemicStatus { get; set; } = "inferred";

    public double? Confidence { get; set; }
}
