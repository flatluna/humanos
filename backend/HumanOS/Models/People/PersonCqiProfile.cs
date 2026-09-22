namespace HumanOS.Models.People;

/// <summary>
/// A Person's CQI ("Cognitive/Communication/Intelligence" profiling report,
/// e.g. HermanScience) profile — the structured insights extracted by
/// <see cref="HumanOS.Agents.CqiReportExtractionAgent"/> from an uploaded
/// CQI assessment PDF (leading pattern/type, anchor foundation, strengths,
/// growth direction, etc.). Powers the "CQI Workspace" feature: understand
/// how a person currently thinks, learns, communicates, makes decisions,
/// and works.
///
/// One optional row per Person, replaced whenever a new report is
/// uploaded — same "created lazily, one row per Person" convention as
/// <see cref="PersonResume"/>.
/// </summary>
public sealed class PersonCqiProfile
{
    public Guid PersonCqiProfileId { get; set; }

    public Guid PersonId { get; set; }

    /// <summary>E.g. "Type 6" — the CQI/Enneagram-style leading pattern type code.</summary>
    public string? LeadingPatternType { get; set; }

    /// <summary>E.g. "The Verifier" — the leading pattern's name.</summary>
    public string? LeadingPatternName { get; set; }

    /// <summary>JSON-serialized <see cref="HumanOS.Contracts.People.CqiProfileData"/> —
    /// the full structured report (profile snapshot, thinking/decisions,
    /// communication, strengths, anchor foundation, etc.), always
    /// read/written as a whole object.</summary>
    public string ProfileJson { get; set; } = "{}";

    public string? SourceFileName { get; set; }

    /// <summary>The Azure OpenAI deployment/model that produced <see cref="ProfileJson"/>.</summary>
    public string? ExtractionModel { get; set; }

    public DateTime? ExtractedDate { get; set; }

    public DateTime CreatedDate { get; set; }

    public DateTime UpdatedDate { get; set; }

    public Person? Person { get; set; }
}
