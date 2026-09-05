using FluentAssertions;
using HumanOS.Agents.Studio;
using HumanOS.Data;
using HumanOS.Models.Capabilities.Studio;
using HumanOS.Services;
using Microsoft.Data.SqlTypes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace HumanOS.Tests.Services;

/// <summary>Returns a fixed, caller-provided vector for any text instead
/// of calling Azure OpenAI — lets semantic-fallback matching be tested
/// deterministically via cosine similarity of hand-picked vectors.</summary>
file sealed class FakeEmbeddingService(float[] queryVector) : CapabilityEmbeddingService(new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build())
{
    public override bool IsConfigured => true;

    public override Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default) =>
        Task.FromResult(queryVector);
}

file sealed class FixedDbContextFactory(DbContextOptions<HumanOsDbContext> options) : IDbContextFactory<HumanOsDbContext>
{
    public HumanOsDbContext CreateDbContext() => new(options);

    public ValueTask<HumanOsDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        => new(new HumanOsDbContext(options));
}

public class StructuredLearningKnowledgeRetrievalServiceTests
{
    private static DbContextOptions<HumanOsDbContext> NewInMemoryOptions() =>
        new DbContextOptionsBuilder<HumanOsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

    /// <summary>Seeds: 1 document/Schema ("AlgebraNumerosImaginarios") with 1
    /// section/Engram whose text mentions "i" and "número", linked to 2
    /// entities ("Unidad imaginaria" primary, "Número" as a Hop-1 neighbor
    /// living in its own separate Schema/document).</summary>
    private static async Task<Guid> SeedGraphAsync(DbContextOptions<HumanOsDbContext> options, Guid tenantId)
    {
        var doc1 = new StructuredLearningDocument
        {
            StructuredLearningDocumentId = Guid.NewGuid(), TenantId = tenantId, SessionId = Guid.NewGuid(),
            DocumentName = "AlgebraNumerosImaginarios", CreatedDate = DateTime.UtcNow
        };
        var section1 = new StructuredLearningDocumentSection
        {
            StructuredLearningDocumentSectionId = Guid.NewGuid(), StructuredLearningDocumentId = doc1.StructuredLearningDocumentId,
            TenantId = tenantId, SortOrder = 1, ChapterTitle = "Cap 1", SubchapterTitle = "Definición de la unidad imaginaria",
            Text = "La unidad imaginaria i se define como la raíz de un número negativo.",
            TotalCharacters = 10, TotalTokens = 5, Embedding = new SqlVector<float>(new[] { 1f, 0f, 0f }), CreatedDate = DateTime.UtcNow
        };

        var doc2 = new StructuredLearningDocument
        {
            StructuredLearningDocumentId = Guid.NewGuid(), TenantId = tenantId, SessionId = Guid.NewGuid(),
            DocumentName = "AlgebraBasica", CreatedDate = DateTime.UtcNow
        };
        var section2 = new StructuredLearningDocumentSection
        {
            StructuredLearningDocumentSectionId = Guid.NewGuid(), StructuredLearningDocumentId = doc2.StructuredLearningDocumentId,
            TenantId = tenantId, SortOrder = 1, ChapterTitle = "Cap 1", SubchapterTitle = "¿Qué es un número?",
            Text = "Un número es un concepto matemático fundamental.",
            TotalCharacters = 10, TotalTokens = 5, Embedding = new SqlVector<float>(new[] { 1f, 0f, 0f }), CreatedDate = DateTime.UtcNow
        };

        var entityUnidad = new StructuredLearningEntity
        {
            StructuredLearningEntityId = Guid.NewGuid(), TenantId = tenantId, Name = "Unidad imaginaria",
            EntityType = "MathematicalConcept", NormalizedValue = StructuredLearningEntityExtractionService.Normalize("Unidad imaginaria"),
            Aliases = ["i"], Embedding = new SqlVector<float>(new[] { 1f, 0f, 0f }), CreatedDate = DateTime.UtcNow
        };
        var entityNumero = new StructuredLearningEntity
        {
            StructuredLearningEntityId = Guid.NewGuid(), TenantId = tenantId, Name = "Número",
            EntityType = "MathematicalConcept", NormalizedValue = StructuredLearningEntityExtractionService.Normalize("Número"),
            Embedding = new SqlVector<float>(new[] { 0f, 1f, 0f }), CreatedDate = DateTime.UtcNow
        };

        await using var ctx = new HumanOsDbContext(options);
        ctx.StructuredLearningDocuments.AddRange(doc1, doc2);
        ctx.StructuredLearningDocumentSections.AddRange(section1, section2);
        ctx.StructuredLearningEntities.AddRange(entityUnidad, entityNumero);
        ctx.StructuredLearningEntitySections.AddRange(
            new StructuredLearningEntitySection
            {
                StructuredLearningEntitySectionId = Guid.NewGuid(), StructuredLearningEntityId = entityUnidad.StructuredLearningEntityId,
                StructuredLearningDocumentSectionId = section1.StructuredLearningDocumentSectionId, TenantId = tenantId,
                SourceText = "unidad imaginaria", CreatedDate = DateTime.UtcNow
            },
            new StructuredLearningEntitySection
            {
                StructuredLearningEntitySectionId = Guid.NewGuid(), StructuredLearningEntityId = entityNumero.StructuredLearningEntityId,
                StructuredLearningDocumentSectionId = section1.StructuredLearningDocumentSectionId, TenantId = tenantId,
                SourceText = "número negativo", CreatedDate = DateTime.UtcNow
            },
            new StructuredLearningEntitySection
            {
                StructuredLearningEntitySectionId = Guid.NewGuid(), StructuredLearningEntityId = entityNumero.StructuredLearningEntityId,
                StructuredLearningDocumentSectionId = section2.StructuredLearningDocumentSectionId, TenantId = tenantId,
                SourceText = "número", CreatedDate = DateTime.UtcNow
            });
        await ctx.SaveChangesAsync();

        return section1.StructuredLearningDocumentSectionId;
    }

    private static readonly EngramRelevanceRerankerAgent EmptyReranker =
        new(new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build());

    [Fact]
    public async Task BuildRetrievalPackageAsync_ExactCanonicalMatch_RetrievesEngramAndSchema()
    {
        var options = NewInMemoryOptions();
        var tenantId = Guid.NewGuid();
        await SeedGraphAsync(options, tenantId);

        var service = new StructuredLearningKnowledgeRetrievalService(
            new FixedDbContextFactory(options), new FakeEmbeddingService([0f, 0f, 1f]), EmptyReranker);

        var package = await service.BuildRetrievalPackageAsync(tenantId, "Unidad imaginaria", CancellationToken.None);

        package.MatchedEntities.Should().ContainSingle(e => e.CanonicalName == "Unidad imaginaria" && e.MatchType == "Exact");
        package.RetrievedEngrams.Should().ContainSingle(e => e.Title == "Definición de la unidad imaginaria");
        package.Schemas.Should().ContainSingle(s => s.SchemaName == "AlgebraNumerosImaginarios");
    }

    [Fact]
    public async Task BuildRetrievalPackageAsync_AliasMatch_ResolvesToCanonicalEntity()
    {
        var options = NewInMemoryOptions();
        var tenantId = Guid.NewGuid();
        await SeedGraphAsync(options, tenantId);

        var service = new StructuredLearningKnowledgeRetrievalService(
            new FixedDbContextFactory(options), new FakeEmbeddingService([0f, 0f, 1f]), EmptyReranker);

        var package = await service.BuildRetrievalPackageAsync(tenantId, "i", CancellationToken.None);

        package.MatchedEntities.Should().ContainSingle(e => e.CanonicalName == "Unidad imaginaria" && e.MatchType == "Alias");
    }

    [Fact]
    public async Task BuildRetrievalPackageAsync_NoExactOrAliasMatch_FallsBackToSemanticSearch()
    {
        var options = NewInMemoryOptions();
        var tenantId = Guid.NewGuid();
        await SeedGraphAsync(options, tenantId);

        // Query vector nearly identical to entityUnidad's [1,0,0] — no exact/alias match exists for this text.
        var service = new StructuredLearningKnowledgeRetrievalService(
            new FixedDbContextFactory(options), new FakeEmbeddingService([0.99f, 0.01f, 0f]), EmptyReranker);

        var package = await service.BuildRetrievalPackageAsync(tenantId, "número imaginario que es raíz de negativo", CancellationToken.None);

        package.MatchedEntities.Should().ContainSingle(e => e.CanonicalName == "Unidad imaginaria" && e.MatchType == "Semantic");
    }

    [Fact]
    public async Task BuildRetrievalPackageAsync_RelatedEntityInAnotherSchema_SurfacedAsHop1()
    {
        var options = NewInMemoryOptions();
        var tenantId = Guid.NewGuid();
        await SeedGraphAsync(options, tenantId);

        var service = new StructuredLearningKnowledgeRetrievalService(
            new FixedDbContextFactory(options), new FakeEmbeddingService([0f, 0f, 1f]), EmptyReranker);

        var package = await service.BuildRetrievalPackageAsync(tenantId, "Unidad imaginaria", CancellationToken.None);

        // "Número" co-occurs in the SAME section as "Unidad imaginaria" and
        // has its own Engram in a DIFFERENT Schema (AlgebraBasica) — the
        // "1 hop" associative-network behavior.
        package.RelatedEntities.Should().ContainSingle(e => e.CanonicalName == "Número");
        package.RelatedEngrams.Should().ContainSingle(e => e.Title == "¿Qué es un número?" && e.SchemaName == "AlgebraBasica");
    }
}
