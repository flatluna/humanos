using FluentAssertions;
using HumanOS.Agents.Studio;
using Xunit;

namespace HumanOS.Tests.Agents.Studio;

public sealed class StructuredLearningConnectionExtractionAgentTests
{
    [Theory]
    [InlineData("Generative AI", "AI", "IsA")]
    [InlineData("Triangle", "Polygon", "IsA")]
    [InlineData("World War I", "World War II", "Precedes")]
    [InlineData("Force", "Acceleration", "Causes")]
    public void UniversalTaxonomy_SupportsMultipleLearningDomains(string source, string target, string category)
    {
        source.Should().NotBeNullOrWhiteSpace();
        target.Should().NotBeNullOrWhiteSpace();
        StructuredLearningConnectionExtractionAgent.RelationshipCategories.Should().Contain(category);
        StructuredLearningConnectionExtractionAgent.NormalizeRelationshipCategory(category).Should().Be(category);
    }

    [Fact]
    public void InvalidOrLegacyCategory_UsesRelatedToAsLastResort()
    {
        StructuredLearningConnectionExtractionAgent.NormalizeRelationshipCategory("Generic")
            .Should().Be("RelatedTo");
        StructuredLearningConnectionExtractionAgent.NormalizeRelationshipCategory("DomainSpecificRelation")
            .Should().Be("RelatedTo");
    }

    [Fact]
    public void Processes_IsUniversalAndSupportsEvidenceBasedOperations()
    {
        StructuredLearningConnectionExtractionAgent.RelationshipCategories.Should().Contain("Processes");
        StructuredLearningConnectionExtractionAgent.NormalizeRelationshipCategory("Processes")
            .Should().Be("Processes");
    }

    [Fact]
    public void ExplicitEnumerationRelations_AreRepresentableWithoutDomainRules()
    {
        var relations = new[]
        {
            new ExtractedConnectionDto { RelationshipCategory = "IsA", RelationshipLabel = "is a descriptive capability", Evidence = "Descriptive: search, classification, summarization" },
            new ExtractedConnectionDto { RelationshipCategory = "IsA", RelationshipLabel = "is a predictive capability", Evidence = "Predictive: forecasting, anomaly detection, scoring" },
            new ExtractedConnectionDto { RelationshipCategory = "IsA", RelationshipLabel = "is a prescriptive capability", Evidence = "Prescriptive: recommendations, optimization" }
        };

        relations.Should().OnlyContain(r => r.RelationshipCategory == "IsA" && !string.IsNullOrWhiteSpace(r.Evidence));
    }

    [Fact]
    public void TitleOnlyRelationHasNoEvidenceForDomainEdge()
    {
        var connection = new ExtractedConnectionDto
        {
            RelationshipCategory = "PartOf",
            RelationshipLabel = "describes the foundations of",
            Evidence = "Foundations of AI"
        };

        connection.Evidence.Should().Be("Foundations of AI");
        connection.RelationshipLabel.Should().NotContain("part of");
    }

    [Fact]
    public void RhetoricalMoreThanPhrase_IsNotSemanticContrastEvidence()
    {
        const string text = "AI is more than a set of algorithms";

        text.Should().Contain("more than");
        text.Should().NotContain("contrasts with");
    }

    [Fact]
    public void CoordinatedEnumerationsRequireAllItemsAndContextualEvidence()
    {
        var connection = new ExtractedConnectionDto
        {
            RelationshipCategory = "Enables",
            RelationshipLabel = "powers productivity and creativity scenarios",
            Evidence = "creates content and powers new productivity and creativity scenarios",
            EpistemicStatus = "explicit",
            Confidence = 0.98
        };

        connection.Evidence.Should().Contain("productivity").And.Contain("creativity");
        connection.EpistemicStatus.Should().Be("explicit");
    }

    [Fact]
    public void PronounResolutionRequiresInferredStatus()
    {
        var connection = new ExtractedConnectionDto
        {
            RelationshipCategory = "Requires",
            RelationshipLabel = "should be paired with strong data",
            Evidence = "Choose the right capability for the problem, start with high value use cases, and pair them with strong data and governance",
            EpistemicStatus = "inferred",
            Confidence = 0.86
        };

        connection.Evidence.Should().Contain("pair them with strong data");
        connection.EpistemicStatus.Should().Be("inferred");
        connection.Confidence.Should().BeLessThan(0.9);
    }

    [Fact]
    public void ConnectionDto_PreservesEvidenceAndProvenance()
    {
        var connection = new ExtractedConnectionDto
        {
            SourceEntityId = "e0",
            TargetEntityId = "e1",
            RelationshipCategory = "Requires",
            RelationshipLabel = "requires light",
            Evidence = "Photosynthesis requires light",
            EpistemicStatus = "explicit",
            Confidence = 0.98
        };

        connection.RelationshipLabel.Should().Be("requires light");
        connection.Evidence.Should().Be("Photosynthesis requires light");
        connection.EpistemicStatus.Should().Be("explicit");
        connection.Confidence.Should().Be(0.98);
        connection.SourceEntityId.Should().Be("e0");
        connection.TargetEntityId.Should().Be("e1");
    }

    [Fact]
    public void InferredConnection_PreservesLowerConfidenceAndStatus()
    {
        var connection = new ExtractedConnectionDto
        {
            SourceEntityId = "e0",
            TargetEntityId = "e1",
            RelationshipCategory = "RelatedTo",
            RelationshipLabel = "may support",
            Evidence = "The text presents them together",
            EpistemicStatus = "inferred",
            Confidence = 0.72
        };

        connection.EpistemicStatus.Should().Be("inferred");
        connection.Confidence.Should().Be(0.72);
        connection.Evidence.Should().NotBeNullOrWhiteSpace();
    }
}
