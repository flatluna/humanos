using HumanOS.Data;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.AzureFunctions.Api;

public sealed class StructuredLearningSectionVideoDto
{
    public Guid IllustrationId { get; set; }
    public string Purpose { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
    /// <summary>The final prompt actually sent to Veo/Omni for this clip
    /// (2026-08-25) — lets a reviewer validate what each clip in a
    /// multi-clip sequence actually asked for.</summary>
    public string Prompt { get; set; } = string.Empty;
}

/// <summary>Lists every Google Veo video already generated for one
/// Structured Learning section (2026-08-21) — used by Studio's edit mode
/// to show existing videos and how many of the
/// <see cref="StructuredLearningVideoOrchestrator.MaxVideosPerSection"/>
/// slots remain.</summary>
public sealed class GetStructuredLearningSectionVideosFunction
{
    private readonly HumanOsDbContext _dbContext;

    public GetStructuredLearningSectionVideosFunction(HumanOsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [Function("GetStructuredLearningSectionVideos")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "studio/structured-learning/sections/{sectionId:guid}/videos")]
        HttpRequestData request,
        Guid sectionId,
        CancellationToken cancellationToken)
    {
        var videos = await _dbContext.StructuredLearningIllustrations
            .AsNoTracking()
            .Where(i => i.StructuredLearningDocumentSectionId == sectionId && i.MediaType == "Video")
            .OrderBy(i => i.CreatedDate)
            .Select(i => new StructuredLearningSectionVideoDto
            {
                IllustrationId = i.StructuredLearningIllustrationId,
                Purpose = i.Purpose.ToString(),
                CreatedDate = i.CreatedDate,
                Prompt = i.Prompt
            })
            .ToListAsync(cancellationToken);

        return await FunctionResponseFactory.SuccessResponseAsync(request, videos, cancellationToken: cancellationToken);
    }
}
