using System.Text.RegularExpressions;

namespace HumanOS.Services.DocumentClassification.Classifiers;

/// <summary>Continuous narrative/expository prose with no explicit numbered
/// index — history, geography, science, or technical material written as
/// flowing text. Necessarily a weaker/softer signal than the other
/// classifiers (there's no structural marker for "absence of structure");
/// scores on topical vocabulary + rhetorical connectors typical of
/// expository narrative, and structurally wins mostly by NOT matching
/// AcademicWithIndex's numbered markers (see DocumentClassificationEngine).</summary>
public sealed partial class AcademicNarrativeClassifier : KeywordPatternClassifier
{
    public override DocumentType Type => DocumentType.AcademicNarrative;

    protected override IReadOnlyList<ClassificationRule> Rules { get; } =
    [
        new("Historical period/civilization language", HistoryRegex(), 1.5),
        new("Geographic feature language", GeographyRegex(), 1.5),
        new("Scientific/natural-phenomenon language", ScienceRegex(), 1.5),
        new("Chronology/cause-effect connectors (POSTERIORMENTE/COMO CONSECUENCIA)", NarrativeConnectorRegex(), 1.0),
        new("Expository framing language (SE CARACTERIZA POR/SE ORIGINA EN)", ExpositoryRegex(), 1.0),
    ];

    [GeneratedRegex(@"\b(CIVILIZACI[OÓ]N|IMPERIO|PER[IÍ]ODO\s+(HIST[OÓ]RICO)?|SIGLO\s+[IVXLCDM]+|GUERRA\s+(MUNDIAL|CIVIL))\b", RegexOptions.IgnoreCase)]
    private static partial Regex HistoryRegex();

    [GeneratedRegex(@"\b(CONTINENTE|COORDENADAS|R[IÍ]O|CORDILLERA|CLIMA\s+(TROPICAL|TEMPLADO|[AÁ]RIDO)|RELIEVE)\b", RegexOptions.IgnoreCase)]
    private static partial Regex GeographyRegex();

    [GeneratedRegex(@"\b(FEN[OÓ]MENO\s+(NATURAL|F[IÍ]SICO)|ECOSISTEMA|REACCI[OÓ]N\s+QU[IÍ]MICA|ORGANISMO)\b", RegexOptions.IgnoreCase)]
    private static partial Regex ScienceRegex();

    [GeneratedRegex(@"\b(POSTERIORMENTE|COMO\s+CONSECUENCIA|A\s+RA[IÍ]Z\s+DE|DIO\s+ORIGEN\s+A)\b", RegexOptions.IgnoreCase)]
    private static partial Regex NarrativeConnectorRegex();

    [GeneratedRegex(@"\b(SE\s+CARACTERIZA\s+POR|SE\s+ORIGINA\s+EN|SE\s+DESARROLL[OÓ]\s+(A\s+PARTIR\s+DE|EN))\b", RegexOptions.IgnoreCase)]
    private static partial Regex ExpositoryRegex();
}
