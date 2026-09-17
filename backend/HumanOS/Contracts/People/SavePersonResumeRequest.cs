namespace HumanOS.Contracts.People;

/// <summary>Body for PUT /people/{personId}/resume — the final,
/// HRAdmin-reviewed résumé fields to persist (whether hand-typed or
/// accepted from a ResumeExtractionAgent proposal).</summary>
public sealed class SavePersonResumeRequest
{
    public string? Address { get; set; }

    public string? LinkedInUrl { get; set; }

    public string? ProfessionalObjective { get; set; }

    public string? ProfessionalSummary { get; set; }

    public List<string>? Skills { get; set; }

    public List<string>? WorkExperience { get; set; }

    public List<string>? Education { get; set; }

    public List<string>? Certifications { get; set; }

    public List<string>? Languages { get; set; }

    public string? RawResumeText { get; set; }

    public StudentAcademicProfile? StudentAcademicProfile { get; set; }
}
