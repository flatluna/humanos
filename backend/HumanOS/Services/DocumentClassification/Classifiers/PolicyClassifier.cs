using System.Text.RegularExpressions;

namespace HumanOS.Services.DocumentClassification.Classifiers;

/// <summary>Internal governance rule statements — code of conduct,
/// harassment policy, data governance policy. Normative language
/// (prohibitions/definitions/reporting duties) WITHOUT a party-to-party
/// contract and WITHOUT government issuance, and without a sequential step
/// order (that's Procedure instead).</summary>
public sealed partial class PolicyClassifier : KeywordPatternClassifier
{
    public override DocumentType Type => DocumentType.Policy;

    protected override IReadOnlyList<ClassificationRule> Rules { get; } =
    [
        new("Policy self-identification (POLITICA DE/POLICY)", PolicyTitleRegex(), 3.0),
        new("Prohibition language (ESTA PROHIBIDO/NO SE PERMITE)", ProhibitionRegex(), 2.5),
        new("Conduct definition language (SE CONSIDERA/SE DEFINE COMO)", DefinitionRegex(), 2.0),
        new("Reporting duty language (DEBE REPORTAR/CANAL DE DENUNCIA)", ReportingRegex(), 2.0),
        new("Compliance/governance section headings", ComplianceHeadingRegex(), 1.5),
        new("Sanction/consequence language (SANCION/MEDIDAS DISCIPLINARIAS)", SanctionRegex(), 1.5),
    ];

    [GeneratedRegex(@"\bPOL[IÍ]TICA\s+(DE|INTERNA\s+DE)\b|\bCODE\s+OF\s+CONDUCT\b", RegexOptions.IgnoreCase)]
    private static partial Regex PolicyTitleRegex();

    [GeneratedRegex(@"\b(EST[AÁ]\s+PROHIBIDO|NO\s+SE\s+PERMITE|QUEDA\s+PROHIBIDO)\b", RegexOptions.IgnoreCase)]
    private static partial Regex ProhibitionRegex();

    [GeneratedRegex(@"\b(SE\s+CONSIDERA(?:\s+COMO)?|SE\s+DEFINE\s+COMO|CONSTITUYE\s+ACOSO)\b", RegexOptions.IgnoreCase)]
    private static partial Regex DefinitionRegex();

    [GeneratedRegex(@"\b(DEBE\s+REPORTAR|CANAL\s+DE\s+DENUNCIA|L[IÍ]NEA\s+DE\s+[EÉ]TICA)\b", RegexOptions.IgnoreCase)]
    private static partial Regex ReportingRegex();

    [GeneratedRegex(@"\b(CUMPLIMIENTO|GOBIERNO\s+DE\s+DATOS|ALCANCE\s+DE\s+LA\s+POL[IÍ]TICA)\b", RegexOptions.IgnoreCase)]
    private static partial Regex ComplianceHeadingRegex();

    [GeneratedRegex(@"\b(SANCI[OÓ]N|MEDIDAS\s+DISCIPLINARIAS|CONSECUENCIAS\s+DEL\s+INCUMPLIMIENTO)\b", RegexOptions.IgnoreCase)]
    private static partial Regex SanctionRegex();
}
