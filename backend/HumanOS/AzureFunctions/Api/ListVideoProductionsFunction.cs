using HumanOS.Data;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.AzureFunctions.Api;

/// <summary>Lists existing Video Productions for a tenant, most recent
/// first (2026-09-24, mirrors ListFilmProductionsFunction) — lets the
/// wizard's hero screen offer "reopen" instead of only "start a brand new
/// production" (the frontend had no other way to get back to a saved
/// project's clips/final video after a page reload).</summary>
public sealed class ListVideoProductionsFunction
{
    private readonly HumanOsDbContext _dbContext;

    public ListVideoProductionsFunction(HumanOsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [Function("ListVideoProductions")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "studio/video-productions")]
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var productions = await _dbContext.VideoProductions
            .AsNoTracking()
            .OrderByDescending(p => p.CreatedDate)
            .ToListAsync(cancellationToken);

        return await FunctionResponseFactory.SuccessResponseAsync(request, productions, cancellationToken: cancellationToken);
    }
}
