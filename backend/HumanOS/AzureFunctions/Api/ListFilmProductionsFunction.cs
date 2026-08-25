using HumanOS.Data;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.AzureFunctions.Api;

/// <summary>Lists existing Film Productions for a tenant, most recent first
/// (2026-08-24) — lets the wizard's hero screen offer "reopen" instead of
/// only "start a brand new production" (the frontend has no other way to
/// get back to a production's scenes after a page reload).</summary>
public sealed class ListFilmProductionsFunction
{
    private readonly HumanOsDbContext _dbContext;

    public ListFilmProductionsFunction(HumanOsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [Function("ListFilmProductions")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "studio/film-productions")]
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var productions = await _dbContext.FilmProductions
            .AsNoTracking()
            .OrderByDescending(p => p.CreatedDate)
            .ToListAsync(cancellationToken);

        return await FunctionResponseFactory.SuccessResponseAsync(request, productions, cancellationToken: cancellationToken);
    }
}
