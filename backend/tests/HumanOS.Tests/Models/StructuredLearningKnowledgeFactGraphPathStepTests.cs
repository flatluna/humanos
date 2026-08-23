using FluentAssertions;
using HumanOS.Data;
using HumanOS.Models.Capabilities.Studio;
using Microsoft.Data.SqlTypes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace HumanOS.Tests.Models;

public sealed class StructuredLearningKnowledgeFactGraphPathStepTests
{
    private static DbContextOptions<HumanOsDbContext> NewOptions() =>
        new DbContextOptionsBuilder<HumanOsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

    private static (StructuredLearningDocument Document, StructuredLearningDocumentSection Engram, StructuredLearningKnowledgeFact Fact) CreateFactGraph()
    {
        var document = new StructuredLearningDocument
        {
            StructuredLearningDocumentId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            SessionId = Guid.NewGuid(),
            DocumentName = "Path test",
            CreatedDate = DateTime.UtcNow
        };
        var engram = new StructuredLearningDocumentSection
        {
            StructuredLearningDocumentSectionId = Guid.NewGuid(),
            StructuredLearningDocumentId = document.StructuredLearningDocumentId,
            TenantId = document.TenantId,
            SortOrder = 1,
            ChapterTitle = "Chapter",
            Capability = "Capability",
            SubchapterTitle = "Engram",
            Text = "Path text",
            TotalCharacters = 9,
            TotalTokens = 2,
            Embedding = new SqlVector<float>(new[] { 1f, 2f, 3f }),
            CreatedDate = DateTime.UtcNow,
            Document = document
        };
        document.Sections = [engram];
        var fact = new StructuredLearningKnowledgeFact
        {
            KnowledgeFactId = Guid.NewGuid(),
            EngramId = engram.StructuredLearningDocumentSectionId,
            Statement = "A fact follows a graph path.",
            FactType = "Relationship",
            Importance = "Core",
            DerivationType = "Composite",
            EpistemicStatus = "explicit",
            Confidence = 0.9,
            Language = "en",
            CreatedAt = DateTime.UtcNow,
            Engram = engram
        };
        return (document, engram, fact);
    }

    private static StructuredLearningConnection CreateConnection(Guid documentId, Guid source, Guid target) => new()
    {
        StructuredLearningConnectionId = Guid.NewGuid(),
        StructuredLearningDocumentId = documentId,
        TenantId = Guid.NewGuid(),
        SourceEntityId = source,
        TargetEntityId = target,
        RelationshipLabel = "supports",
        RelationshipCategory = "Supports",
        EpistemicStatus = "explicit",
        CreatedDate = DateTime.UtcNow
    };

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task ValidStepOrders_Persist(int stepOrder)
    {
        var options = NewOptions();
        var (document, _, fact) = CreateFactGraph();
        var connection = CreateConnection(document.StructuredLearningDocumentId, Guid.NewGuid(), Guid.NewGuid());
        var step = new StructuredLearningKnowledgeFactGraphPathStep
        {
            KnowledgeFactGraphPathStepId = Guid.NewGuid(),
            KnowledgeFactId = fact.KnowledgeFactId,
            ConnectionId = connection.StructuredLearningConnectionId,
            StepOrder = stepOrder,
            CreatedAt = DateTime.UtcNow
        };

        await using var context = new HumanOsDbContext(options);
        context.StructuredLearningDocuments.Add(document);
        context.StructuredLearningKnowledgeFacts.Add(fact);
        context.StructuredLearningConnections.Add(connection);
        context.StructuredLearningKnowledgeFactGraphPathSteps.Add(step);
        await context.SaveChangesAsync();

        (await context.StructuredLearningKnowledgeFactGraphPathSteps.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task CompositePath_CanBeReadInExplicitStepOrder()
    {
        var options = NewOptions();
        var (document, _, fact) = CreateFactGraph();
        var connections = Enumerable.Range(1, 3)
            .Select(_ => CreateConnection(document.StructuredLearningDocumentId, Guid.NewGuid(), Guid.NewGuid()))
            .ToList();
        var steps = connections.Select((connection, index) => new StructuredLearningKnowledgeFactGraphPathStep
        {
            KnowledgeFactGraphPathStepId = Guid.NewGuid(),
            KnowledgeFactId = fact.KnowledgeFactId,
            ConnectionId = connection.StructuredLearningConnectionId,
            StepOrder = 3 - index,
            CreatedAt = DateTime.UtcNow
        }).ToList();

        await using var context = new HumanOsDbContext(options);
        context.StructuredLearningDocuments.Add(document);
        context.StructuredLearningKnowledgeFacts.Add(fact);
        context.StructuredLearningConnections.AddRange(connections);
        context.StructuredLearningKnowledgeFactGraphPathSteps.AddRange(steps);
        await context.SaveChangesAsync();

        var ordered = await context.StructuredLearningKnowledgeFactGraphPathSteps
            .Where(step => step.KnowledgeFactId == fact.KnowledgeFactId)
            .OrderBy(step => step.StepOrder)
            .Select(step => step.StepOrder)
            .ToListAsync();
        ordered.Should().Equal(1, 2, 3);
    }

    [Fact]
    public void PathStep_ModelDeclaresIntegrityConstraintsAndRelationships()
    {
        using var context = new HumanOsDbContext(NewOptions());
        var entity = context.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(StructuredLearningKnowledgeFactGraphPathStep))!;
        entity.GetCheckConstraints().Single().Sql.Should().Contain("[StepOrder] >= 1").And.Contain("[StepOrder] <= 3");
        entity.GetIndexes().Should().Contain(index => index.IsUnique && index.Properties.Select(p => p.Name).SequenceEqual(new[] { "KnowledgeFactId", "StepOrder" }));
        entity.GetIndexes().Should().Contain(index => index.IsUnique && index.Properties.Select(p => p.Name).SequenceEqual(new[] { "KnowledgeFactId", "ConnectionId" }));
        entity.GetIndexes().Should().Contain(index => index.Properties.Select(p => p.Name).SequenceEqual(new[] { "ConnectionId" }));
    }

    [Fact]
    public void PathStep_ForeignKeysUseFactCascadeAndConnectionRestrict()
    {
        using var context = new HumanOsDbContext(NewOptions());
        var entity = context.Model.FindEntityType(typeof(StructuredLearningKnowledgeFactGraphPathStep))!;
        var factForeignKey = entity.GetForeignKeys().Single(fk => fk.PrincipalEntityType.ClrType == typeof(StructuredLearningKnowledgeFact));
        var connectionForeignKey = entity.GetForeignKeys().Single(fk => fk.PrincipalEntityType.ClrType == typeof(StructuredLearningConnection));
        factForeignKey.DeleteBehavior.Should().Be(DeleteBehavior.Cascade);
        connectionForeignKey.DeleteBehavior.Should().Be(DeleteBehavior.Restrict);
    }
}
