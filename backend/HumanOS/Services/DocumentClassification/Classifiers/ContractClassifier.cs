using System.Text.RegularExpressions;

namespace HumanOS.Services.DocumentClassification.Classifiers;

/// <summary>Contrato de servicios, NDA — numbered "Cláusulas" naming
/// bilateral parties and their obligations/rights.</summary>
public sealed partial class ContractClassifier : KeywordPatternClassifier
{
    public override DocumentType Type => DocumentType.Contract;

    protected override IReadOnlyList<ClassificationRule> Rules { get; } =
    [
        new("Numbered CLAUSULA marker", ClausulaRegex(), 3.0),
        new("Named contracting parties (EL/LA ... EN LO SUCESIVO)", PartiesRegex(), 2.5),
        new("Contract preamble (CELEBRAN/CELEBRAN EL PRESENTE CONTRATO)", PreambleRegex(), 2.5),
        new("Obligation/right language (SE OBLIGA A / TENDRA DERECHO A)", ObligationRegex(), 1.5),
        new("Termination/jurisdiction boilerplate (RESCISION/JURISDICCION)", BoilerplateRegex(), 1.0),
    ];

    [GeneratedRegex(@"\bCL[AÁ]USULA\s+[IVXLCDM\d]+", RegexOptions.IgnoreCase)]
    private static partial Regex ClausulaRegex();

    [GeneratedRegex(@"\bEN\s+LO\s+SUCESIVO\s+(SE\s+LE\s+)?DENOMINAR[AÁ]", RegexOptions.IgnoreCase)]
    private static partial Regex PartiesRegex();

    [GeneratedRegex(@"\b(CELEBRAN\s+EL\s+PRESENTE\s+CONTRATO|CONTRATO\s+QUE\s+CELEBRAN)\b", RegexOptions.IgnoreCase)]
    private static partial Regex PreambleRegex();

    [GeneratedRegex(@"\b(SE\s+OBLIGA\s+A|TENDR[AÁ]\s+DERECHO\s+A|OBLIGACIONES\s+DE\s+LAS\s+PARTES)\b", RegexOptions.IgnoreCase)]
    private static partial Regex ObligationRegex();

    [GeneratedRegex(@"\b(RESCISI[OÓ]N|JURISDICCI[OÓ]N\s+COMPETENTE|TERMINACI[OÓ]N\s+ANTICIPADA)\b", RegexOptions.IgnoreCase)]
    private static partial Regex BoilerplateRegex();
}
