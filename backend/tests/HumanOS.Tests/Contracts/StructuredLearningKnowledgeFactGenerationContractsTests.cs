using System.Text.Json;
using FluentAssertions;
using HumanOS.Contracts.Studio;
using Xunit;

namespace HumanOS.Tests.Contracts;

public sealed class StructuredLearningKnowledgeFactGenerationContractsTests
{
    private static JsonSerializerOptions JsonOptions => new(JsonSerializerDefaults.Web);

    [Fact]
    public void Request_RepresentsEngramEntitiesAndConnections()
    {
        var engramId = Guid.NewGuid();
        var entityId = Guid.NewGuid();
        var connectionId = Guid.NewGuid();
        var request = new StructuredLearningKnowledgeFactGenerationRequest
        {
            EngramId = engramId,
            EngramName = "AI foundations",
            Language = "en",
            SourceText = "Generative AI creates content.",
            Entities =
            [
                new StructuredLearningKnowledgeFactEntityInput
                {
                    EntityId = entityId,
                    CanonicalName = "Generative AI",
                    EntityType = "Technology",
                    Description = "A technology that generates content.",
                    Aliases = ["GenAI"]
                }
            ],
            Connections =
            [
                new StructuredLearningKnowledgeFactConnectionInput
                {
                    ConnectionId = connectionId,
                    SourceEntityId = entityId,
                    SourceEntityName = "Generative AI",
                    TargetEntityId = Guid.NewGuid(),
                    TargetEntityName = "Content",
                    RelationshipCategory = "Produces",
                    RelationshipLabel = "creates",
                    Evidence = "Generative AI creates content.",
                    EpistemicStatus = "explicit",
                    Confidence = 0.99
                }
            ]
        };

        request.EngramId.Should().Be(engramId);
        request.Entities.Should().ContainSingle().Which.EntityId.Should().Be(entityId);
        request.Connections.Should().ContainSingle().Which.ConnectionId.Should().Be(connectionId);
    }

    [Fact]
    public void Request_RoundTripsWithProjectJsonConfiguration()
    {
        var request = new StructuredLearningKnowledgeFactGenerationRequest
        {
            EngramId = Guid.NewGuid(),
            EngramName = "AI foundations",
            Language = "en",
            SourceText = "AI creates content.",
            Entities = [new StructuredLearningKnowledgeFactEntityInput { EntityId = Guid.NewGuid(), CanonicalName = "AI" }],
            Connections = [new StructuredLearningKnowledgeFactConnectionInput { ConnectionId = Guid.NewGuid(), RelationshipCategory = "Produces" }]
        };

        var json = JsonSerializer.Serialize(request, JsonOptions);
        var roundTrip = JsonSerializer.Deserialize<StructuredLearningKnowledgeFactGenerationRequest>(json, JsonOptions);

        roundTrip.Should().NotBeNull();
        roundTrip!.EngramId.Should().Be(request.EngramId);
        roundTrip.Entities.Should().HaveCount(1);
        roundTrip.Connections.Should().HaveCount(1);
        json.Should().Contain("engramId").And.Contain("sourceText");
    }

    [Fact]
    public void Response_PreservesFactsAndMissingGraphFacts()
    {
        var response = new StructuredLearningKnowledgeFactGenerationResponse
        {
            EngramId = Guid.NewGuid(),
            EngramName = "AI foundations",
            Language = "en",
            KnowledgeFacts = [new StructuredLearningGeneratedKnowledgeFact { Statement = "AI creates content.", Confidence = 0.9 }],
            MissingGraphFacts = [new StructuredLearningMissingGraphFact { Statement = "AI improves decisions.", Reason = "No supporting connection", Confidence = 0.6 }]
        };

        var roundTrip = JsonSerializer.Deserialize<StructuredLearningKnowledgeFactGenerationResponse>(
            JsonSerializer.Serialize(response, JsonOptions), JsonOptions);

        roundTrip!.EngramId.Should().Be(response.EngramId);
        roundTrip.KnowledgeFacts.Should().ContainSingle().Which.Statement.Should().Be("AI creates content.");
        roundTrip.MissingGraphFacts.Should().ContainSingle().Which.Reason.Should().Be("No supporting connection");
    }

    [Fact]
    public void AtomicFact_ContainsOneConnectionAndOneOrderedStep()
    {
        var connectionId = Guid.NewGuid();
        var fact = new StructuredLearningGeneratedKnowledgeFact
        {
            DerivationType = "Atomic",
            SourceConnectionIds = [connectionId],
            GraphPath = [new StructuredLearningKnowledgeFactGraphPathStepOutput { ConnectionId = connectionId, StepOrder = 1 }]
        };

        fact.DerivationType.Should().Be("Atomic");
        fact.SourceConnectionIds.Should().Equal(connectionId);
        fact.GraphPath.Should().ContainSingle().Which.StepOrder.Should().Be(1);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public void CompositeFact_PreservesOrderedTwoOrThreeStepPaths(int stepCount)
    {
        var connectionIds = Enumerable.Range(0, stepCount).Select(_ => Guid.NewGuid()).ToList();
        var fact = new StructuredLearningGeneratedKnowledgeFact
        {
            DerivationType = "Composite",
            SourceConnectionIds = connectionIds,
            GraphPath = connectionIds.Select((id, index) => new StructuredLearningKnowledgeFactGraphPathStepOutput { ConnectionId = id, StepOrder = index + 1 }).ToList()
        };

        fact.SourceConnectionIds.Should().Equal(connectionIds);
        fact.GraphPath.Select(step => step.StepOrder).Should().Equal(Enumerable.Range(1, stepCount));
        fact.GraphPath.Select(step => step.ConnectionId).Should().Equal(connectionIds);
    }

    [Fact]
    public void GraphPathStep_ContainsOnlyConnectionIdAndStepOrder()
    {
        var properties = typeof(StructuredLearningKnowledgeFactGraphPathStepOutput).GetProperties().Select(property => property.Name);

        properties.Should().BeEquivalentTo(["ConnectionId", "StepOrder"]);
    }

    [Fact]
    public void SourceEntityIds_EvidenceAndConfidenceArePreserved()
    {
        var entityIds = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        var fact = new StructuredLearningGeneratedKnowledgeFact
        {
            SourceEntityIds = entityIds.ToList(),
            Evidence = ["First supporting fragment.", "Second supporting fragment."],
            EpistemicStatus = "inferred",
            Confidence = 0.72
        };

        fact.SourceEntityIds.Should().Equal(entityIds);
        fact.Evidence.Should().HaveCount(2);
        fact.Confidence.Should().BeInRange(0, 1);
        fact.EpistemicStatus.Should().Be("inferred");
    }

    [Fact]
    public void MissingGraphFact_PreservesAllFields()
    {
        var missing = new StructuredLearningMissingGraphFact
        {
            Statement = "AI improves decisions.",
            Reason = "The graph has no supporting path.",
            EpistemicStatus = "inferred",
            Confidence = 0.61
        };

        missing.Statement.Should().NotBeNullOrWhiteSpace();
        missing.Reason.Should().Be("The graph has no supporting path.");
        missing.EpistemicStatus.Should().Be("inferred");
        missing.Confidence.Should().Be(0.61);
    }

    [Fact]
    public void Serialization_ExcludesPersistenceAndEntityOnlyProperties()
    {
        var fact = new StructuredLearningGeneratedKnowledgeFact
        {
            Statement = "A generated fact.",
            GraphPath = [new StructuredLearningKnowledgeFactGraphPathStepOutput { ConnectionId = Guid.NewGuid(), StepOrder = 1 }]
        };
        var json = JsonSerializer.Serialize(fact, JsonOptions);

        json.Should().NotContain("knowledgeFactId");
        json.Should().NotContain("embedding");
        json.Should().NotContain("tenantId");
        json.Should().NotContain("sourceEntityName");
        json.Should().NotContain("relationshipLabel");
        json.Should().NotContain("navigation");
    }

    [Fact]
    public void EmptyCollections_RoundTripAsEmptyCollections()
    {
        var request = new StructuredLearningKnowledgeFactGenerationRequest();
        var response = new StructuredLearningKnowledgeFactGenerationResponse();
        var fact = new StructuredLearningGeneratedKnowledgeFact();

        var requestRoundTrip = JsonSerializer.Deserialize<StructuredLearningKnowledgeFactGenerationRequest>(JsonSerializer.Serialize(request, JsonOptions), JsonOptions)!;
        var responseRoundTrip = JsonSerializer.Deserialize<StructuredLearningKnowledgeFactGenerationResponse>(JsonSerializer.Serialize(response, JsonOptions), JsonOptions)!;
        var factRoundTrip = JsonSerializer.Deserialize<StructuredLearningGeneratedKnowledgeFact>(JsonSerializer.Serialize(fact, JsonOptions), JsonOptions)!;

        requestRoundTrip.Entities.Should().BeEmpty();
        requestRoundTrip.Connections.Should().BeEmpty();
        responseRoundTrip.KnowledgeFacts.Should().BeEmpty();
        responseRoundTrip.MissingGraphFacts.Should().BeEmpty();
        factRoundTrip.SourceEntityIds.Should().BeEmpty();
        factRoundTrip.SourceConnectionIds.Should().BeEmpty();
        factRoundTrip.GraphPath.Should().BeEmpty();
        factRoundTrip.Evidence.Should().BeEmpty();
    }
}
