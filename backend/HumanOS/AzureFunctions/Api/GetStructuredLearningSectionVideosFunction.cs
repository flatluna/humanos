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

    /// <summary>Which model actually generated this clip (2026-08-26) —
    /// e.g. "veo-3.1-fast-generate-preview", "gemini-omni-flash-preview",
    /// or "merged" — lets the UI show/total the real per-clip cost.</summary>
    public string ImageModel { get; set; } = string.Empty;

    /// <summary>Whether students see this video in real-mode (2026-08-26).</summary>
    public bool IsPublished { get; set; } = true;
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
        // Real-mode student runtime calls this SAME endpoint through
        // StructuredLearningRuntimeProxyFunction, which appends a
        // `personId` query param (the volunteer's resolved HumanOS
        // PersonId) — Studio's own edit-mode calls never send it. Reuse
        // that existing signal (2026-08-26) to hide unpublished videos
        // from students while edit mode keeps seeing every video.
        var isLearnerRequest = !string.IsNullOrWhiteSpace(
            System.Web.HttpUtility.ParseQueryString(request.Url.Query)["personId"]);

        var query = _dbContext.StructuredLearningIllustrations
            .AsNoTracking()
            .Where(i => i.StructuredLearningDocumentSectionId == sectionId && i.MediaType == "Video");

        if (isLearnerRequest)
        {
            query = query.Where(i => i.IsPublished);
        }

        var videos = await query
            .OrderBy(i => i.CreatedDate)
            .Select(i => new StructuredLearningSectionVideoDto
            {
                IllustrationId = i.StructuredLearningIllustrationId,
                Purpose = i.Purpose.ToString(),
                CreatedDate = i.CreatedDate,
                Prompt = i.Prompt,
                ImageModel = i.ImageModel,
                IsPublished = i.IsPublished
            })
            .ToListAsync(cancellationToken);

        return await FunctionResponseFactory.SuccessResponseAsync(request, videos, cancellationToken: cancellationToken);
    }
}
