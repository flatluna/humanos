using HumanOS.Data;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;
using System.Net;

namespace HumanOS.AzureFunctions.Api;

public sealed class DatabaseHealthFunction
{
    private readonly HumanOsDbContext _dbContext;

    public DatabaseHealthFunction(HumanOsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [Function("DatabaseHealth")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "get",
            Route = "health/database")]
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var canConnect = await _dbContext.Database.CanConnectAsync(
            cancellationToken);

        var response = request.CreateResponse(
            canConnect
                ? HttpStatusCode.OK
                : HttpStatusCode.ServiceUnavailable);

        await response.WriteAsJsonAsync(
            new
            {
                status = canConnect ? "Healthy" : "Unhealthy",
                database = "HumanOSDev",
                checkedAtUtc = DateTime.UtcNow
            },
            cancellationToken);

        return response;
    }
}
