using System.Text.RegularExpressions;

namespace HumanOS.Services.DocumentClassification;

/// <summary>One weighted signal a KeywordPatternClassifier looks for —
/// higher Weight means a stronger, more type-specific signal (e.g. the
/// literal marker "ARTICULO 5" is far more specific to Law than the loose
/// word "artículo" appearing once in prose).</summary>
public sealed record ClassificationRule(string Description, Regex Pattern, double Weight);

/// <summary>
/// Shared scoring engine for every per-type IDocumentClassifier — each
/// concrete classifier (see Classifiers/) only supplies its own list of
/// <see cref="ClassificationRule"/>s; this base class owns the (identical,
/// deterministic) math that turns rule matches into a 0-1 score plus
/// auditable evidence, so the math is defined ONCE instead of once per type.
///
/// SCORING: for each rule, occurrences are counted (capped at
/// <see cref="MaxOccurrencesPerRule"/> so one repeated common word can't
/// dominate the score on its own) and multiplied by the rule's Weight; the
/// summed raw score is squashed into (0,1) via a saturating curve
/// (score = raw / (raw + K)) — monotonic, bounded, and easy to reason about
/// (see <see cref="SaturationConstant"/>).
/// </summary>
public abstract class KeywordPatternClassifier : IDocumentClassifier
{
    /// <summary>Caps how many occurrences of the SAME rule count toward the
    /// score — prevents one very common pattern appearing 50 times from
    /// alone saturating the score regardless of every other rule.</summary>
    private const int MaxOccurrencesPerRule = 5;

    /// <summary>Saturation constant for the raw-to-(0,1) score curve.
    /// Smaller values reach high confidence with fewer/weaker matches;
    /// larger values require stronger evidence. 6.0 was chosen so that
    /// ~3-4 solid, high-weight (2.0-3.0) rule matches alone already cross
    /// the 0.70 "Primary" threshold, while a single weak/ambiguous match
    /// stays well under the 0.15 "Unknown" floor.</summary>
    protected virtual double SaturationConstant => 6.0;

    public abstract DocumentType Type { get; }

    /// <summary>The weighted signals that identify this DocumentType —
    /// declared once per concrete classifier, in Classifiers/.</summary>
    protected abstract IReadOnlyList<ClassificationRule> Rules { get; }

    public DocumentTypeCandidate Classify(string sourceText)
    {
        if (string.IsNullOrWhiteSpace(sourceText))
        {
            return new DocumentTypeCandidate { Type = Type, Score = 0, Evidence = [] };
        }

        var evidence = new List<ClassificationEvidence>();
        var rawScore = 0.0;

        foreach (var rule in Rules)
        {
            var matches = rule.Pattern.Matches(sourceText);
            if (matches.Count == 0)
            {
                continue;
            }

            var cappedCount = Math.Min(matches.Count, MaxOccurrencesPerRule);
            rawScore += rule.Weight * cappedCount;

            evidence.Add(new ClassificationEvidence
            {
                RuleDescription = rule.Description,
                MatchedText = matches[0].Value.Trim(),
                Occurrences = matches.Count,
                Weight = rule.Weight
            });
        }

        var score = rawScore / (rawScore + SaturationConstant);
        return new DocumentTypeCandidate { Type = Type, Score = score, Evidence = evidence };
    }
}
