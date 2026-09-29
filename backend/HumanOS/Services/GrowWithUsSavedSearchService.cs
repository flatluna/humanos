using HumanOS.Agents.Studio;
using HumanOS.Data;
using HumanOS.Models.People;
using Microsoft.Data.SqlTypes;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.Services;

/// <summary>Summary shape for the left-side "saved searches" panel — no
/// PlanJson (kept small for the list endpoint).</summary>
public sealed class GrowWithUsSavedSearchSummary
{
    public Guid GrowWithUsSavedSearchId { get; set; }

    public string Topic { get; set; } = string.Empty;

    public DateTime CreatedDate { get; set; }
}

/// <summary>
/// Persists and retrieves "Grow With Us" saved searches (2026-09-28) — see
/// <see cref="GrowWithUsSavedSearch"/>'s doc comment. Embeds the saved
/// <see cref="GrowWithUsSavedSearch.Topic"/> via <see cref="CapabilityEmbeddingService"/>
/// (best-effort — a save never fails just because embedding is
/// unconfigured/unavailable) so <see cref="SearchAsync"/> can find past
/// searches semantically, not just by exact substring.
/// </summary>
public sealed class GrowWithUsSavedSearchService
{
    private readonly HumanOsDbContext _dbContext;
    private readonly CapabilityEmbeddingService _embeddingService;

    public GrowWithUsSavedSearchService(HumanOsDbContext dbContext, CapabilityEmbeddingService embeddingService)
    {
        _dbContext = dbContext;
        _embeddingService = embeddingService;
    }

    public async Task<GrowWithUsSavedSearchSummary> SaveAsync(
        Guid personId,
        string topic,
        string planJson,
        CancellationToken cancellationToken = default)
    {
        SqlVector<float>? embedding = null;
        if (_embeddingService.IsConfigured)
        {
            try
            {
                var vector = await _embeddingService.GenerateEmbeddingAsync(topic, cancellationToken);
                embedding = new SqlVector<float>(vector);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Best-effort — saving the search must never fail just
                // because the embedding call failed.
            }
        }

        var entity = new GrowWithUsSavedSearch
        {
            GrowWithUsSavedSearchId = Guid.NewGuid(),
            PersonId = personId,
            Topic = topic,
            PlanJson = planJson,
            Embedding = embedding,
            CreatedDate = DateTime.UtcNow,
        };

        _dbContext.GrowWithUsSavedSearches.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new GrowWithUsSavedSearchSummary
        {
            GrowWithUsSavedSearchId = entity.GrowWithUsSavedSearchId,
            Topic = entity.Topic,
            CreatedDate = entity.CreatedDate,
        };
    }

    /// <summary>List of a person's saved searches, most recent first —
    /// for the left-side panel.</summary>
    public async Task<List<GrowWithUsSavedSearchSummary>> GetListAsync(
        Guid personId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.GrowWithUsSavedSearches
            .AsNoTracking()
            .Where(s => s.PersonId == personId)
            .OrderByDescending(s => s.CreatedDate)
            .Select(s => new GrowWithUsSavedSearchSummary
            {
                GrowWithUsSavedSearchId = s.GrowWithUsSavedSearchId,
                Topic = s.Topic,
                CreatedDate = s.CreatedDate,
            })
            .ToListAsync(cancellationToken);
    }

    /// <summary>Full saved row (including PlanJson) — for re-opening a
    /// past plan from the panel.</summary>
    public async Task<GrowWithUsSavedSearch?> GetByIdAsync(
        Guid personId,
        Guid savedSearchId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.GrowWithUsSavedSearches
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.PersonId == personId && s.GrowWithUsSavedSearchId == savedSearchId, cancellationToken);
    }

    /// <summary>Semantic search over a person's saved searches. Ada-002
    /// embeddings are known to be anisotropic — even completely unrelated
    /// short phrases (e.g. a person's name like "Jorge" or "Angeles")
    /// consistently land at a fairly LOW cosine distance from any given
    /// topic, so distance alone is NOT a reliable inclusion signal
    /// (confirmed 2026-09-28 repeatedly: unrelated names still matched at
    /// cutoffs 0.7, 0.45, and even the combined "distance OR keyword"
    /// version wrongly matched everything again since distance alone kept
    /// passing). A result is only ever included if it shares at least one
    /// real keyword (3+ chars) with the query, checked against BOTH the
    /// short Topic/title and the FULL saved plan
    /// (<see cref="GrowWithUsSavedSearch.PlanJson"/>) — the latter so a
    /// term that only appears inside the plan body (e.g. "Apply ADKAR", a
    /// best practice heading, not the plan's own title) still matches.
    /// Cosine distance is used ONLY to rank keyword-matched results by
    /// relevance, never to admit a result that has no keyword overlap.
    /// Falls back to a case-insensitive substring match over
    /// <see cref="GrowWithUsSavedSearch.Topic"/> when the embedding
    /// service isn't configured.</summary>
    private static readonly char[] WordSeparators = [' ', '\t', '\n', '\r', ',', '.', ';', ':', '!', '?', '\'', '"', '-'];

    public async Task<List<GrowWithUsSavedSearchSummary>> SearchAsync(
        Guid personId,
        string queryText,
        int topK = 10,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(queryText))
        {
            return await GetListAsync(personId, cancellationToken);
        }

        var queryWords = queryText
            .Split(WordSeparators, StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length >= 3)
            .Select(w => w.ToLowerInvariant())
            .ToList();

        if (queryWords.Count == 0)
        {
            return [];
        }

        if (_embeddingService.IsConfigured)
        {
            try
            {
                var queryVector = new SqlVector<float>(await _embeddingService.GenerateEmbeddingAsync(queryText, cancellationToken));

                var candidates = await _dbContext.GrowWithUsSavedSearches
                    .AsNoTracking()
                    .Where(s => s.PersonId == personId && s.Embedding != null)
                    .Select(s => new
                    {
                        s.GrowWithUsSavedSearchId,
                        s.Topic,
                        s.PlanJson,
                        s.CreatedDate,
                        Distance = EF.Functions.VectorDistance("cosine", s.Embedding!.Value, queryVector),
                    })
                    .OrderBy(s => s.Distance)
                    .ToListAsync(cancellationToken);

                return candidates
                    .Where(s => queryWords.Any(w => s.Topic.Contains(w, StringComparison.OrdinalIgnoreCase)
                        || s.PlanJson.Contains(w, StringComparison.OrdinalIgnoreCase)))
                    .Take(topK)
                    .Select(s => new GrowWithUsSavedSearchSummary
                    {
                        GrowWithUsSavedSearchId = s.GrowWithUsSavedSearchId,
                        Topic = s.Topic,
                        CreatedDate = s.CreatedDate,
                    })
                    .ToList();
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Best-effort — fall through to the substring fallback below.
            }
        }

        return await _dbContext.GrowWithUsSavedSearches
            .AsNoTracking()
            .Where(s => s.PersonId == personId && EF.Functions.Like(s.Topic, $"%{queryText}%"))
            .OrderByDescending(s => s.CreatedDate)
            .Take(topK)
            .Select(s => new GrowWithUsSavedSearchSummary
            {
                GrowWithUsSavedSearchId = s.GrowWithUsSavedSearchId,
                Topic = s.Topic,
                CreatedDate = s.CreatedDate,
            })
            .ToListAsync(cancellationToken);
    }
}
