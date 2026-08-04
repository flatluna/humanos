using System.Net;
using HumanOS.Data;
using HumanOS.Models.Capabilities.Graph;
using HumanOS.Models.Learning;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace HumanOS.AzureFunctions.Api;

/// <summary>
/// Capability Studio ONLY, dev/demo feature (2026-08-02) — "Aprobar
/// (temporal)". Lets a reviewer force a gated step (DeepDive, Recall,
/// Production, Assessment) to complete without actually demonstrating
/// mastery, so a demo/pilot walkthrough doesn't have to answer every
/// card/question. Purely additive: reuses
/// <see cref="InstructorRuntimeOrchestrator.AdvanceToNextStepAsync"/>'s
/// existing <c>bypassGatedStepGuard</c> escape hatch (already used
/// internally by TutorService once a real gate says "complete") and
/// <see cref="InstructorRuntimeOrchestrator.CompleteNodeAsync"/> — NEVER
/// touches TutorService/DeepDiveLoopGate/RecallLoopGate/AssessmentEvaluator/
/// AdaptiveAssessmentEngine, so the real grading logic used by actual
/// students is completely untouched by this feature.
///
/// Locked behind the <c>AllowStudioAutoApprove</c> app setting (must be the
/// literal string "true") — absent/false in every deployed environment by
/// default (see appsettings.json, which has no such key), only set in this
/// project's own local.settings.json for local pilot/demo use. Even when
/// enabled, this only ever force-advances whatever LearningSessionNodeId
/// the caller already has legitimate access to via the normal runtime
/// flow — it grants no additional access, it just skips a grading gate.
/// </summary>
public sealed class StudioForceAdvanceStepFunction
{
    private readonly InstructorRuntimeOrchestrator _orchestrator;
    private readonly HumanOsDbContext _dbContext;
    private readonly IConfiguration _configuration;

    public StudioForceAdvanceStepFunction(InstructorRuntimeOrchestrator orchestrator, HumanOsDbContext dbContext, IConfiguration configuration)
    {
        _orchestrator = orchestrator;
        _dbContext = dbContext;
        _configuration = configuration;
    }

    [Function("StudioForceAdvanceStep")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "studio/runtime/nodes/{learningSessionNodeId:guid}/force-advance")]
        HttpRequestData request,
        Guid learningSessionNodeId,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(_configuration["AllowStudioAutoApprove"], "true", StringComparison.OrdinalIgnoreCase))
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.Forbidden, "AutoApproveDisabled",
                "Studio auto-approve is disabled in this environment (AllowStudioAutoApprove is not set to \"true\").", cancellationToken);
        }

        var sessionNode = await _dbContext.LearningSessionNodes
            .Include(n => n.Steps)
            .FirstOrDefaultAsync(n => n.LearningSessionNodeId == learningSessionNodeId, cancellationToken);

        if (sessionNode is null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "NodeNotFound",
                $"No LearningSessionNode found with id {learningSessionNodeId}.", cancellationToken);
        }

        var activeStep = sessionNode.Steps.SingleOrDefault(s => s.Status == LearningSessionStepStatus.Active);
        if (activeStep is null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.Conflict, "NoActiveStep",
                "This node has no Active step to force-advance.", cancellationToken);
        }

        try
        {
            if (activeStep.StepType == ExperienceStepType.Assessment)
            {
                // No "next step" after Assessment — force-complete the node
                // directly instead, same as a real Passed round would. The
                // real AssessmentEvaluator (see AssessmentEvaluator.cs) always
                // writes a LearningAssessmentResult row before a node can be
                // marked Completed; CompleteNodeAsync itself never creates
                // one, so without this the node summary / graph "Mastered"
                // star would show no score (FinalScore reads the best
                // Passed LearningAssessmentResult). 2026-08-02: fabricate a
                // stand-in Passed result (score 90, clearly labeled as an
                // auto-approved bypass) so the demo node still shows up as
                // Mastered with a score, exactly like a real Passed round.
                _dbContext.LearningAssessmentResults.Add(new LearningAssessmentResult
                {
                    LearningSessionNodeId = learningSessionNodeId,
                    Score = 90,
                    Passed = true,
                    Feedback = "Auto-aprobado (demo/Capability Studio) — no se evaluó evidencia real."
                });
                await _dbContext.SaveChangesAsync(cancellationToken);

                await _orchestrator.CompleteNodeAsync(_dbContext, learningSessionNodeId, cancellationToken);
                return await FunctionResponseFactory.SuccessResponseAsync(
                    request, new ForceAdvanceStepResponse { NodeCompleted = true }, cancellationToken: cancellationToken);
            }

            var nextStep = await _orchestrator.AdvanceToNextStepAsync(
                _dbContext, learningSessionNodeId, cancellationToken, bypassGatedStepGuard: true);

            return await FunctionResponseFactory.SuccessResponseAsync(
                request, new ForceAdvanceStepResponse { NextStep = RuntimeGraphApiMappers.ToStepDto(nextStep) }, cancellationToken: cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.Conflict, "CannotForceAdvance", ex.Message, cancellationToken);
        }
    }
}
