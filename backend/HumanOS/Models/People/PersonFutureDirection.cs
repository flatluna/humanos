namespace HumanOS.Models.People;

/// <summary>
/// Growth Plan Step 2: "Where You Want to Go" — the goals and motivations
/// the person selects. One row per person (1:1 with Person), overwritten
/// each time the person redoes the step.
/// </summary>
public sealed class PersonFutureDirection
{
    public Guid PersonFutureDirectionId { get; set; }

    public Guid PersonId { get; set; }

    /// <summary>Comma-separated goal IDs selected by the person.</summary>
    public string SelectedGoalIds { get; set; } = string.Empty;

    /// <summary>Comma-separated motivation codes (e.g. "autonomy,mastery,relatedness").</summary>
    public string SelectedMotivationCodes { get; set; } = string.Empty;

    public bool Completed { get; set; }

    public DateTime CreatedDate { get; set; }

    public DateTime UpdatedDate { get; set; }

    /*
     * Navigation properties
     */

    public Person Person { get; set; } = null!;
}
