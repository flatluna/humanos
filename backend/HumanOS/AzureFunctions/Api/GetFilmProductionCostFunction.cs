using System.Net;
using HumanOS.Agents.Studio;
using HumanOS.Data;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace HumanOS.AzureFunctions.Api;

public sealed class FilmProductionCostResponse
{
    public int SceneCount { get; set; }
    public int CompletedImageCount { get; set; }
    public int CompletedVideoCount { get; set; }
    public long TotalInputTokens { get; set; }
    public long TotalOutputTokens { get; set; }
    public long TotalCachedInputTokens { get; set; }
    public decimal EstimatedCostUsd { get; set; }
    public decimal ImageCostUsd { get; set; }
    public decimal InputCostUsd { get; set; }
    public decimal OutputCostUsd { get; set; }
}

/// <summary>
/// Cost/usage summary for one Film Production (2026-08-24) — counts every
/// scene's prompt (Agent A token usage), the one-time script-breakdown
/// agent call (if the auto-storyboard mode was used), and every completed
/// image/video, then reuses <see cref="TokenCostEstimator"/> (same
/// estimator as Structured Learning's cost dashboard) for the USD estimate.
/// </summary>
public sealed class GetFilmProductionCostFunction
{
    private readonly HumanOsDbContext _dbContext;
    private readonly IConfiguration _configuration;

    public GetFilmProductionCostFunction(HumanOsDbContext dbContext, IConfiguration configuration)
    {
        _dbContext = dbContext;
        _configuration = configuration;
    }

    [Function("GetFilmProductionCost")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "studio/film-productions/{filmProductionId:guid}/cost")]
        HttpRequestData request,
        Guid filmProductionId,
        CancellationToken cancellationToken)
    {
        var production = await _dbContext.FilmProductions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.FilmProductionId == filmProductionId, cancellationToken);

        if (production is null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "ProductionNotFound",
                $"No FilmProduction found with id {filmProductionId}.", cancellationToken);
        }

        var scenes = await _dbContext.FilmScenes
            .AsNoTracking()
            .Where(s => s.FilmProductionId == filmProductionId)
            .Select(s => new
            {
                s.ContentInputTokens,
                s.ContentOutputTokens,
                s.ContentCachedInputTokens,
                s.ContentModelName,
                HasImage = s.FinalImageStoragePath != null,
                HasVideo = s.FinalVideoStoragePath != null,
            })
            .ToListAsync(cancellationToken);

        var tokenUsage = new List<AgentTokenUsage>();
        foreach (var scene in scenes)
        {
            tokenUsage.Add(new AgentTokenUsage
            {
                AgentName = "FilmSceneContentAgent",
                ModelName = scene.ContentModelName ?? string.Empty,
                InputTokens = scene.ContentInputTokens,
                OutputTokens = scene.ContentOutputTokens,
                CachedInputTokens = scene.ContentCachedInputTokens,
            });
        }

        if (production.BreakdownInputTokens > 0 || production.BreakdownOutputTokens > 0)
        {
            tokenUsage.Add(new AgentTokenUsage
            {
                AgentName = "FilmScriptBreakdownAgent",
                ModelName = production.BreakdownModelName ?? string.Empty,
                InputTokens = production.BreakdownInputTokens,
                OutputTokens = production.BreakdownOutputTokens,
                CachedInputTokens = production.BreakdownCachedInputTokens,
            });
        }

        var completedImageCount = scenes.Count(s => s.HasImage);
        var estimate = TokenCostEstimator.Estimate(tokenUsage, completedImageCount, _configuration);

        var response = new FilmProductionCostResponse
        {
            SceneCount = scenes.Count,
            CompletedImageCount = completedImageCount,
            CompletedVideoCount = scenes.Count(s => s.HasVideo),
            TotalInputTokens = estimate.BillableInputTokens + estimate.CachedInputTokens,
            TotalOutputTokens = estimate.OutputTokens,
            TotalCachedInputTokens = estimate.CachedInputTokens,
            EstimatedCostUsd = estimate.TotalCostUsd,
            ImageCostUsd = estimate.ImageCostUsd,
            InputCostUsd = estimate.InputCostUsd,
            OutputCostUsd = estimate.OutputCostUsd,
        };

        return await FunctionResponseFactory.SuccessResponseAsync(request, response, cancellationToken: cancellationToken);
    }
}
