using HumanOS.Data;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;
using System.Net;

namespace HumanOS.AzureFunctions.Api;

public class DatabaseHealthFunction
{
    private readonly HumanOsDbContext? _dbContext;

    public DatabaseHealthFunction(HumanOsDbContext? dbContext = null)
    {
        _dbContext = dbContext;
    }

    [Function("DatabaseHealth")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "get",
            Route = "health/database")]
        HttpRequestData request)
    {
        HttpStatusCode statusCode = HttpStatusCode.ServiceUnavailable;
        string status = "Unavailable";
        string message = "Database context not configured";

        if (_dbContext != null)
        {
            try
            {
                await using var connection = _dbContext.Database.GetDbConnection();
                await connection.OpenAsync();
                statusCode = HttpStatusCode.OK;
                status = "Healthy";
                message = "";
            }
            catch (Exception ex)
            {
                statusCode = HttpStatusCode.ServiceUnavailable;
                status = "Unhealthy";
                message = ex.ToString();
            }
        }

        var response = request.CreateResponse(statusCode);

        await response.WriteAsJsonAsync(
            new
            {
                status,
                database = "HumanOSDev",
                message,
                checkedAtUtc = DateTime.UtcNow
            });

        return response;
    }
}
