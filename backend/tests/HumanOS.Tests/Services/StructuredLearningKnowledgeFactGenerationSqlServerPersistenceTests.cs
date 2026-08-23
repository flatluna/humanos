using Azure.Core;
using Azure.Identity;
using FluentAssertions;
using HumanOS.Agents.Studio;
using HumanOS.Contracts.Studio;
using HumanOS.Data;
using HumanOS.Models.Capabilities.Studio;
using HumanOS.Services;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace HumanOS.Tests.Services;

[Trait("Category", "Integration")]
public sealed class StructuredLearningKnowledgeFactGenerationSqlServerPersistenceTests
{
    public enum SaveFailureMode { Fact, PathStep, AfterDelete, LastFact, OneOfManyPathSteps }

    private sealed class SaveFailureInterceptor : SaveChangesInterceptor
    {
        public SaveFailureMode? Mode { get; set; }

        public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
        {
            ThrowIfArmed(eventData.Context);
            return result;
        }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            ThrowIfArmed(eventData.Context);
            return ValueTask.FromResult(result);
        }

        private void ThrowIfArmed(DbContext? context)
        {
            if (Mode is null || context is null) return;
            var entries = context.ChangeTracker.Entries().ToList();
            var addedFacts = entries.Count(entry => entry.Entity is StructuredLearningKnowledgeFact && entry.State == EntityState.Added);
            var addedSteps = entries.Count(entry => entry.Entity is StructuredLearningKnowledgeFactGraphPathStep && entry.State == EntityState.Added);
            var deletedFacts = entries.Count(entry => entry.Entity is StructuredLearningKnowledgeFact && entry.State == EntityState.Deleted);
            var shouldFail = Mode switch
            {
                SaveFailureMode.Fact => addedFacts > 0,
                SaveFailureMode.PathStep => addedSteps > 0,
                SaveFailureMode.AfterDelete => deletedFacts > 0,
                SaveFailureMode.LastFact => addedFacts >= 2,
                SaveFailureMode.OneOfManyPathSteps => addedSteps >= 2,
                _ => false
            };
            if (shouldFail) throw new InvalidOperationException($"Controlled test failure: {Mode}");
        }
    }

    private sealed class AzureSqlFactory(string baseConnectionString, SaveFailureInterceptor? interceptor = null) : IDbContextFactory<HumanOsDbContext>
    {
        public HumanOsDbContext CreateDbContext()
        {
            var credential = new AzureCliCredential();
            var token = credential.GetToken(new TokenRequestContext(["https://database.windows.net/.default"]));
            var connection = new SqlConnection(baseConnectionString) { AccessToken = token.Token };
            connection.Open();
            var options = new DbContextOptionsBuilder<HumanOsDbContext>().UseSqlServer(connection);
            if (interceptor is not null) options.AddInterceptors(interceptor);
            return new HumanOsDbContext(options.Options);
        }

        public async ValueTask<HumanOsDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        {
            var credential = new AzureCliCredential();
            var token = await credential.GetTokenAsync(new TokenRequestContext(["https://database.windows.net/.default"]), cancellationToken);
            var connection = new SqlConnection(baseConnectionString) { AccessToken = token.Token };
            await connection.OpenAsync(cancellationToken);
            var options = new DbContextOptionsBuilder<HumanOsDbContext>().UseSqlServer(connection);
            if (interceptor is not null) options.AddInterceptors(interceptor);
            return new HumanOsDbContext(options.Options);
        }
    }

    private sealed class FakeAgent(StructuredLearningKnowledgeFactGenerationResponse response, Exception? exception = null)
        : StructuredLearningKnowledgeFactGenerationAgent(new ConfigurationBuilder().Build())
    {
        public int Calls { get; private set; }

        public override Task<StructuredLearningKnowledgeFactGenerationResponse> GenerateAsync(
            StructuredLearningKnowledgeFactGenerationRequest request, CancellationToken cancellationToken)
        {
            Calls++;
            if (exception is not null) throw exception;
            return Task.FromResult(response);
        }
    }

    [Theory]
    [InlineData(SaveFailureMode.Fact)]
    [InlineData(SaveFailureMode.PathStep)]
    [InlineData(SaveFailureMode.AfterDelete)]
    [InlineData(SaveFailureMode.LastFact)]
    [InlineData(SaveFailureMode.OneOfManyPathSteps)]
    public async Task ControlledWriteFailureRollsBackEntireKnowledgeFactTransaction(SaveFailureMode failureMode)
    {
        var connectionString = Environment.GetEnvironmentVariable("HUMANOS_SQL_TEST_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(connectionString)) return;
        var runId = Guid.NewGuid(); var documentId = Guid.NewGuid(); var sectionId = Guid.NewGuid(); var tenantId = Guid.NewGuid();
        var entityIds = Enumerable.Range(0, 3).Select(_ => Guid.NewGuid()).ToList();
        var connectionIds = new[] { Guid.NewGuid(), Guid.NewGuid() };
        var oldFactId = Guid.NewGuid(); var oldStepId = Guid.NewGuid();
        var interceptor = new SaveFailureInterceptor(); var factory = new AzureSqlFactory(connectionString, interceptor);
        var sourceText = "Entity 0 enables Entity 1 and Entity 1 enables Entity 2.";
        try
        {
            await using (var seed = await factory.CreateDbContextAsync())
            {
                var document = new StructuredLearningDocument { StructuredLearningDocumentId = documentId, TenantId = tenantId, SessionId = runId, DocumentName = $"Rollback {runId}", ConfigSnapshotJson = "{\"LanguageCode\":\"en\"}", CreatedDate = DateTime.UtcNow };
                var section = new StructuredLearningDocumentSection { StructuredLearningDocumentSectionId = sectionId, StructuredLearningDocumentId = documentId, TenantId = tenantId, SortOrder = 1, ChapterTitle = "Test", Capability = "Test", SubchapterTitle = "Step5", Text = sourceText, TotalCharacters = sourceText.Length, TotalTokens = 10, Embedding = new Microsoft.Data.SqlTypes.SqlVector<float>(Enumerable.Repeat(1f, 1536).ToArray()), CreatedDate = DateTime.UtcNow };
                var entities = entityIds.Select((id, index) => new StructuredLearningEntity { StructuredLearningEntityId = id, TenantId = tenantId, Name = $"Entity {index}", EntityType = "Concept", NormalizedValue = $"rollback-{runId}-{index}", CreatedDate = DateTime.UtcNow }).ToList();
                seed.StructuredLearningDocuments.Add(document); seed.StructuredLearningDocumentSections.Add(section); seed.StructuredLearningEntities.AddRange(entities);
                seed.StructuredLearningEntitySections.AddRange(entities.Select(entity => new StructuredLearningEntitySection { StructuredLearningEntitySectionId = Guid.NewGuid(), StructuredLearningEntityId = entity.StructuredLearningEntityId, StructuredLearningDocumentSectionId = sectionId, TenantId = tenantId, SourceText = sourceText, ResolutionStatus = "new", CreatedDate = DateTime.UtcNow }));
                seed.StructuredLearningConnections.AddRange(connectionIds.Select((id, index) => new StructuredLearningConnection { StructuredLearningConnectionId = id, StructuredLearningDocumentId = documentId, TenantId = tenantId, SourceEntityId = entityIds[index], TargetEntityId = entityIds[index + 1], SourceSectionId = sectionId, RelationshipLabel = "enables", RelationshipCategory = "Enables", Evidence = sourceText, EpistemicStatus = "explicit", Confidence = 0.95, CreatedDate = DateTime.UtcNow }));
                await seed.SaveChangesAsync();
            }
            await using (var seedFacts = await factory.CreateDbContextAsync())
            {
                seedFacts.StructuredLearningKnowledgeFacts.Add(new StructuredLearningKnowledgeFact { KnowledgeFactId = oldFactId, EngramId = sectionId, Statement = "OLD FACT", FactType = "Relationship", Importance = "Core", DerivationType = "Atomic", EpistemicStatus = "explicit", Confidence = 0.8, Language = "en", CreatedAt = DateTime.UtcNow });
                seedFacts.StructuredLearningKnowledgeFactGraphPathSteps.Add(new StructuredLearningKnowledgeFactGraphPathStep { KnowledgeFactGraphPathStepId = oldStepId, KnowledgeFactId = oldFactId, ConnectionId = connectionIds[0], StepOrder = 1, CreatedAt = DateTime.UtcNow });
                await seedFacts.SaveChangesAsync();
            }
            var pathLength = failureMode == SaveFailureMode.OneOfManyPathSteps ? 2 : 1;
            var factCount = failureMode == SaveFailureMode.LastFact ? 2 : 1;
            var response = new StructuredLearningKnowledgeFactGenerationResponse { EngramId = sectionId, EngramName = "Step5", Language = "en", KnowledgeFacts = Enumerable.Range(0, factCount).Select(index => new StructuredLearningGeneratedKnowledgeFact { Statement = $"NEW FACT {index} {runId}", FactType = "Relationship", Importance = "Core", DerivationType = "Atomic", SourceEntityIds = entityIds.Take(pathLength + 1).ToList(), SourceConnectionIds = connectionIds.Take(pathLength).ToList(), GraphPath = connectionIds.Take(pathLength).Select((id, step) => new StructuredLearningKnowledgeFactGraphPathStepOutput { ConnectionId = id, StepOrder = step + 1 }).ToList(), Evidence = [sourceText], EpistemicStatus = "explicit", Confidence = 0.9 }).ToList() };
            interceptor.Mode = failureMode;
            var result = await new StructuredLearningKnowledgeFactGenerationService(new FakeAgent(response), factory, NullLogger<StructuredLearningKnowledgeFactGenerationService>.Instance).GenerateValidateAndPersistAsync(sectionId, CancellationToken.None);
            result.PersistenceSucceeded.Should().BeFalse();

            await using var verify = await factory.CreateDbContextAsync();
            var facts = await verify.StructuredLearningKnowledgeFacts.Where(fact => fact.EngramId == sectionId).ToListAsync();
            var steps = await verify.StructuredLearningKnowledgeFactGraphPathSteps.Where(step => facts.Select(fact => fact.KnowledgeFactId).Contains(step.KnowledgeFactId)).ToListAsync();
            facts.Select(fact => fact.KnowledgeFactId).Should().Equal(oldFactId);
            steps.Select(step => step.KnowledgeFactGraphPathStepId).Should().Equal(oldStepId);
            (await verify.StructuredLearningKnowledgeFactGraphPathSteps.CountAsync(step => !facts.Select(fact => fact.KnowledgeFactId).Contains(step.KnowledgeFactId))).Should().Be(0);
        }
        finally
        {
            await using var cleanup = await factory.CreateDbContextAsync();
            var factIds = await cleanup.StructuredLearningKnowledgeFacts.Where(fact => fact.EngramId == sectionId).Select(fact => fact.KnowledgeFactId).ToListAsync();
            if (factIds.Count > 0) await cleanup.StructuredLearningKnowledgeFactGraphPathSteps.Where(step => factIds.Contains(step.KnowledgeFactId)).ExecuteDeleteAsync();
            await cleanup.StructuredLearningKnowledgeFactGraphPathSteps.Where(step => step.KnowledgeFactId == oldStepId).ExecuteDeleteAsync();
            await cleanup.StructuredLearningKnowledgeFacts.Where(fact => fact.EngramId == sectionId).ExecuteDeleteAsync();
            await cleanup.StructuredLearningConnections.Where(connection => connection.StructuredLearningDocumentId == documentId).ExecuteDeleteAsync();
            await cleanup.StructuredLearningEntitySections.Where(link => link.StructuredLearningDocumentSectionId == sectionId).ExecuteDeleteAsync();
            await cleanup.StructuredLearningEntities.Where(entity => entity.TenantId == tenantId).ExecuteDeleteAsync();
            await cleanup.StructuredLearningDocumentSections.Where(section => section.StructuredLearningDocumentSectionId == sectionId).ExecuteDeleteAsync();
            await cleanup.StructuredLearningDocuments.Where(document => document.StructuredLearningDocumentId == documentId).ExecuteDeleteAsync();
        }
    }

    [Fact]
    public async Task RegenerationReplacesFactsAndPreservesPreviousSetWhenGenerationOrValidationFails()
    {
        var connectionString = Environment.GetEnvironmentVariable("HUMANOS_SQL_TEST_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var runId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var sectionId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var entityIds = new[] { Guid.NewGuid(), Guid.NewGuid() };
        var connectionId = Guid.NewGuid();
        var oldFactId = Guid.NewGuid();
        var factory = new AzureSqlFactory(connectionString);
        var sourceText = "Entity 0 enables Entity 1.";
        var validResponse = (string statement) => new StructuredLearningKnowledgeFactGenerationResponse
        {
            EngramId = sectionId, EngramName = "Step5", Language = "en",
            KnowledgeFacts = [new StructuredLearningGeneratedKnowledgeFact
            {
                Statement = statement, FactType = "Relationship", Importance = "Core", DerivationType = "Atomic",
                SourceEntityIds = entityIds.ToList(), SourceConnectionIds = [connectionId],
                GraphPath = [new StructuredLearningKnowledgeFactGraphPathStepOutput { ConnectionId = connectionId, StepOrder = 1 }],
                Evidence = [sourceText], EpistemicStatus = "explicit", Confidence = 0.9
            }]
        };

        try
        {
            await using (var seed = await factory.CreateDbContextAsync())
            {
                var document = new StructuredLearningDocument { StructuredLearningDocumentId = documentId, TenantId = tenantId, SessionId = runId, DocumentName = $"Regeneration {runId}", CreatedDate = DateTime.UtcNow, ConfigSnapshotJson = "{\"LanguageCode\":\"en\"}" };
                var section = new StructuredLearningDocumentSection { StructuredLearningDocumentSectionId = sectionId, StructuredLearningDocumentId = documentId, TenantId = tenantId, SortOrder = 1, ChapterTitle = "Test", Capability = "Test", SubchapterTitle = "Step5", Text = sourceText, TotalCharacters = sourceText.Length, TotalTokens = 5, Embedding = new Microsoft.Data.SqlTypes.SqlVector<float>(Enumerable.Repeat(1f, 1536).ToArray()), CreatedDate = DateTime.UtcNow };
                var entities = entityIds.Select((id, index) => new StructuredLearningEntity { StructuredLearningEntityId = id, TenantId = tenantId, Name = $"Entity {index}", EntityType = "Concept", NormalizedValue = $"regen-{runId}-{index}", CreatedDate = DateTime.UtcNow }).ToList();
                seed.StructuredLearningDocuments.Add(document); seed.StructuredLearningDocumentSections.Add(section); seed.StructuredLearningEntities.AddRange(entities);
                seed.StructuredLearningEntitySections.AddRange(entities.Select(entity => new StructuredLearningEntitySection { StructuredLearningEntitySectionId = Guid.NewGuid(), StructuredLearningEntityId = entity.StructuredLearningEntityId, StructuredLearningDocumentSectionId = sectionId, TenantId = tenantId, SourceText = sourceText, ResolutionStatus = "new", CreatedDate = DateTime.UtcNow }));
                seed.StructuredLearningConnections.Add(new StructuredLearningConnection { StructuredLearningConnectionId = connectionId, StructuredLearningDocumentId = documentId, TenantId = tenantId, SourceEntityId = entityIds[0], TargetEntityId = entityIds[1], SourceSectionId = sectionId, RelationshipLabel = "enables", RelationshipCategory = "Enables", Evidence = sourceText, EpistemicStatus = "explicit", Confidence = 0.95, CreatedDate = DateTime.UtcNow });
                await seed.SaveChangesAsync();
            }

            var oldStepId = Guid.NewGuid();
            await using (var seedFacts = await factory.CreateDbContextAsync())
            {
                seedFacts.StructuredLearningKnowledgeFacts.Add(new StructuredLearningKnowledgeFact { KnowledgeFactId = oldFactId, EngramId = sectionId, Statement = "OLD FACT", FactType = "Relationship", Importance = "Core", DerivationType = "Atomic", EpistemicStatus = "explicit", Confidence = 0.8, Language = "en", CreatedAt = DateTime.UtcNow });
                seedFacts.StructuredLearningKnowledgeFactGraphPathSteps.Add(new StructuredLearningKnowledgeFactGraphPathStep { KnowledgeFactGraphPathStepId = oldStepId, KnowledgeFactId = oldFactId, ConnectionId = connectionId, StepOrder = 1, CreatedAt = DateTime.UtcNow });
                await seedFacts.SaveChangesAsync();
            }

            async Task<(StructuredLearningKnowledgeFactGenerationResult Result, List<Guid> FactIds, List<Guid> StepIds)> ExecuteAndReload(FakeAgent agent)
            {
                var result = await new StructuredLearningKnowledgeFactGenerationService(agent, factory, NullLogger<StructuredLearningKnowledgeFactGenerationService>.Instance).GenerateValidateAndPersistAsync(sectionId, CancellationToken.None);
                await using var verify = await factory.CreateDbContextAsync();
                var facts = await verify.StructuredLearningKnowledgeFacts.Where(fact => fact.EngramId == sectionId).ToListAsync();
                var factIds = facts.Select(fact => fact.KnowledgeFactId).ToList();
                var stepIds = await verify.StructuredLearningKnowledgeFactGraphPathSteps.Where(step => factIds.Contains(step.KnowledgeFactId)).Select(step => step.KnowledgeFactGraphPathStepId).ToListAsync();
                return (result, factIds, stepIds);
            }

            var first = await ExecuteAndReload(new FakeAgent(validResponse("NEW FACT 1")));
            first.Result.PersistenceSucceeded.Should().BeTrue();
            first.FactIds.Should().ContainSingle().And.NotContain(oldFactId);
            first.StepIds.Should().ContainSingle().And.NotContain(oldStepId);

            var second = await ExecuteAndReload(new FakeAgent(validResponse("NEW FACT 2")));
            second.Result.PersistenceSucceeded.Should().BeTrue();
            second.FactIds.Should().ContainSingle().And.NotContain(first.FactIds[0]);
            second.StepIds.Should().ContainSingle().And.NotContain(first.StepIds[0]);

            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            var canceledService = new StructuredLearningKnowledgeFactGenerationService(new FakeAgent(validResponse("CANCELED")), factory, NullLogger<StructuredLearningKnowledgeFactGenerationService>.Instance);
            await FluentActions.Awaiting(() => canceledService.GenerateValidateAndPersistAsync(sectionId, cancellation.Token))
                .Should().ThrowAsync<OperationCanceledException>();
            await using (var canceledVerify = await factory.CreateDbContextAsync())
            {
                var canceledFactIds = await canceledVerify.StructuredLearningKnowledgeFacts.Where(fact => fact.EngramId == sectionId).Select(fact => fact.KnowledgeFactId).ToListAsync();
                var canceledStepIds = await canceledVerify.StructuredLearningKnowledgeFactGraphPathSteps.Where(step => canceledFactIds.Contains(step.KnowledgeFactId)).Select(step => step.KnowledgeFactGraphPathStepId).ToListAsync();
                canceledFactIds.Should().Equal(second.FactIds);
                canceledStepIds.Should().Equal(second.StepIds);
            }

            var exceptionResult = await ExecuteAndReload(new FakeAgent(validResponse("IGNORED"), new InvalidOperationException("fake failure")));
            exceptionResult.Result.PersistenceSucceeded.Should().BeFalse();
            exceptionResult.FactIds.Should().Equal(second.FactIds); exceptionResult.StepIds.Should().Equal(second.StepIds);

            var invalid = validResponse("INVALID"); invalid.KnowledgeFacts[0].Evidence = ["not in source"];
            var invalidResult = await ExecuteAndReload(new FakeAgent(invalid));
            invalidResult.Result.PersistenceSucceeded.Should().BeFalse();
            invalidResult.FactIds.Should().Equal(second.FactIds); invalidResult.StepIds.Should().Equal(second.StepIds);

            var emptyResult = await ExecuteAndReload(new FakeAgent(new StructuredLearningKnowledgeFactGenerationResponse { EngramId = sectionId, EngramName = "Step5", Language = "en" }));
            emptyResult.Result.PersistenceSucceeded.Should().BeFalse();
            emptyResult.Result.Errors.Should().ContainSingle().Which.Should().Contain("no KnowledgeFacts");
            emptyResult.FactIds.Should().Equal(second.FactIds); emptyResult.StepIds.Should().Equal(second.StepIds);
        }
        finally
        {
            await using var cleanup = await factory.CreateDbContextAsync();
            var factIds = await cleanup.StructuredLearningKnowledgeFacts.Where(fact => fact.EngramId == sectionId).Select(fact => fact.KnowledgeFactId).ToListAsync();
            if (factIds.Count > 0) await cleanup.StructuredLearningKnowledgeFactGraphPathSteps.Where(step => factIds.Contains(step.KnowledgeFactId)).ExecuteDeleteAsync();
            await cleanup.StructuredLearningKnowledgeFactGraphPathSteps.Where(step => step.KnowledgeFactId == oldFactId).ExecuteDeleteAsync();
            await cleanup.StructuredLearningKnowledgeFacts.Where(fact => fact.EngramId == sectionId).ExecuteDeleteAsync();
            await cleanup.StructuredLearningConnections.Where(connection => connection.StructuredLearningDocumentId == documentId).ExecuteDeleteAsync();
            await cleanup.StructuredLearningEntitySections.Where(link => link.StructuredLearningDocumentSectionId == sectionId).ExecuteDeleteAsync();
            await cleanup.StructuredLearningEntities.Where(entity => entity.TenantId == tenantId).ExecuteDeleteAsync();
            await cleanup.StructuredLearningDocumentSections.Where(section => section.StructuredLearningDocumentSectionId == sectionId).ExecuteDeleteAsync();
            await cleanup.StructuredLearningDocuments.Where(document => document.StructuredLearningDocumentId == documentId).ExecuteDeleteAsync();
        }
    }

    [Theory]
    [InlineData(1, "Atomic")]
    [InlineData(2, "Composite")]
    [InlineData(3, "Composite")]
    public async Task PersistsAndReloadsAtomicOrCompositePath(int pathLength, string derivationType)
    {
        var connectionString = Environment.GetEnvironmentVariable("HUMANOS_SQL_TEST_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var runId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var sectionId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var entityIds = Enumerable.Range(0, pathLength + 1).Select(_ => Guid.NewGuid()).ToList();
        var connectionIds = Enumerable.Range(0, pathLength).Select(_ => Guid.NewGuid()).ToList();
        var factId = Guid.NewGuid();
        var factory = new AzureSqlFactory(connectionString);
        var agent = new FakeAgent(new());

        try
        {
            await using (var seed = await factory.CreateDbContextAsync())
            {
                var document = new StructuredLearningDocument { StructuredLearningDocumentId = documentId, TenantId = tenantId, SessionId = runId, DocumentName = $"Step5 test {runId}", CreatedDate = DateTime.UtcNow };
                var section = new StructuredLearningDocumentSection { StructuredLearningDocumentSectionId = sectionId, StructuredLearningDocumentId = documentId, TenantId = tenantId, SortOrder = 1, ChapterTitle = "Test", Capability = "Test", SubchapterTitle = "Step5", Text = "Entity 0 enables Entity 1.", TotalCharacters = 25, TotalTokens = 5, Embedding = new Microsoft.Data.SqlTypes.SqlVector<float>(Enumerable.Repeat(1f, 1536).ToArray()), CreatedDate = DateTime.UtcNow };
                seed.StructuredLearningDocuments.Add(document);
                seed.StructuredLearningDocumentSections.Add(section);
                var entities = entityIds.Select((id, index) => new StructuredLearningEntity { StructuredLearningEntityId = id, TenantId = tenantId, Name = $"Entity {index}", EntityType = "Concept", NormalizedValue = $"step5-{runId}-{index}", CreatedDate = DateTime.UtcNow }).ToList();
                seed.StructuredLearningEntities.AddRange(entities);
                seed.StructuredLearningEntitySections.AddRange(entities.Select(entity => new StructuredLearningEntitySection { StructuredLearningEntitySectionId = Guid.NewGuid(), StructuredLearningEntityId = entity.StructuredLearningEntityId, StructuredLearningDocumentSectionId = sectionId, TenantId = tenantId, SourceText = "Entity", ResolutionStatus = "new", CreatedDate = DateTime.UtcNow }));
                seed.StructuredLearningConnections.AddRange(connectionIds.Select((id, index) => new StructuredLearningConnection { StructuredLearningConnectionId = id, StructuredLearningDocumentId = documentId, TenantId = tenantId, SourceEntityId = entityIds[index], TargetEntityId = entityIds[index + 1], SourceSectionId = sectionId, RelationshipLabel = "enables", RelationshipCategory = "Enables", Evidence = "Entity enables Entity", EpistemicStatus = "explicit", Confidence = 0.95, CreatedDate = DateTime.UtcNow }));
                await seed.SaveChangesAsync();
            }

            var response = new StructuredLearningKnowledgeFactGenerationResponse { EngramId = sectionId, EngramName = "Step5", Language = "en", KnowledgeFacts = [new StructuredLearningGeneratedKnowledgeFact { Statement = $"Fact {runId}", FactType = "Relationship", Importance = "Core", DerivationType = derivationType, SourceEntityIds = entityIds, SourceConnectionIds = connectionIds, GraphPath = connectionIds.Select((id, index) => new StructuredLearningKnowledgeFactGraphPathStepOutput { ConnectionId = id, StepOrder = index + 1 }).ToList(), Evidence = ["Entity 0 enables Entity 1."], EpistemicStatus = "explicit", Confidence = 0.9 }] };
            agent = new FakeAgent(response);
            var service = new StructuredLearningKnowledgeFactGenerationService(agent, factory, NullLogger<StructuredLearningKnowledgeFactGenerationService>.Instance);
            var result = await service.GenerateValidateAndPersistAsync(sectionId, CancellationToken.None);
            result.PersistenceSucceeded.Should().BeTrue();
            agent.Calls.Should().Be(1);

            await using var verify = await factory.CreateDbContextAsync();
            var facts = await verify.StructuredLearningKnowledgeFacts.Where(fact => fact.EngramId == sectionId).ToListAsync();
            var steps = await verify.StructuredLearningKnowledgeFactGraphPathSteps.Where(step => step.KnowledgeFactId == facts.Single().KnowledgeFactId).OrderBy(step => step.StepOrder).ToListAsync();
            facts.Should().ContainSingle();
            steps.Select(step => step.StepOrder).Should().Equal(Enumerable.Range(1, pathLength));
            (await verify.StructuredLearningKnowledgeFacts.CountAsync(fact => fact.EngramId == sectionId)).Should().Be(1);
        }
        finally
        {
            await using var cleanup = await factory.CreateDbContextAsync();
            await cleanup.StructuredLearningKnowledgeFactGraphPathSteps.Where(step => step.KnowledgeFactId == factId).ExecuteDeleteAsync();
            var factIds = await cleanup.StructuredLearningKnowledgeFacts.Where(fact => fact.EngramId == sectionId).Select(fact => fact.KnowledgeFactId).ToListAsync();
            if (factIds.Count > 0) await cleanup.StructuredLearningKnowledgeFactGraphPathSteps.Where(step => factIds.Contains(step.KnowledgeFactId)).ExecuteDeleteAsync();
            await cleanup.StructuredLearningKnowledgeFacts.Where(fact => fact.EngramId == sectionId).ExecuteDeleteAsync();
            await cleanup.StructuredLearningConnections.Where(connection => connection.StructuredLearningDocumentId == documentId).ExecuteDeleteAsync();
            await cleanup.StructuredLearningEntitySections.Where(link => link.StructuredLearningDocumentSectionId == sectionId).ExecuteDeleteAsync();
            await cleanup.StructuredLearningEntities.Where(entity => entity.TenantId == tenantId).ExecuteDeleteAsync();
            await cleanup.StructuredLearningDocumentSections.Where(section => section.StructuredLearningDocumentSectionId == sectionId).ExecuteDeleteAsync();
            await cleanup.StructuredLearningDocuments.Where(document => document.StructuredLearningDocumentId == documentId).ExecuteDeleteAsync();
        }
    }
}
