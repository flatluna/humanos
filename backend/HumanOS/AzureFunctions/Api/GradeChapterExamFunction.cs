using System.Net;
using System.Text.Json;
using HumanOS.Data;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.AzureFunctions.Api;

/// <summary>
/// Stateless grading for one Traditional Course chapter's final
/// multiple-choice exam — per the explicit simplicity-scoping decision for
/// this feature, NO attempt/history rows are written to SQL; the caller
/// submits its answers, the server compares them against
/// <see cref="Models.Capabilities.Traditional.CapabilityChapterExamQuestion.CorrectOptionIndex"/>
/// (never sent to the client — see GetTraditionalCourseFunction) and
/// returns the score + per-question correctness in the same call.
/// </summary>
public sealed class GradeChapterExamFunction
{
    private readonly HumanOsDbContext _dbContext;

    public GradeChapterExamFunction(HumanOsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public sealed class AnswerDto
    {
        public Guid QuestionId { get; set; }

        public int SelectedOptionIndex { get; set; }
    }

    public sealed class GradeRequest
    {
        public List<AnswerDto> Answers { get; set; } = [];
    }

    public sealed class QuestionResultDto
    {
        public Guid QuestionId { get; set; }

        public bool IsCorrect { get; set; }

        public int CorrectOptionIndex { get; set; }

        public int? SelectedOptionIndex { get; set; }
    }

    public sealed class GradeResultDto
    {
        public Guid ChapterId { get; set; }

        public int TotalQuestions { get; set; }

        public int CorrectCount { get; set; }

        public double ScorePercent { get; set; }

        public List<QuestionResultDto> Questions { get; set; } = [];
    }

    [Function("GradeChapterExam")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "studio/traditional-course/chapters/{chapterId:guid}/grade-exam")]
        HttpRequestData request,
        Guid chapterId,
        CancellationToken cancellationToken)
    {
        var body = await JsonSerializer.DeserializeAsync<GradeRequest>(
            request.Body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }, cancellationToken);

        if (body is null || body.Answers.Count == 0)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidRequest", "'answers' must contain at least one entry.", cancellationToken);
        }

        var questions = await _dbContext.CapabilityChapterExamQuestions
            .AsNoTracking()
            .Where(q => q.CapabilityChapterId == chapterId)
            .OrderBy(q => q.SortOrder)
            .ToListAsync(cancellationToken);

        if (questions.Count == 0)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "ChapterExamNotFound",
                $"No exam questions found for CapabilityChapterId '{chapterId}'.", cancellationToken);
        }

        var answersByQuestionId = body.Answers
            .GroupBy(a => a.QuestionId)
            .ToDictionary(g => g.Key, g => g.First().SelectedOptionIndex);

        var questionResults = questions.Select(q =>
        {
            var hasAnswer = answersByQuestionId.TryGetValue(q.CapabilityChapterExamQuestionId, out var selectedOptionIndex);
            return new QuestionResultDto
            {
                QuestionId = q.CapabilityChapterExamQuestionId,
                CorrectOptionIndex = q.CorrectOptionIndex,
                SelectedOptionIndex = hasAnswer ? selectedOptionIndex : null,
                IsCorrect = hasAnswer && selectedOptionIndex == q.CorrectOptionIndex
            };
        }).ToList();

        var correctCount = questionResults.Count(q => q.IsCorrect);

        var result = new GradeResultDto
        {
            ChapterId = chapterId,
            TotalQuestions = questions.Count,
            CorrectCount = correctCount,
            ScorePercent = Math.Round(100.0 * correctCount / questions.Count, 1),
            Questions = questionResults
        };

        return await FunctionResponseFactory.SuccessResponseAsync(request, result, cancellationToken: cancellationToken);
    }
}
