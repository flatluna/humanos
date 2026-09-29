using Microsoft.Data.SqlTypes;

namespace HumanOS.Models.People;

/// <summary>
/// A saved "Grow With Us" search (2026-09-28) — persists the user's prompt
/// (as <see cref="Topic"/>, used both as the display name in the saved
/// searches panel and as the text embedded for semantic search) together
/// with the full generated <see cref="HumanOS.Agents.GrowWithUsPlanProposal"/>
/// (serialized as <see cref="PlanJson"/>) so it can be retrieved again
/// later without re-generating it. One row per generated plan (history,
/// not a single "latest" row like <see cref="PersonCareerPlan"/>).
/// </summary>
public sealed class GrowWithUsSavedSearch
{
    public Guid GrowWithUsSavedSearchId { get; set; }

    public Guid PersonId { get; set; }

    /// <summary>The original free-text prompt the user typed — shown as
    /// the saved item's name in the left-side panel.</summary>
    public string Topic { get; set; } = string.Empty;

    /// <summary>The full <c>GrowWithUsPlanProposal</c>, JSON-serialized.</summary>
    public string PlanJson { get; set; } = "{}";

    /// <summary>Embedding of <see cref="Topic"/> (text-embedding-ada-002,
    /// 1536 dims) for semantic search over past searches. Null when the
    /// embedding service wasn't configured at save time.</summary>
    public SqlVector<float>? Embedding { get; set; }

    public DateTime CreatedDate { get; set; }

    public Person Person { get; set; } = null!;
}
