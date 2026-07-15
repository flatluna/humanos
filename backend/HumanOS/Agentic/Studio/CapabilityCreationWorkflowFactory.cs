using HumanOS.Agents.Studio;
using HumanOS.Data;
using Microsoft.Agents.AI.Workflows;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.Agentic.Studio;

/// <summary>
/// Builds the Human OS Studio capability-creation Workflow graph:
///
///   Curador -&gt; Arquitecto -&gt; [GATE 1] -&gt; Gate1Decision
///     -&gt; (conditional) -&gt; ModuleQueueInitializer -&gt; (conditional) -&gt; Instructor
///     -&gt; Metrico -&gt; ModuleCompletionRouter -&gt; (conditional, loops back to
///     Instructor for the NEXT module, or the SAME module on a bounded
///     revision retry — see ModuleCompletionRouterExecutor.MaxRetries,
///     Paso 7 2026-07-14) -&gt; [&gt;=85% Verified?] -&gt; Experiencia -&gt; [GATE 2]
///     -&gt; Gate2Decision -&gt; (conditional) -&gt; Publish
///   (Gate1Decision/Gate2Decision also conditionally route to
///   Gate1RejectionExecutor/Gate2RejectionExecutor on rejection. ModuleQueueInitializer/
///   ModuleCompletionRouter route to ModuleRevisionRequiredExecutor instead
///   of Experiencia when generation is done but FEWER than
///   ModuleCompletionGate.MinVerifiedRatio of modules are Verified — see
///   ModuleCompletionGate, Paso 5 2026-07-14 (threshold relaxed Paso 7):
///   "all modules generated" is NOT "capability ready to assemble", but
///   100% Verified is also not required to publish.)
///
/// Instructor/Metrico run sequentially, one module at a time, through the
/// two module-router executors and conditional edges (same pattern as the
/// Agent Framework's conditional-edges sample) — prepared so the module
/// loop can later become a real fan-out/fan-in (AddFanOutEdge/
/// AddFanInBarrierEdge) without changing the agents.
///
/// A fresh graph (with fresh executor instances) must be built per
/// capability-creation run.
/// </summary>
internal static class CapabilityCreationWorkflowFactory
{
    public static Workflow Build(
        CuradorAgent curador,
        ArquitectoAgent arquitecto,
        InstructorAgent instructor,
        MetricoAgent metrico,
        ExperienciaAgent experiencia,
        IDbContextFactory<HumanOsDbContext> dbContextFactory,
        CapabilityEmbeddingService embeddingService)
    {
        var curadorExecutor = new CuradorExecutor(curador);
        var arquitectoExecutor = new ArquitectoExecutor(arquitecto);
        var gate1 = RequestPort.Create<CapabilityBlueprint, GateDecision>("Gate1-ArchitectReview");
        var gate1Decision = new Gate1DecisionExecutor();
        var gate1Rejection = new Gate1RejectionExecutor();
        var moduleQueueInitializer = new ModuleQueueInitializerExecutor();
        var instructorExecutor = new InstructorExecutor(instructor);
        var metricoExecutor = new MetricoExecutor(metrico);
        var moduleCompletionRouter = new ModuleCompletionRouterExecutor();
        var moduleRevisionRequired = new ModuleRevisionRequiredExecutor();
        var experienciaExecutor = new ExperienciaExecutor(experiencia);
        var gate2 = RequestPort.Create<CapabilityPackage, GateDecision>("Gate2-FinalReview");
        var gate2Decision = new Gate2DecisionExecutor();
        var gate2Rejection = new Gate2RejectionExecutor();
        var publish = new PublishExecutor(dbContextFactory, embeddingService);

        var builder = new WorkflowBuilder(curadorExecutor);
        builder
            .AddEdge(curadorExecutor, arquitectoExecutor)
            .AddEdge(arquitectoExecutor, gate1)
            .AddEdge(gate1, gate1Decision)
            .AddEdge<Gate1Outcome>(gate1Decision, moduleQueueInitializer, condition: IsGate1Approved)
            .AddEdge<Gate1Outcome>(gate1Decision, gate1Rejection, condition: IsGate1Rejected)
            .AddEdge<ModuleRouterOutput>(moduleQueueInitializer, instructorExecutor, condition: HasNextModule)
            .AddEdge<ModuleRouterOutput>(moduleQueueInitializer, experienciaExecutor, condition: ModuleCompletionGate.MeetsPublishThreshold)
            .AddEdge<ModuleRouterOutput>(moduleQueueInitializer, moduleRevisionRequired, condition: ModuleCompletionGate.RequiresRevision)
            .AddEdge(instructorExecutor, metricoExecutor)
            .AddEdge(metricoExecutor, moduleCompletionRouter)
            .AddEdge<ModuleRouterOutput>(moduleCompletionRouter, instructorExecutor, condition: HasNextModule)
            .AddEdge<ModuleRouterOutput>(moduleCompletionRouter, experienciaExecutor, condition: ModuleCompletionGate.MeetsPublishThreshold)
            .AddEdge<ModuleRouterOutput>(moduleCompletionRouter, moduleRevisionRequired, condition: ModuleCompletionGate.RequiresRevision)
            .AddEdge(experienciaExecutor, gate2)
            .AddEdge(gate2, gate2Decision)
            .AddEdge<Gate2Outcome>(gate2Decision, publish, condition: IsGate2Approved)
            .AddEdge<Gate2Outcome>(gate2Decision, gate2Rejection, condition: IsGate2Rejected)
            .WithOutputFrom(publish, gate1Rejection, gate2Rejection, moduleRevisionRequired);

        return builder.Build();
    }

    private static bool HasNextModule(ModuleRouterOutput? message) => message?.NextModule is not null;

    private static bool IsGate1Approved(Gate1Outcome? message) => message?.ApprovedBlueprint is not null;

    private static bool IsGate1Rejected(Gate1Outcome? message) => message?.ApprovedBlueprint is null;

    private static bool IsGate2Approved(Gate2Outcome? message) => message?.ApprovedPackage is not null;

    private static bool IsGate2Rejected(Gate2Outcome? message) => message?.ApprovedPackage is null;
}

