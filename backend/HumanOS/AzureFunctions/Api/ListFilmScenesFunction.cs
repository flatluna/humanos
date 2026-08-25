using System.Net;
using HumanOS.Data;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.AzureFunctions.Api;

/// <summary>Lists all scenes for a FilmProduction, ordered by SortOrder —
/// used to render the wizard's scene list/loop.</summary>
public sealed class ListFilmScenesFunction
{
    private readonly HumanOsDbContext _dbContext;

    public ListFilmScenesFunction(HumanOsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [Function("ListFilmScenes")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "studio/film-productions/{filmProductionId:guid}/scenes")]
        HttpRequestData request,
        Guid filmProductionId,
        CancellationToken cancellationToken)
    {
        var scenes = await _dbContext.FilmScenes
            .AsNoTracking()
            .Where(s => s.FilmProductionId == filmProductionId)
            .OrderBy(s => s.SortOrder)
            .ToListAsync(cancellationToken);

        return await FunctionResponseFactory.SuccessResponseAsync(request, scenes, cancellationToken: cancellationToken);
    }
}
