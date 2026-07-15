using Microsoft.Agents.AI.Workflows;

namespace HumanOS.Agentic.Studio;

/// <summary>Terminal step for a Gate 2 rejection — yields the rejection
/// message as the workflow's output.</summary>
internal sealed class Gate2RejectionExecutor : Executor<Gate2Outcome, string>
{
    public Gate2RejectionExecutor() : base("Gate2Rejection")
    {
    }

    public override async ValueTask<string> HandleAsync(
        Gate2Outcome outcome,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        var message = outcome.RejectionMessage ?? "Gate 2 rejected.";
        await context.YieldOutputAsync(message, cancellationToken);
        return message;
    }
}
