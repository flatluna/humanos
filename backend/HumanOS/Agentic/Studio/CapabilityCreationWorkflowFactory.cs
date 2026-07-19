using HumanOS.Agents.Studio;
using HumanOS.Data;
using Microsoft.Agents.AI.Workflows;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.Agentic.Studio;

/// <summary>
/// Builds the Human OS Studio capability-creation Workflow graph:
///
///   Curador -&gt; Arquitecto -&gt; [GATE 1] -&gt; Gate1Decision
///     -&gt; (conditional) -&gt; ParallelModuleGeneration -&gt; [&gt;=85% Verified?]
///     -&gt; Experiencia -&gt; [GATE 2] -&gt; Gate2Decision -&gt; (conditional) -&gt; Publish
///   (Gate1Decision/Gate2Decision also conditionally route to
///   Gate1RejectionExecutor/Gate2RejectionExecutor on rejection.
///   ParallelModuleGeneration routes to ModuleRevisionRequiredExecutor
///   instead of Experiencia when generation is done but FEWER than
///   ModuleCompletionGate.MinVerifiedRatio of modules are Verified — see
///   ModuleCompletionGate, Paso 5 2026-07-14 (threshold relaxed Paso 7):
///   "all modules generated" is NOT "capability ready to assemble", but
///   100% Verified is also not required to publish.)
///
/// Module generation (2026-07-16, replacing the previous SEQUENTIAL
/// one-at-a-time loop — see ParallelModuleGenerationExecutor's remarks for
/// why the Agent Framework's AddFanOutEdge/AddFanInBarrierEdge primitives
/// weren't used): every module in the approved blueprint is now processed
/// concurrently (bounded, see ParallelModuleGenerationExecutor.MaxConcurrency)
/// inside a SINGLE executor, instead of one at a time through four separate
/// executors and conditional loop-back edges.
///
/// A fresh graph (with fresh executor instances) must be built per
/// capability-creation run.
/// </summary>
internal static class CapabilityCreationWorkflowFactory
{
    public static Workflow Build(
        CuradorAgent curador,
        TocExtractionAgent tocExtraction,
        ArquitectoAgent arquitecto,
        InstructorAgent instructor,
        MetricoAgent metrico,
        ExperienciaAgent experiencia,
        IDbContextFactory<HumanOsDbContext> dbContextFactory,
        CapabilityEmbeddingService embeddingService)
    {
        var curadorExecutor = new CuradorExecutor(curador, tocExtraction);
        var arquitectoExecutor = new ArquitectoExecutor(arquitecto);
        var gate1 = RequestPort.Create<CapabilityBlueprint, GateDecision>("Gate1-ArchitectReview");
        var gate1Decision = new Gate1DecisionExecutor();
        var gate1Rejection = new Gate1RejectionExecutor();
        var parallelModuleGeneration = new ParallelModuleGenerationExecutor(instructor, metrico);
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
            .AddEdge<Gate1Outcome>(gate1Decision, parallelModuleGeneration, condition: IsGate1Approved)
            .AddEdge<Gate1Outcome>(gate1Decision, gate1Rejection, condition: IsGate1Rejected)
            .AddEdge<ModuleRouterOutput>(parallelModuleGeneration, experienciaExecutor, condition: ModuleCompletionGate.MeetsPublishThreshold)
            .AddEdge<ModuleRouterOutput>(parallelModuleGeneration, moduleRevisionRequired, condition: ModuleCompletionGate.RequiresRevision)
            .AddEdge(experienciaExecutor, gate2)
            .AddEdge(gate2, gate2Decision)
            .AddEdge<Gate2Outcome>(gate2Decision, publish, condition: IsGate2Approved)
            .AddEdge<Gate2Outcome>(gate2Decision, gate2Rejection, condition: IsGate2Rejected)
            .WithOutputFrom(publish, gate1Rejection, gate2Rejection, moduleRevisionRequired);

        return builder.Build();
    }

    private static bool IsGate1Approved(Gate1Outcome? message) => message?.ApprovedBlueprint is not null;

    private static bool IsGate1Rejected(Gate1Outcome? message) => message?.ApprovedBlueprint is null;

    private static bool IsGate2Approved(Gate2Outcome? message) => message?.ApprovedPackage is not null;

    private static bool IsGate2Rejected(Gate2Outcome? message) => message?.ApprovedPackage is null;
}

