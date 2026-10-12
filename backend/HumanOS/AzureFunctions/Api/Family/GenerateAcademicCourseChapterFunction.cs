using System.Net;
using System.Text.Json;
using HumanOS.Agents.Family;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api.Family;

/// <summary>
/// POST /family/academic/generate-course-chapter
/// "Step 2" (2026-10-08) of the two-step course generation flow — runs
/// ProfesorExpertAgent.GenerateChapterContentAsync for ONE already-approved
/// chapter outline (see <see cref="GenerateAcademicCourseIndexFunction"/>).
/// The caller invokes this ONCE PER CHAPTER, sequentially, assembling the
/// final CourseDocumentMarkdown by concatenating each chapter's
/// ChapterMarkdown in order after the index — never batching multiple
/// chapters into one call.
/// </summary>
public sealed class GenerateAcademicCourseChapterFunction
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private readonly ProfesorExpertAgent _agent;

    public GenerateAcademicCourseChapterFunction(ProfesorExpertAgent agent)
    {
        _agent = agent;
    }

    [Function("GenerateAcademicCourseChapter")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "family/academic/generate-course-chapter")]
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        AcademicChapterContentRequest? body;
        try
        {
            body = await JsonSerializer.DeserializeAsync<AcademicChapterContentRequest>(request.Body, JsonOptions, cancellationToken);
        }
        catch (JsonException ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidJson", $"Malformed JSON payload: {ex.Message}", cancellationToken);
        }

        if (body is null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "MissingPayload", "A valid chapter content request payload is required.", cancellationToken);
        }

        try
        {
            var result = await _agent.GenerateChapterContentAsync(body, cancellationToken);
            return await FunctionResponseFactory.SuccessResponseAsync(request, result, HttpStatusCode.OK, cancellationToken);
        }
        catch (Exception ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.InternalServerError, "ChapterGenerationFailed", ex.Message, cancellationToken);
        }
    }
}
