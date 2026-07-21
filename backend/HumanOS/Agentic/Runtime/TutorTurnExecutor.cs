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
                if (request.Mode is TutorInteractionMode.Recall)
                {
                    // Fixed 2026-07-20: during Recall the student is
                    // deliberately NOT looking at the illustration anymore (that's
                    // the whole point of this stage) — these captions are
                    // background context for YOU to understand what was taught,
                    // never material to quiz the student on. A real production
                    // bug: the Tutor was asking things like "¿cuántas frases tenía
                    // el resumen mostrado junto al documento?" — testing memory of
                    // how an example was PRESENTED, not the underlying capability.
                    promptBuilder.AppendLine("ILLUSTRATION(S) SHOWN EARLIER DURING TEACHING (background only — the student is NOT looking at these right now and must NOT be asked about their incidental presentation details: exact words/counts/labels/colors shown, how many items appeared, etc. Never say \"como ves en la ilustración\" here. Use these only to understand the concept being taught; your question must test the underlying capability, never trivia about the illustration itself):");
                }
                else
                {
                    promptBuilder.AppendLine("ILLUSTRATION(S) THE STUDENT CAN SEE ON SCREEN RIGHT NOW (reference them explicitly, e.g. \"como ves en la ilustracion...\" — do not ignore them):");
                }
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

        if (request.Mode == TutorInteractionMode.Recall && !string.IsNullOrWhiteSpace(request.CurrentQuestionBeingAnswered))
        {
            promptBuilder.AppendLine("THE QUESTION THE STUDENT IS ANSWERING RIGHT NOW (you asked this last turn — verify against THIS question's exact values, not any other question above):");
            promptBuilder.AppendLine(request.CurrentQuestionBeingAnswered);
            promptBuilder.AppendLine();
        }

        if (!string.IsNullOrWhiteSpace(request.DocumentSummary) || request.KeyEntities.Count > 0)
        {
            // Fixed 2026-07-20: static document-wide complement to the RAG
            // section below — see TutorTurnRequest.DocumentSummary/
            // KeyEntities doc comments and
            // /memories/repo/tutor-document-wide-context-gap.md. Framed as
            // background/disambiguation only, never license to teach beyond
            // this node's own scope.
            promptBuilder.AppendLine("DOCUMENT-WIDE BACKGROUND (orientation to the whole source material — use ONLY to correctly understand/disambiguate a brief reference to something not covered by STEP CONTENT above; never to teach material outside this node's own scope):");
            if (!string.IsNullOrWhiteSpace(request.DocumentSummary))
            {
                promptBuilder.AppendLine(request.DocumentSummary);
            }
            if (request.KeyEntities.Count > 0)
            {
                promptBuilder.AppendLine("Key entities mentioned in the source material:");
                foreach (var entity in request.KeyEntities)
                {
                    promptBuilder.AppendLine($"- {entity}");
                }
            }
            promptBuilder.AppendLine();
        }

        if (request.RetrievedKnowledge.Count > 0)
        {
            // Fixed 2026-07-20: supplementary cross-node RAG context — see
            // TutorTurnRequest.RetrievedKnowledge's doc comment and
            // /memories/repo/tutor-document-wide-context-gap.md. Framed
            // explicitly as OPTIONAL/supplementary so the model doesn't
            // treat it as license to teach beyond this node's own scope.
            promptBuilder.AppendLine("RELATED INFORMATION FROM ELSEWHERE IN THE SOURCE MATERIAL (other nodes of the same learning graph — use this ONLY if the student's message asks for a specific fact, name, number, date, or reference that is NOT part of STEP CONTENT above; never use it to teach something outside this node's own scope; if you do use it, briefly attribute it, e.g. \"eso lo vimos en '<node>'...\"):");
            foreach (var snippet in request.RetrievedKnowledge)
            {
                promptBuilder.AppendLine($"- [{snippet.NodeName}] {snippet.Content}");
            }
            promptBuilder.AppendLine();
        }

        promptBuilder.AppendLine("STUDENT'S MESSAGE THIS TURN:");
        promptBuilder.AppendLine(request.StudentMessage);

        return promptBuilder.ToString();
    }
}
