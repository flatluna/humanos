using HumanOS.Data;
using HumanOS.Models.People;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.Services;

/// <summary>Summary shape for the left-side "saved searches" panel — no ResultJson (kept
/// small for the list endpoint).</summary>
public sealed class ContentRecommendationSavedSearchSummary
{
    public Guid ContentRecommendationSavedSearchId { get; set; }

    public string Topic { get; set; } = string.Empty;

    public DateTime CreatedDate { get; set; }
}

/// <summary>
/// Persists and retrieves "content recommendation" saved searches (2026-09-29) — see
/// <see cref="ContentRecommendationSavedSearch"/>'s doc comment.
/// </summary>
public sealed class ContentRecommendationService
{
    private readonly HumanOsDbContext _dbContext;

    public ContentRecommendationService(HumanOsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ContentRecommendationSavedSearchSummary> SaveAsync(
        Guid personId,
        string topic,
        string resultJson,
        CancellationToken cancellationToken = default)
    {
        var entity = new ContentRecommendationSavedSearch
        {
            ContentRecommendationSavedSearchId = Guid.NewGuid(),
            PersonId = personId,
            Topic = topic,
            ResultJson = resultJson,
            CreatedDate = DateTime.UtcNow,
        };

        _dbContext.ContentRecommendationSavedSearches.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new ContentRecommendationSavedSearchSummary
        {
            ContentRecommendationSavedSearchId = entity.ContentRecommendationSavedSearchId,
            Topic = entity.Topic,
            CreatedDate = entity.CreatedDate,
        };
    }

    /// <summary>List of a person's saved searches, most recent first — for the left-side
    /// panel.</summary>
    public async Task<List<ContentRecommendationSavedSearchSummary>> GetListAsync(
        Guid personId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.ContentRecommendationSavedSearches
            .AsNoTracking()
            .Where(s => s.PersonId == personId)
            .OrderByDescending(s => s.CreatedDate)
            .Select(s => new ContentRecommendationSavedSearchSummary
            {
                ContentRecommendationSavedSearchId = s.ContentRecommendationSavedSearchId,
                Topic = s.Topic,
                CreatedDate = s.CreatedDate,
            })
            .ToListAsync(cancellationToken);
    }

    /// <summary>Full saved row (including ResultJson) — for re-opening a past
    /// recommendation report from the panel, or to send it by email.</summary>
    public async Task<ContentRecommendationSavedSearch?> GetByIdAsync(
        Guid personId,
        Guid savedSearchId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.ContentRecommendationSavedSearches
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.PersonId == personId && s.ContentRecommendationSavedSearchId == savedSearchId, cancellationToken);
    }
}
