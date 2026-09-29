using System.Net;
using System.Text.Json;
using HumanOS.Data;
using HumanOS.Storage;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.AzureFunctions.Api;

public sealed class SaveVideoProductionDraftRequest
{
    public string Objective { get; set; } = string.Empty;
    public string Instructions { get; set; } = string.Empty;
    public string Environment { get; set; } = string.Empty;
    public int MaxClips { get; set; }
    public List<string> Prompts { get; set; } = [];
    public string ImageMode { get; set; } = "none";
    public string ImagePrompt { get; set; } = string.Empty;
    public string ImageEditPrompt { get; set; } = string.Empty;
    /// <summary>Raw base64 bytes of the wizard's own uploaded starting
    /// image (upload/edit modes only) — omitted/null when there's nothing
    /// new to save (e.g. the reviewer didn't touch step 4 again).</summary>
    public string? StartingImageBase64 { get; set; }
    public string? StartingImageContentType { get; set; }
}

/// <summary>Saves the wizard's own draft inputs and planned prompt
/// sequence (2026-09-24) — a VideoProductionClip row only exists once the
/// user actually clicks "Create video" for that clip, so without this a
/// generated prompt sequence the user never clicked through on was never
/// persisted anywhere, and reopening the project later showed nothing at
/// all. Called right after prompt-sequence generation succeeds.</summary>
public sealed class SaveVideoProductionDraftFunction
{
    private readonly HumanOsDbContext _dbContext;
    private readonly CapabilityGraphIllustrationStorageService _storage;

    public SaveVideoProductionDraftFunction(HumanOsDbContext dbContext, CapabilityGraphIllustrationStorageService storage)
    {
        _dbContext = dbContext;
        _storage = storage;
    }

    [Function("SaveVideoProductionDraft")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "studio/video-productions/{videoProductionId:guid}/draft")]
        HttpRequestData request,
        Guid videoProductionId,
        CancellationToken cancellationToken)
    {
        SaveVideoProductionDraftRequest? body;
        try
        {
            body = await JsonSerializer.DeserializeAsync<SaveVideoProductionDraftRequest>(
                request.Body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }, cancellationToken);
        }
        catch (JsonException)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidJson", "The request body contains invalid JSON.", cancellationToken);
        }

        if (body is null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "MissingFields", "A draft body is required.", cancellationToken);
        }

        var production = await _dbContext.VideoProductions
            .FirstOrDefaultAsync(p => p.VideoProductionId == videoProductionId, cancellationToken);

        if (production is null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "ProductionNotFound", $"No VideoProduction found with id {videoProductionId}.", cancellationToken);
        }

        production.Objective = body.Objective;
        production.Instructions = body.Instructions;
        production.Environment = body.Environment;
        production.MaxClips = body.MaxClips;
        production.PromptsJson = JsonSerializer.Serialize(body.Prompts);
        production.ImageMode = body.ImageMode;
        production.ImagePrompt = body.ImagePrompt;
        production.ImageEditPrompt = body.ImageEditPrompt;

        if (!string.IsNullOrEmpty(body.StartingImageBase64) && _storage.IsConfigured)
        {
            var imageBytes = Convert.FromBase64String(body.StartingImageBase64);
            using var stream = new MemoryStream(imageBytes);
            production.StartingImageStoragePath = await _storage.UploadIllustrationAsync(
                production.TenantId, videoProductionId, videoProductionId, 0, stream,
                body.StartingImageContentType ?? "image/png", cancellationToken);
            production.StartingImageContentType = body.StartingImageContentType ?? "image/png";
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return await FunctionResponseFactory.SuccessResponseAsync(request, production, cancellationToken: cancellationToken);
    }
}
