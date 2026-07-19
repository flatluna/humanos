using HumanOS.Data;
using HumanOS.Tests;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace HumanOS.AzureFunctions.Api;

/// <summary>
/// Test-only wrapper for <see cref="TestGraphProgressionFlow"/> — drives a
/// REAL CapabilityGraph (any domain) through the full Runtime flow and
/// GraphProgressionEngine, node by node, to prove Paso 5 works end-to-end.
/// Query params: capabilityId (required), personId (optional — defaults to
/// the first Person row found).
/// </summary>
public sealed class TestGraphProgressionFlowFunction
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<TestGraphProgressionFlowFunction> _logger;
    private readonly IDbContextFactory<HumanOsDbContext>? _dbContextFactory;

    public TestGraphProgressionFlowFunction(
        IConfiguration configuration,
        ILogger<TestGraphProgressionFlowFunction> logger,
        IDbContextFactory<HumanOsDbContext>? dbContextFactory = null)
    {
        _configuration = configuration;
        _logger = logger;
        _dbContextFactory = dbContextFactory;
    }

    [Function("test-graph-progression-flow")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "test/graph-progression")] HttpRequestData req)
    {
        var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);

        if (!Guid.TryParse(query["capabilityId"], out var capabilityId))
        {
            var badRequest = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
            await badRequest.WriteAsJsonAsync(new { error = "Query param 'capabilityId' (Guid) is required." });
            return badRequest;
        }

        Guid? personId = Guid.TryParse(query["personId"], out var parsedPersonId) ? parsedPersonId : null;
        var forceProgressPastRealFailure = string.Equals(query["forceProgressPastRealFailure"], "true", StringComparison.OrdinalIgnoreCase);

        if (_dbContextFactory is null)
        {
            var response500 = req.CreateResponse(System.Net.HttpStatusCode.InternalServerError);
            await response500.WriteAsJsonAsync(new { error = "No IDbContextFactory<HumanOsDbContext> configured." });
            return response500;
        }

        _logger.LogInformation("Starting Graph Progression flow test for CapabilityId {CapabilityId}...", capabilityId);

        try
        {
            var test = new TestGraphProgressionFlow(_configuration, _dbContextFactory);
            var report = await test.RunAsync(capabilityId, personId, forceProgressPastRealFailure, req.FunctionContext.CancellationToken);

            var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new
            {
                success = true,
                message = "Test completed. Check GRAPH_PROGRESSION_TEST_RESULTS.txt in backend directory.",
                report
            });
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Test failed");
            var response = req.CreateResponse(System.Net.HttpStatusCode.InternalServerError);
            await response.WriteAsJsonAsync(new { error = ex.Message });
            return response;
        }
    }
}
