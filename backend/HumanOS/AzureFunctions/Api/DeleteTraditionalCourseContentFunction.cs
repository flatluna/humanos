using System.Net;
using HumanOS.Data;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.AzureFunctions.Api;

public sealed class DeleteTraditionalCourseChapterFunction
{
    private readonly HumanOsDbContext _dbContext;
    private readonly TraditionalCourseDeletionService _deletionService;

    public DeleteTraditionalCourseChapterFunction(HumanOsDbContext dbContext, TraditionalCourseDeletionService deletionService)
    {
        _dbContext = dbContext;
        _deletionService = deletionService;
    }

    [Function("DeleteTraditionalCourseChapter")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "studio/traditional-course/chapters/{chapterId:guid}")]
        HttpRequestData request,
        Guid chapterId,
        CancellationToken cancellationToken)
    {
        var chapterExists = await _dbContext.CapabilityChapters
            .AsNoTracking()
            .AnyAsync(c => c.CapabilityChapterId == chapterId, cancellationToken);

        if (!chapterExists)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.NotFound,
                "ChapterNotFound",
                $"No chapter {chapterId} found.",
                cancellationToken);
        }

        var deleted = await _deletionService.DeleteChapterAsync(chapterId, cancellationToken);

        if (!deleted)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.NotFound,
                "ChapterNotFound",
                "The requested chapter was not found.",
                cancellationToken);
        }

        return request.CreateResponse(HttpStatusCode.NoContent);
    }
}

public sealed class DeleteTraditionalCourseLessonFunction
{
    private readonly HumanOsDbContext _dbContext;
    private readonly TraditionalCourseDeletionService _deletionService;

    public DeleteTraditionalCourseLessonFunction(HumanOsDbContext dbContext, TraditionalCourseDeletionService deletionService)
    {
        _dbContext = dbContext;
        _deletionService = deletionService;
    }

    [Function("DeleteTraditionalCourseLesson")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "studio/traditional-course/lessons/{lessonId:guid}")]
        HttpRequestData request,
        Guid lessonId,
        CancellationToken cancellationToken)
    {
        var lessonExists = await _dbContext.CapabilityLessons
            .AsNoTracking()
            .AnyAsync(l => l.CapabilityLessonId == lessonId, cancellationToken);

        if (!lessonExists)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.NotFound,
                "LessonNotFound",
                $"No lesson {lessonId} found.",
                cancellationToken);
        }

        var deleted = await _deletionService.DeleteLessonAsync(lessonId, cancellationToken);

        if (!deleted)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.NotFound,
                "LessonNotFound",
                "The requested lesson was not found.",
                cancellationToken);
        }

        return request.CreateResponse(HttpStatusCode.NoContent);
    }
}
