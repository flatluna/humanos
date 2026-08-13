using System.Text.RegularExpressions;
using HumanOS.Agents.Studio;
using HumanOS.Data;
using HumanOS.Models.Capabilities.Traditional;
using HumanOS.Storage;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.Services;

/// <summary>
/// Review/edit service for Traditional Course content. Mirrors the structured-
/// learning and Graph review flows: free-text AI rewrite plus explicit manual
/// save with confirmation. This is the server-side backend that powers the
/// Edit mode controls in the Studio course page.
/// </summary>
public sealed class TraditionalCourseReviewService
{
    private readonly TraditionalCourseReviewAgent _agent;
    private readonly GraphIllustrationImageService _imageService;
    private readonly CapabilityGraphIllustrationStorageService _illustrationStorage;

    public TraditionalCourseReviewService(
        TraditionalCourseReviewAgent agent,
        GraphIllustrationImageService imageService,
        CapabilityGraphIllustrationStorageService illustrationStorage)
    {
        _agent = agent;
        _imageService = imageService;
        _illustrationStorage = illustrationStorage;
    }

    public async Task<CapabilityChapter> EditChapterTitleAsync(
        HumanOsDbContext dbContext,
        Guid chapterId,
        string instruction,
        CancellationToken cancellationToken = default)
    {
        var chapter = await dbContext.CapabilityChapters
            .FirstOrDefaultAsync(c => c.CapabilityChapterId == chapterId, cancellationToken)
            ?? throw new InvalidOperationException($"Traditional course chapter {chapterId} not found.");

        if (string.IsNullOrWhiteSpace(instruction))
        {
            throw new InvalidOperationException("Instruction is required.");
        }

        var prompt = $"""
            Rewrite this chapter title for a traditional course.

            Entity: Chapter
            Current title: {chapter.Title}
            Reviewer instruction: {instruction}

            Keep it short, clear, and faithful to the course content. Return only the updated title.
            """;

        var result = await _agent.RewriteAsync(prompt, cancellationToken);
        chapter.Title = result.UpdatedText.Trim();
        await dbContext.SaveChangesAsync(cancellationToken);
        return chapter;
    }

    public async Task<CapabilityChapter> SetChapterTitleManuallyAsync(
        HumanOsDbContext dbContext,
        Guid chapterId,
        string title,
        CancellationToken cancellationToken = default)
    {
        var chapter = await dbContext.CapabilityChapters
            .FirstOrDefaultAsync(c => c.CapabilityChapterId == chapterId, cancellationToken)
            ?? throw new InvalidOperationException($"Traditional course chapter {chapterId} not found.");

        chapter.Title = string.IsNullOrWhiteSpace(title) ? chapter.Title : title.Trim();
        await dbContext.SaveChangesAsync(cancellationToken);
        return chapter;
    }

    public async Task<CapabilityLesson> EditLessonTitleAsync(
        HumanOsDbContext dbContext,
        Guid lessonId,
        string instruction,
        CancellationToken cancellationToken = default)
    {
        var lesson = await dbContext.CapabilityLessons
            .FirstOrDefaultAsync(l => l.CapabilityLessonId == lessonId, cancellationToken)
            ?? throw new InvalidOperationException($"Traditional course lesson {lessonId} not found.");

        if (string.IsNullOrWhiteSpace(instruction))
        {
            throw new InvalidOperationException("Instruction is required.");
        }

        var prompt = $"""
            Rewrite this lesson title for a traditional course.

            Entity: Lesson
            Current title: {lesson.Title}
            Reviewer instruction: {instruction}

            Keep it concise and aligned with the lesson content. Return only the updated title.
            """;

        var result = await _agent.RewriteAsync(prompt, cancellationToken);
        lesson.Title = result.UpdatedText.Trim();
        await dbContext.SaveChangesAsync(cancellationToken);
        return lesson;
    }

    public async Task<CapabilityLesson> SetLessonTitleManuallyAsync(
        HumanOsDbContext dbContext,
        Guid lessonId,
        string title,
        CancellationToken cancellationToken = default)
    {
        var lesson = await dbContext.CapabilityLessons
            .FirstOrDefaultAsync(l => l.CapabilityLessonId == lessonId, cancellationToken)
            ?? throw new InvalidOperationException($"Traditional course lesson {lessonId} not found.");

        lesson.Title = string.IsNullOrWhiteSpace(title) ? lesson.Title : title.Trim();
        await dbContext.SaveChangesAsync(cancellationToken);
        return lesson;
    }

    public async Task<CapabilityLessonCard> EditCardTextAsync(
        HumanOsDbContext dbContext,
        Guid cardId,
        string instruction,
        CancellationToken cancellationToken = default)
    {
        var card = await dbContext.CapabilityLessonCards
            .FirstOrDefaultAsync(c => c.CapabilityLessonCardId == cardId, cancellationToken)
            ?? throw new InvalidOperationException($"Traditional course card {cardId} not found.");

        if (string.IsNullOrWhiteSpace(instruction))
        {
            throw new InvalidOperationException("Instruction is required.");
        }

        var directReplacement = ShouldTreatAsDirectTextReplacement(instruction);
        var prompt = directReplacement
            ? $"""
                Replace the current card text with the exact content requested by the reviewer.

                Current card title: {card.Title}
                Current card text:
                {card.Text}

                Reviewer instruction: {instruction}

                IMPORTANT: This is a full replacement request. Do not preserve any of the existing wording,
                structure, examples, or summary from the current card text unless the reviewer explicitly asks for it.
                Rewrite the card text to match the new requested wording exactly in spirit and meaning,
                and keep it polished, accurate, and readable.
                If the reviewer asks for a visual change, also return a concrete illustration prompt;
                otherwise leave IllustrationPrompt empty.
                """
            : $"""
                Rewrite this traditional-course reading card text.

                Current card title: {card.Title}
                Current card text:
                {card.Text}

                Reviewer instruction: {instruction}

                Keep the same learning intent, preserve all important facts, and improve clarity.
                If the instruction requests an image/illustration change, also return a concrete
                illustration prompt for the updated scene; otherwise leave it blank.
                """;

        var result = await _agent.RewriteAsync(prompt, cancellationToken);
        if (!string.IsNullOrWhiteSpace(result.UpdatedText))
        {
            card.Text = result.UpdatedText.Trim();
        }

        if (ShouldRequestIllustrationUpdate(instruction) || !string.IsNullOrWhiteSpace(result.IllustrationPrompt))
        {
            var illustrationPrompt = !string.IsNullOrWhiteSpace(result.IllustrationPrompt)
                ? result.IllustrationPrompt
                : BuildIllustrationPrompt(card.Title, card.Text, instruction);

            await TryGenerateIllustrationAsync(dbContext, card, illustrationPrompt, cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return card;
    }

    public static bool ShouldTreatAsDirectTextReplacement(string instruction)
    {
        var trimmed = (instruction ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(trimmed)) return false;

        var normalized = Regex.Replace(trimmed, @"\s+", " ").Trim();
        var lower = normalized.ToLowerInvariant();

        return lower.Length >= 180
            || lower.Contains("your role is")
            || lower.Contains("there's no need to take over")
            || lower.Contains("do not need to take over")
            || lower.Contains("replace the text")
            || lower.Contains("rewrite the text to this")
            || lower.Contains("use this exact text")
            || lower.Contains("replace this with")
            || lower.Contains("change this text to")
            || lower.Contains("change the text to")
            || lower.Contains("add this text")
            || lower.Contains("add this paragraph")
            || lower.Contains("insert this text")
            || lower.Contains("rewrite this card to")
            || lower.Contains("rewrite this text as follows")
            || lower.Contains("write the following")
            || lower.Contains("here is the text")
            || lower.Contains("full replacement");
    }

    public static bool ShouldRequestIllustrationUpdate(string instruction)
    {
        var trimmed = (instruction ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(trimmed)) return false;

        var normalized = Regex.Replace(trimmed, @"\s+", " ").Trim();
        var lower = normalized.ToLowerInvariant();

        if (lower.Contains("image") || lower.Contains("illustration") || lower.Contains("picture") || lower.Contains("visual"))
        {
            return true;
        }

        if (lower.Contains("people") || lower.Contains("scene") || lower.Contains("background") || lower.Contains("show more"))
        {
            return true;
        }

        return lower.Contains("change the image")
            || lower.Contains("replace the image")
            || lower.Contains("add an image")
            || lower.Contains("add a picture")
            || lower.Contains("new image")
            || lower.Contains("different image")
            || lower.Contains("more people");
    }

    private async Task TryGenerateIllustrationAsync(
        HumanOsDbContext dbContext,
        CapabilityLessonCard card,
        string illustrationPrompt,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(illustrationPrompt) || !_imageService.IsConfigured || !_illustrationStorage.IsConfigured)
        {
            return;
        }

        try
        {
            var capabilityId = await dbContext.CapabilityLessons
                .Where(l => l.CapabilityLessonId == card.CapabilityLessonId)
                .Select(l => l.CapabilityChapter!.CapabilityId)
                .FirstOrDefaultAsync(cancellationToken);

            if (capabilityId == Guid.Empty)
            {
                return;
            }

            var imageIndex = await dbContext.CapabilityTraditionalIllustrations
                .Where(i => i.CapabilityId == capabilityId)
                .CountAsync(cancellationToken);

            var generated = await _imageService.GenerateAsync(illustrationPrompt, cancellationToken: cancellationToken);
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
                Prompt = illustrationPrompt,
                Caption = card.Title,
                Purpose = TraditionalIllustrationPurpose.Card,
                ImageModel = generated.ImageModel,
                Width = generated.Width,
                Height = generated.Height,
                CreatedDate = DateTime.UtcNow
            };

            dbContext.CapabilityTraditionalIllustrations.Add(illustration);
            card.CapabilityTraditionalIllustrationId = illustration.CapabilityTraditionalIllustrationId;
        }
        catch (Exception) when (true)
        {
            // Best-effort: text updates should still succeed even when image generation
            // is unavailable or quota-limited; the reviewer can retry later.
        }
    }

    private static string BuildIllustrationPrompt(string title, string text, string instruction)
    {
        var subject = !string.IsNullOrWhiteSpace(title)
            ? title
            : (string.IsNullOrWhiteSpace(text) ? "the lesson concept" : text);

        var compactSubject = Regex.Replace(subject, @"\s+", " ").Trim();
        var normalizedInstruction = Regex.Replace(instruction, @"\s+", " ").Trim();

        return $"A clear educational illustration for a traditional course card about '{compactSubject}'. " +
               $"Adjust the scene to match this request: {normalizedInstruction}. " +
               "Single focal scene, classroom or learning context, polished style, no text, no watermark, no logo.";
    }

    public async Task<CapabilityLessonCard> SetCardTextManuallyAsync(
        HumanOsDbContext dbContext,
        Guid cardId,
        string text,
        CancellationToken cancellationToken = default)
    {
        var card = await dbContext.CapabilityLessonCards
            .FirstOrDefaultAsync(c => c.CapabilityLessonCardId == cardId, cancellationToken)
            ?? throw new InvalidOperationException($"Traditional course card {cardId} not found.");

        card.Text = string.IsNullOrWhiteSpace(text) ? card.Text : text.Trim();
        await dbContext.SaveChangesAsync(cancellationToken);
        return card;
    }
}
