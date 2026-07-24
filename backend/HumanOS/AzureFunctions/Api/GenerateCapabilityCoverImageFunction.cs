using System.Net;
using System.Text.Json;
using HumanOS.Agents.Studio;
using HumanOS.Data;
using HumanOS.Storage;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.AzureFunctions.Api;

/// <summary>
/// (Re)generates the course-level cover image for a Capability that
/// already has a persisted CapabilityGraph. Exists for two reasons:
/// backfilling capabilities created before the automatic cover-image step
/// existed in <see cref="Services.PdfCapabilityGraphPipelineService"/>
/// (2026-07-21), and letting a designer retry after a best-effort
/// generation failure. Overwrites any existing cover image.
/// </summary>
public sealed class GenerateCapabilityCoverImageFunction
{
    private readonly HumanOsDbContext _dbContext;
    private readonly GraphIllustrationImageService _imageService;
    private readonly CapabilityGraphIllustrationStorageService _illustrationStorage;

    public GenerateCapabilityCoverImageFunction(
        HumanOsDbContext dbContext,
        GraphIllustrationImageService imageService,
        CapabilityGraphIllustrationStorageService illustrationStorage)
    {
        _dbContext = dbContext;
        _imageService = imageService;
        _illustrationStorage = illustrationStorage;
    }

    public sealed class GenerateRequest
    {
        public Guid TenantId { get; set; }
    }

    [Function("GenerateCapabilityCoverImage")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "capabilities/{capabilityId:guid}/cover-image/generate")]
        HttpRequestData request,
        Guid capabilityId,
        CancellationToken cancellationToken)
    {
        if (!_imageService.IsConfigured || !_illustrationStorage.IsConfigured)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.ServiceUnavailable, "ImageGenerationNotConfigured",
                "Image generation is not configured (missing Azure OpenAI or Data Lake settings).",
                cancellationToken);
        }

        var body = await JsonSerializer.DeserializeAsync<GenerateRequest>(
            request.Body,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
            cancellationToken);

        if (body is null || body.TenantId == Guid.Empty)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidRequest",
                "'tenantId' is required.", cancellationToken);
        }

        var capability = await _dbContext.Capabilities
            .Include(c => c.CapabilityGraph)
            .SingleOrDefaultAsync(c => c.CapabilityId == capabilityId, cancellationToken);

        if (capability is null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "CapabilityNotFound",
                $"No capability found with id {capabilityId}.", cancellationToken);
        }

        if (capability.CapabilityGraph is null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.Conflict, "GraphNotFound",
                "This capability has no CapabilityGraph yet — a cover image needs a generated graph first.",
                cancellationToken);
        }

        var coverPrompt =
            $"A clean, modern, professional editorial illustration representing a course titled " +
            $"'{capability.Name}'. {capability.CapabilityGraph.ExecutiveSummary ?? capability.Description}. " +
            "Flat design, no text, no letters, no words, wide banner composition, soft color palette.";

        var generatedCover = await _imageService.GenerateAsync(coverPrompt, cancellationToken);
        using var coverStream = generatedCover.ImageBytes.ToStream();
        var storagePath = await _illustrationStorage.UploadCoverImageAsync(
            body.TenantId, capabilityId, coverStream, cancellationToken: cancellationToken);

        capability.CapabilityGraph.CoverImageStoragePath = storagePath;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return await FunctionResponseFactory.SuccessResponseAsync(
            request, new { capabilityId, coverImageUrl = $"/capabilities/{capabilityId}/cover-image" }, cancellationToken: cancellationToken);
    }
}
