using HumanOS.Agents.Studio;
using HumanOS.Contracts.Studio;
using HumanOS.Data;
using HumanOS.Models.Capabilities.Studio;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace HumanOS.Services;

public sealed class StructuredLearningKnowledgeFactGenerationResult
{
    public Guid EngramId { get; init; }
    public bool GenerationSucceeded { get; init; }
    public bool PersistenceSucceeded { get; init; }
    public int KnowledgeFactsCreated { get; init; }
    public int MissingGraphFactsCount { get; init; }
    public IReadOnlyList<Guid> KnowledgeFactIds { get; init; } = [];
    public IReadOnlyList<string> Errors { get; init; } = [];
}

/// <summary>Step 5 application service: builds a reduced request from one
/// persisted Engram/Graph, validates the proposal, then replaces its facts
/// atomically. It never invokes pedagogy or persists MissingGraphFacts.</summary>
public sealed class StructuredLearningKnowledgeFactGenerationService
{
    private readonly StructuredLearningKnowledgeFactGenerationAgent _agent;
    private readonly IDbContextFactory<HumanOsDbContext> _dbContextFactory;
    private readonly ILogger<StructuredLearningKnowledgeFactGenerationService> _logger;

    public StructuredLearningKnowledgeFactGenerationService(
        StructuredLearningKnowledgeFactGenerationAgent agent,
        IDbContextFactory<HumanOsDbContext> dbContextFactory,
        ILogger<StructuredLearningKnowledgeFactGenerationService> logger)
    {
        _agent = agent;
        _dbContextFactory = dbContextFactory;
        _logger = logger;
    }

    public bool IsConfigured => _agent.IsConfigured;

    public async Task<StructuredLearningKnowledgeFactGenerationResult> GenerateValidateAndPersistAsync(
        Guid engramId, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting Knowledge Facts generation for Engram {EngramId}", engramId);
        await using var readContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var section = await readContext.StructuredLearningDocumentSections
            .Include(s => s.Document)
            .SingleOrDefaultAsync(s => s.StructuredLearningDocumentSectionId == engramId, cancellationToken);
        if (section is null)
        {
            return Failure(engramId, "Engram was not found.");
        }
        if (string.IsNullOrWhiteSpace(section.Text))
        {
            return Failure(engramId, "Engram SourceText is empty.");
        }

        var language = ResolveLanguage(section.Document.ConfigSnapshotJson, _logger, engramId);
        var entityRows = await readContext.StructuredLearningEntitySections
            .Where(link => link.StructuredLearningDocumentSectionId == engramId)
            .Select(link => new
            {
                link.Entity.StructuredLearningEntityId,
                link.Entity.Name,
                link.Entity.EntityType,
                link.Entity.Description,
                link.Entity.Aliases
            })
            .Distinct()
            .ToListAsync(cancellationToken);
        var connectionRows = await readContext.StructuredLearningConnections
            .Where(connection => connection.StructuredLearningDocumentId == section.StructuredLearningDocumentId &&
                                 connection.SourceSectionId == engramId)
            .Include(connection => connection.SourceEntity)
            .Include(connection => connection.TargetEntity)
            .ToListAsync(cancellationToken);
        var entityInputs = entityRows.Select(entity => new StructuredLearningKnowledgeFactEntityInput
        {
            EntityId = entity.StructuredLearningEntityId,
            CanonicalName = entity.Name,
            EntityType = entity.EntityType,
            Description = entity.Description,
            Aliases = entity.Aliases.ToList()
        }).ToList();
        var connectionInputs = connectionRows.Select(connection => new StructuredLearningKnowledgeFactConnectionInput
        {
            ConnectionId = connection.StructuredLearningConnectionId,
            SourceEntityId = connection.SourceEntityId,
            SourceEntityName = connection.SourceEntity.Name,
            TargetEntityId = connection.TargetEntityId,
            TargetEntityName = connection.TargetEntity.Name,
            RelationshipCategory = connection.RelationshipCategory,
            RelationshipLabel = connection.RelationshipLabel,
            Evidence = connection.Evidence,
            EpistemicStatus = connection.EpistemicStatus,
            Confidence = connection.Confidence
        }).ToList();
        var hadPreviousFacts = await readContext.StructuredLearningKnowledgeFacts
            .AnyAsync(fact => fact.EngramId == engramId, cancellationToken);
        var request = new StructuredLearningKnowledgeFactGenerationRequest
        {
            EngramId = engramId,
            EngramName = section.SubchapterTitle,
            Language = language,
            SourceText = section.Text,
            Entities = entityInputs,
            Connections = connectionInputs
        };
        _logger.LogInformation("Knowledge Facts request for Engram {EngramId}: {EntityCount} entities, {ConnectionCount} connections", engramId, entityInputs.Count, connectionInputs.Count);

        StructuredLearningKnowledgeFactGenerationResponse response;
        try
        {
            // Raw (unvalidated): the model occasionally puts one invalid
            // fact (e.g. a discontinuous Composite GraphPath) alongside many
            // otherwise-valid ones. Validating fact-by-fact below drops only
            // the bad apples instead of discarding the whole batch, which
            // previously turned a single bad fact into zero facts for the
            // entire Engram.
            response = await _agent.GenerateRawAsync(request, cancellationToken);
            response.KnowledgeFacts = FilterValidFacts(request, response, entityInputs, connectionInputs, engramId);
            if (response.KnowledgeFacts.Count == 0)
            {
                // Empty is only a hard failure when it would REGRESS an
                // engram that already had persisted facts (protects against
                // a flaky/failed regeneration silently wiping good data).
                // A genuinely sparse engram (e.g. a bare outline entry with
                // no real content, or no Entities/Connections to derive
                // facts from) legitimately has zero facts on its FIRST
                // generation — that's a valid, not an error, outcome.
                if (hadPreviousFacts)
                {
                    throw new InvalidOperationException("Knowledge Fact generation returned no valid KnowledgeFacts; previous facts must be preserved.");
                }
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning(exception, "Knowledge Facts generation/validation failed for Engram {EngramId}; previous facts retained", engramId);
            return Failure(engramId, exception.Message);
        }

        _logger.LogInformation("Knowledge Facts validated for Engram {EngramId}: {FactCount} proposed, {MissingCount} missing graph facts", engramId, response.KnowledgeFacts.Count, response.MissingGraphFacts.Count);
        try
        {
            await using var writeContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            var executionStrategy = writeContext.Database.CreateExecutionStrategy();
            await executionStrategy.ExecuteAsync(async () =>
            {
                await using var transaction = await writeContext.Database.BeginTransactionAsync(cancellationToken);
                var previousFacts = await writeContext.StructuredLearningKnowledgeFacts
                    .Where(fact => fact.EngramId == engramId)
                    .ToListAsync(cancellationToken);
                writeContext.StructuredLearningKnowledgeFacts.RemoveRange(previousFacts);
                foreach (var proposal in response.KnowledgeFacts)
                {
                    var fact = new StructuredLearningKnowledgeFact
                    {
                        KnowledgeFactId = Guid.NewGuid(),
                        EngramId = engramId,
                        Statement = NormalizeWhitespace(proposal.Statement),
                        FactType = proposal.FactType,
                        Importance = proposal.Importance,
                        DerivationType = proposal.DerivationType,
                        EpistemicStatus = proposal.EpistemicStatus,
                        Confidence = proposal.Confidence,
                        Language = language,
                        CreatedAt = DateTime.UtcNow
                    };
                    writeContext.StructuredLearningKnowledgeFacts.Add(fact);
                    foreach (var pathStep in proposal.GraphPath.OrderBy(step => step.StepOrder))
                    {
                        writeContext.StructuredLearningKnowledgeFactGraphPathSteps.Add(new StructuredLearningKnowledgeFactGraphPathStep
                        {
                            KnowledgeFactGraphPathStepId = Guid.NewGuid(),
                            KnowledgeFactId = fact.KnowledgeFactId,
                            ConnectionId = pathStep.ConnectionId,
                            StepOrder = pathStep.StepOrder,
                            CreatedAt = DateTime.UtcNow
                        });
                    }
                }

                await writeContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            });
            var persisted = await LoadPersistedAsync(engramId, cancellationToken);
            var persistedIds = persisted.Select(fact => fact.KnowledgeFactId).ToList();
            _logger.LogInformation("Knowledge Facts transaction committed for Engram {EngramId}: {FactCount} facts", engramId, persisted.Count);
            return new StructuredLearningKnowledgeFactGenerationResult
            {
                EngramId = engramId,
                GenerationSucceeded = true,
                PersistenceSucceeded = true,
                KnowledgeFactsCreated = persisted.Count,
                MissingGraphFactsCount = response.MissingGraphFacts.Count,
                KnowledgeFactIds = persistedIds
            };
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(exception, "Knowledge Facts transaction rolled back for Engram {EngramId}", engramId);
            return Failure(engramId, exception.Message, generationSucceeded: true);
        }
    }

    public async Task<List<StructuredLearningKnowledgeFact>> LoadPersistedAsync(Guid engramId, CancellationToken cancellationToken)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await context.StructuredLearningKnowledgeFacts
            .Where(fact => fact.EngramId == engramId)
            .OrderBy(fact => fact.Importance)
            .ThenBy(fact => fact.KnowledgeFactId)
            .ToListAsync(cancellationToken);
    }

    /// <summary>Validates each proposed fact independently (reusing the same
    /// identity/structural/context checks as the strict path) and keeps only
    /// the ones that pass, logging a warning for every fact it drops. This
    /// prevents one malformed fact from wiping out an entire otherwise-good
    /// generation for the Engram.</summary>
    private List<StructuredLearningGeneratedKnowledgeFact> FilterValidFacts(
        StructuredLearningKnowledgeFactGenerationRequest request,
        StructuredLearningKnowledgeFactGenerationResponse response,
        IReadOnlyList<StructuredLearningKnowledgeFactEntityInput> entities,
        IReadOnlyList<StructuredLearningKnowledgeFactConnectionInput> connections,
        Guid engramId)
    {
        var validFacts = new List<StructuredLearningGeneratedKnowledgeFact>();
        var seenStatements = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var fact in response.KnowledgeFacts)
        {
            var candidate = new StructuredLearningKnowledgeFactGenerationResponse
            {
                EngramId = response.EngramId,
                EngramName = response.EngramName,
                Language = response.Language,
                KnowledgeFacts = [fact]
            };
            try
            {
                ValidateAgainstPersistedContext(request, candidate, entities, connections);
                if (!seenStatements.Add(NormalizeWhitespace(fact.Statement).ToLowerInvariant()))
                {
                    _logger.LogWarning("Knowledge Facts generation for Engram {EngramId} dropped duplicate fact '{Statement}'", engramId, fact.Statement);
                    continue;
                }
                validFacts.Add(fact);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogWarning("Knowledge Facts generation for Engram {EngramId} dropped invalid fact '{Statement}': {Reason}", engramId, fact.Statement, exception.Message);
            }
        }

        return validFacts;
    }

    private static void ValidateAgainstPersistedContext(
        StructuredLearningKnowledgeFactGenerationRequest request,
        StructuredLearningKnowledgeFactGenerationResponse response,
        IReadOnlyList<StructuredLearningKnowledgeFactEntityInput> entities,
        IReadOnlyList<StructuredLearningKnowledgeFactConnectionInput> connections)
    {
        StructuredLearningKnowledgeFactGenerationAgent.ValidateResponse(request, response);
        var entityIds = entities.Select(entity => entity.EntityId).ToHashSet();
        var entityTypes = entities.ToDictionary(entity => entity.EntityId, entity => entity.EntityType);
        var connectionMap = connections.ToDictionary(connection => connection.ConnectionId);
        var sourceText = NormalizeWhitespace(request.SourceText);
        foreach (var fact in response.KnowledgeFacts)
        {
            if (fact.SourceEntityIds.Any(id => !entityIds.Contains(id)))
            {
                throw new InvalidOperationException($"Fact '{fact.Statement}' references an entity outside the Engram.");
            }
            foreach (var evidence in fact.Evidence)
            {
                var normalizedEvidence = NormalizeWhitespace(evidence);
                if (!sourceText.Contains(normalizedEvidence, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException($"Fact '{fact.Statement}' has evidence not found in the Engram SourceText.");
                }
            }
            var pathIds = fact.GraphPath.Select(step => step.ConnectionId).ToList();
            var pathEntityIds = pathIds
                .SelectMany(pathId => new[] { connectionMap[pathId].SourceEntityId, connectionMap[pathId].TargetEntityId })
                .Distinct()
                .ToHashSet();
            if (!fact.SourceEntityIds.ToHashSet().SetEquals(pathEntityIds))
            {
                throw new InvalidOperationException($"Fact '{fact.Statement}' has SourceEntityIds inconsistent with its GraphPath.");
            }
            if (fact.SourceEntityIds.Count > 0 && fact.SourceEntityIds.All(id => IsEducationalContainer(entityTypes[id])))
            {
                throw new InvalidOperationException($"Fact '{fact.Statement}' is based only on an educational container.");
            }
            foreach (var pathId in pathIds)
            {
                if (!connectionMap.ContainsKey(pathId))
                {
                    throw new InvalidOperationException($"Fact '{fact.Statement}' references a connection outside the Engram.");
                }
                var connection = connectionMap[pathId];
                if (connection.SourceEntityId == Guid.Empty || connection.TargetEntityId == Guid.Empty)
                {
                    throw new InvalidOperationException($"Fact '{fact.Statement}' references an invalid connection.");
                }
            }
        }
    }

    private static string NormalizeWhitespace(string value) =>
        string.Join(' ', value.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static string ResolveLanguage(string? snapshotJson, ILogger logger, Guid engramId)
    {
        if (!string.IsNullOrWhiteSpace(snapshotJson))
        {
            try
            {
                var snapshot = JsonSerializer.Deserialize<JsonElement>(snapshotJson);
                if (snapshot.TryGetProperty("LanguageCode", out var language) &&
                    !string.IsNullOrWhiteSpace(language.GetString()))
                {
                    return language.GetString()!;
                }
            }
            catch (JsonException) { }
        }

        logger.LogWarning("No persisted language found for Engram {EngramId}; using temporary fallback 'en'", engramId);
        return "en";
    }

    private static bool IsEducationalContainer(string entityType) =>
        NormalizeWhitespace(entityType).Replace(" ", string.Empty, StringComparison.OrdinalIgnoreCase) is
            "learningmodule" or "section" or "activity" or "engram";

    private static StructuredLearningKnowledgeFactGenerationResult Failure(Guid engramId, string error, bool generationSucceeded = false) => new()
    {
        EngramId = engramId,
        GenerationSucceeded = generationSucceeded,
        PersistenceSucceeded = false,
        Errors = [error]
    };
}
