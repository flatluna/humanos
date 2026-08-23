using HumanOS.Data;
using HumanOS.Models.Capabilities.Studio;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.Services;

/// <summary>
/// Persists one <see cref="StructuredLearningGenerationUsage"/> row per
/// LLM/embedding call across the Structured Learning pipeline (Extraction,
/// Knowledge Graph, Pedagogy) — mirrors
/// <see cref="CapabilityGenerationUsagePersistenceService"/>'s design.
/// Best-effort by convention: every call site wraps this in its own
/// try/catch so a usage-logging failure never blocks the actual pipeline
/// step it's measuring.
/// </summary>
public sealed class StructuredLearningGenerationUsageService
{
    private readonly IDbContextFactory<HumanOsDbContext> _dbContextFactory;

    public StructuredLearningGenerationUsageService(IDbContextFactory<HumanOsDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task RecordAsync(
        Guid documentId,
        Guid? sectionId,
        string stepName,
        string agentName,
        string? modelName,
        int inputTokens,
        int outputTokens,
        int cachedInputTokens,
        long elapsedMilliseconds,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        dbContext.StructuredLearningGenerationUsages.Add(new StructuredLearningGenerationUsage
        {
            StructuredLearningGenerationUsageId = Guid.NewGuid(),
            StructuredLearningDocumentId = documentId,
            StructuredLearningDocumentSectionId = sectionId,
            StepName = stepName,
            AgentName = agentName,
            ModelName = modelName,
            InputTokens = inputTokens,
            OutputTokens = outputTokens,
            CachedInputTokens = cachedInputTokens,
            ElapsedMilliseconds = elapsedMilliseconds,
            CreatedDate = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
