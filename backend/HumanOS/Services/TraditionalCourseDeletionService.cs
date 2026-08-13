using HumanOS.Data;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.Services;

/// <summary>
/// Deletes one traditional-course chapter or lesson while preserving the course's
/// reading order and cleaning up any generated illustration metadata that may
/// still point at the removed content.
/// </summary>
public sealed class TraditionalCourseDeletionService
{
    private readonly IDbContextFactory<HumanOsDbContext> _dbContextFactory;

    public TraditionalCourseDeletionService(IDbContextFactory<HumanOsDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task<bool> DeleteChapterAsync(Guid chapterId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var chapter = await dbContext.CapabilityChapters
            .Include(c => c.Lessons)
            .ThenInclude(l => l.Cards)
            .Include(c => c.ExamQuestions)
            .FirstOrDefaultAsync(c => c.CapabilityChapterId == chapterId, cancellationToken);

        if (chapter is null)
        {
            return false;
        }

        var illustrationIds = chapter.ExamQuestions
            .Where(q => q.CapabilityTraditionalIllustrationId.HasValue)
            .Select(q => q.CapabilityTraditionalIllustrationId!.Value)
            .Concat(chapter.Lessons
                .SelectMany(l => l.Cards)
                .Where(c => c.CapabilityTraditionalIllustrationId.HasValue)
                .Select(c => c.CapabilityTraditionalIllustrationId!.Value))
            .Distinct()
            .ToList();

        var strategy = dbContext.Database.CreateExecutionStrategy();

        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

            if (illustrationIds.Count > 0)
            {
                var illustrationRows = await dbContext.CapabilityTraditionalIllustrations
                    .Where(i => illustrationIds.Contains(i.CapabilityTraditionalIllustrationId))
                    .ToListAsync(cancellationToken);

                if (illustrationRows.Count > 0)
                {
                    dbContext.CapabilityTraditionalIllustrations.RemoveRange(illustrationRows);
                }
            }

            dbContext.CapabilityChapters.Remove(chapter);

            var laterChapters = await dbContext.CapabilityChapters
                .Where(c => c.CapabilityId == chapter.CapabilityId && c.SortOrder > chapter.SortOrder)
                .OrderBy(c => c.SortOrder)
                .ToListAsync(cancellationToken);

            foreach (var later in laterChapters)
            {
                later.SortOrder -= 1;
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        });

        return true;
    }

    public async Task<bool> DeleteLessonAsync(Guid lessonId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var lesson = await dbContext.CapabilityLessons
            .Include(l => l.Cards)
            .FirstOrDefaultAsync(l => l.CapabilityLessonId == lessonId, cancellationToken);

        if (lesson is null)
        {
            return false;
        }

        var illustrationIds = lesson.Cards
            .Where(c => c.CapabilityTraditionalIllustrationId.HasValue)
            .Select(c => c.CapabilityTraditionalIllustrationId!.Value)
            .Distinct()
            .ToList();

        var strategy = dbContext.Database.CreateExecutionStrategy();

        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

            if (illustrationIds.Count > 0)
            {
                var illustrationRows = await dbContext.CapabilityTraditionalIllustrations
                    .Where(i => illustrationIds.Contains(i.CapabilityTraditionalIllustrationId))
                    .ToListAsync(cancellationToken);

                if (illustrationRows.Count > 0)
                {
                    dbContext.CapabilityTraditionalIllustrations.RemoveRange(illustrationRows);
                }
            }

            dbContext.CapabilityLessons.Remove(lesson);

            var laterLessons = await dbContext.CapabilityLessons
                .Where(l => l.CapabilityChapterId == lesson.CapabilityChapterId && l.SortOrder > lesson.SortOrder)
                .OrderBy(l => l.SortOrder)
                .ToListAsync(cancellationToken);

            foreach (var later in laterLessons)
            {
                later.SortOrder -= 1;
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        });

        return true;
    }
}
