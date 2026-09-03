namespace HumanOS.Contracts.Gamification;

/// <summary>
/// An engram item in the neuroscience-based spaced review queue.
/// </summary>
public sealed class ReviewQueueItemDto
{
    public string EngramId { get; set; } = string.Empty;

    public Guid CourseId { get; set; }

    public string CourseName { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public int RecallPercent { get; set; }

    /// <summary>"Critical" | "Important" | "Foundational"</summary>
    public string Tier { get; set; } = "Foundational";

    public int ImpactScore { get; set; }

    /// <summary>Calculated priority: ImpactScore * (100 - RecallPercent)</summary>
    public double PriorityScore { get; set; }

    public int ReviewCount { get; set; }

    public DateTime? LastCompletedDate { get; set; }

    public bool IsStructuredLearning { get; set; }
}

/// <summary>
/// Review queue response containing phase details, completion metrics, and interleaved review queue items.
/// </summary>
public sealed class ReviewQueueResponseDto
{
    /// <summary>"Phase1" (&lt;80% ready courses completed) | "Phase2" (&gt;=80% ready courses completed)</summary>
    public string Phase { get; set; } = "Phase1";

    /// <summary>"Learning" | "Maintenance"</summary>
    public string PhaseName { get; set; } = "Learning";

    public double ReadyCoursesCompletionPct { get; set; }

    public int TotalPending { get; set; }

    public List<ReviewQueueItemDto> Items { get; set; } = [];
}
