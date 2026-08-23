using System.Text.RegularExpressions;

namespace HumanOS.Services.DocumentClassification.Classifiers;

/// <summary>Sequential ordered steps with inputs/outputs — SOPs, cooking
/// recipes, mechanical repair manuals, business processes. Structurally
/// identical regardless of domain (Cocina/Mecánica/Procesos de Negocio live
/// in Subject/Topic, not here).</summary>
public sealed partial class ProcedureClassifier : KeywordPatternClassifier
{
    public override DocumentType Type => DocumentType.Procedure;

    protected override IReadOnlyList<ClassificationRule> Rules { get; } =
    [
        new("Numbered PASO/STEP marker", PasoRegex(), 3.0),
        new("SOP self-identification (PROCEDIMIENTO/SOP)", SopTitleRegex(), 2.5),
        new("Process metadata headings (OBJETIVO/ALCANCE/RESPONSABLE)", ProcessMetadataRegex(), 2.0),
        new("Ingredient/tool list language (INGREDIENTES/HERRAMIENTAS)", InputListRegex(), 2.0),
        new("Imperative sequencing language (A CONTINUACION/LUEGO/DESPUES)", SequencingRegex(), 1.0),
        new("Yield/duration language (PORCIONES/TIEMPO DE)", YieldRegex(), 1.0),
    ];

    [GeneratedRegex(@"\b(PASO|STEP|ACTIVIDAD)\s+\d+", RegexOptions.IgnoreCase)]
    private static partial Regex PasoRegex();

    [GeneratedRegex(@"\b(PROCEDIMIENTO\s+OPERATIVO\s+ESTANDAR|\bSOP\b|PROCEDIMIENTO\s+DE\s+)", RegexOptions.IgnoreCase)]
    private static partial Regex SopTitleRegex();

    [GeneratedRegex(@"\b(OBJETIVO|ALCANCE|RESPONSABLE(S)?)\s*:", RegexOptions.IgnoreCase)]
    private static partial Regex ProcessMetadataRegex();

    [GeneratedRegex(@"\b(INGREDIENTES|HERRAMIENTAS\s+NECESARIAS|MATERIALES\s+REQUERIDOS)\b", RegexOptions.IgnoreCase)]
    private static partial Regex InputListRegex();

    [GeneratedRegex(@"\b(A\s+CONTINUACI[OÓ]N|POSTERIORMENTE|UNA\s+VEZ\s+QUE)\b", RegexOptions.IgnoreCase)]
    private static partial Regex SequencingRegex();

    [GeneratedRegex(@"\b(PORCIONES|TIEMPO\s+DE\s+(PREPARACI[OÓ]N|COCCI[OÓ]N|EJECUCI[OÓ]N))\b", RegexOptions.IgnoreCase)]
    private static partial Regex YieldRegex();
}
