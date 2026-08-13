using System.Net;
using System.Text.RegularExpressions;
using HumanOS.Agents.Studio;
using HumanOS.Data;
using HumanOS.Models.Capabilities.Traditional;
using HumanOS.Storage;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace HumanOS.AzureFunctions.Api;

public sealed class AddTraditionalCourseLessonRequest
{
    public string Title { get; set; } = string.Empty;
}

public sealed class AddTraditionalCourseLessonFunction
{
    private readonly HumanOsDbContext _dbContext;
    private readonly GraphIllustrationImageService _imageService;
    private readonly CapabilityGraphIllustrationStorageService _illustrationStorage;

    public AddTraditionalCourseLessonFunction(HumanOsDbContext dbContext, GraphIllustrationImageService imageService, IConfiguration configuration)
    {
        _dbContext = dbContext;
        _imageService = imageService;
        _illustrationStorage = new CapabilityGraphIllustrationStorageService(configuration);
    }

    public static string BuildLessonCardIllustrationPrompt(string lessonTitle, string? lessonText = null)
    {
        var title = string.IsNullOrWhiteSpace(lessonTitle) ? "this lesson" : lessonTitle.Trim();
        var context = string.IsNullOrWhiteSpace(lessonText) ? "the lesson topic" : Regex.Replace(lessonText.Trim(), @"\s+", " ");
        var summary = context.Length > 220 ? context[..220].TrimEnd() : context;

        return $"Create a clean, professional educational illustration for '{title}'. Show a professor teaching the lesson concept in a welcoming classroom environment, visually reinforcing the key idea: {summary}. Keep the composition clear and modern, with realistic classroom details, warm lighting, and no text or logos in the image.";
    }

    public static CapabilityLessonCard CreateDefaultLessonCard(string lessonTitle, Guid? lessonId = null)
    {
        var normalizedTitle = string.IsNullOrWhiteSpace(lessonTitle) ? "Lesson overview" : lessonTitle.Trim();

        return new CapabilityLessonCard
        {
            CapabilityLessonCardId = Guid.NewGuid(),
            CapabilityLessonId = lessonId ?? Guid.Empty,
            Title = normalizedTitle,
            Text = string.Empty,
            SortOrder = 1,
            CapabilityTraditionalIllustrationId = null,
            CreatedDate = DateTime.UtcNow
        };
    }

    public static CapabilityLessonCard CreateStarterCard(string lessonTitle, string? customText, Guid? lessonId = null)
    {
        var normalizedTitle = string.IsNullOrWhiteSpace(lessonTitle) ? "Lesson overview" : lessonTitle.Trim();
        var text = string.IsNullOrWhiteSpace(customText) ? string.Empty : customText.Trim();

        return new CapabilityLessonCard
        {
            CapabilityLessonCardId = Guid.NewGuid(),
            CapabilityLessonId = lessonId ?? Guid.Empty,
            Title = normalizedTitle,
            Text = text,
            SortOrder = 1,
            CapabilityTraditionalIllustrationId = null,
            CreatedDate = DateTime.UtcNow
        };
    }

    [Function("AddTraditionalCourseLesson")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "studio/traditional-course/chapters/{chapterId:guid}/lessons/add")]
        HttpRequestData request,
        Guid chapterId,
        CancellationToken cancellationToken)
    {
        AddTraditionalCourseLessonRequest? body;
        try
        {
            body = await request.ReadFromJsonAsync<AddTraditionalCourseLessonRequest>(cancellationToken)
                ?? new AddTraditionalCourseLessonRequest();
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

        var chapter = await _dbContext.CapabilityChapters
            .Include(c => c.Lessons)
            .FirstOrDefaultAsync(c => c.CapabilityChapterId == chapterId, cancellationToken);

        if (chapter is null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.NotFound,
                "TraditionalCourseChapterNotFound",
                $"No traditional course chapter found with ChapterId '{chapterId}'.",
                cancellationToken);
        }

        var nextSortOrder = chapter.Lessons.Any() ? chapter.Lessons.Max(l => l.SortOrder) + 1 : 1;

        var lesson = new CapabilityLesson
        {
            CapabilityLessonId = Guid.NewGuid(),
            CapabilityChapterId = chapterId,
            Title = title,
            SortOrder = nextSortOrder,
            CreatedDate = DateTime.UtcNow
        };

        var defaultCard = CreateDefaultLessonCard(title, lesson.CapabilityLessonId);
        lesson.Cards.Add(defaultCard);

        _dbContext.CapabilityLessons.Add(lesson);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await TryGenerateLessonCardIllustrationAsync(defaultCard, chapter.CapabilityId, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var dto = new LessonDto
        {
            LessonId = lesson.CapabilityLessonId,
            Title = lesson.Title,
            SortOrder = lesson.SortOrder,
            Cards = [
                new CardDto
                {
                    CardId = defaultCard.CapabilityLessonCardId,
                    Title = defaultCard.Title,
                    Text = defaultCard.Text,
                    SortOrder = defaultCard.SortOrder,
                    IllustrationId = defaultCard.CapabilityTraditionalIllustrationId
                }
            ]
        };

        return await FunctionResponseFactory.SuccessResponseAsync(request, dto, cancellationToken: cancellationToken);
    }

    private async Task TryGenerateLessonCardIllustrationAsync(CapabilityLessonCard card, Guid capabilityId, CancellationToken cancellationToken)
    {
        if (card is null || string.IsNullOrWhiteSpace(card.Text) || !_imageService.IsConfigured || !_illustrationStorage.IsConfigured)
        {
            return;
        }

        try
        {
            var imageIndex = await _dbContext.CapabilityTraditionalIllustrations
                .Where(i => i.CapabilityId == capabilityId)
                .CountAsync(cancellationToken);

            var prompt = BuildLessonCardIllustrationPrompt(card.Title, card.Text);
            var generated = await _imageService.GenerateAsync(prompt, cancellationToken: cancellationToken);
            using var stream = generated.ImageBytes.ToStream();

            var storagePath = await _illustrationStorage.UploadIllustrationAsync(
                Guid.Empty,
                capabilityId,
                card.CapabilityLessonCardId,
                imageIndex + 1,
                stream,
                cancellationToken: cancellationToken);

            var illustration = new CapabilityTraditionalIllustration
            {
                CapabilityTraditionalIllustrationId = Guid.NewGuid(),
                CapabilityId = capabilityId,
                StoragePath = storagePath,
                Prompt = prompt,
                Caption = card.Title,
                Purpose = TraditionalIllustrationPurpose.Card,
                ImageModel = generated.ImageModel,
                Width = generated.Width,
                Height = generated.Height,
                CreatedDate = DateTime.UtcNow
            };

            _dbContext.CapabilityTraditionalIllustrations.Add(illustration);
            card.CapabilityTraditionalIllustrationId = illustration.CapabilityTraditionalIllustrationId;
        }
        catch (Exception) when (true)
        {
            // Best-effort: creating the lesson should not fail when illustration
            // generation is unavailable or quota-limited.
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

public sealed class AddTraditionalCourseLessonCardRequest
{
    public string Title { get; set; } = string.Empty;

    public string Text { get; set; } = string.Empty;
}

public sealed class AddTraditionalCourseLessonCardFunction
{
    private readonly HumanOsDbContext _dbContext;
    private readonly GraphIllustrationImageService _imageService;
    private readonly CapabilityGraphIllustrationStorageService _illustrationStorage;

    public AddTraditionalCourseLessonCardFunction(HumanOsDbContext dbContext, GraphIllustrationImageService imageService, IConfiguration configuration)
    {
        _dbContext = dbContext;
        _imageService = imageService;
        _illustrationStorage = new CapabilityGraphIllustrationStorageService(configuration);
    }

    public static CapabilityLessonCard CreateStarterCard(string lessonTitle, string? customText, Guid? lessonId = null)
    {
        var normalizedTitle = string.IsNullOrWhiteSpace(lessonTitle) ? "Lesson overview" : lessonTitle.Trim();
        var text = string.IsNullOrWhiteSpace(customText) ? string.Empty : customText.Trim();

        return new CapabilityLessonCard
        {
            CapabilityLessonCardId = Guid.NewGuid(),
            CapabilityLessonId = lessonId ?? Guid.Empty,
            Title = normalizedTitle,
            Text = text,
            SortOrder = 1,
            CapabilityTraditionalIllustrationId = null,
            CreatedDate = DateTime.UtcNow
        };
    }

    [Function("AddTraditionalCourseLessonCard")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "studio/traditional-course/lessons/{lessonId:guid}/cards/add")]
        HttpRequestData request,
        Guid lessonId,
        CancellationToken cancellationToken)
    {
        AddTraditionalCourseLessonCardRequest? body;
        try
        {
            body = await request.ReadFromJsonAsync<AddTraditionalCourseLessonCardRequest>(cancellationToken)
                ?? new AddTraditionalCourseLessonCardRequest();
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

        var lesson = await _dbContext.CapabilityLessons
            .Include(l => l.Cards)
            .Include(l => l.CapabilityChapter)
            .FirstOrDefaultAsync(l => l.CapabilityLessonId == lessonId, cancellationToken);

        if (lesson is null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.NotFound,
                "TraditionalCourseLessonNotFound",
                $"No traditional course lesson found with LessonId '{lessonId}'.",
                cancellationToken);
        }

        var cardTitle = string.IsNullOrWhiteSpace(body.Title) ? lesson.Title : body.Title.Trim();
        var cardText = string.IsNullOrWhiteSpace(body.Text)
            ? AddTraditionalCourseLessonFunction.CreateDefaultLessonCard(cardTitle, lesson.CapabilityLessonId).Text
            : body.Text.Trim();

        var newCard = AddTraditionalCourseLessonFunction.CreateStarterCard(cardTitle, cardText, lesson.CapabilityLessonId);
        newCard.SortOrder = lesson.Cards.Any() ? lesson.Cards.Max(c => c.SortOrder) + 1 : 1;

        _dbContext.CapabilityLessonCards.Add(newCard);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await TryGenerateLessonCardIllustrationAsync(newCard, lesson.CapabilityChapter!.CapabilityId, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return await FunctionResponseFactory.SuccessResponseAsync(request, new CardDto
        {
            CardId = newCard.CapabilityLessonCardId,
            Title = newCard.Title,
            Text = newCard.Text,
            SortOrder = newCard.SortOrder,
            IllustrationId = newCard.CapabilityTraditionalIllustrationId
        }, cancellationToken: cancellationToken);
    }

    private async Task TryGenerateLessonCardIllustrationAsync(CapabilityLessonCard card, Guid capabilityId, CancellationToken cancellationToken)
    {
        if (card is null || string.IsNullOrWhiteSpace(card.Text) || !_imageService.IsConfigured || !_illustrationStorage.IsConfigured)
        {
            return;
        }

        try
        {
            var imageIndex = await _dbContext.CapabilityTraditionalIllustrations
                .Where(i => i.CapabilityId == capabilityId)
                .CountAsync(cancellationToken);

            var prompt = AddTraditionalCourseLessonFunction.BuildLessonCardIllustrationPrompt(card.Title, card.Text);
            var generated = await _imageService.GenerateAsync(prompt, cancellationToken: cancellationToken);
            using var stream = generated.ImageBytes.ToStream();

            var storagePath = await _illustrationStorage.UploadIllustrationAsync(
                Guid.Empty,
                capabilityId,
                card.CapabilityLessonCardId,
                imageIndex + 1,
                stream,
                cancellationToken: cancellationToken);

            var illustration = new CapabilityTraditionalIllustration
            {
                CapabilityTraditionalIllustrationId = Guid.NewGuid(),
                CapabilityId = capabilityId,
                StoragePath = storagePath,
                Prompt = prompt,
                Caption = card.Title,
                Purpose = TraditionalIllustrationPurpose.Card,
                ImageModel = generated.ImageModel,
                Width = generated.Width,
                Height = generated.Height,
                CreatedDate = DateTime.UtcNow
            };

            _dbContext.CapabilityTraditionalIllustrations.Add(illustration);
            card.CapabilityTraditionalIllustrationId = illustration.CapabilityTraditionalIllustrationId;
        }
        catch (Exception) when (true)
        {
            // Best-effort: lesson creation should still succeed even when image
            // generation is unavailable or quota-limited.
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
