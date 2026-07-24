namespace HumanOS.Models.People;

/// <summary>
/// Growth Plan Step 3: "Your Starting Point" — the real capabilities the
/// person picks from the catalog, plus any "gap capabilities" they invent
/// (topics not found in the catalog). One row per person (1:1 with Person),
/// overwritten each time the person redoes the step.
/// </summary>
public sealed class PersonStartingPoint
{
    public Guid PersonStartingPointId { get; set; }

    public Guid PersonId { get; set; }

    /// <summary>Comma-separated capability IDs selected from the real catalog.</summary>
    public string SelectedCapabilityIds { get; set; } = string.Empty;

    /// <summary>JSON: { "subject_code": ["gap_name_1", "gap_name_2"] } for
    /// capabilities the person named but aren't in the catalog yet.</summary>
    public string GapCapabilitiesBySubjectJson { get; set; } = "{}";

    /// <summary>JSON array: one entry per subject/program the person accepted
    /// from the growth-path agent's recommendation (snapshot, not a live link).</summary>
    public string AcceptedRecommendationsJson { get; set; } = "[]";

    public bool Completed { get; set; }

    public DateTime CreatedDate { get; set; }

    public DateTime UpdatedDate { get; set; }

    /*
     * Navigation properties
     */

    public Person Person { get; set; } = null!;
}
