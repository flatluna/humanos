using Microsoft.Agents.AI.Workflows;

namespace HumanOS.Agentic.Studio;

/// <summary>Terminal step for a Gate 1 rejection — yields the rejection
/// message as the workflow's output.</summary>
internal sealed class Gate1RejectionExecutor : Executor<Gate1Outcome, string>
{
    public Gate1RejectionExecutor() : base("Gate1Rejection")
    {
    }

    public override async ValueTask<string> HandleAsync(
        Gate1Outcome outcome,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        var message = outcome.RejectionMessage ?? "Gate 1 rejected.";
        await context.YieldOutputAsync(message, cancellationToken);
        return message;
    }
}
