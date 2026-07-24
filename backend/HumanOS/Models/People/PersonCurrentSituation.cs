namespace HumanOS.Models.People;

/// <summary>
/// Growth Plan Step 1: "Your Current Situation" — the areas/subjects the person
/// cares about right now, plus a self-assessed level in each.
/// One row per person (1:1 with Person), overwritten each time the person
/// redoes the step (not a history — we only track the latest attempt).
/// </summary>
public sealed class PersonCurrentSituation
{
    public Guid PersonCurrentSituationId { get; set; }

    public Guid PersonId { get; set; }

    /// <summary>Comma-separated subject codes (e.g. "matematicas,finanzas,idiomas").</summary>
    public string SelectedSubjectCodes { get; set; } = string.Empty;

    /// <summary>JSON: { "subject_code": "SelfAssessedLevel" }, where SelfAssessedLevel is
    /// one of: "Beginner" | "Intermediate" | "Advanced".</summary>
    public string SelfAssessedLevelsJson { get; set; } = "{}";

    public bool Completed { get; set; }

    public DateTime CreatedDate { get; set; }

    public DateTime UpdatedDate { get; set; }

    /*
     * Navigation properties
     */

    public Person Person { get; set; } = null!;
}
