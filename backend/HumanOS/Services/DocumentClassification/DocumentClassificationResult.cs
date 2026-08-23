namespace HumanOS.Services.DocumentClassification;

/// <summary>One matched rule that contributed to a candidate's score — kept
/// for auditability/explainability (2026-08-17 requirement: classification
/// must be explainable, never a black-box score). MatchedText is a verbatim
/// excerpt from the source, never invented/paraphrased, mirroring the same
/// "Evidence must be literal" discipline already used by
/// StructuredLearningConnectionExtractionAgent/StructuredLearningKnowledgeFact.</summary>
public sealed class ClassificationEvidence
{
    /// <summary>Human-readable description of the rule that matched (e.g.
    /// "Numbered article marker (ARTICULO N)").</summary>
    public string RuleDescription { get; init; } = string.Empty;

    /// <summary>Verbatim excerpt from the source text where this rule matched.</summary>
    public string MatchedText { get; init; } = string.Empty;

    /// <summary>How many times this rule matched in the whole document
    /// (capped internally before scoring — see KeywordPatternClassifier).</summary>
    public int Occurrences { get; init; }

    /// <summary>This rule's configured weight — higher means a stronger,
    /// more specific signal for its DocumentType.</summary>
    public double Weight { get; init; }
}

/// <summary>One DocumentType's classification attempt for a document, with
/// its score and the evidence that produced it.</summary>
public sealed class DocumentTypeCandidate
{
    public DocumentType Type { get; init; }

    /// <summary>0-1, saturating — see KeywordPatternClassifier.ComputeScore.</summary>
    public double Score { get; init; }

    public IReadOnlyList<ClassificationEvidence> Evidence { get; init; } = [];
}

/// <summary>Final result of DocumentClassificationEngine.Classify — a single
/// forced PrimaryType (2026-08-17 decision: types are mutually exclusive, so
/// exactly one downstream extraction strategy always applies unambiguously),
/// plus every candidate's score for transparency/audit in the wizard.</summary>
public sealed class DocumentClassificationResult
{
    public DocumentType PrimaryType { get; init; }

    /// <summary>The winning candidate's own Score — 0 when PrimaryType is Unknown.</summary>
    public double Confidence { get; init; }

    /// <summary>Every classifier's result, sorted by Score descending —
    /// lets the wizard show "we also considered X (0.40)" next to the
    /// recommended type, and lets a human override with full visibility.</summary>
    public IReadOnlyList<DocumentTypeCandidate> Candidates { get; init; } = [];
}
