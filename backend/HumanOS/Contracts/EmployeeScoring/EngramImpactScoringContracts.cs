using System.Text.Json.Serialization;

namespace HumanOS.Contracts.EmployeeScoring;

/// <summary>
/// Input representing one course Engram to be scored for impact.
/// </summary>
public sealed class EngramInput
{
    [JsonPropertyName("engramId")]
    public string EngramId { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;
}

/// <summary>
/// Scoring result produced for a single Engram relative to an employee's profile.
/// </summary>
public sealed class EngramScoreResult
{
    [JsonPropertyName("engramId")]
    public string EngramId { get; set; } = string.Empty;

    /// <summary>
    /// Impact score between 0 and 100.
    /// </summary>
    [JsonPropertyName("score")]
    public int Score { get; set; }

    /// <summary>
    /// "Critical" | "Important" | "Refresher" | "Base / Foundational"
    /// </summary>
    [JsonPropertyName("tier")]
    public string Tier { get; set; } = "Base / Foundational";

    /// <summary>
    /// "KPI" | "Coaching" | "Role" | "Skill"
    /// </summary>
    [JsonPropertyName("primarySignal")]
    public string PrimarySignal { get; set; } = "Role";

    /// <summary>
    /// Short learner-facing reason (max 300 characters, addressing the learner using 'your').
    /// </summary>
    [JsonPropertyName("reason")]
    public string Reason { get; set; } = string.Empty;
}

/// <summary>
/// Response DTO representing the persisted impact score for an Engram (read by UI).
/// </summary>
public sealed class EngramImpactScoreDto
{
    public string EngramId { get; set; } = string.Empty;

    public int Score { get; set; }

    public string Tier { get; set; } = string.Empty;

    public string PrimarySignal { get; set; } = string.Empty;

    public string Reason { get; set; } = string.Empty;

    public DateTime GeneratedAt { get; set; }
}

/// <summary>
/// Envelope model for structured JSON responses from the LLM.
/// </summary>
public sealed class EngramImpactScoringResponse
{
    [JsonPropertyName("scores")]
    public List<EngramScoreResult> Scores { get; set; } = [];
}
