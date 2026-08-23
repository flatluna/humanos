namespace HumanOS.Services.DocumentClassification;

/// <summary>
/// The structural/rhetorical shape of a source document — deliberately NOT a
/// content domain (Math/History/Finanzas/Cocina keep living in Subject/Topic,
/// see StructuredLearningDocument.SubjectId/TopicId). Drives which chapter/engram
/// extraction strategy and which Hypothesis/Learn/Recall/Assessment style a
/// document should use downstream. Forced single-value by design (2026-08-17
/// decision): a document gets exactly one DocumentType, never several at once,
/// so exactly one extraction strategy always applies unambiguously.
/// </summary>
public enum DocumentType
{
    /// <summary>No candidate classifier scored high enough confidence — the
    /// generic/current pipeline behavior applies, unchanged.</summary>
    Unknown,

    /// <summary>Contrato de servicios, NDA — bilateral obligations/rights
    /// between named parties, usually organized in numbered "Cláusulas".</summary>
    Contract,

    /// <summary>Ley, Reglamento, NOM — CAPITULO/TITULO/ARTICULO/FRACCION
    /// hierarchical structure, already well handled by SyllabusOutlineParser.</summary>
    Law,

    /// <summary>Internal governance rule statements without a party-to-party
    /// contract and without government issuance — e.g. a sexual harassment
    /// policy, a code of conduct, a data governance policy. Structurally:
    /// normative statements (prohibitions/definitions/reporting duties)
    /// without a sequential step order.</summary>
    Policy,

    /// <summary>Sequential ordered steps with inputs/outputs — SOPs, cooking
    /// recipes, mechanical repair manuals, business processes. Structurally
    /// identical regardless of domain; the domain (Cocina/Mecánica/Procesos
    /// de Negocio) is captured by Subject/Topic, not by this type.</summary>
    Procedure,

    /// <summary>A textbook/course with a real, numbered table of contents
    /// (Capítulo/Unidad/Tema/Lección) — already handled by SyllabusOutlineParser
    /// Tier 0, Chapter=Level/Subsection=Node mapping applies 1:1.</summary>
    AcademicWithIndex,

    /// <summary>Continuous narrative/expository prose with no explicit
    /// numbered index — history, geography, science, or technical material
    /// written as flowing text rather than a formal syllabus. Needs inferred
    /// structure (TocExtractionAgent.InferOutlineAsync) rather than regex.</summary>
    AcademicNarrative,

    /// <summary>Abstract/Introduction/Methodology/Results/Discussion/References
    /// — the standard IMRaD scientific paper structure.</summary>
    ResearchPaper
}
