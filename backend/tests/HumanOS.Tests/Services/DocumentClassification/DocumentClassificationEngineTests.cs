using FluentAssertions;
using HumanOS.Services.DocumentClassification;
using HumanOS.Services.DocumentClassification.Classifiers;
using Xunit;

namespace HumanOS.Tests.Services.DocumentClassification;

public class DocumentClassificationEngineTests
{
    private static DocumentClassificationEngine BuildEngine() => new(
    [
        new ContractClassifier(),
        new LawClassifier(),
        new PolicyClassifier(),
        new ProcedureClassifier(),
        new AcademicWithIndexClassifier(),
        new AcademicNarrativeClassifier(),
        new ResearchPaperClassifier()
    ]);

    [Fact]
    public void Classify_DetectsLaw()
    {
        const string text = """
            LEY DEL IMPUESTO AL VALOR AGREGADO

            TITULO I
            Disposiciones Generales

            CAPITULO I
            De las Personas Obligadas

            ARTICULO 1o. Estan obligadas al pago del impuesto establecido en esta Ley
            las personas fisicas y morales que en territorio nacional realicen actos.

            ARTICULO 2o. Para los efectos de esta Ley se considera:
            FRACCION I. Enajenacion de bienes.
            FRACCION II. Prestacion de servicios independientes.

            EL CONGRESO DECRETA la presente Ley.
            """;

        var result = BuildEngine().Classify(text);

        result.PrimaryType.Should().Be(HumanOS.Services.DocumentClassification.DocumentType.Law);
        result.Confidence.Should().BeGreaterThan(DocumentClassificationEngine.UnknownThreshold);
    }

    [Fact]
    public void Classify_DetectsContract()
    {
        const string text = """
            CONTRATO DE PRESTACION DE SERVICIOS QUE CELEBRAN, por una parte
            Empresa ABC, en lo sucesivo se le denominara "EL PRESTADOR", y por otra
            parte Juan Perez, en lo sucesivo se le denominara "EL CLIENTE".

            CLAUSULA PRIMERA. Objeto del contrato. EL PRESTADOR SE OBLIGA A entregar
            los servicios descritos en el Anexo A.

            CLAUSULA SEGUNDA. Obligaciones de las partes. EL CLIENTE TENDRA DERECHO A
            solicitar reportes mensuales.

            CLAUSULA TERCERA. Rescision y jurisdiccion competente.
            """;

        var result = BuildEngine().Classify(text);

        result.PrimaryType.Should().Be(HumanOS.Services.DocumentClassification.DocumentType.Contract);
    }

    [Fact]
    public void Classify_DetectsPolicy()
    {
        const string text = """
            POLITICA DE PREVENCION DEL ACOSO SEXUAL

            Alcance de la politica: aplica a todo el personal de la organizacion.

            Se considera acoso sexual cualquier conducta de naturaleza sexual no
            deseada. Esta prohibido cualquier comportamiento que genere un ambiente
            hostil.

            Todo empleado debe reportar incidentes a traves del canal de denuncia
            o la linea de etica. El incumplimiento de esta politica dara lugar a
            sanciones y medidas disciplinarias.
            """;

        var result = BuildEngine().Classify(text);

        result.PrimaryType.Should().Be(HumanOS.Services.DocumentClassification.DocumentType.Policy);
    }

    [Fact]
    public void Classify_DetectsProcedure_ForRecipe()
    {
        const string text = """
            RECETA: TACOS DE CANASTA

            INGREDIENTES
            - 1 kg de tortillas
            - 500g de papa

            Tiempo de preparacion: 40 minutos. Porciones: 4.

            PASO 1. Cocer las papas hasta que esten suaves.
            PASO 2. A continuacion, machacar las papas y sazonar.
            PASO 3. Una vez que la mezcla este lista, rellenar las tortillas.
            PASO 4. Posteriormente, calentar los tacos al vapor.
            """;

        var result = BuildEngine().Classify(text);

        result.PrimaryType.Should().Be(HumanOS.Services.DocumentClassification.DocumentType.Procedure);
    }

    [Fact]
    public void Classify_DetectsAcademicWithIndex()
    {
        const string text = """
            INDICE GENERAL

            UNIDAD I. Introduccion a la Contabilidad
            CAPITULO 1. La partida doble
            TEMA 1. Activos y pasivos
            LECCION 1. El balance general

            OBJETIVOS DE APRENDIZAJE: el estudiante comprendera los principios basicos.

            EJERCICIOS
            1. Defina activo y pasivo.

            RESUMEN DEL CAPITULO: se revisaron los conceptos fundamentales.
            """;

        var result = BuildEngine().Classify(text);

        result.PrimaryType.Should().Be(HumanOS.Services.DocumentClassification.DocumentType.AcademicWithIndex);
    }

    [Fact]
    public void Classify_DetectsResearchPaper()
    {
        const string text = """
            Abstract

            This paper presents a novel approach to distributed systems.

            Introduction

            Prior work (Smith et al., 2021) has explored similar ideas.

            Methodology

            We conducted experiments using a controlled dataset.

            Results

            Our approach outperformed the baseline by 12%.

            Discussion

            These results suggest a strong correlation.

            References

            Smith, J. (2021). Distributed Systems Review.
            """;

        var result = BuildEngine().Classify(text);

        result.PrimaryType.Should().Be(HumanOS.Services.DocumentClassification.DocumentType.ResearchPaper);
    }

    [Fact]
    public void Classify_ReturnsUnknown_ForAmbiguousShortText()
    {
        const string text = "Hola, este es un mensaje corto sin estructura clara.";

        var result = BuildEngine().Classify(text);

        result.PrimaryType.Should().Be(HumanOS.Services.DocumentClassification.DocumentType.Unknown);
        result.Confidence.Should().Be(0);
    }

    [Fact]
    public void Classify_ReturnsUnknown_ForEmptyText()
    {
        var result = BuildEngine().Classify(string.Empty);

        result.PrimaryType.Should().Be(HumanOS.Services.DocumentClassification.DocumentType.Unknown);
        result.Candidates.Should().OnlyContain(candidate => candidate.Score == 0);
    }

    [Fact]
    public void Classify_ExposesEvidenceForWinningCandidate()
    {
        const string text = "ARTICULO 1o. ARTICULO 2o. ARTICULO 3o. CAPITULO I. TITULO I.";

        var result = BuildEngine().Classify(text);

        var winner = result.Candidates.First(c => c.Type == result.PrimaryType);
        winner.Evidence.Should().NotBeEmpty();
        winner.Evidence.Should().OnlyContain(e => !string.IsNullOrWhiteSpace(e.MatchedText));
    }

    [Fact]
    public void Classify_CandidatesAreSortedDescendingByScore()
    {
        const string text = "ARTICULO 1o. CAPITULO I. FRACCION I.";

        var result = BuildEngine().Classify(text);

        result.Candidates.Should().BeInDescendingOrder(candidate => candidate.Score);
    }
}
