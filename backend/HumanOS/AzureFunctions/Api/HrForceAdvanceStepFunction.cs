using System.Net;
using HumanOS.Data;
using HumanOS.Models.Capabilities.Graph;
using HumanOS.Models.Learning;
using HumanOS.Models.People;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.AzureFunctions.Api;

/// <summary>
/// POST /instructor-runtime/nodes/{learningSessionNodeId}/hr-force-advance
/// — 2026-08-02, "demo mode" for HRAdmins. Explicit user request: "esto
/// [el auto-aprobar] es solo para usuarios que tienen admin HR, es para
/// poder demostrar y avanzar los demos con los empleados, esto no es para
/// todos." Lets an HRAdmin force-complete a gated step (DeepDive/Recall/
/// Production/Assessment) on an employee's REAL learning session, inside
/// the real student app (human-os-employee), so they can live-demo/walk a
/// course forward with an employee without waiting on real mastery.
///
/// Unlike <see cref="StudioForceAdvanceStepFunction"/> (Capability
/// Studio's own dev/demo-only equivalent, gated by a blanket
/// AllowStudioAutoApprove app setting with NO per-caller check at all),
/// THIS endpoint is gated by a REAL server-side identity check every
/// call: the caller must resolve (via the same X-Azure-OID/X-Azure-TID
/// headers <see cref="GetMeFunction"/> uses) to a Person whose OrgRole is
/// HRAdmin, AND that Person must belong to the SAME Tenant as the student
/// whose session is being force-advanced. There is no config flag to
/// enable/disable this — the OrgRole + tenant check IS the security
/// boundary, so this is safe to leave present in every environment.
///
/// Reuses the exact same orchestrator calls as
/// StudioForceAdvanceStepFunction (AdvanceToNextStepAsync's
/// bypassGatedStepGuard escape hatch, plus a fabricated Passed
/// LearningAssessmentResult for the terminal Assessment step) — never
/// touches TutorService/DeepDiveLoopGate/RecallLoopGate/
/// AssessmentEvaluator/AdaptiveAssessmentEngine, so real grading for
/// every other student is completely untouched.
/// </summary>
public sealed class HrForceAdvanceStepFunction
{
    private readonly InstructorRuntimeOrchestrator _orchestrator;
    private readonly HumanOsDbContext _dbContext;
    private readonly PersonService _personService;

    public HrForceAdvanceStepFunction(InstructorRuntimeOrchestrator orchestrator, HumanOsDbContext dbContext, PersonService personService)
    {
        _orchestrator = orchestrator;
        _dbContext = dbContext;
        _personService = personService;
    }

    [Function("HrForceAdvanceStep")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "instructor-runtime/nodes/{learningSessionNodeId:guid}/hr-force-advance")]
        HttpRequestData request,
        Guid learningSessionNodeId,
        CancellationToken cancellationToken)
    {
        if (!request.Headers.TryGetValues("X-Azure-OID", out var oidValues) || string.IsNullOrWhiteSpace(oidValues.FirstOrDefault()) ||
            !request.Headers.TryGetValues("X-Azure-TID", out var tidValues) || string.IsNullOrWhiteSpace(tidValues.FirstOrDefault()))
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.Unauthorized, "MissingIdentity", "Caller identity headers are required.", cancellationToken);
        }

        var callerPerson = await _personService.GetByAzureIdentityAsync(oidValues.First(), tidValues.First(), cancellationToken);
        if (callerPerson is null || !string.Equals(callerPerson.OrgRole, nameof(PersonOrgRole.HRAdmin), StringComparison.Ordinal))
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.Forbidden, "HRAdminRequired",
                "Only an HRAdmin can force-advance a learning step.", cancellationToken);
        }

        var sessionNode = await _dbContext.LearningSessionNodes
            .Include(n => n.Steps)
            .Include(n => n.LearningSession)
            .FirstOrDefaultAsync(n => n.LearningSessionNodeId == learningSessionNodeId, cancellationToken);

        if (sessionNode?.LearningSession is null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "NodeNotFound",
                $"No LearningSessionNode found with id {learningSessionNodeId}.", cancellationToken);
        }

        var studentTenantId = await _dbContext.People
            .Where(p => p.PersonId == sessionNode.LearningSession.PersonId)
            .Select(p => p.TenantId)
            .FirstOrDefaultAsync(cancellationToken);

        if (studentTenantId is null || studentTenantId != callerPerson.TenantId)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.Forbidden, "TenantMismatch",
                "This learning session does not belong to your organization.", cancellationToken);
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
                // directly instead, same as a real Passed round would (see
                // StudioForceAdvanceStepFunction's doc comment for why a
                // fabricated LearningAssessmentResult is required so the
                // node summary/graph "Mastered" star still shows a score).
                _dbContext.LearningAssessmentResults.Add(new LearningAssessmentResult
                {
                    LearningSessionNodeId = learningSessionNodeId,
                    Score = 90,
                    Passed = true,
                    Feedback = "Auto-aprobado por HRAdmin (demo) — no se evaluó evidencia real.",
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
