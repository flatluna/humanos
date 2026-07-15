using HumanOS.Agents.Studio;
using HumanOS.Data;
using HumanOS.Models.Capabilities;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Data.SqlTypes;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.Agentic.Studio;

/// <summary>
/// Terminal step of the Human OS Studio pipeline. On GATE 2 approval,
/// persists the approved <see cref="CapabilityPackage"/> as
/// Capability/CapabilityLevel/CapabilityModule/CapabilityModuleMetric rows,
/// chunks + embeds each module's script and the consolidated
/// TutorKnowledgeBase into CapabilityKnowledgeChunk (Azure SQL native
/// VECTOR column, via the configured embeddings deployment), then yields
/// the package as the workflow's final output.
/// </summary>
internal sealed class PublishExecutor : Executor<Gate2Outcome, CapabilityPackage>
{
    private readonly IDbContextFactory<HumanOsDbContext> _dbContextFactory;
    private readonly CapabilityEmbeddingService _embeddingService;

    public PublishExecutor(
        IDbContextFactory<HumanOsDbContext> dbContextFactory,
        CapabilityEmbeddingService embeddingService) : base("Publish")
    {
        _dbContextFactory = dbContextFactory;
        _embeddingService = embeddingService;
    }

    public override async ValueTask<CapabilityPackage> HandleAsync(
        Gate2Outcome outcome,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        var package = outcome.ApprovedPackage
            ?? throw new ArgumentException("PublishExecutor only handles routed messages with ApprovedPackage set.");

        var state = await context.ReadStateAsync<PipelineState>(
            package.BlueprintId.ToString(),
            scopeName: ArquitectoExecutor.PipelineStateScope);

        if (state is null)
        {
            throw new InvalidOperationException(
                $"No pipeline state found for blueprint '{package.BlueprintId}'.");
        }

        await PersistAsync(context, state.Blueprint, state.CapabilityDomainId, package, cancellationToken);

        // Executor<TInput> (no declared TOutput) cannot YieldOutputAsync an
        // arbitrary type — the protocol only declares yieldable types that
        // match TOutput. Declaring CapabilityPackage as TOutput here (even
        // though there are no further edges from Publish) registers it as
        // yieldable, which WithOutputFrom(publish, ...) needs.
        await context.YieldOutputAsync(package, cancellationToken);
        return package;
    }

    private async Task PersistAsync(
        IWorkflowContext context,
        CapabilityBlueprint blueprint,
        Guid capabilityDomainId,
        CapabilityPackage package,
        CancellationToken cancellationToken)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        await context.AddEventAsync(new PublishTaskProgressEvent("Capability", "Processing"), cancellationToken);

        var capability = new Capability
        {
            CapabilityId = Guid.NewGuid(),
            CapabilityDomainId = capabilityDomainId,
            Code = BuildUniqueCode(package.CapabilityName),
            Name = package.CapabilityName,
            Description = blueprint.Goal,
            IsActive = true
        };

        await context.AddEventAsync(new PublishTaskProgressEvent("Capability", "Completed"), cancellationToken);
        await context.AddEventAsync(new PublishTaskProgressEvent("Levels", "Processing"), cancellationToken);

        // Surface the persisted id back to the caller (used by the
        // frontend to deep-link to /courses/{capabilityId} once Published).
        package.CapabilityId = capability.CapabilityId;

        // Maps each module's ModuleId (in-memory identity, still intact
        // since the blueprint's ModuleSkeleton instances are threaded by
        // reference through the whole in-process run, never re-serialized)
        // to the CapabilityLevel row it belongs to.
        var levelByModuleId = new Dictionary<Guid, CapabilityLevel>();

        for (var levelIndex = 0; levelIndex < blueprint.Levels.Count; levelIndex++)
        {
            var levelBlueprint = blueprint.Levels[levelIndex];
            var level = new CapabilityLevel
            {
                CapabilityLevelId = Guid.NewGuid(),
                CapabilityId = capability.CapabilityId,
                Layer = levelBlueprint.Layer,
                SortOrder = levelIndex,
                Title = levelBlueprint.Title,
                HumanTransformation = levelBlueprint.HumanTransformation
            };
            capability.Levels.Add(level);

            foreach (var module in levelBlueprint.Modules)
            {
                levelByModuleId[module.ModuleId] = level;
            }
        }

        await context.AddEventAsync(new PublishTaskProgressEvent("Levels", "Completed"), cancellationToken);
        await context.AddEventAsync(new PublishTaskProgressEvent("Modules", "Processing"), cancellationToken);
        await context.AddEventAsync(new PublishTaskProgressEvent("Metrics", "Processing"), cancellationToken);

        var knowledgeChunks = new List<CapabilityKnowledgeChunk>();
        await context.AddEventAsync(new PublishTaskProgressEvent("KnowledgeChunks", "Processing"), cancellationToken);

        for (var moduleIndex = 0; moduleIndex < package.Modules.Count; moduleIndex++)
        {
            var completed = package.Modules[moduleIndex];

            if (!levelByModuleId.TryGetValue(completed.Module.ModuleId, out var level))
            {
                throw new InvalidOperationException(
                    $"Completed module '{completed.Module.Title}' does not match any level in the blueprint.");
            }

            var capabilityModule = new CapabilityModule
            {
                CapabilityModuleId = Guid.NewGuid(),
                CapabilityLevelId = level.CapabilityLevelId,
                SortOrder = moduleIndex,
                Title = completed.Module.Title,
                Description = completed.Module.Description,
                Type = completed.Module.Type,
                Script = completed.Script.Script,
                MetricRationale = completed.Metrics.Rationale
            };
            level.Modules.Add(capabilityModule);

            foreach (var metric in completed.Metrics.Metrics)
            {
                capabilityModule.Metrics.Add(new CapabilityModuleMetric
                {
                    CapabilityModuleId = capabilityModule.CapabilityModuleId,
                    Metric = metric
                });
            }

            // Paso 6 (2026-07-14): persist the FULL evidence-based
            // verification (not just the bare TargetMetric list above) —
            // see HUMAN-OS-STUDIO.md §15. Append-only: a future retry
            // would add another row here rather than overwrite this one.
            var verification = completed.Metrics.Verification;
            var verificationRow = new CapabilityModuleVerification
            {
                CapabilityModuleVerificationId = Guid.NewGuid(),
                CapabilityModuleId = capabilityModule.CapabilityModuleId,
                TargetMetric = verification.TargetMetric,
                Status = verification.Status,
                Evidence = verification.Evidence,
                EvidenceLocation = verification.EvidenceLocation,
                Explanation = verification.Explanation,
                RecallStatus = verification.Recall.Status,
                RecallEvidence = verification.Recall.Evidence,
                RecallEvidenceLocation = verification.Recall.EvidenceLocation,
                RecallOccursBeforeInstruction = verification.Recall.OccursBeforeInstruction
            };

            for (var criterionIndex = 0; criterionIndex < verification.SuccessCriteriaResults.Count; criterionIndex++)
            {
                var result = verification.SuccessCriteriaResults[criterionIndex];
                verificationRow.SuccessCriteriaResults.Add(new CapabilityModuleSuccessCriterionResult
                {
                    CapabilityModuleSuccessCriterionResultId = Guid.NewGuid(),
                    CapabilityModuleVerificationId = verificationRow.CapabilityModuleVerificationId,
                    SortOrder = criterionIndex,
                    Criterion = result.Criterion,
                    IsSatisfied = result.IsSatisfied,
                    Evidence = result.Evidence
                });
            }

            capabilityModule.Verifications.Add(verificationRow);

            var moduleChunks = TextChunker.Chunk(completed.Script.Script);
            for (var chunkIndex = 0; chunkIndex < moduleChunks.Count; chunkIndex++)
            {
                var embedding = await _embeddingService.GenerateEmbeddingAsync(
                    moduleChunks[chunkIndex], cancellationToken);

                knowledgeChunks.Add(new CapabilityKnowledgeChunk
                {
                    CapabilityKnowledgeChunkId = Guid.NewGuid(),
                    CapabilityId = capability.CapabilityId,
                    CapabilityModuleId = capabilityModule.CapabilityModuleId,
                    SortOrder = chunkIndex,
                    Content = moduleChunks[chunkIndex],
                    Embedding = new SqlVector<float>(embedding)
                });
            }
        }

        await context.AddEventAsync(new PublishTaskProgressEvent("Modules", "Completed"), cancellationToken);
        await context.AddEventAsync(new PublishTaskProgressEvent("Metrics", "Completed"), cancellationToken);
        await context.AddEventAsync(new PublishTaskProgressEvent("KnowledgeChunks", "Completed"), cancellationToken);
        await context.AddEventAsync(new PublishTaskProgressEvent("Embeddings", "Processing"), cancellationToken);

        // Capability-wide overview chunks (CapabilityModuleId = null), for
        // tutor questions that span multiple modules.
        var overviewChunks = TextChunker.Chunk(package.TutorKnowledgeBase);
        for (var chunkIndex = 0; chunkIndex < overviewChunks.Count; chunkIndex++)
        {
            var embedding = await _embeddingService.GenerateEmbeddingAsync(
                overviewChunks[chunkIndex], cancellationToken);

            knowledgeChunks.Add(new CapabilityKnowledgeChunk
            {
                CapabilityKnowledgeChunkId = Guid.NewGuid(),
                CapabilityId = capability.CapabilityId,
                CapabilityModuleId = null,
                SortOrder = chunkIndex,
                Content = overviewChunks[chunkIndex],
                Embedding = new SqlVector<float>(embedding)
            });
        }

        db.Capabilities.Add(capability);
        db.CapabilityKnowledgeChunks.AddRange(knowledgeChunks);

        await db.SaveChangesAsync(cancellationToken);

        // Only now, after the DB commit actually succeeds, is Embeddings
        // truly "done" — if SaveChangesAsync throws (e.g. the known Azure
        // SQL serverless auto-pause transient timeout), this event never
        // fires and the frontend correctly keeps showing this task stuck
        // at "Processing" instead of a false "Completed".
        await context.AddEventAsync(new PublishTaskProgressEvent("Embeddings", "Completed"), cancellationToken);
    }

    private static string BuildUniqueCode(string capabilityName)
    {
        var slug = new string(capabilityName
            .Trim()
            .ToLowerInvariant()
            .Select(c => char.IsLetterOrDigit(c) ? c : '-')
            .ToArray());

        while (slug.Contains("--", StringComparison.Ordinal))
        {
            slug = slug.Replace("--", "-");
        }

        slug = slug.Trim('-');

        if (string.IsNullOrEmpty(slug))
        {
            slug = "capability";
        }

        if (slug.Length > 150)
        {
            slug = slug[..150];
        }

        var suffix = Guid.NewGuid().ToString("N")[..8];
        return $"{slug}-{suffix}";
    }
}
