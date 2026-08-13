using System.Net;
using System.Text.Json;
using HumanOS.Data;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

public sealed class EditTraditionalCourseTitleRequest
{
    public string Instruction { get; set; } = string.Empty;
}

public sealed class SetTraditionalCourseTitleRequest
{
    public string Title { get; set; } = string.Empty;
    public bool Confirmed { get; set; }
}

public sealed class EditTraditionalCourseTextRequest
{
    public string Instruction { get; set; } = string.Empty;
}

public sealed class SetTraditionalCourseTextRequest
{
    public string Text { get; set; } = string.Empty;
    public bool Confirmed { get; set; }
}

public sealed class EditTraditionalCourseChapterTitleFunction
{
    private readonly TraditionalCourseReviewService _service;
    private readonly HumanOsDbContext _dbContext;

    public EditTraditionalCourseChapterTitleFunction(TraditionalCourseReviewService service, HumanOsDbContext dbContext)
    {
        _service = service;
        _dbContext = dbContext;
    }

    [Function("EditTraditionalCourseChapterTitle")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "studio/traditional-course/chapters/{chapterId:guid}/title/edit")]
        HttpRequestData request,
        Guid chapterId,
        CancellationToken cancellationToken)
    {
        EditTraditionalCourseTitleRequest? body;
        try
        {
            body = await JsonSerializer.DeserializeAsync<EditTraditionalCourseTitleRequest>(request.Body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }, cancellationToken)
                ?? new EditTraditionalCourseTitleRequest();
        }
        catch (JsonException)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(request, HttpStatusCode.BadRequest, "InvalidJson", "The request body contains invalid JSON.", cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(body.Instruction))
        {
            return await FunctionResponseFactory.ErrorResponseAsync(request, HttpStatusCode.BadRequest, "MissingFields", "Instruction is required.", cancellationToken);
        }

        try
        {
            var updated = await _service.EditChapterTitleAsync(_dbContext, chapterId, body.Instruction, cancellationToken);
            return await FunctionResponseFactory.SuccessResponseAsync(request, new ChapterDto
            {
                ChapterId = updated.CapabilityChapterId,
                Title = updated.Title,
                SortOrder = updated.SortOrder,
                Lessons = [],
                ExamQuestions = []
            }, cancellationToken: cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(request, HttpStatusCode.BadRequest, "InvalidEdit", ex.Message, cancellationToken);
        }
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

public sealed class SetTraditionalCourseChapterTitleFunction
{
    private readonly TraditionalCourseReviewService _service;
    private readonly HumanOsDbContext _dbContext;

    public SetTraditionalCourseChapterTitleFunction(TraditionalCourseReviewService service, HumanOsDbContext dbContext)
    {
        _service = service;
        _dbContext = dbContext;
    }

    [Function("SetTraditionalCourseChapterTitle")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "studio/traditional-course/chapters/{chapterId:guid}/title")]
        HttpRequestData request,
        Guid chapterId,
        CancellationToken cancellationToken)
    {
        SetTraditionalCourseTitleRequest? body;
        try
        {
            body = await JsonSerializer.DeserializeAsync<SetTraditionalCourseTitleRequest>(request.Body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }, cancellationToken)
                ?? new SetTraditionalCourseTitleRequest();
        }
        catch (JsonException)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(request, HttpStatusCode.BadRequest, "InvalidJson", "The request body contains invalid JSON.", cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(body.Title))
        {
            return await FunctionResponseFactory.ErrorResponseAsync(request, HttpStatusCode.BadRequest, "MissingFields", "Title is required.", cancellationToken);
        }

        if (!body.Confirmed)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(request, HttpStatusCode.BadRequest, "ConfirmationRequired", "You must confirm before saving manual edits.", cancellationToken);
        }

        try
        {
            var updated = await _service.SetChapterTitleManuallyAsync(_dbContext, chapterId, body.Title, cancellationToken);
            return await FunctionResponseFactory.SuccessResponseAsync(request, new ChapterDto
            {
                ChapterId = updated.CapabilityChapterId,
                Title = updated.Title,
                SortOrder = updated.SortOrder,
                Lessons = [],
                ExamQuestions = []
            }, cancellationToken: cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(request, HttpStatusCode.BadRequest, "InvalidEdit", ex.Message, cancellationToken);
        }
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

public sealed class EditTraditionalCourseLessonTitleFunction
{
    private readonly TraditionalCourseReviewService _service;
    private readonly HumanOsDbContext _dbContext;

    public EditTraditionalCourseLessonTitleFunction(TraditionalCourseReviewService service, HumanOsDbContext dbContext)
    {
        _service = service;
        _dbContext = dbContext;
    }

    [Function("EditTraditionalCourseLessonTitle")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "studio/traditional-course/lessons/{lessonId:guid}/title/edit")]
        HttpRequestData request,
        Guid lessonId,
        CancellationToken cancellationToken)
    {
        EditTraditionalCourseTitleRequest? body;
        try
        {
            body = await JsonSerializer.DeserializeAsync<EditTraditionalCourseTitleRequest>(request.Body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }, cancellationToken)
                ?? new EditTraditionalCourseTitleRequest();
        }
        catch (JsonException)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(request, HttpStatusCode.BadRequest, "InvalidJson", "The request body contains invalid JSON.", cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(body.Instruction))
        {
            return await FunctionResponseFactory.ErrorResponseAsync(request, HttpStatusCode.BadRequest, "MissingFields", "Instruction is required.", cancellationToken);
        }

        try
        {
            var updated = await _service.EditLessonTitleAsync(_dbContext, lessonId, body.Instruction, cancellationToken);
            return await FunctionResponseFactory.SuccessResponseAsync(request, new LessonDto
            {
                LessonId = updated.CapabilityLessonId,
                Title = updated.Title,
                SortOrder = updated.SortOrder,
                Cards = []
            }, cancellationToken: cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(request, HttpStatusCode.BadRequest, "InvalidEdit", ex.Message, cancellationToken);
        }
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
}

public sealed class SetTraditionalCourseLessonTitleFunction
{
    private readonly TraditionalCourseReviewService _service;
    private readonly HumanOsDbContext _dbContext;

    public SetTraditionalCourseLessonTitleFunction(TraditionalCourseReviewService service, HumanOsDbContext dbContext)
    {
        _service = service;
        _dbContext = dbContext;
    }

    [Function("SetTraditionalCourseLessonTitle")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "studio/traditional-course/lessons/{lessonId:guid}/title")]
        HttpRequestData request,
        Guid lessonId,
        CancellationToken cancellationToken)
    {
        SetTraditionalCourseTitleRequest? body;
        try
        {
            body = await JsonSerializer.DeserializeAsync<SetTraditionalCourseTitleRequest>(request.Body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }, cancellationToken)
                ?? new SetTraditionalCourseTitleRequest();
        }
        catch (JsonException)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(request, HttpStatusCode.BadRequest, "InvalidJson", "The request body contains invalid JSON.", cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(body.Title))
        {
            return await FunctionResponseFactory.ErrorResponseAsync(request, HttpStatusCode.BadRequest, "MissingFields", "Title is required.", cancellationToken);
        }

        if (!body.Confirmed)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(request, HttpStatusCode.BadRequest, "ConfirmationRequired", "You must confirm before saving manual edits.", cancellationToken);
        }

        try
        {
            var updated = await _service.SetLessonTitleManuallyAsync(_dbContext, lessonId, body.Title, cancellationToken);
            return await FunctionResponseFactory.SuccessResponseAsync(request, new LessonDto
            {
                LessonId = updated.CapabilityLessonId,
                Title = updated.Title,
                SortOrder = updated.SortOrder,
                Cards = []
            }, cancellationToken: cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(request, HttpStatusCode.BadRequest, "InvalidEdit", ex.Message, cancellationToken);
        }
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
}

public sealed class EditTraditionalCourseCardTextFunction
{
    private readonly TraditionalCourseReviewService _service;
    private readonly HumanOsDbContext _dbContext;

    public EditTraditionalCourseCardTextFunction(TraditionalCourseReviewService service, HumanOsDbContext dbContext)
    {
        _service = service;
        _dbContext = dbContext;
    }

    [Function("EditTraditionalCourseCardText")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "studio/traditional-course/cards/{cardId:guid}/text/edit")]
        HttpRequestData request,
        Guid cardId,
        CancellationToken cancellationToken)
    {
        EditTraditionalCourseTextRequest? body;
        try
        {
            body = await JsonSerializer.DeserializeAsync<EditTraditionalCourseTextRequest>(request.Body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }, cancellationToken)
                ?? new EditTraditionalCourseTextRequest();
        }
        catch (JsonException)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(request, HttpStatusCode.BadRequest, "InvalidJson", "The request body contains invalid JSON.", cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(body.Instruction))
        {
            return await FunctionResponseFactory.ErrorResponseAsync(request, HttpStatusCode.BadRequest, "MissingFields", "Instruction is required.", cancellationToken);
        }

        try
        {
            var updated = await _service.EditCardTextAsync(_dbContext, cardId, body.Instruction, cancellationToken);
            return await FunctionResponseFactory.SuccessResponseAsync(request, new CardDto
            {
                CardId = updated.CapabilityLessonCardId,
                Title = updated.Title,
                Text = updated.Text,
                SortOrder = updated.SortOrder,
                IllustrationId = updated.CapabilityTraditionalIllustrationId
            }, cancellationToken: cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(request, HttpStatusCode.BadRequest, "InvalidEdit", ex.Message, cancellationToken);
        }
    }

    private sealed class CardDto
    {
        public Guid CardId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public int SortOrder { get; set; }
        public Guid? IllustrationId { get; set; }
    }
}

public sealed class SetTraditionalCourseCardTextFunction
{
    private readonly TraditionalCourseReviewService _service;
    private readonly HumanOsDbContext _dbContext;

    public SetTraditionalCourseCardTextFunction(TraditionalCourseReviewService service, HumanOsDbContext dbContext)
    {
        _service = service;
        _dbContext = dbContext;
    }

    [Function("SetTraditionalCourseCardText")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "studio/traditional-course/cards/{cardId:guid}/text")]
        HttpRequestData request,
        Guid cardId,
        CancellationToken cancellationToken)
    {
        SetTraditionalCourseTextRequest? body;
        try
        {
            body = await JsonSerializer.DeserializeAsync<SetTraditionalCourseTextRequest>(request.Body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }, cancellationToken)
                ?? new SetTraditionalCourseTextRequest();
        }
        catch (JsonException)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(request, HttpStatusCode.BadRequest, "InvalidJson", "The request body contains invalid JSON.", cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(body.Text))
        {
            return await FunctionResponseFactory.ErrorResponseAsync(request, HttpStatusCode.BadRequest, "MissingFields", "Text is required.", cancellationToken);
        }

        if (!body.Confirmed)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(request, HttpStatusCode.BadRequest, "ConfirmationRequired", "You must confirm before saving manual edits.", cancellationToken);
        }

        try
        {
            var updated = await _service.SetCardTextManuallyAsync(_dbContext, cardId, body.Text, cancellationToken);
            return await FunctionResponseFactory.SuccessResponseAsync(request, new CardDto
            {
                CardId = updated.CapabilityLessonCardId,
                Title = updated.Title,
                Text = updated.Text,
                SortOrder = updated.SortOrder,
                IllustrationId = updated.CapabilityTraditionalIllustrationId
            }, cancellationToken: cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(request, HttpStatusCode.BadRequest, "InvalidEdit", ex.Message, cancellationToken);
        }
    }

    private sealed class CardDto
    {
        public Guid CardId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public int SortOrder { get; set; }
        public Guid? IllustrationId { get; set; }
    }
}
