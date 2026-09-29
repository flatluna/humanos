using System.Net;
using System.Text.Json;
using HumanOS.Data;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.AzureFunctions.Api;

public sealed class SetVideoProductionTitleRequest
{
    public string Title { get; set; } = string.Empty;
}

/// <summary>Renames a Video Production (2026-09-23) — the Title is what
/// identifies the saved production/merged video in the database; it does
/// not change any Data Lake blob path (those are keyed by the production's
/// own Guid, not its name).</summary>
public sealed class SetVideoProductionTitleFunction
{
    private readonly HumanOsDbContext _dbContext;

    public SetVideoProductionTitleFunction(HumanOsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [Function("SetVideoProductionTitle")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "studio/video-productions/{videoProductionId:guid}/title")]
        HttpRequestData request,
        Guid videoProductionId,
        CancellationToken cancellationToken)
    {
        SetVideoProductionTitleRequest? body;
        try
        {
            body = await JsonSerializer.DeserializeAsync<SetVideoProductionTitleRequest>(
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

        var production = await _dbContext.VideoProductions
            .FirstOrDefaultAsync(p => p.VideoProductionId == videoProductionId, cancellationToken);

        if (production is null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "ProductionNotFound", $"No VideoProduction found with id {videoProductionId}.", cancellationToken);
        }

        production.Title = body.Title.Trim().Slice(200);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return await FunctionResponseFactory.SuccessResponseAsync(request, production, cancellationToken: cancellationToken);
    }
}

internal static class StringSliceExtensions
{
    public static string Slice(this string value, int maxLength) => value.Length <= maxLength ? value : value[..maxLength];
}
