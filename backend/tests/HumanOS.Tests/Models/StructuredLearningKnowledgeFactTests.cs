using FluentAssertions;
using HumanOS.Data;
using HumanOS.Models.Capabilities.Studio;
using Microsoft.Data.SqlTypes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace HumanOS.Tests.Models;

public sealed class StructuredLearningKnowledgeFactTests
{
    private static DbContextOptions<HumanOsDbContext> NewOptions() =>
        new DbContextOptionsBuilder<HumanOsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

    private static StructuredLearningDocumentSection CreateEngram(Guid documentId, Guid tenantId) => new()
    {
        StructuredLearningDocumentSectionId = Guid.NewGuid(),
        StructuredLearningDocumentId = documentId,
        TenantId = tenantId,
        SortOrder = 1,
        ChapterTitle = "Chapter",
        Capability = "Capability",
        SubchapterTitle = "Engram",
        Text = "The text supports this fact.",
        TotalCharacters = 28,
        TotalTokens = 6,
        Embedding = new SqlVector<float>(new[] { 1f, 2f, 3f }),
        CreatedDate = DateTime.UtcNow
    };

    [Fact]
    public async Task ValidKnowledgeFact_PersistsAndBelongsToEngram()
    {
        var options = NewOptions();
        var document = new StructuredLearningDocument
        {
            StructuredLearningDocumentId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            SessionId = Guid.NewGuid(),
            DocumentName = "Test document",
            CreatedDate = DateTime.UtcNow
        };
        var engram = CreateEngram(document.StructuredLearningDocumentId, document.TenantId);
        document.Sections = [engram];
        var fact = new StructuredLearningKnowledgeFact
        {
            KnowledgeFactId = Guid.NewGuid(),
            EngramId = engram.StructuredLearningDocumentSectionId,
            Statement = "A capability helps solve a problem.",
            FactType = "Definition",
            Importance = "Core",
            DerivationType = "Atomic",
            EpistemicStatus = "explicit",
            Confidence = 0.95,
            Language = "en",
            CreatedAt = DateTime.UtcNow
        };

        await using (var context = new HumanOsDbContext(options))
        {
            context.StructuredLearningDocuments.Add(document);
            context.StructuredLearningDocumentSections.Add(engram);
            context.StructuredLearningKnowledgeFacts.Add(fact);
            await context.SaveChangesAsync();
        }

        await using var verify = new HumanOsDbContext(options);
        var saved = await verify.StructuredLearningKnowledgeFacts.SingleAsync();
        saved.EngramId.Should().Be(engram.StructuredLearningDocumentSectionId);
        saved.Statement.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void KnowledgeFact_ModelRequiresStatementAndConstrainsValues()
    {
        using var context = new HumanOsDbContext(NewOptions());
        var entityType = context.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(StructuredLearningKnowledgeFact))!;
        entityType.FindProperty(nameof(StructuredLearningKnowledgeFact.Statement))!.IsNullable.Should().BeFalse();
        entityType.FindProperty(nameof(StructuredLearningKnowledgeFact.EngramId))!.IsNullable.Should().BeFalse();
        entityType.GetCheckConstraints().Select(c => c.Name).Should().Contain([
            "CK_KnowledgeFacts_FactType",
            "CK_KnowledgeFacts_Importance",
            "CK_KnowledgeFacts_DerivationType",
            "CK_KnowledgeFacts_EpistemicStatus",
            "CK_KnowledgeFacts_Confidence",
            "CK_KnowledgeFacts_Statement_NotEmpty"
        ]);
    }

    [Fact]
    public void KnowledgeFact_ConfidenceRangeIsDeclaredInModel()
    {
        using var context = new HumanOsDbContext(NewOptions());
        var constraint = context.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(StructuredLearningKnowledgeFact))!
            .GetCheckConstraints()
            .Single(c => c.Name == "CK_KnowledgeFacts_Confidence");

        constraint.Sql.Should().Contain("[Confidence] >= 0").And.Contain("[Confidence] <= 1");
    }

    [Fact]
    public void KnowledgeFact_HasEngramIndexes()
    {
        using var context = new HumanOsDbContext(NewOptions());
        var indexes = context.Model.FindEntityType(typeof(StructuredLearningKnowledgeFact))!.GetIndexes();
        indexes.Should().Contain(i => i.Properties.Select(p => p.Name).SequenceEqual(new[] { nameof(StructuredLearningKnowledgeFact.EngramId) }));
        indexes.Should().Contain(i => i.Properties.Select(p => p.Name).SequenceEqual(new[] {
            nameof(StructuredLearningKnowledgeFact.EngramId), nameof(StructuredLearningKnowledgeFact.Importance) }));
    }

    [Fact]
    public async Task DeletingDocument_CascadesToEngramAndKnowledgeFact()
    {
        var options = NewOptions();
        var document = new StructuredLearningDocument
        {
            StructuredLearningDocumentId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            SessionId = Guid.NewGuid(),
            DocumentName = "Delete test",
            CreatedDate = DateTime.UtcNow
        };
        var engram = CreateEngram(document.StructuredLearningDocumentId, document.TenantId);
        document.Sections = [engram];
        var fact = new StructuredLearningKnowledgeFact
        {
            KnowledgeFactId = Guid.NewGuid(),
            EngramId = engram.StructuredLearningDocumentSectionId,
            Statement = "A fact belongs to an Engram.",
            FactType = "Structure",
            Importance = "Supporting",
            DerivationType = "Composite",
            EpistemicStatus = "inferred",
            Confidence = 0.7,
            Language = "en",
            CreatedAt = DateTime.UtcNow
        };

        await using (var context = new HumanOsDbContext(options))
        {
            context.StructuredLearningDocuments.Add(document);
            context.StructuredLearningDocumentSections.Add(engram);
            context.StructuredLearningKnowledgeFacts.Add(fact);
            await context.SaveChangesAsync();
            context.StructuredLearningDocuments.Remove(document);
            await context.SaveChangesAsync();
        }

        await using var verify = new HumanOsDbContext(options);
        (await verify.StructuredLearningKnowledgeFacts.CountAsync()).Should().Be(0);
    }
}
