using HumanOS.Data;
using HumanOS.Tests;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace HumanOS.AzureFunctions.Api;

public sealed class TestCuradorGraphArchitectFlowFunction
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<TestCuradorGraphArchitectFlowFunction> _logger;
    private readonly IDbContextFactory<HumanOsDbContext>? _dbContextFactory;

    public TestCuradorGraphArchitectFlowFunction(
        IConfiguration configuration,
        ILogger<TestCuradorGraphArchitectFlowFunction> logger,
        IDbContextFactory<HumanOsDbContext>? dbContextFactory = null)
    {
        _configuration = configuration;
        _logger = logger;
        _dbContextFactory = dbContextFactory;
    }

    [Function("test-curador-grapharchitect-flow")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "test/curador-grapharchitect")] HttpRequestData req)
    {
        _logger.LogInformation("Starting Curador → GraphArchitect flow test...");

        try
        {
            var test = new TestCuradorGraphArchitectFlow(_configuration, _dbContextFactory);
            await test.RunAsync();

            var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new
            {
                success = true,
                message = "Test completed. Check CURADOR_GRAPHARCHITECT_RESULTS.txt in backend directory."
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
