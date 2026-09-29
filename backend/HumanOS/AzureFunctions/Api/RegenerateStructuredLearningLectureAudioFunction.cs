using System.Net;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

public sealed class RegenerateStructuredLearningLectureAudioResponseDto
{
    public Guid SectionId { get; set; }
    public string SpeechStoragePath { get; set; } = string.Empty;
}

/// <summary>
/// "Regenerate audio" escape hatch (2026-09-26) — re-synthesizes the Teaching step's audio
/// from its CURRENT Content verbatim, without re-inventing the lecture script via
/// StructuredLearningProfessorLectureAgent (see StructuredLearningProfessorLectureFunction for
/// that fuller flow). Meant to run right after a reviewer edits the Teaching step's text
/// (manually via SetStructuredLearningStepContentFunction or via EditStructuredLearningStepFunction)
/// so the audio narration matches whatever text is now on screen.
/// </summary>
public sealed class RegenerateStructuredLearningLectureAudioFunction
{
    private readonly StructuredLearningProfessorLectureService _lectureService;

    public RegenerateStructuredLearningLectureAudioFunction(StructuredLearningProfessorLectureService lectureService)
    {
        _lectureService = lectureService;
    }

    [Function("RegenerateStructuredLearningLectureAudio")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "studio/structured-learning/sections/{sectionId:guid}/professor-lecture/audio/regenerate")]
        HttpRequestData request,
        Guid sectionId,
        CancellationToken cancellationToken)
    {
        try
        {
            var storagePath = await _lectureService.RegenerateAudioFromCurrentContentAsync(sectionId, cancellationToken);
            return await FunctionResponseFactory.SuccessResponseAsync(request, new RegenerateStructuredLearningLectureAudioResponseDto
            {
                SectionId = sectionId,
                SpeechStoragePath = storagePath
            }, cancellationToken: cancellationToken);
        }
        catch (KeyNotFoundException ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "SectionNotFound", ex.Message, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "AudioRegenerationUnavailable", ex.Message, cancellationToken);
        }
    }
}
