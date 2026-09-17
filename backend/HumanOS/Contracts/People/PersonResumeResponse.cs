namespace HumanOS.Contracts.People;

/// <summary>Response for GET /people/{personId}/resume and PUT
/// /people/{personId}/resume — a Person's résumé/CV. Returned with every
/// field empty/default (not a 404) when the person hasn't saved a résumé
/// yet, so the EmployeeDetailPage dashboard can always render the form.</summary>
public sealed class PersonResumeResponse
{
    public Guid PersonId { get; set; }

    public string? Address { get; set; }

    public string? LinkedInUrl { get; set; }

    public string? ProfessionalObjective { get; set; }

    public string? ProfessionalSummary { get; set; }

    public IReadOnlyList<string> Skills { get; set; } = [];

    public IReadOnlyList<string> WorkExperience { get; set; } = [];

    public IReadOnlyList<string> Education { get; set; } = [];

    public IReadOnlyList<string> Certifications { get; set; } = [];

    public IReadOnlyList<string> Languages { get; set; } = [];

    public string? RawResumeText { get; set; }

    public StudentAcademicProfile? StudentAcademicProfile { get; set; }

    public DateTime? UpdatedDate { get; set; }
}
