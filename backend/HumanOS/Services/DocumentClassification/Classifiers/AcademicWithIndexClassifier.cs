using System.Text.RegularExpressions;

namespace HumanOS.Services.DocumentClassification.Classifiers;

/// <summary>Textbook/course with a real, numbered table of contents
/// (Capítulo/Unidad/Tema/Lección) — already well handled downstream by
/// SyllabusOutlineParser's Tier 0 detection; this classifier only needs to
/// confirm the numbered-index signal is actually present.</summary>
public sealed partial class AcademicWithIndexClassifier : KeywordPatternClassifier
{
    public override DocumentType Type => DocumentType.AcademicWithIndex;

    protected override IReadOnlyList<ClassificationRule> Rules { get; } =
    [
        new("Numbered CAPITULO/UNIDAD marker", ChapterUnitRegex(), 3.0),
        new("Numbered TEMA/LECCION marker", TopicLessonRegex(), 2.0),
        new("Exercises/summary section headings", ExerciseSummaryRegex(), 1.5),
        new("Table-of-contents heading (INDICE/CONTENIDO)", TocHeadingRegex(), 2.5),
        new("Learning-objective language (OBJETIVOS DE APRENDIZAJE)", ObjectivesRegex(), 1.5),
    ];

    [GeneratedRegex(@"\b(CAP[IÍ]TULO|UNIDAD)\s+[IVXLCDM\d]+", RegexOptions.IgnoreCase)]
    private static partial Regex ChapterUnitRegex();

    [GeneratedRegex(@"\b(TEMA|LECCI[OÓ]N)\s+[IVXLCDM\d]+", RegexOptions.IgnoreCase)]
    private static partial Regex TopicLessonRegex();

    [GeneratedRegex(@"\b(EJERCICIOS|RESUMEN\s+DEL\s+CAP[IÍ]TULO|AUTOEVALUACI[OÓ]N)\b", RegexOptions.IgnoreCase)]
    private static partial Regex ExerciseSummaryRegex();

    [GeneratedRegex(@"\b([IÍ]NDICE(\s+GENERAL)?|TABLA\s+DE\s+CONTENIDO)\b", RegexOptions.IgnoreCase)]
    private static partial Regex TocHeadingRegex();

    [GeneratedRegex(@"\bOBJETIVOS?\s+DE\s+APRENDIZAJE\b", RegexOptions.IgnoreCase)]
    private static partial Regex ObjectivesRegex();
}
