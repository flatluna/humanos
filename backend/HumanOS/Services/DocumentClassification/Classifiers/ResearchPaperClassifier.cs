using System.Text.RegularExpressions;

namespace HumanOS.Services.DocumentClassification.Classifiers;

/// <summary>Abstract/Introduction/Methodology/Results/Discussion/References
/// — the standard IMRaD scientific paper structure.</summary>
public sealed partial class ResearchPaperClassifier : KeywordPatternClassifier
{
    public override DocumentType Type => DocumentType.ResearchPaper;

    protected override IReadOnlyList<ClassificationRule> Rules { get; } =
    [
        new("ABSTRACT section heading", AbstractRegex(), 3.0),
        new("METHODOLOGY/METHODS section heading", MethodologyRegex(), 2.5),
        new("RESULTS section heading", ResultsRegex(), 2.0),
        new("DISCUSSION/CONCLUSION section heading", DiscussionRegex(), 1.5),
        new("REFERENCES/BIBLIOGRAPHY section heading", ReferencesRegex(), 2.0),
        new("Citation pattern (Author, Year)", CitationRegex(), 1.0),
    ];

    [GeneratedRegex(@"^\s*(ABSTRACT|RESUMEN)\s*$", RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    private static partial Regex AbstractRegex();

    [GeneratedRegex(@"^\s*(METHODOLOGY|METHODS|METODOLOG[IÍ]A)\s*$", RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    private static partial Regex MethodologyRegex();

    [GeneratedRegex(@"^\s*(RESULTS|RESULTADOS)\s*$", RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    private static partial Regex ResultsRegex();

    [GeneratedRegex(@"^\s*(DISCUSSION|DISCUSI[OÓ]N|CONCLUSIONS?|CONCLUSIONES)\s*$", RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    private static partial Regex DiscussionRegex();

    [GeneratedRegex(@"^\s*(REFERENCES|REFERENCIAS|BIBLIOGRAPHY|BIBLIOGRAF[IÍ]A)\s*$", RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    private static partial Regex ReferencesRegex();

    [GeneratedRegex(@"\([A-Z][a-zA-Z]+(?:\s+et\s+al\.)?,\s*\d{4}\)")]
    private static partial Regex CitationRegex();
}
