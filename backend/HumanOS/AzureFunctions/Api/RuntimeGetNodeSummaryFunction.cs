using System.Net;
using System.Web;
using HumanOS.Data;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.AzureFunctions.Api;

/// <summary>
/// Read-only "what happened the last time I completed this node" recap,
/// shown when a person opens a node that is already Mastered on the map
/// (product decision, 2026-07-19 — reopening a Mastered node shows the
/// last attempt's results by default, it never silently starts a new
/// attempt; "practicar de nuevo" stays an explicit separate action).
/// Enriched with Memory, Reward, and Impact blocks. See
/// <see cref="InstructorRuntimeOrchestrator.GetNodeSummaryAsync"/>.
/// </summary>
public sealed class RuntimeGetNodeSummaryFunction
{
    private readonly InstructorRuntimeOrchestrator _orchestrator;
    private readonly HumanOsDbContext _dbContext;
    private readonly PersonService _personService;

    public RuntimeGetNodeSummaryFunction(
        InstructorRuntimeOrchestrator orchestrator,
        HumanOsDbContext dbContext,
        PersonService personService)
    {
        _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _personService = personService ?? throw new ArgumentNullException(nameof(personService));
    }

    [Function("RuntimeGetNodeSummary")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "instructor-runtime/nodes/summary")]
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var query = HttpUtility.ParseQueryString(request.Url.Query);

        Guid? personId = null;
        if (Guid.TryParse(query["personId"], out var parsedPersonId))
        {
            personId = parsedPersonId;
        }

        if (personId is null)
        {
            personId = await ResolveAuthenticatedPersonIdAsync(request, cancellationToken);
        }

        if (personId is null || !Guid.TryParse(query["capabilityGraphNodeId"], out var capabilityGraphNodeId))
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "MissingFields",
                "Query parameter capabilityGraphNodeId is required (and personId if not authenticated).", cancellationToken);
        }

        try
        {
            var summary = await _orchestrator.GetNodeSummaryAsync(_dbContext, personId.Value, capabilityGraphNodeId, cancellationToken);
            var response = RuntimeGraphApiMappers.ToNodeSummaryDto(summary);
            return await FunctionResponseFactory.SuccessResponseAsync(request, response, cancellationToken: cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "NodeNotCompletedYet", ex.Message, cancellationToken);
        }
    }

    private async Task<Guid?> ResolveAuthenticatedPersonIdAsync(
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        // 1. Identity headers (MSAL / Enterprise auth)
        if (request.Headers.TryGetValues("X-Azure-OID", out var oidValues) && !string.IsNullOrWhiteSpace(oidValues.FirstOrDefault()) &&
            request.Headers.TryGetValues("X-Azure-TID", out var tidValues) && !string.IsNullOrWhiteSpace(tidValues.FirstOrDefault()))
        {
            var person = await _personService.GetByAzureIdentityAsync(oidValues.First(), tidValues.First(), cancellationToken);
            if (person is not null)
            {
                return person.PersonId;
            }
        }

        // 2. Direct header
        if (request.Headers.TryGetValues("X-Person-Id", out var personValues) &&
            Guid.TryParse(personValues.FirstOrDefault(), out var parsedHeaderId))
        {
            return parsedHeaderId;
        }

        // 3. Query string parameter (?personId=... or ?employeeId=...)
        var query = HttpUtility.ParseQueryString(request.Url.Query);
        var queryPersonId = query["personId"] ?? query["employeeId"];
        if (!string.IsNullOrWhiteSpace(queryPersonId) && Guid.TryParse(queryPersonId, out var parsedQueryId))
        {
            return parsedQueryId;
        }

        // 4. Fallback for single-tenant / local development context
        var defaultPersonId = await _dbContext.People
            .Where(p => p.IsActive)
            .OrderBy(p => p.CreatedDate)
            .Select(p => (Guid?)p.PersonId)
            .FirstOrDefaultAsync(cancellationToken);

        return defaultPersonId;
    }
}
