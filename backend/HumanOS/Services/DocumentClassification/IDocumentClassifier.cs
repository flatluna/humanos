namespace HumanOS.Services.DocumentClassification;

/// <summary>Strategy Pattern contract — one implementation per DocumentType
/// (see Classifiers/ folder). Pure/deterministic: no I/O, no LLM calls, safe
/// to call repeatedly and in parallel.</summary>
public interface IDocumentClassifier
{
    DocumentType Type { get; }

    /// <summary>Scores how well sourceText matches this classifier's
    /// DocumentType. Never throws for empty/whitespace text — returns a
    /// zero-score candidate instead.</summary>
    DocumentTypeCandidate Classify(string sourceText);
}
