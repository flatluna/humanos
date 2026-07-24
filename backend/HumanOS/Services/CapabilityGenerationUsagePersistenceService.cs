using HumanOS.Agents.Studio;
using HumanOS.Data;
using HumanOS.Models.Capabilities.Graph;

namespace HumanOS.Services;

/// <summary>
/// Persists a pipeline run's in-memory <see cref="AgentTokenUsage"/> list as
/// real <see cref="CapabilityGenerationUsage"/> rows — the SQL backing store
/// for the cost-per-capability dashboard (2026-07-23). See
/// <see cref="CapabilityGenerationUsage"/>'s doc comment for why this exists
/// (token usage was previously computed but only ever lived in-memory).
///
/// Called ONCE, best-effort, near the very end of
/// <see cref="PdfCapabilityGraphPipelineService"/>'s run — a failure here
/// must never affect the already-completed Capability/graph, so callers are
/// expected to wrap this in their own try/catch (matching every other
/// best-effort step in that pipeline).
/// </summary>
public sealed class CapabilityGenerationUsagePersistenceService
{
    public async Task PersistAsync(
        HumanOsDbContext dbContext,
        Guid capabilityId,
        Guid capabilityGraphId,
        IReadOnlyList<AgentTokenUsage> tokenUsage,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(tokenUsage);

        if (tokenUsage.Count == 0)
        {
            return;
        }

        foreach (var usage in tokenUsage)
        {
            dbContext.CapabilityGenerationUsages.Add(new CapabilityGenerationUsage
            {
                CapabilityGenerationUsageId = Guid.NewGuid(),
                CapabilityId = capabilityId,
                CapabilityGraphId = capabilityGraphId,
                AgentName = usage.AgentName,
                ModelName = string.IsNullOrWhiteSpace(usage.ModelName) ? null : usage.ModelName,
                SectionLabel = usage.ModuleId,
                InputTokens = usage.InputTokens,
                OutputTokens = usage.OutputTokens,
                CachedInputTokens = usage.CachedInputTokens,
                CreatedDate = DateTime.UtcNow,
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
