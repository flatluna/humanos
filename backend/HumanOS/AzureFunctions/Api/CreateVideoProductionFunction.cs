using System.Net;
using System.Text.Json;
using HumanOS.Data;
using HumanOS.Models.Capabilities.Studio;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

public sealed class CreateVideoProductionRequest
{
    public Guid TenantId { get; set; }
    public string Title { get; set; } = string.Empty;
}

/// <summary>Creates the root row for a new standalone "Video Productions"
/// run (2026-09-23) — see <see cref="VideoProduction"/>. Deliberately does
/// NOT use FilmProduction (that entity is tied to the slow per-scene
/// image-generation pipeline).</summary>
public sealed class CreateVideoProductionFunction
{
    private readonly HumanOsDbContext _dbContext;

    public CreateVideoProductionFunction(HumanOsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [Function("CreateVideoProduction")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "studio/video-productions")]
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        CreateVideoProductionRequest? body;
        try
        {
            body = await JsonSerializer.DeserializeAsync<CreateVideoProductionRequest>(
                request.Body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }, cancellationToken);
        }
        catch (JsonException)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidJson", "The request body contains invalid JSON.", cancellationToken);
        }

        if (body is null || string.IsNullOrWhiteSpace(body.Title))
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "MissingFields", "Title is required.", cancellationToken);
        }

        var production = new VideoProduction { TenantId = body.TenantId, Title = body.Title };
        _dbContext.VideoProductions.Add(production);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return await FunctionResponseFactory.SuccessResponseAsync(request, production, cancellationToken: cancellationToken);
    }
}
