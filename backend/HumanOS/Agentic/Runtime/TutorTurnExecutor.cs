using System.Text;
using HumanOS.Agents.Runtime;
using Microsoft.Agents.AI.Workflows;

namespace HumanOS.Agentic.Runtime;

/// <summary>
/// The single Executor of <see cref="TutorWorkflowFactory"/>'s Workflow —
/// runs one on-demand Tutor interaction: builds the LLM prompt from a
/// <see cref="TutorTurnRequest"/>, calls <see cref="TutorAgentV2"/>, raises
/// <see cref="TutorPedagogicalEvent"/> observability events, and yields the
/// <see cref="TutorTurnResult"/> as the workflow's output.
///
/// Deliberately a single node for now (no loop-back edges): the Recall
/// "up to 5 attempts / 85% threshold" bounded logic lives in CODE one layer
/// up (<see cref="Services.TutorService"/>, driven by LearningEvidence
/// count already persisted for the step) — each student attempt is its own
/// full workflow run, not an in-workflow loop. This mirrors how
/// AssessmentEvaluator's Score>=70 cutoff is decided in code, never trusted
/// from the LLM.
/// </summary>
internal sealed class TutorTurnExecutor : Executor<TutorTurnRequest, TutorTurnResult>
{
    private readonly TutorAgentV2 _tutorAgent;

    public TutorTurnExecutor(TutorAgentV2 tutorAgent) : base("TutorTurn")
    {
        _tutorAgent = tutorAgent;
    }

    public override async ValueTask<TutorTurnResult> HandleAsync(
        TutorTurnRequest request,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        await context.AddEventAsync(new TutorTurnStartedEvent(request.Mode), cancellationToken);

        var prompt = BuildPrompt(request);
        var turn = await _tutorAgent.RespondAsync(prompt, cancellationToken);

        // Recall score never leaks out of Recall mode — defensive
        // clamp/null-out here even though TutorAgentV2's instructions
        // already forbid it, same "LLM proposes, code has final say"
        // discipline used across every other Human OS agent.
        var recallScore = request.Mode == TutorInteractionMode.Recall
            ? turn.Response.RecallScore is { } score ? Math.Clamp(score, 0, 100) : (int?)null
            : null;

        var result = new TutorTurnResult
        {
            Response = new TutorTurnResponse
            {
                Message = turn.Response.Message,
                RecallScore = recallScore
            },
            Illustrations = request.Illustrations,
            TokenUsage = turn.TokenUsage
        };

        await context.AddEventAsync(new TutorTurnCompletedEvent(request.Mode, recallScore), cancellationToken);

        await context.YieldOutputAsync(result, cancellationToken);
        return result;
    }

    private static string BuildPrompt(TutorTurnRequest request)
    {
        var promptBuilder = new StringBuilder();
        promptBuilder.AppendLine($"MODE: {request.Mode}");
        promptBuilder.AppendLine();

        if (request.Mode == TutorInteractionMode.AssessmentFeedback)
        {
            promptBuilder.AppendLine("RAW ASSESSMENT FEEDBACK (translate this, do not re-judge):");
            promptBuilder.AppendLine(request.RawAssessmentFeedback ?? string.Empty);
        }
        else
        {
            promptBuilder.AppendLine("STEP CONTENT (your only source of domain knowledge):");
            promptBuilder.AppendLine(request.StepContent);

            if (request.Illustrations.Count > 0)
            {
                promptBuilder.AppendLine();
                promptBuilder.AppendLine("ILLUSTRATION(S) THE STUDENT CAN SEE ON SCREEN RIGHT NOW (reference them explicitly, e.g. \"como ves en la ilustracion...\" — do not ignore them):");
                foreach (var illustration in request.Illustrations)
                {
                    promptBuilder.AppendLine($"- {illustration.Caption}");
                }
            }
        }

        promptBuilder.AppendLine();

        if (request.Mode == TutorInteractionMode.Recall)
        {
            var priorPrompts = request.History
                .Where(h => !string.IsNullOrWhiteSpace(h.TutorPrompt))
                .Select(h => h.TutorPrompt!)
                .ToList();

            if (priorPrompts.Count > 0)
            {
                promptBuilder.AppendLine("RECALL PROMPTS ALREADY ASKED THIS LOOP (never repeat one of these literally or with trivial rewording \u2014 your new question must use different concrete values/objects):");
                foreach (var prior in priorPrompts)
                {
                    promptBuilder.AppendLine($"- {prior}");
                }
                promptBuilder.AppendLine();
            }
        }

        if (request.History.Count > 0)
        {
            promptBuilder.AppendLine("CONVERSATION HISTORY (oldest first):");
            foreach (var entry in request.History)
            {
                if (!string.IsNullOrWhiteSpace(entry.TutorPrompt))
                {
                    promptBuilder.AppendLine($"Tutor: {entry.TutorPrompt}");
                }

                promptBuilder.AppendLine($"Student: {entry.StudentResponse}");
            }

            promptBuilder.AppendLine();
        }

        promptBuilder.AppendLine("STUDENT'S MESSAGE THIS TURN:");
        promptBuilder.AppendLine(request.StudentMessage);

        return promptBuilder.ToString();
    }
}
