using FluentAssertions;
using HumanOS.Agents.Studio;
using HumanOS.Contracts.Studio;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace HumanOS.Tests.Agents.Studio;

public sealed class StructuredLearningKnowledgeFactGenerationAgentTests
{
    private static StructuredLearningKnowledgeFactGenerationRequest CreateRequest(int connectionCount = 3)
    {
        var entities = Enumerable.Range(0, connectionCount + 1)
            .Select(_ => Guid.NewGuid())
            .ToList();
        var connections = Enumerable.Range(0, connectionCount)
            .Select(index => new StructuredLearningKnowledgeFactConnectionInput
            {
                ConnectionId = Guid.NewGuid(),
                SourceEntityId = entities[index],
                SourceEntityName = $"Entity {index}",
                TargetEntityId = entities[index + 1],
                TargetEntityName = $"Entity {index + 1}",
                RelationshipCategory = "Enables",
                RelationshipLabel = "enables",
                Evidence = $"Entity {index} enables Entity {index + 1}.",
                EpistemicStatus = "explicit",
                Confidence = 0.95
            }).ToList();
        return new StructuredLearningKnowledgeFactGenerationRequest
        {
            EngramId = Guid.NewGuid(),
            EngramName = "Test Engram",
            Language = "en",
            SourceText = "Entity 0 enables Entity 1 and Entity 1 enables Entity 2.",
            Entities = entities.Select((id, index) => new StructuredLearningKnowledgeFactEntityInput
            {
                EntityId = id,
                CanonicalName = $"Entity {index}",
                EntityType = "Concept"
            }).ToList(),
            Connections = connections
        };
    }

    private sealed class FakeKnowledgeFactAgent(StructuredLearningKnowledgeFactGenerationResponse response)
        : StructuredLearningKnowledgeFactGenerationAgent(new ConfigurationBuilder().Build())
    {
        protected override Task<StructuredLearningKnowledgeFactGenerationResponse> RunModelAsync(
            StructuredLearningKnowledgeFactGenerationRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(response);
    }

    private static StructuredLearningGeneratedKnowledgeFact CreateFact(
        StructuredLearningKnowledgeFactGenerationRequest request, int pathLength, string derivationType = "Composite")
    {
        var path = request.Connections.Take(pathLength).Select((connection, index) =>
            new StructuredLearningKnowledgeFactGraphPathStepOutput { ConnectionId = connection.ConnectionId, StepOrder = index + 1 }).ToList();
        return new StructuredLearningGeneratedKnowledgeFact
        {
            Statement = $"A valid {derivationType} fact with {pathLength} steps.",
            FactType = "Relationship",
            Importance = "Core",
            DerivationType = derivationType,
            SourceEntityIds = request.Entities.Take(pathLength + 1).Select(entity => entity.EntityId).ToList(),
            SourceConnectionIds = path.Select(step => step.ConnectionId).ToList(),
            GraphPath = path,
            Evidence = [request.SourceText],
            EpistemicStatus = "explicit",
            Confidence = 0.9
        };
    }

    [Fact]
    public async Task GenerateAsync_AcceptsRequestAndReturnsStructuredResponse()
    {
        var request = CreateRequest(1);
        var response = new StructuredLearningKnowledgeFactGenerationResponse
        {
            EngramId = request.EngramId,
            EngramName = request.EngramName,
            Language = request.Language,
            KnowledgeFacts = [CreateFact(request, 1, "Atomic")]
        };

        var result = await new FakeKnowledgeFactAgent(response).GenerateAsync(request, CancellationToken.None);

        result.Should().BeSameAs(response);
        result.KnowledgeFacts.Should().ContainSingle();
    }

    [Fact]
    public async Task GenerateAsync_AcceptsAtomicFactWithOneValidStep()
    {
        var request = CreateRequest(1);
        var response = new StructuredLearningKnowledgeFactGenerationResponse
        {
            EngramId = request.EngramId, EngramName = request.EngramName, Language = request.Language,
            KnowledgeFacts = [CreateFact(request, 1, "Atomic")]
        };

        var result = await new FakeKnowledgeFactAgent(response).GenerateAsync(request, CancellationToken.None);
        result.KnowledgeFacts.Single().GraphPath.Single().StepOrder.Should().Be(1);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public async Task GenerateAsync_AcceptsContinuousCompositePath(int pathLength)
    {
        var request = CreateRequest(pathLength);
        var response = new StructuredLearningKnowledgeFactGenerationResponse
        {
            EngramId = request.EngramId, EngramName = request.EngramName, Language = request.Language,
            KnowledgeFacts = [CreateFact(request, pathLength)]
        };

        var result = await new FakeKnowledgeFactAgent(response).GenerateAsync(request, CancellationToken.None);
        result.KnowledgeFacts.Single().GraphPath.Select(step => step.StepOrder).Should().Equal(Enumerable.Range(1, pathLength));
    }

    [Fact]
    public async Task GenerateAsync_NormalizesEvidenceWhitespaceWithoutChangingContent()
    {
        var request = CreateRequest(1);
        var fact = CreateFact(request, 1, "Atomic");
        fact.Evidence = ["  Entity 0\r\n enables\tEntity 1.  "];
        var response = new StructuredLearningKnowledgeFactGenerationResponse
        {
            EngramId = request.EngramId, EngramName = request.EngramName, Language = request.Language,
            KnowledgeFacts = [fact]
        };

        var result = await new FakeKnowledgeFactAgent(response).GenerateAsync(request, CancellationToken.None);

        result.KnowledgeFacts.Single().Evidence.Should().Equal("Entity 0 enables Entity 1.");
    }

    [Fact]
    public async Task GenerateAsync_RejectsCompositeWhenPathIsNotContinuous()
    {
        var request = CreateRequest(2);
        var fact = CreateFact(request, 2);
        request.Connections[1].SourceEntityId = Guid.NewGuid();
        var response = new StructuredLearningKnowledgeFactGenerationResponse
        {
            EngramId = request.EngramId, EngramName = request.EngramName, Language = request.Language,
            KnowledgeFacts = [fact]
        };

        var action = () => new FakeKnowledgeFactAgent(response).GenerateAsync(request, CancellationToken.None);
        await action.Should().ThrowAsync<InvalidOperationException>().WithMessage("*discontinuous GraphPath*");
    }

    [Fact]
    public async Task GenerateAsync_RejectsUnknownConnectionId()
    {
        var request = CreateRequest(1);
        var fact = CreateFact(request, 1, "Atomic");
        fact.GraphPath[0].ConnectionId = Guid.NewGuid();
        fact.SourceConnectionIds[0] = fact.GraphPath[0].ConnectionId;
        var response = new StructuredLearningKnowledgeFactGenerationResponse
        {
            EngramId = request.EngramId, EngramName = request.EngramName, Language = request.Language,
            KnowledgeFacts = [fact]
        };

        var action = () => new FakeKnowledgeFactAgent(response).GenerateAsync(request, CancellationToken.None);
        await action.Should().ThrowAsync<InvalidOperationException>().WithMessage("*unknown ConnectionId*");
    }

    [Fact]
    public async Task GenerateAsync_RejectsDiscontinuousCompositePathAndDuplicateStatements()
    {
        var request = CreateRequest(2);
        var fact = CreateFact(request, 2);
        var secondConnection = request.Connections[1];
        secondConnection.SourceEntityId = Guid.NewGuid();
        var response = new StructuredLearningKnowledgeFactGenerationResponse
        {
            EngramId = request.EngramId, EngramName = request.EngramName, Language = request.Language,
            KnowledgeFacts = [fact]
        };

        var action = () => new FakeKnowledgeFactAgent(response).GenerateAsync(request, CancellationToken.None);
        await action.Should().ThrowAsync<InvalidOperationException>().WithMessage("*discontinuous GraphPath*");
    }

    [Fact]
    public async Task GenerateAsync_RejectsInvalidAtomicCardinalityAndInvalidEntityId()
    {
        var request = CreateRequest(2);
        var fact = CreateFact(request, 2, "Atomic");
        fact.SourceEntityIds.Add(Guid.NewGuid());
        var response = new StructuredLearningKnowledgeFactGenerationResponse
        {
            EngramId = request.EngramId, EngramName = request.EngramName, Language = request.Language,
            KnowledgeFacts = [fact]
        };

        var action = () => new FakeKnowledgeFactAgent(response).GenerateAsync(request, CancellationToken.None);
        await action.Should().ThrowAsync<InvalidOperationException>().WithMessage("*invalid or duplicate EntityIds*");
    }

    [Fact]
    public async Task GenerateAsync_RejectsDuplicateStatementsAndOutOfRangeConfidence()
    {
        var request = CreateRequest(1);
        var first = CreateFact(request, 1, "Atomic");
        var second = CreateFact(request, 1, "Atomic");
        second.Statement = first.Statement.ToUpperInvariant();
        var response = new StructuredLearningKnowledgeFactGenerationResponse
        {
            EngramId = request.EngramId, EngramName = request.EngramName, Language = request.Language,
            KnowledgeFacts = [first, second]
        };

        var action = () => new FakeKnowledgeFactAgent(response).GenerateAsync(request, CancellationToken.None);
        await action.Should().ThrowAsync<InvalidOperationException>().WithMessage("*unique, non-empty Statements*");
    }

    [Fact]
    public async Task GenerateAsync_PreservesLanguageEvidenceAndMissingGraphFacts()
    {
        var request = CreateRequest(1);
        request.Language = "es";
        var response = new StructuredLearningKnowledgeFactGenerationResponse
        {
            EngramId = request.EngramId,
            EngramName = request.EngramName,
            Language = request.Language,
            KnowledgeFacts = [CreateFact(request, 1, "Atomic")],
            MissingGraphFacts = [new StructuredLearningMissingGraphFact
            {
                Statement = "La idea no tiene soporte suficiente.",
                Reason = "No existe una conexión válida.",
                EpistemicStatus = "explicit",
                Confidence = 0.8
            }]
        };

        var result = await new FakeKnowledgeFactAgent(response).GenerateAsync(request, CancellationToken.None);
        result.Language.Should().Be("es");
        result.KnowledgeFacts.Single().Evidence.Should().Contain(request.SourceText);
        result.MissingGraphFacts.Should().ContainSingle();
    }
}
