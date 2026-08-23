using FluentAssertions;
using HumanOS.Agents.Studio;
using HumanOS.Data;
using HumanOS.Models.Capabilities.Studio;
using HumanOS.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace HumanOS.Tests.Services;

/// <summary>Fake agent that returns a fixed, pre-scripted extraction
/// result instead of calling Azure OpenAI — lets the service's
/// dedupe/link/idempotency logic be tested deterministically.</summary>
file sealed class FakeEntityExtractionAgent : StructuredLearningEntityExtractionAgent
{
    private readonly Queue<StructuredLearningEntityExtractionResponse> _responses;

    public FakeEntityExtractionAgent(params StructuredLearningEntityExtractionResponse[] responses)
        : base(new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build())
    {
        _responses = new Queue<StructuredLearningEntityExtractionResponse>(responses);
    }

    public override bool IsConfigured => true;

    public override Task<StructuredLearningEntityExtractionResult> ExtractAsync(
        string sectionTitle, string sectionText, CancellationToken cancellationToken) =>
        Task.FromResult(new StructuredLearningEntityExtractionResult
        {
            Response = _responses.Count > 0 ? _responses.Dequeue() : new StructuredLearningEntityExtractionResponse(),
            ModelName = "fake-model"
        });

    public override Task<StructuredLearningEntityExtractionResult> ExtractAsync(
        string sectionTitle, string sectionText, IReadOnlyList<string> existingClassNames,
        IReadOnlyList<(string Id, string Name, IReadOnlyList<string> Aliases)> existingMasterEntities,
        CancellationToken cancellationToken) =>
        ExtractAsync(sectionTitle, sectionText, cancellationToken);
}

file sealed class FixedDbContextFactory(DbContextOptions<HumanOsDbContext> options) : IDbContextFactory<HumanOsDbContext>
{
    public HumanOsDbContext CreateDbContext() => new(options);

    public ValueTask<HumanOsDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        => new(new HumanOsDbContext(options));
}

public class StructuredLearningEntityExtractionServiceTests
{
    private static DbContextOptions<HumanOsDbContext> NewInMemoryOptions() =>
        new DbContextOptionsBuilder<HumanOsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

    private static async Task<(Guid TenantId, Guid SectionId)> SeedSectionAsync(
        DbContextOptions<HumanOsDbContext> options, string text, string title = "Test section")
    {
        var tenantId = Guid.NewGuid();
        var document = new StructuredLearningDocument
        {
            StructuredLearningDocumentId = Guid.NewGuid(),
            TenantId = tenantId,
            SessionId = Guid.NewGuid(),
            DocumentName = "AlgebraNumerosImaginarios",
            CreatedDate = DateTime.UtcNow
        };
        var section = new StructuredLearningDocumentSection
        {
            StructuredLearningDocumentSectionId = Guid.NewGuid(),
            StructuredLearningDocumentId = document.StructuredLearningDocumentId,
            TenantId = tenantId,
            SortOrder = 1,
            ChapterTitle = "Chapter 1",
            SubchapterTitle = title,
            Text = text,
            TotalCharacters = text.Length,
            TotalTokens = 10,
            Embedding = new Microsoft.Data.SqlTypes.SqlVector<float>(new[] { 1f, 2f, 3f }),
            CreatedDate = DateTime.UtcNow,
            Document = document
        };
        document.Sections = new List<StructuredLearningDocumentSection> { section };

        await using var ctx = new HumanOsDbContext(options);
        ctx.StructuredLearningDocuments.Add(document);
        ctx.StructuredLearningDocumentSections.Add(section);
        await ctx.SaveChangesAsync();

        return (tenantId, section.StructuredLearningDocumentSectionId);
    }

    [Fact]
    public async Task ExtractForSectionAsync_MathematicalEntities_PersistsExpressionsVerbatim()
    {
        var options = NewInMemoryOptions();
        var text = "La unidad imaginaria se define como i = √−1, y su cuadrado cumple i² = −1.";
        var (_, sectionId) = await SeedSectionAsync(options, text);

        var response = new StructuredLearningEntityExtractionResponse
        {
            Entities =
            [
                new ExtractedEntityDto { CanonicalName = "i = √−1", EntityType = "MathematicalExpression", SourceText = "i = √−1" },
                new ExtractedEntityDto { CanonicalName = "i² = −1", EntityType = "MathematicalExpression", SourceText = "i² = −1" },
                new ExtractedEntityDto { CanonicalName = "Unidad imaginaria", EntityType = "MathematicalConcept", SourceText = "unidad imaginaria" },
            ]
        };

        var service = new StructuredLearningEntityExtractionService(
            new FakeEntityExtractionAgent(response), new CapabilityEmbeddingService(new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build()),
            new StructuredLearningGenerationUsageService(new FixedDbContextFactory(options)),
            new StructuredLearningEntityClassService(new FixedDbContextFactory(options)), new FixedDbContextFactory(options));

        var created = await service.ExtractForSectionAsync(sectionId, CancellationToken.None);

        created.Should().Be(3);
        await using var verify = new HumanOsDbContext(options);
        var entities = await verify.StructuredLearningEntities.ToListAsync();
        entities.Should().Contain(e => e.Name == "i = √−1" && e.EntityType == "MathematicalExpression");
        entities.Should().Contain(e => e.Name == "i² = −1");
        var links = await verify.StructuredLearningEntitySections.ToListAsync();
        links.Should().HaveCount(3);
        links.Should().Contain(l => l.SourceText == "i = √−1");
    }

    [Fact]
    public async Task ExtractForSectionAsync_NgoOnboardingEntities_DoesNotExtractActions()
    {
        var options = NewInMemoryOptions();
        var text = "We love to think of our volunteers as kind, friendly companions who help participants during group activities within the group.";
        var (_, sectionId) = await SeedSectionAsync(options, text, "Companionship and Support During Group Activities");

        var response = new StructuredLearningEntityExtractionResponse
        {
            Entities =
            [
                new ExtractedEntityDto { CanonicalName = "Volunteer", EntityType = "PersonRole", SourceText = "volunteers" },
                new ExtractedEntityDto { CanonicalName = "Participant", EntityType = "PersonRole", SourceText = "participants" },
                new ExtractedEntityDto { CanonicalName = "Group Activity", EntityType = "Activity", SourceText = "group activities" },
                new ExtractedEntityDto { CanonicalName = "Group", EntityType = "SocialGroup", SourceText = "within the group" },
            ]
        };

        var service = new StructuredLearningEntityExtractionService(
            new FakeEntityExtractionAgent(response), new CapabilityEmbeddingService(new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build()),
            new StructuredLearningGenerationUsageService(new FixedDbContextFactory(options)),
            new StructuredLearningEntityClassService(new FixedDbContextFactory(options)), new FixedDbContextFactory(options));

        await service.ExtractForSectionAsync(sectionId, CancellationToken.None);

        await using var verify = new HumanOsDbContext(options);
        var names = (await verify.StructuredLearningEntities.ToListAsync()).Select(e => e.Name).ToList();
        names.Should().BeEquivalentTo(["Volunteer", "Participant", "Group Activity", "Group"]);
        names.Should().NotContain(["helping", "cheering", "supporting", "engaged", "take part"]);
    }

    [Fact]
    public async Task ExtractForSectionAsync_EmptyExtraction_ReturnsZeroAndCreatesNothing()
    {
        var options = NewInMemoryOptions();
        var (_, sectionId) = await SeedSectionAsync(options, "Please help, encourage, and support everyone.");

        var service = new StructuredLearningEntityExtractionService(
            new FakeEntityExtractionAgent(new StructuredLearningEntityExtractionResponse()),
            new CapabilityEmbeddingService(new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build()),
            new StructuredLearningGenerationUsageService(new FixedDbContextFactory(options)),
            new StructuredLearningEntityClassService(new FixedDbContextFactory(options)), new FixedDbContextFactory(options));

        var created = await service.ExtractForSectionAsync(sectionId, CancellationToken.None);

        created.Should().Be(0);
        await using var verify = new HumanOsDbContext(options);
        (await verify.StructuredLearningEntities.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task ExtractForSectionAsync_RunTwice_IsIdempotent_NoDuplicateEntitiesOrLinks()
    {
        var options = NewInMemoryOptions();
        var (_, sectionId) = await SeedSectionAsync(options, "Volunteers support participants.");

        var response = new StructuredLearningEntityExtractionResponse
        {
            Entities = [new ExtractedEntityDto { CanonicalName = "Volunteer", EntityType = "PersonRole", SourceText = "Volunteers" }]
        };

        var service = new StructuredLearningEntityExtractionService(
            new FakeEntityExtractionAgent(response, response),
            new CapabilityEmbeddingService(new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build()),
            new StructuredLearningGenerationUsageService(new FixedDbContextFactory(options)),
            new StructuredLearningEntityClassService(new FixedDbContextFactory(options)), new FixedDbContextFactory(options));

        var firstRun = await service.ExtractForSectionAsync(sectionId, CancellationToken.None);
        var secondRun = await service.ExtractForSectionAsync(sectionId, CancellationToken.None);

        firstRun.Should().Be(1);
        secondRun.Should().Be(0, "re-running extraction for the same section must not create duplicate links");

        await using var verify = new HumanOsDbContext(options);
        (await verify.StructuredLearningEntities.CountAsync()).Should().Be(1);
        (await verify.StructuredLearningEntitySections.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task ExtractForSectionAsync_EquivalentAliases_CreatesOneCanonicalEntity()
    {
        var options = NewInMemoryOptions();
        var text = "Search, also called Intelligent search, finds relevant information.";
        var (_, sectionId) = await SeedSectionAsync(options, text);
        var response = new StructuredLearningEntityExtractionResponse
        {
            Entities =
            [
                new ExtractedEntityDto { CanonicalName = "Search", EntityType = "GeneralConcept", SourceText = "Search", Aliases = ["Intelligent search"] },
                new ExtractedEntityDto { CanonicalName = "Intelligent search", EntityType = "GeneralConcept", SourceText = "Intelligent search", Aliases = ["Search"] }
            ]
        };
        var service = new StructuredLearningEntityExtractionService(
            new FakeEntityExtractionAgent(response), new CapabilityEmbeddingService(new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build()),
            new StructuredLearningGenerationUsageService(new FixedDbContextFactory(options)),
            new StructuredLearningEntityClassService(new FixedDbContextFactory(options)), new FixedDbContextFactory(options));

        await service.ExtractForSectionAsync(sectionId, CancellationToken.None);

        await using var verify = new HumanOsDbContext(options);
        (await verify.StructuredLearningEntities.CountAsync()).Should().Be(1);
        (await verify.StructuredLearningEntities.SingleAsync()).Aliases.Should().Contain("Intelligent search");
    }
}
