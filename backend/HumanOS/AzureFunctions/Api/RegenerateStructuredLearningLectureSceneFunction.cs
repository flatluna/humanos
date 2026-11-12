using System.Net;
using System.Text.Json;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

public sealed class RegenerateStructuredLearningLectureSceneRequestDto
{
    public bool RegenerateImage { get; set; } = true;
    public bool RegenerateVoice { get; set; } = true;
}

public sealed class RegenerateStructuredLearningLectureSceneResponseDto
{
    public Guid SectionId { get; set; }
    public int SceneIndex { get; set; }
    public Guid? IllustrationId { get; set; }
    public Guid? AlternateIllustrationId { get; set; }
    public string? AudioStoragePath { get; set; }
}

/// <summary>
/// Per-scene "change image/voice" escape hatch (2026-11-07) for the Synced Teaching
/// scene player — regenerates ONLY the requested media (image and/or voice) for ONE
/// scene, reusing that scene's existing Text/ImagePrompt verbatim, so a reviewer or
/// student can swap out a single mismatched illustration or a bad voice take without
/// re-running the whole lecture generation. See
/// RegenerateStructuredLearningLectureAudioFunction for the older whole-section,
/// audio-only escape hatch this complements.
/// </summary>
public sealed class RegenerateStructuredLearningLectureSceneFunction
{
    private readonly StructuredLearningProfessorLectureService _lectureService;

    public RegenerateStructuredLearningLectureSceneFunction(StructuredLearningProfessorLectureService lectureService)
    {
        _lectureService = lectureService;
    }

    [Function("RegenerateStructuredLearningLectureScene")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "studio/structured-learning/sections/{sectionId:guid}/professor-lecture/scenes/{sceneIndex:int}/regenerate")]
        HttpRequestData request,
        Guid sectionId,
        int sceneIndex,
        CancellationToken cancellationToken)
    {
        RegenerateStructuredLearningLectureSceneRequestDto? body = null;
        try
        {
            body = await JsonSerializer.DeserializeAsync<RegenerateStructuredLearningLectureSceneRequestDto>(
                request.Body, cancellationToken: cancellationToken);
        }
        catch (JsonException)
        {
            // Empty/missing body falls back to the DTO defaults (regenerate both).
        }

        var regenerateImage = body?.RegenerateImage ?? true;
        var regenerateVoice = body?.RegenerateVoice ?? true;

        try
        {
            var (illustrationId, alternateIllustrationId, audioStoragePath) = await _lectureService.RegenerateSceneAsync(
                sectionId, sceneIndex, regenerateImage, regenerateVoice, cancellationToken);

            return await FunctionResponseFactory.SuccessResponseAsync(request, new RegenerateStructuredLearningLectureSceneResponseDto
            {
                SectionId = sectionId,
                SceneIndex = sceneIndex,
                IllustrationId = illustrationId,
                AlternateIllustrationId = alternateIllustrationId,
                AudioStoragePath = audioStoragePath
            }, cancellationToken: cancellationToken);
        }
        catch (KeyNotFoundException ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "SectionOrSceneNotFound", ex.Message, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "SceneRegenerationUnavailable", ex.Message, cancellationToken);
        }
    }
}
