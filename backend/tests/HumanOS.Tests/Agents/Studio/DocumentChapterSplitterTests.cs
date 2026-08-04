using FluentAssertions;
using HumanOS.Agents.Studio;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace HumanOS.Tests.Agents.Studio;

/// <summary>
/// Tests for <see cref="DocumentChapterSplitter"/>'s 3-tier structure
/// detection (2026-08-03): Tier 0 (<see cref="SyllabusOutlineParser"/>,
/// deterministic) → Tier 1 (<see cref="TocExtractionAgent"/>, LLM) → Tier 2
/// (hard reject in "Estructurado"/strict mode). Uses an UNCONFIGURED
/// <see cref="TocExtractionAgent"/> (empty <see cref="IConfiguration"/>, so
/// <see cref="TocExtractionAgent.IsConfigured"/> is false and no real LLM
/// call is ever attempted) — these tests only exercise Tier 0 + Tier 2,
/// which is exactly what's deterministic/testable without a live model.
/// </summary>
public class DocumentChapterSplitterTests
{
    private static TocExtractionAgent UnconfiguredTocExtractionAgent()
    {
        IConfiguration configuration = new ConfigurationBuilder().Build();
        return new TocExtractionAgent(configuration);
    }

    [Fact]
    public async Task SplitIntoChapterMaterialsAsync_Tier0ConfidentStructure_SplitsOneItemPerModule()
    {
        const string text = """
            ### Módulo 1: Introducción
            - 1.1. Primer tema
            - 1.2. Segundo tema

            ### Módulo 2: Avanzado
            - 2.1. Tercer tema
            - 2.2. Cuarto tema
            """;

        var source = new RawMaterialItem
        {
            Type = RawMaterialType.UserNote,
            Label = "Curso de prueba",
            Content = text
        };

        var result = await DocumentChapterSplitter.SplitIntoChapterMaterialsAsync(
            source, UnconfiguredTocExtractionAgent(), strictSyllabusMode: false);

        result.Should().HaveCount(2);
        result[0].ChapterTitle.Should().Be("Módulo 1: Introducción");
        result[0].Content.Should().Contain("1.1. Primer tema").And.NotContain("Módulo 2");
        result[1].ChapterTitle.Should().Be("Módulo 2: Avanzado");
        result[1].Content.Should().Contain("2.1. Tercer tema");
    }

    [Fact]
    public async Task SplitIntoChapterMaterialsAsync_Tier0Fails_StrictMode_ThrowsSyllabusStructureNotFoundException()
    {
        const string unstructuredText = """
            Este documento no tiene ningún índice numerado. Es simplemente
            un artículo corrido sin estructura de módulos o unidades, y su
            longitud es corta.
            """;

        var source = new RawMaterialItem
        {
            Type = RawMaterialType.UserNote,
            Label = "Documento libre",
            Content = unstructuredText
        };

        var act = () => DocumentChapterSplitter.SplitIntoChapterMaterialsAsync(
            source, UnconfiguredTocExtractionAgent(), strictSyllabusMode: true);

        var exception = await act.Should().ThrowAsync<SyllabusStructureNotFoundException>();
        exception.Which.Message.Should().Contain("Estructurado");
    }

    [Fact]
    public async Task SplitIntoChapterMaterialsAsync_Tier0Fails_NonStrictMode_FallsBackToSingleItem_NoException()
    {
        const string unstructuredText = """
            Este documento no tiene ningún índice numerado. Es simplemente
            un artículo corrido sin estructura de módulos o unidades, y su
            longitud es corta.
            """;

        var source = new RawMaterialItem
        {
            Type = RawMaterialType.UserNote,
            Label = "Documento libre",
            Content = unstructuredText
        };

        var result = await DocumentChapterSplitter.SplitIntoChapterMaterialsAsync(
            source, UnconfiguredTocExtractionAgent(), strictSyllabusMode: false);

        result.Should().HaveCount(1);
        result[0].Content.Should().Be(unstructuredText);
    }

    [Fact]
    public async Task SplitIntoChapterMaterialsAsync_Tier0FlatStructureNoSubsections_StillSplitsPerModule()
    {
        const string text = """
            Unidad 1: Vocales
            El contenido de la primera unidad va aquí, sin subsecciones numeradas.

            Unidad 2: Consonantes
            El contenido de la segunda unidad va aquí también.

            Unidad 3: Sílabas
            El contenido de la tercera unidad.
            """;

        var source = new RawMaterialItem
        {
            Type = RawMaterialType.UserNote,
            Label = "Curso de fonética",
            Content = text
        };

        var result = await DocumentChapterSplitter.SplitIntoChapterMaterialsAsync(
            source, UnconfiguredTocExtractionAgent(), strictSyllabusMode: true);

        result.Should().HaveCount(3);
        result.Select(r => r.ChapterTitle).Should().Equal(
            "Unidad 1: Vocales", "Unidad 2: Consonantes", "Unidad 3: Sílabas");
    }
}
