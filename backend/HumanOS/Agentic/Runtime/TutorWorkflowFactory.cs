using HumanOS.Agents.Runtime;
using Microsoft.Agents.AI.Workflows;

namespace HumanOS.Agentic.Runtime;

/// <summary>
/// Builds TutorAgentV2's Workflow graph — currently a single Executor
/// (<see cref="TutorTurnExecutor"/>), no edges. Deliberately minimal: the
/// mandate (see /memories/repo/agent-framework-native-architecture-mandate.md)
/// requires TutorAgentV2 be a genuine Workflow (Executors/Edges/State/
/// Events/Checkpoints), not a bare LLM wrapper — but it does NOT require
/// artificial graph complexity. This single-node shape already gets: (1)
/// the Executor/Workflow scaffolding itself, (2) observability via
/// TutorPedagogicalEvent, (3) a stable seam to grow real edges from later
/// (e.g. a future confusion-detection or knowledge-lookup Executor
/// upstream of TutorTurnExecutor) without breaking callers of this factory.
///
/// A fresh graph (with a fresh Executor instance) must be built per Tutor
/// turn — same "fresh graph per run" convention as
/// CapabilityCreationWorkflowFactory.Build.
///
/// Workflow-as-Agent (the mandate's "must ALSO be exposed as a
/// Workflow-as-Agent for future composability" requirement): the installed
/// Microsoft.Agents.AI.Workflows package (1.13.0) exposes this via
/// <c>WorkflowHostingExtensions.AsAIAgent(Workflow, id, name, description,
/// executionEnvironment, includeExceptionDetails, includeWorkflowOutputsInResponse)</c>,
/// which requires the workflow's primary input type to be chat-compatible.
/// This workflow's input is the custom <see cref="TutorTurnRequest"/> DTO
/// (not a chat message list), so wrapping it via <c>AsAIAgent</c> is
/// deferred until an actual composability consumer (e.g. a future
/// CoachingAgent) needs it — at that point either TutorTurnRequest gains a
/// chat-compatible adapter, or a thin translation Executor is added
/// upstream. Not needed for the current on-demand TutorService caller.
/// </summary>
internal static class TutorWorkflowFactory
{
    public static Workflow Build(TutorAgentV2 tutorAgent)
    {
        var tutorTurnExecutor = new TutorTurnExecutor(tutorAgent);

        var builder = new WorkflowBuilder(tutorTurnExecutor);
        builder.WithOutputFrom(tutorTurnExecutor);

        return builder.Build();
    }
}
