using System.Text.RegularExpressions;

namespace HumanOS.Services.DocumentClassification.Classifiers;

/// <summary>Ley, Reglamento, NOM — CAPITULO/TITULO/ARTICULO/FRACCION/INCISO
/// hierarchical markers, DECRETO/LEY issuance language.</summary>
public sealed partial class LawClassifier : KeywordPatternClassifier
{
    public override DocumentType Type => DocumentType.Law;

    protected override IReadOnlyList<ClassificationRule> Rules { get; } =
    [
        new("Numbered ARTICULO marker", ArticuloRegex(), 3.0),
        new("Numbered CAPITULO marker", CapituloRegex(), 2.0),
        new("TITULO marker", TituloRegex(), 1.5),
        new("FRACCION/INCISO marker", FraccionIncisoRegex(), 2.0),
        new("DECRETO/LEY issuance language", DecretoLeyRegex(), 2.5),
        new("NOM/Norma Oficial reference", NomRegex(), 2.5),
        new("REGLAMENTO reference", ReglamentoRegex(), 1.5),
    ];

    [GeneratedRegex(@"\bART[IÍ]CULO\s+\d+", RegexOptions.IgnoreCase)]
    private static partial Regex ArticuloRegex();

    [GeneratedRegex(@"\bCAP[IÍ]TULO\s+[IVXLCDM\d]+", RegexOptions.IgnoreCase)]
    private static partial Regex CapituloRegex();

    [GeneratedRegex(@"\bT[IÍ]TULO\s+[IVXLCDM\d]+", RegexOptions.IgnoreCase)]
    private static partial Regex TituloRegex();

    [GeneratedRegex(@"\b(FRACCI[OÓ]N|INCISO)\s+[IVXLCDM\d a-z]+", RegexOptions.IgnoreCase)]
    private static partial Regex FraccionIncisoRegex();

    [GeneratedRegex(@"\b(DECRETO|EL\s+CONGRESO\s+DECRETA|SE\s+EXPIDE\s+LA\s+(LEY|PRESENTE\s+LEY))\b", RegexOptions.IgnoreCase)]
    private static partial Regex DecretoLeyRegex();

    [GeneratedRegex(@"\bNOM-\d+", RegexOptions.IgnoreCase)]
    private static partial Regex NomRegex();

    [GeneratedRegex(@"\bREGLAMENTO\s+(DE|DEL|INTERNO)\b", RegexOptions.IgnoreCase)]
    private static partial Regex ReglamentoRegex();
}
