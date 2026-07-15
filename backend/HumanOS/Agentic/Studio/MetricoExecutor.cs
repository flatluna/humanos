using HumanOS.Agents.Studio;
using Microsoft.Agents.AI.Workflows;

namespace HumanOS.Agentic.Studio;

/// <summary>Workflow executor wrapping <see cref="MetricoAgent"/>. Feeds
/// its result to <see cref="ModuleCompletionRouterExecutor"/>, closing the
/// per-module loop.</summary>
internal sealed class MetricoExecutor : Executor<ModuleScriptWorkItem, CompletedModuleResult>
{
    private readonly MetricoAgent _agent;

    public MetricoExecutor(MetricoAgent agent) : base("Metrico")
    {
        _agent = agent;
    }

    public override async ValueTask<CompletedModuleResult> HandleAsync(
        ModuleScriptWorkItem input,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        var state = await context.ReadStateAsync<PipelineState>(
            input.Item.BlueprintId.ToString(),
            scopeName: ArquitectoExecutor.PipelineStateScope);

        // Paso 7 (2026-07-14): the try/catch now wraps AssignMetricsAsync
        // itself, not just CompletedModuleValidator below. Previously, a
        // structural contract violation caught INSIDE AssignMetricsAsync
        // by MetricVerificationValidator (e.g. "Recall must occur before
        // instruction") threw BEFORE this method got a chance to catch
        // anything, crashing the whole workflow run instead of becoming a
        // retryable Failed module — a real, previously-unfixed bug (see
        // HUMAN-OS-STUDIO.md §16.4). Both validators' violations now
        // become ModuleProcessingStatus.Failed uniformly, so
        // ModuleCompletionRouterExecutor's bounded retry can act on them
        // exactly like a pedagogical RequiresRevision outcome.
        MetricoResult? result = null;
        CompletedModule completedModule;
        try
        {
            result = await _agent.AssignMetricsAsync(
                input.Item.Layer, input.Item.Module, input.Script, cancellationToken);

            // Paso 4 (2026-07-14): MetricoAgent now verifies ONLY the
            // module's own approved TargetMetric with precise evidence —
            // the old "always force-insert the TargetMetric" and
            // "closed-scope secondary metrics" safety nets are gone
            // entirely, since there is no secondary-metrics list left to
            // filter. Metrics contains the TargetMetric if (and only if)
            // MetricVerificationValidator confirmed it was genuinely
            // reported Verified.

            // Paso 5 (2026-07-14): a module having "run through" the
            // Instructor and Métrico does NOT mean it is complete — the
            // final, cross-agent gate decides Verified vs RequiresRevision
            // vs Failed.
            completedModule = new CompletedModule
            {
                Module = input.Item.Module,
                Script = input.Script,
                Metrics = result.Assignment
            };

            completedModule.Status = CompletedModuleValidator.Validate(
                input.Item.Module, input.Script, result.Assignment.Verification, input.Item.Layer);
        }
        catch (InvalidOperationException ex)
        {
            // Structural contract violation between agents (e.g.
            // TargetMetric changed, Recall not before instruction) — a bug,
            // not a legitimate pedagogical judgment. See
            // CompletedModuleValidator's remarks for the Failed/
            // RequiresRevision distinction.
            completedModule = new CompletedModule
            {
                Module = input.Item.Module,
                Script = input.Script,
                Metrics = result?.Assignment ?? new ModuleMetricAssignment(),
                Status = ModuleProcessingStatus.Failed,
                FailureReason = ex.Message
            };
        }

        // Paso 4: record the Métrico's token usage for this module into
        // the shared per-run state (same pattern as the Instructor's, see
        // InstructorExecutor.cs) — secondary/observability concern only.
        // Only available if the LLM call itself succeeded (result is not
        // null); a validator exception thrown before AssignMetricsAsync
        // could return means this attempt's token usage isn't recorded.
        if (state is not null && result is not null)
        {
            state.TokenUsage.Add(result.TokenUsage);
            await context.QueueStateUpdateAsync(
                input.Item.BlueprintId.ToString(),
                state,
                scopeName: ArquitectoExecutor.PipelineStateScope);
        }

        // Progress event — reflects the module's REAL outcome, not just
        // "the agents finished running" (see ModuleProcessingStatus).
        switch (completedModule.Status)
        {
            case ModuleProcessingStatus.Verified:
                await context.AddEventAsync(
                    new ModuleVerifiedEvent(input.Item.Module.ModuleId, input.Item.Module.Title), cancellationToken);
                break;
            case ModuleProcessingStatus.RequiresRevision:
                await context.AddEventAsync(
                    new ModuleRequiresRevisionEvent(
                        input.Item.Module.ModuleId, input.Item.Module.Title, completedModule.Metrics.Rationale),
                    cancellationToken);
                break;
            default:
                await context.AddEventAsync(
                    new ModuleProcessingFailedEvent(
                        input.Item.Module.ModuleId, input.Item.Module.Title, completedModule.FailureReason ?? "Unknown error."),
                    cancellationToken);
                break;
        }

        return new CompletedModuleResult
        {
            BlueprintId = input.Item.BlueprintId,
            Layer = input.Item.Layer,
            Attempt = input.Item.Attempt,
            Completed = completedModule
        };
    }
}
