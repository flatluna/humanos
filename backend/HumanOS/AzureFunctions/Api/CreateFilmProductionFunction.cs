using System.Net;
using System.Text.Json;
using HumanOS.Data;
using HumanOS.Models.Capabilities.Studio;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

public sealed class CreateFilmProductionRequest
{
    public Guid TenantId { get; set; }
    public string Title { get; set; } = string.Empty;
}

/// <summary>Creates the root row for a new "Film Productions" wizard run
/// (2026-08-24) — see <see cref="FilmProduction"/>.</summary>
public sealed class CreateFilmProductionFunction
{
    private readonly HumanOsDbContext _dbContext;

    public CreateFilmProductionFunction(HumanOsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [Function("CreateFilmProduction")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "studio/film-productions")]
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        CreateFilmProductionRequest? body;
        try
        {
            body = await JsonSerializer.DeserializeAsync<CreateFilmProductionRequest>(
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

        var production = new FilmProduction { TenantId = body.TenantId, Title = body.Title };
        _dbContext.FilmProductions.Add(production);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return await FunctionResponseFactory.SuccessResponseAsync(request, production, cancellationToken: cancellationToken);
    }
}
