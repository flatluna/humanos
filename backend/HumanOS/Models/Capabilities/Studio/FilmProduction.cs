namespace HumanOS.Models.Capabilities.Studio;

/// <summary>
/// Root container for a "Film Productions" wizard run (2026-08-24) — a
/// user-authored short film made of an ordered list of <see cref="FilmScene"/>
/// rows, each turned into one finished image via the two-agent pipeline (see
/// <see cref="HumanOS.Services.FilmProductionOrchestrator"/>). Deliberately
/// separate from StructuredLearning (this produces standalone
/// scenes/images, not pedagogical content) even though it reuses the same
/// Data Lake illustration storage and image-generation infrastructure.
/// </summary>
public sealed class FilmProduction
{
    public Guid FilmProductionId { get; set; } = Guid.NewGuid();

    public Guid TenantId { get; set; }

    public string Title { get; set; } = string.Empty;

    /// <summary>Blob path of the final merged film (all scene videos
    /// concatenated in SortOrder) — set once <c>MergeFilmProductionVideos</c>
    /// has run.</summary>
    public string? FinalFilmStoragePath { get; set; }

    /// <summary>Cumulative FilmScriptBreakdownAgent token usage across every
    /// "from-script" call for this production (2026-08-24) — feeds the
    /// production-level cost summary alongside each FilmScene's own
    /// ContentInputTokens/ContentOutputTokens, see
    /// <see cref="HumanOS.Services.TokenCostEstimator"/>. Accumulated
    /// (+=) rather than overwritten since a production can run the
    /// auto-storyboard more than once.</summary>
    public int BreakdownInputTokens { get; set; }

    public int BreakdownOutputTokens { get; set; }

    public int BreakdownCachedInputTokens { get; set; }

    public string? BreakdownModelName { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
}
