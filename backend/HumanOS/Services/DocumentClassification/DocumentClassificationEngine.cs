namespace HumanOS.Services.DocumentClassification;

/// <summary>
/// Deterministic (no LLM/embeddings) document type detection engine
/// (2026-08-17). Runs every registered <see cref="IDocumentClassifier"/>
/// over a document's already-extracted plain text and forces a single
/// PrimaryType — types are mutually exclusive by design (see DocumentType's
/// own doc comment), so exactly one downstream chapter/engram extraction
/// strategy always applies unambiguously. All classifiers' scores are kept
/// in the result for wizard transparency/audit even though only the winner
/// is acted on.
/// </summary>
public sealed class DocumentClassificationEngine
{
    /// <summary>Below this score, PrimaryType is forced to Unknown even if
    /// it's technically the highest-scoring candidate — an ambiguous/weak
    /// signal should never be presented as a confident recommendation.</summary>
    public const double UnknownThreshold = 0.15;

    private readonly IReadOnlyList<IDocumentClassifier> _classifiers;

    public DocumentClassificationEngine(IEnumerable<IDocumentClassifier> classifiers)
    {
        _classifiers = classifiers.ToList();
    }

    public DocumentClassificationResult Classify(string sourceText)
    {
        var candidates = _classifiers
            .Select(classifier => classifier.Classify(sourceText))
            .OrderByDescending(candidate => candidate.Score)
            .ToList();

        var winner = candidates.FirstOrDefault();
        var isConfident = winner is not null && winner.Score >= UnknownThreshold;

        return new DocumentClassificationResult
        {
            PrimaryType = isConfident ? winner!.Type : DocumentType.Unknown,
            Confidence = isConfident ? winner!.Score : 0,
            Candidates = candidates
        };
    }
}
