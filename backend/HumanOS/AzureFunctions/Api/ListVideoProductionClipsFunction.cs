using HumanOS.Data;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.AzureFunctions.Api;

/// <summary>Lists all clips for a VideoProduction, ordered by SortOrder
/// (2026-09-24, mirrors ListFilmScenesFunction) — used to reopen a
/// previously-saved production's clip list/final video after a page reload.</summary>
public sealed class ListVideoProductionClipsFunction
{
    private readonly HumanOsDbContext _dbContext;

    public ListVideoProductionClipsFunction(HumanOsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [Function("ListVideoProductionClips")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "studio/video-productions/{videoProductionId:guid}/clips")]
        HttpRequestData request,
        Guid videoProductionId,
        CancellationToken cancellationToken)
    {
        var clips = await _dbContext.VideoProductionClips
            .AsNoTracking()
            .Where(c => c.VideoProductionId == videoProductionId)
            .OrderBy(c => c.SortOrder)
            .ToListAsync(cancellationToken);

        return await FunctionResponseFactory.SuccessResponseAsync(request, clips, cancellationToken: cancellationToken);
    }
}
