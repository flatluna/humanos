using HumanOS.Agents.Studio;
using Microsoft.Agents.AI.Workflows;

namespace HumanOS.Agentic.Studio;

/// <summary>
/// Replaces the old SEQUENTIAL module loop (ModuleQueueInitializerExecutor
/// -&gt; InstructorExecutor -&gt; MetricoExecutor -&gt; ModuleCompletionRouterExecutor,
/// removed 2026-07-16 — see git history for the previous shape) with
/// BOUNDED-CONCURRENCY parallel module generation: every module in the
/// approved blueprint is processed concurrently (up to
/// <see cref="MaxConcurrency"/> at a time) instead of one at a time. Each
/// module still goes through the EXACT same Instructor -&gt; Métrico -&gt;
/// bounded-retry sequence as before (see <see cref="ProcessModuleAsync"/>)
/// — only the ACROSS-module scheduling changed, not the per-module
/// pedagogical logic, validation, or progress events.
///
/// Why not the Agent Framework's AddFanOutEdge/AddFanInBarrierEdge graph
/// primitives? Investigated (2026-07-16, user asked "es posible crear los
/// cursos en paralelo?"): AddFanOutEdge routes messages to a FIXED set of
/// DISTINCT target executor nodes, and AddFanInBarrierEdge waits for every
/// source to emit "at least one" message before releasing — neither
/// cleanly models "a variable-length queue of N modules, processed by a
/// bounded worker pool, waiting for ALL N (not just K lanes) to finish"
/// without a much bigger rewrite (per-lane sub-queues + a merge step).
/// Plain Task.WhenAll + SemaphoreSlim inside a single executor achieves the
/// identical practical outcome (parallel LLM calls, bounded concurrency)
/// with far less risk to this already-relied-upon pipeline (an active
/// capability-creation run was mid-flight in the Studio UI while this was
/// implemented).
///
/// IMPORTANT ordering guarantee: PublishExecutor assigns each
/// CapabilityModule.SortOrder from its POSITION in
/// AllModulesCompleted.Modules (not from any separately-stored index) —
/// so the curriculum's intended sequence (Foundation module 1 before
/// module 2, etc.) depends on that list staying in the blueprint's
/// original module order. Task.WhenAll preserves the input sequence's
/// positions in its result array regardless of which task finishes first,
/// so iterating <c>orderedWork</c> and awaiting the same-shaped
/// Task.WhenAll over it keeps this invariant automatically — no separate
/// re-sort needed, but this is the reason the two lists must stay
/// positionally aligned rather than, say, collecting results into a
/// concurrent bag.
/// </summary>
internal sealed class ParallelModuleGenerationExecutor : Executor<Gate1Outcome, ModuleRouterOutput>
{
    /// <summary>Max modules processed concurrently. Bounded deliberately —
    /// unbounded fan-out risks Azure OpenAI TPM/RPM rate-limit errors.
    /// Raised from 3 to 5 (2026-07-16, explicit user request) after live
    /// testing showed 3-concurrent runs completing without rate-limit
    /// errors — revisit downward if 429s start appearing in practice.</summary>
    internal const int MaxConcurrency = 5;

    /// <summary>Same bound as the old ModuleCompletionRouterExecutor.MaxRetries
    /// — up to 1 + MaxRetries total Instructor attempts before a
    /// non-Verified outcome is accepted as final for that module.</summary>
    internal const int MaxRetries = 2;

    private readonly InstructorAgent _instructor;
    private readonly MetricoAgent _metrico;

    public ParallelModuleGenerationExecutor(InstructorAgent instructor, MetricoAgent metrico)
        : base("ParallelModuleGeneration")
    {
        _instructor = instructor;
        _metrico = metrico;
    }

    public override async ValueTask<ModuleRouterOutput> HandleAsync(
        Gate1Outcome outcome,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        var blueprint = outcome.ApprovedBlueprint
            ?? throw new ArgumentException(
                "ParallelModuleGenerationExecutor only handles routed messages with ApprovedBlueprint set.");

        var orderedWork = blueprint.Levels
            .SelectMany(level => level.Modules.Select(module => new PendingModuleRef { Layer = level.Layer, Module = module }))
            .ToList();

        await context.AddEventAsync(new ModuleQueueStartedEvent(orderedWork.Count), cancellationToken);

        if (orderedWork.Count == 0)
        {
            return new ModuleRouterOutput
            {
                Completed = new AllModulesCompleted { BlueprintId = blueprint.BlueprintId, Modules = [] }
            };
        }

        var state = await context.ReadStateAsync<PipelineState>(
            blueprint.BlueprintId.ToString(), scopeName: ArquitectoExecutor.PipelineStateScope);
        var corpus = state?.CuratedCorpus ?? new CuratedCorpus();

        using var concurrencyGate = new SemaphoreSlim(MaxConcurrency);
        using var tokenUsageGate = new SemaphoreSlim(1, 1);

        async Task<CompletedModule> ProcessWithLimitAsync(PendingModuleRef pending)
        {
            await concurrencyGate.WaitAsync(cancellationToken);
            try
            {
                return await ProcessModuleAsync(blueprint.BlueprintId, pending, corpus, context, tokenUsageGate, cancellationToken);
            }
            finally
            {
                concurrencyGate.Release();
            }
        }

        // Task.WhenAll over an ordered input sequence yields a result array
        // in the SAME order as the input tasks were created, regardless of
        // completion order — see the class remarks on why this matters.
        // ModuleFinalizedEvent (the ONLY event that advances the frontend's
        // "N de Total" progress counter) is raised INSIDE ProcessModuleAsync
        // as soon as EACH module's own outcome is final — not batched here
        // after every module finishes, which would freeze progress at 0
        // until the very last module completes and defeat the whole point
        // of a live progress counter.
        var completedModules = await Task.WhenAll(orderedWork.Select(ProcessWithLimitAsync));

        return new ModuleRouterOutput
        {
            Completed = new AllModulesCompleted { BlueprintId = blueprint.BlueprintId, Modules = [.. completedModules] }
        };
    }

    /// <summary>Runs one module through Instructor -&gt; Métrico, retrying
    /// (same module, same agent, with Métrico's feedback attached) up to
    /// <see cref="MaxRetries"/> times if it doesn't reach Verified — the
    /// exact same per-module logic the old InstructorExecutor/MetricoExecutor/
    /// ModuleCompletionRouterExecutor trio used to implement across three
    /// separate workflow steps.</summary>
    private async Task<CompletedModule> ProcessModuleAsync(
        Guid blueprintId,
        PendingModuleRef pending,
        CuratedCorpus corpus,
        IWorkflowContext context,
        SemaphoreSlim tokenUsageGate,
        CancellationToken cancellationToken)
    {
        RevisionContext? revision = null;

        for (var attempt = 0; ; attempt++)
        {
            await context.AddEventAsync(
                new ModuleScriptStartedEvent(pending.Module.ModuleId, pending.Module.Title), cancellationToken);

            var instructorResult = await _instructor.WriteScriptAsync(
                pending.Layer, pending.Module, corpus, revision, cancellationToken);

            await RecordTokenUsageAsync(blueprintId, instructorResult.TokenUsage, context, tokenUsageGate, cancellationToken);

            // Paso 7 (2026-07-14, preserved unchanged): a structural
            // contract violation caught by MetricVerificationValidator
            // INSIDE AssignMetricsAsync becomes ModuleProcessingStatus.Failed
            // uniformly with a pedagogical RequiresRevision outcome, rather
            // than crashing the whole run.
            MetricoResult? metricoResult = null;
            CompletedModule completedModule;
            try
            {
                metricoResult = await _metrico.AssignMetricsAsync(
                    pending.Layer, pending.Module, instructorResult.Script, cancellationToken);

                completedModule = new CompletedModule
                {
                    Module = pending.Module,
                    Script = instructorResult.Script,
                    Metrics = metricoResult.Assignment
                };

                completedModule.Status = CompletedModuleValidator.Validate(
                    pending.Module, instructorResult.Script, metricoResult.Assignment.Verification, pending.Layer);
            }
            catch (InvalidOperationException ex)
            {
                completedModule = new CompletedModule
                {
                    Module = pending.Module,
                    Script = instructorResult.Script,
                    Metrics = metricoResult?.Assignment ?? new ModuleMetricAssignment(),
                    Status = ModuleProcessingStatus.Failed,
                    FailureReason = ex.Message
                };
            }

            if (metricoResult is not null)
            {
                await RecordTokenUsageAsync(blueprintId, metricoResult.TokenUsage, context, tokenUsageGate, cancellationToken);
            }

            switch (completedModule.Status)
            {
                case ModuleProcessingStatus.Verified:
                    await context.AddEventAsync(
                        new ModuleVerifiedEvent(pending.Module.ModuleId, pending.Module.Title), cancellationToken);
                    break;
                case ModuleProcessingStatus.RequiresRevision:
                    await context.AddEventAsync(
                        new ModuleRequiresRevisionEvent(pending.Module.ModuleId, pending.Module.Title, completedModule.Metrics.Rationale),
                        cancellationToken);
                    break;
                default:
                    await context.AddEventAsync(
                        new ModuleProcessingFailedEvent(
                            pending.Module.ModuleId, pending.Module.Title, completedModule.FailureReason ?? "Unknown error."),
                        cancellationToken);
                    break;
            }

            if (completedModule.Status == ModuleProcessingStatus.Verified || attempt >= MaxRetries)
            {
                // This outcome is FINAL for this module (Verified, or
                // retries exhausted) — count it toward progress exactly
                // once, here, never from the per-attempt events above (an
                // attempt that's about to be retried, handled below, must
                // NOT double/triple-count).
                await context.AddEventAsync(
                    new ModuleFinalizedEvent(pending.Module.ModuleId, pending.Module.Title), cancellationToken);
                return completedModule;
            }

            var feedback = completedModule.Status == ModuleProcessingStatus.Failed
                ? completedModule.FailureReason ?? "Structural contract violation reported with no message."
                : completedModule.Metrics.Rationale;

            await context.AddEventAsync(
                new ModuleRetryingEvent(pending.Module.ModuleId, pending.Module.Title, attempt + 1, feedback), cancellationToken);

            revision = new RevisionContext { PreviousScript = instructorResult.Script, Feedback = feedback };
        }
    }

    /// <summary>Appends one agent call's token usage to the shared
    /// per-blueprint <see cref="PipelineState.TokenUsage"/> list
    /// (observability only). Every concurrent module task shares the SAME
    /// PipelineState key (blueprintId), so the read-modify-write round trip
    /// is serialized behind <paramref name="tokenUsageGate"/> — otherwise
    /// concurrent modules would race and silently lose each other's
    /// entries. This is the ONLY shared mutable state this executor
    /// touches; unlike the old ModuleQueueState, there is no per-module
    /// pending/completed queue left to race on at all.</summary>
    private static async Task RecordTokenUsageAsync(
        Guid blueprintId,
        AgentTokenUsage tokenUsage,
        IWorkflowContext context,
        SemaphoreSlim tokenUsageGate,
        CancellationToken cancellationToken)
    {
        await tokenUsageGate.WaitAsync(cancellationToken);
        try
        {
            var state = await context.ReadStateAsync<PipelineState>(
                blueprintId.ToString(), scopeName: ArquitectoExecutor.PipelineStateScope);

            if (state is null)
            {
                return;
            }

            state.TokenUsage.Add(tokenUsage);
            await context.QueueStateUpdateAsync(blueprintId.ToString(), state, scopeName: ArquitectoExecutor.PipelineStateScope);
        }
        finally
        {
            tokenUsageGate.Release();
        }
    }
}
