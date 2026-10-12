using System.Net;
using System.Text.Json;
using HumanOS.Data;
using HumanOS.Models.Capabilities.Studio;
using HumanOS.Storage;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.AzureFunctions.Api;

/// <summary>
/// Serves the synthesized audio file for an engram's Teaching step from Azure Data Lake.
/// </summary>
public sealed class GetStructuredLearningSectionAudioFunction
{
    private readonly HumanOsDbContext _dbContext;
    private readonly CapabilityGraphIllustrationStorageService _storageService;

    public GetStructuredLearningSectionAudioFunction(
        HumanOsDbContext dbContext,
        CapabilityGraphIllustrationStorageService storageService)
    {
        _dbContext = dbContext;
        _storageService = storageService;
    }

    [Function("GetStructuredLearningSectionAudio")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "studio/structured-learning/sections/{sectionId:guid}/audio")]
        HttpRequestData request,
        Guid sectionId,
        CancellationToken cancellationToken)
    {
        if (!_storageService.IsConfigured)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.ServiceUnavailable,
                "StorageNotConfigured",
                "Data Lake storage is not configured.",
                cancellationToken);
        }

        // Look up Teaching step's StepDataJson to locate SpeechStoragePath / per-scene audio
        var teachingStep = await _dbContext.StructuredLearningSectionSteps
            .AsNoTracking()
            .Where(s => s.StructuredLearningDocumentSectionId == sectionId && s.StepType == StructuredLearningStepType.Teaching)
            .Select(s => new { s.StepDataJson, s.StructuredLearningDocumentSectionId, s.Section.TenantId, s.Section.StructuredLearningDocumentId })
            .FirstOrDefaultAsync(cancellationToken);

        // Per-scene audio (2026-10-08 — synced image+audio player): ?scene=N ALWAYS wins
        // when explicitly requested, selecting Scenes[N].AudioStoragePath from StepDataJson
        // instead of the legacy single lecture.mp3 — only falls through to the legacy
        // SpeechStoragePath/default path below when no `scene` param is given at all.
        //
        // Per-card audio (2026-10-08 — YoungLearner "Aprende" Engram Cards): ?card=N
        // selects Cards[N].AudioStoragePath the same way, UNLESS ?variant=cue is also
        // given, in which case it selects Cards[N].RecognitionCueAudioStoragePath
        // instead (the Grouping card's tap-to-select game's randomly-chosen audio
        // clue — a separate clip precisely so it never also plays the card's full
        // Explanation and gives the answer away). Mutually exclusive with `scene` in
        // practice (a Teaching step only ever drives one presentation at a time on
        // the frontend), but both are checked independently so neither query param
        // can shadow the other.
        var query = System.Web.HttpUtility.ParseQueryString(request.Url.Query);
        var sceneParam = query["scene"];
        var cardParam = query["card"];
        var variantParam = query["variant"];

        string? storagePath = null;
        if (teachingStep is not null && !string.IsNullOrWhiteSpace(teachingStep.StepDataJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(teachingStep.StepDataJson);

                if (!string.IsNullOrWhiteSpace(cardParam) && int.TryParse(cardParam, out var cardIndex) &&
                    doc.RootElement.TryGetProperty("Cards", out var cardsProp) && cardsProp.ValueKind == JsonValueKind.Array)
                {
                    var cardsArray = cardsProp.EnumerateArray().ToList();
                    var audioPropertyName = string.Equals(variantParam, "cue", StringComparison.OrdinalIgnoreCase)
                        ? "RecognitionCueAudioStoragePath"
                        : string.Equals(variantParam, "sfx", StringComparison.OrdinalIgnoreCase)
                            ? "SoundEffectStoragePath"
                            : "AudioStoragePath";
                    if (cardIndex >= 0 && cardIndex < cardsArray.Count &&
                        cardsArray[cardIndex].TryGetProperty(audioPropertyName, out var cardAudioProp) &&
                        cardAudioProp.ValueKind == JsonValueKind.String)
                    {
                        storagePath = cardAudioProp.GetString();
                    }
                }

                if (string.IsNullOrWhiteSpace(storagePath) &&
                    !string.IsNullOrWhiteSpace(sceneParam) && int.TryParse(sceneParam, out var sceneIndex) &&
                    doc.RootElement.TryGetProperty("Scenes", out var scenesProp) && scenesProp.ValueKind == JsonValueKind.Array)
                {
                    var scenesArray = scenesProp.EnumerateArray().ToList();
                    if (sceneIndex >= 0 && sceneIndex < scenesArray.Count &&
                        scenesArray[sceneIndex].TryGetProperty("AudioStoragePath", out var sceneAudioProp) &&
                        sceneAudioProp.ValueKind == JsonValueKind.String)
                    {
                        storagePath = sceneAudioProp.GetString();
                    }
                }

                if (string.IsNullOrWhiteSpace(storagePath) &&
                    doc.RootElement.TryGetProperty("SpeechStoragePath", out var prop) && prop.ValueKind == JsonValueKind.String)
                {
                    storagePath = prop.GetString();
                }
            }
            catch { }
        }

        // Fallback default path if not explicitly recorded in JSON
        if (string.IsNullOrWhiteSpace(storagePath) && teachingStep is not null)
        {
            storagePath = $"{teachingStep.TenantId:D}/{teachingStep.StructuredLearningDocumentId:D}/{teachingStep.StructuredLearningDocumentSectionId:D}/speech/lecture.mp3";
        }

        if (string.IsNullOrWhiteSpace(storagePath))
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.NotFound,
                "AudioNotFound",
                "No speech audio is available for this section.",
                cancellationToken);
        }

        try
        {
            await using var stream = await _storageService.DownloadIllustrationAsync(storagePath, cancellationToken);
            var response = request.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "audio/mpeg");
            response.Headers.Add("Cache-Control", "no-cache, no-store, must-revalidate");
            await stream.CopyToAsync(response.Body, cancellationToken);
            return response;
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.NotFound)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.NotFound,
                "AudioBlobNotFound",
                $"Speech audio blob '{storagePath}' was not found in Data Lake.",
                cancellationToken);
        }
        catch (Exception ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.InternalServerError,
                "AudioDownloadFailed",
                ex.ToString(),
                cancellationToken);
        }
    }
}
