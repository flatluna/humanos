using System.Net;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

public sealed class DeleteStructuredLearningLectureSceneResponseDto
{
    public Guid SectionId { get; set; }
    public int DeletedSceneIndex { get; set; }
    public int RemainingSceneCount { get; set; }
}

/// <summary>
/// Hard-deletes ONE scene from the Synced Teaching scene player's lecture
/// (2026-11-07) — a designer-only edit-mode escape hatch for dropping a scene
/// whose recording/illustration shouldn't be in the course at all, as opposed to
/// RegenerateStructuredLearningLectureSceneFunction which only swaps out its media.
/// Remaining scenes are renumbered to stay consecutive; always keeps at least one.
/// </summary>
public sealed class DeleteStructuredLearningLectureSceneFunction
{
    private readonly StructuredLearningProfessorLectureService _lectureService;

    public DeleteStructuredLearningLectureSceneFunction(StructuredLearningProfessorLectureService lectureService)
    {
        _lectureService = lectureService;
    }

    [Function("DeleteStructuredLearningLectureScene")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "studio/structured-learning/sections/{sectionId:guid}/professor-lecture/scenes/{sceneIndex:int}")]
        HttpRequestData request,
        Guid sectionId,
        int sceneIndex,
        CancellationToken cancellationToken)
    {
        try
        {
            var remainingCount = await _lectureService.DeleteSceneAsync(sectionId, sceneIndex, cancellationToken);

            return await FunctionResponseFactory.SuccessResponseAsync(request, new DeleteStructuredLearningLectureSceneResponseDto
            {
                SectionId = sectionId,
                DeletedSceneIndex = sceneIndex,
                RemainingSceneCount = remainingCount
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
                request, HttpStatusCode.BadRequest, "SceneDeletionUnavailable", ex.Message, cancellationToken);
        }
    }
}
