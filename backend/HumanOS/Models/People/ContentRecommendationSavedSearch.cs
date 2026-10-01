namespace HumanOS.Models.People;

/// <summary>
/// A saved "content recommendation" search (2026-09-29) — persists the request's topic
/// (the student's own free-text prompt, or the agent's own Title when the student didn't
/// type one) together with the full generated
/// <see cref="HumanOS.Agents.ContentRecommendationProposal"/> (serialized as
/// <see cref="ResultJson"/>) so it can be reopened later without re-generating it. One row
/// per generation (history), same convention as <see cref="GrowWithUsSavedSearch"/>.
/// </summary>
public sealed class ContentRecommendationSavedSearch
{
    public Guid ContentRecommendationSavedSearchId { get; set; }

    public Guid PersonId { get; set; }

    /// <summary>Display name shown in the saved-searches panel.</summary>
    public string Topic { get; set; } = string.Empty;

    /// <summary>The full <c>ContentRecommendationProposal</c>, JSON-serialized.</summary>
    public string ResultJson { get; set; } = "{}";

    public DateTime CreatedDate { get; set; }

    public Person Person { get; set; } = null!;
}
