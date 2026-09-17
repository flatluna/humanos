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

        // Look up Teaching step's StepDataJson to locate SpeechStoragePath
        var teachingStep = await _dbContext.StructuredLearningSectionSteps
            .AsNoTracking()
            .Where(s => s.StructuredLearningDocumentSectionId == sectionId && s.StepType == StructuredLearningStepType.Teaching)
            .Select(s => new { s.StepDataJson, s.StructuredLearningDocumentSectionId, s.Section.TenantId, s.Section.StructuredLearningDocumentId })
            .FirstOrDefaultAsync(cancellationToken);

        string? storagePath = null;
        if (teachingStep is not null && !string.IsNullOrWhiteSpace(teachingStep.StepDataJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(teachingStep.StepDataJson);
                if (doc.RootElement.TryGetProperty("SpeechStoragePath", out var prop) && prop.ValueKind == JsonValueKind.String)
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
