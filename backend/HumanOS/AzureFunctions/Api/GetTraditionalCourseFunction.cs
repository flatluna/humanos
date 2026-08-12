using System.Net;
using System.Text.Json;
using HumanOS.Data;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.AzureFunctions.Api;

/// <summary>
/// Returns the full content of a Traditional Course (chapters -> lessons ->
/// reading cards, plus each chapter's exam questions/options) for Studio's
/// preview page and the student-facing runtime. Deliberately never
/// includes <see cref="Models.Capabilities.Traditional.CapabilityChapterExamQuestion.CorrectOptionIndex"/>
/// in this response — the answer key must never reach the client; grading
/// happens server-side only (see GradeChapterExamFunction).
/// </summary>
public sealed class GetTraditionalCourseFunction
{
    private readonly HumanOsDbContext _dbContext;

    public GetTraditionalCourseFunction(HumanOsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public sealed class CardDto
    {
        public Guid CardId { get; set; }

        public string Title { get; set; } = string.Empty;

        public string Text { get; set; } = string.Empty;

        public int SortOrder { get; set; }

        public Guid? IllustrationId { get; set; }
    }

    public sealed class LessonDto
    {
        public Guid LessonId { get; set; }

        public string Title { get; set; } = string.Empty;

        public int SortOrder { get; set; }

        public List<CardDto> Cards { get; set; } = [];
    }

    public sealed class ExamQuestionDto
    {
        public Guid QuestionId { get; set; }

        public string QuestionText { get; set; } = string.Empty;

        public List<string> Options { get; set; } = [];

        public int SortOrder { get; set; }

        public Guid? IllustrationId { get; set; }
    }

    public sealed class ChapterDto
    {
        public Guid ChapterId { get; set; }

        public string Title { get; set; } = string.Empty;

        public int SortOrder { get; set; }

        public List<LessonDto> Lessons { get; set; } = [];

        public List<ExamQuestionDto> ExamQuestions { get; set; } = [];
    }

    public sealed class TraditionalCourseDetailDto
    {
        public Guid CapabilityId { get; set; }

        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        public Guid? CoverIllustrationId { get; set; }

        public List<ChapterDto> Chapters { get; set; } = [];
    }

    [Function("GetTraditionalCourse")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "capabilities/{capabilityId:guid}/traditional-course")]
        HttpRequestData request,
        Guid capabilityId,
        CancellationToken cancellationToken)
    {
        var capability = await _dbContext.Capabilities
            .AsNoTracking()
            .Where(c => c.CapabilityId == capabilityId && c.LearningMode == Models.Capabilities.CourseLearningMode.TraditionalChapters)
            .Select(c => new { c.CapabilityId, c.Name, c.Description })
            .FirstOrDefaultAsync(cancellationToken);

        if (capability is null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "TraditionalCourseNotFound",
                $"No traditional-mode course found with CapabilityId '{capabilityId}'.", cancellationToken);
        }

        var coverIllustrationId = await _dbContext.CapabilityTraditionalIllustrations
            .AsNoTracking()
            .Where(i => i.CapabilityId == capabilityId && i.Purpose == Models.Capabilities.Traditional.TraditionalIllustrationPurpose.Cover)
            .Select(i => (Guid?)i.CapabilityTraditionalIllustrationId)
            .FirstOrDefaultAsync(cancellationToken);

        // Materialize the real entities first (Include/ThenInclude) — the
        // OptionsJson -> List&lt;string&gt; deserialization below can't be
        // translated into SQL, so it must run in-memory AFTER the query
        // executes, never inside a Select() that EF still has to translate.
        var chapterEntities = await _dbContext.CapabilityChapters
            .AsNoTracking()
            .Include(c => c.Lessons).ThenInclude(l => l.Cards)
            .Include(c => c.ExamQuestions)
            .Where(c => c.CapabilityId == capabilityId)
            .OrderBy(c => c.SortOrder)
            .ToListAsync(cancellationToken);

        var chapters = chapterEntities.Select(c => new ChapterDto
        {
            ChapterId = c.CapabilityChapterId,
            Title = c.Title,
            SortOrder = c.SortOrder,
            Lessons = c.Lessons.OrderBy(l => l.SortOrder).Select(l => new LessonDto
            {
                LessonId = l.CapabilityLessonId,
                Title = l.Title,
                SortOrder = l.SortOrder,
                Cards = l.Cards.OrderBy(card => card.SortOrder).Select(card => new CardDto
                {
                    CardId = card.CapabilityLessonCardId,
                    Title = card.Title,
                    Text = card.Text,
                    SortOrder = card.SortOrder,
                    IllustrationId = card.CapabilityTraditionalIllustrationId
                }).ToList()
            }).ToList(),
            ExamQuestions = c.ExamQuestions.OrderBy(q => q.SortOrder).Select(q => new ExamQuestionDto
            {
                QuestionId = q.CapabilityChapterExamQuestionId,
                QuestionText = q.QuestionText,
                Options = JsonSerializer.Deserialize<List<string>>(q.OptionsJson) ?? [],
                SortOrder = q.SortOrder,
                IllustrationId = q.CapabilityTraditionalIllustrationId
            }).ToList()
        }).ToList();

        var result = new TraditionalCourseDetailDto
        {
            CapabilityId = capability.CapabilityId,
            Name = capability.Name,
            Description = capability.Description,
            CoverIllustrationId = coverIllustrationId,
            Chapters = chapters
        };

        return await FunctionResponseFactory.SuccessResponseAsync(request, result, cancellationToken: cancellationToken);
    }
}
