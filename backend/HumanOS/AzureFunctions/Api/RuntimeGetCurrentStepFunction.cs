using System.Net;
using HumanOS.Data;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>
/// Runtime V1, Paso 4 (2026-07-17) — Endpoint 3: GET CURRENT STEP.
/// The core "resume where I left off" endpoint — takes ONLY
/// PersonId+CapabilityId (never LearningSessionId/NodeId/StepId from the
/// client) and reconstructs the exact position purely from SQL via
/// <see cref="SessionRecoveryEngine.ResumeSessionAsync"/>. Returns 404 if
/// there is nothing active to resume (distinct from Endpoint 2, which
/// treats "nothing active" as a normal null result — this endpoint is
/// meant to be called once the caller already knows/expects a session to
/// exist, so a 404 here is a genuine caller error).
/// </summary>
public sealed class RuntimeGetCurrentStepFunction
{
    private readonly SessionRecoveryEngine _sessionRecoveryEngine;
    private readonly HumanOsDbContext _dbContext;

    public RuntimeGetCurrentStepFunction(SessionRecoveryEngine sessionRecoveryEngine, HumanOsDbContext dbContext)
    {
        _sessionRecoveryEngine = sessionRecoveryEngine;
        _dbContext = dbContext;
    }

    [Function("RuntimeGetCurrentStep")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "instructor-runtime/sessions/current-step")]
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var query = System.Web.HttpUtility.ParseQueryString(request.Url.Query);

        if (!Guid.TryParse(query["personId"], out var personId) || !Guid.TryParse(query["capabilityId"], out var capabilityId))
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "MissingFields",
                "Query parameters personId and capabilityId are both required.", cancellationToken);
        }

        try
        {
            var resumed = await _sessionRecoveryEngine.ResumeSessionAsync(_dbContext, personId, capabilityId, cancellationToken);
            var response = RuntimeGraphApiMappers.ToCurrentStepResponse(resumed);

            return await FunctionResponseFactory.SuccessResponseAsync(request, response, cancellationToken: cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "NoActiveSession", ex.Message, cancellationToken);
        }
    }
}
