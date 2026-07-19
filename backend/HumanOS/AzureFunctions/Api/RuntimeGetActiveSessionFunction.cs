using System.Net;
using HumanOS.Data;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>
/// Runtime V1, Paso 4 (2026-07-17) — Endpoint 2: GET ACTIVE SESSION.
/// The UI calls this with ONLY PersonId+CapabilityId (never a remembered
/// LearningSessionId) to find out whether there is anything in progress to
/// resume — returns <see langword="null"/> (200 OK, empty body) rather than
/// an error when nothing is active, since "no active session" is a normal,
/// expected outcome (e.g. first visit, or the person already finished).
/// </summary>
public sealed class RuntimeGetActiveSessionFunction
{
    private readonly SessionRecoveryEngine _sessionRecoveryEngine;
    private readonly HumanOsDbContext _dbContext;

    public RuntimeGetActiveSessionFunction(SessionRecoveryEngine sessionRecoveryEngine, HumanOsDbContext dbContext)
    {
        _sessionRecoveryEngine = sessionRecoveryEngine;
        _dbContext = dbContext;
    }

    [Function("RuntimeGetActiveSession")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "instructor-runtime/sessions/active")]
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

        var activeNode = await _sessionRecoveryEngine.GetActiveNodeAsync(_dbContext, personId, capabilityId, cancellationToken);
        if (activeNode is null)
        {
            return await FunctionResponseFactory.SuccessResponseAsync<object?>(request, null, cancellationToken: cancellationToken);
        }

        var resumed = await _sessionRecoveryEngine.ResumeSessionAsync(_dbContext, personId, capabilityId, cancellationToken);
        var sessionInfo = RuntimeGraphApiMappers.ToSessionInfo(resumed);

        return await FunctionResponseFactory.SuccessResponseAsync(request, sessionInfo, cancellationToken: cancellationToken);
    }
}
