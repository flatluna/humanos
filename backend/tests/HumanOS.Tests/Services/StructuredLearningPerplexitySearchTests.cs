using System.Text.Json;
using FluentAssertions;
using HumanOS.Agents.Studio;
using HumanOS.Models.Capabilities.Studio;
using HumanOS.Services;
using Xunit;

namespace HumanOS.Tests.Services;

public class StructuredLearningPerplexitySearchTests
{
    [Fact]
    public void BuildStepDataJson_IncludesWebSearchCitations_WhenTeachingStepHasSources()
    {
        var stepDto = new StructuredLearningSectionStepDto
        {
            StepType = StructuredLearningStepType.Teaching,
            Content = "Core concepts about neural networks.",
            WebSearchCitations = new List<string>
            {
                "https://en.wikipedia.org/wiki/Artificial_neural_network",
                "https://arxiv.org/abs/2301.00000"
            }
        };

        var json = StructuredLearningPedagogyService.BuildStepDataJson(stepDto);

        json.Should().NotBeNull();
        using var doc = JsonDocument.Parse(json!);
        doc.RootElement.TryGetProperty("WebSearchCitations", out var citationsProp).Should().BeTrue();
        citationsProp.GetArrayLength().Should().Be(2);
        citationsProp[0].GetString().Should().Be("https://en.wikipedia.org/wiki/Artificial_neural_network");
        citationsProp[1].GetString().Should().Be("https://arxiv.org/abs/2301.00000");
    }

    [Fact]
    public void SectionMetadata_WithPerplexitySources_SerializesExpectedShape()
    {
        var citations = new List<string> { "https://perplexity.ai/source1", "https://perplexity.ai/source2" };
        var sectionConfigSnapshot = JsonSerializer.Serialize(new
        {
            processVersion = "structured-learning-pedagogy-v1",
            promptVersion = "2026-08-11-v1-source-faithful-professor",
            modelName = "gpt-4o",
            usePerplexitySearch = true,
            hasApiData = citations.Count > 0,
            webSearchSources = citations,
            webSearchService = "Perplexity AI",
            webSearchSnippet = "Verified findings from Perplexity."
        });

        using var doc = JsonDocument.Parse(sectionConfigSnapshot);
        doc.RootElement.GetProperty("usePerplexitySearch").GetBoolean().Should().BeTrue();
        doc.RootElement.GetProperty("hasApiData").GetBoolean().Should().BeTrue();
        doc.RootElement.GetProperty("webSearchService").GetString().Should().Be("Perplexity AI");
        doc.RootElement.GetProperty("webSearchSources").GetArrayLength().Should().Be(2);
    }

    [Fact]
    public void CreationOptions_IncludesUsePerplexitySearchFlag()
    {
        var options = new StructuredLearningCreationOptions
        {
            LanguageCode = "es",
            UsePerplexitySearch = true
        };

        options.UsePerplexitySearch.Should().BeTrue();
    }
}
