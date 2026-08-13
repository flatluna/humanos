using System.Net;
using HumanOS.Data;
using HumanOS.Models.Capabilities;
using HumanOS.Models.Capabilities.Traditional;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.AzureFunctions.Api;

public sealed class AddTraditionalCourseChapterRequest
{
    public string Title { get; set; } = string.Empty;
}

public sealed class AddTraditionalCourseChapterFunction
{
    private readonly HumanOsDbContext _dbContext;

    public AddTraditionalCourseChapterFunction(HumanOsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [Function("AddTraditionalCourseChapter")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "studio/traditional-course/{capabilityId:guid}/chapters/add")]
        HttpRequestData request,
        Guid capabilityId,
        CancellationToken cancellationToken)
    {
        AddTraditionalCourseChapterRequest? body;
        try
        {
            body = await request.ReadFromJsonAsync<AddTraditionalCourseChapterRequest>(cancellationToken)
                ?? new AddTraditionalCourseChapterRequest();
        }
        catch (Exception)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.BadRequest,
                "InvalidJson",
                "The request body contains invalid JSON.",
                cancellationToken);
        }

        var title = body.Title?.Trim();
        if (string.IsNullOrWhiteSpace(title))
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.BadRequest,
                "MissingFields",
                "Title is required.",
                cancellationToken);
        }

        var capabilityExists = await _dbContext.Capabilities
            .AnyAsync(c => c.CapabilityId == capabilityId && c.LearningMode == CourseLearningMode.TraditionalChapters, cancellationToken);

        if (!capabilityExists)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.NotFound,
                "TraditionalCourseNotFound",
                $"No traditional-mode course found with CapabilityId '{capabilityId}'.",
                cancellationToken);
        }

        var nextSortOrder = await _dbContext.CapabilityChapters
            .Where(c => c.CapabilityId == capabilityId)
            .Select(c => (int?)c.SortOrder)
            .MaxAsync(cancellationToken) ?? 0;

        var chapter = new CapabilityChapter
        {
            CapabilityChapterId = Guid.NewGuid(),
            CapabilityId = capabilityId,
            Title = title,
            SortOrder = nextSortOrder + 1,
            CreatedDate = DateTime.UtcNow
        };

        _dbContext.CapabilityChapters.Add(chapter);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var dto = new ChapterDto
        {
            ChapterId = chapter.CapabilityChapterId,
            Title = chapter.Title,
            SortOrder = chapter.SortOrder,
            Lessons = [],
            ExamQuestions = []
        };

        return await FunctionResponseFactory.SuccessResponseAsync(request, dto, cancellationToken: cancellationToken);
    }

    private sealed class ChapterDto
    {
        public Guid ChapterId { get; set; }

        public string Title { get; set; } = string.Empty;

        public int SortOrder { get; set; }

        public List<LessonDto> Lessons { get; set; } = [];

        public List<ExamQuestionDto> ExamQuestions { get; set; } = [];
    }

    private sealed class LessonDto
    {
        public Guid LessonId { get; set; }

        public string Title { get; set; } = string.Empty;

        public int SortOrder { get; set; }

        public List<CardDto> Cards { get; set; } = [];
    }

    private sealed class CardDto
    {
        public Guid CardId { get; set; }

        public string Title { get; set; } = string.Empty;

        public string Text { get; set; } = string.Empty;

        public int SortOrder { get; set; }

        public Guid? IllustrationId { get; set; }
    }

    private sealed class ExamQuestionDto
    {
        public Guid QuestionId { get; set; }

        public string QuestionText { get; set; } = string.Empty;

        public List<string> Options { get; set; } = [];

        public int SortOrder { get; set; }

        public Guid? IllustrationId { get; set; }
    }
}
