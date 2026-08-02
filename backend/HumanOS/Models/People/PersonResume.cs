namespace HumanOS.Models.People;

/// <summary>
/// A per-Person résumé/CV — contact and career details an employee (or an
/// HRAdmin on their behalf) pastes in from the enterprise console's
/// "Personal" employee dashboard, plus the fields
/// <see cref="HumanOS.Agents.ResumeExtractionAgent"/> extracts from that
/// raw text.
///
/// Deliberately kept separate from <see cref="PersonProfile"/> (the
/// person's basic account profile — name, phone, timezone, etc.) and from
/// <see cref="HumanOS.Models.JobDescriptions.JobDescriptionRecord"/> (a
/// per-person JOB DESCRIPTION document extracted from an uploaded PDF —
/// what the organization expects of the role, not the person's own career
/// history). This record is the person's own résumé/CV: address,
/// LinkedIn, professional objective/summary, skills, work experience,
/// education, certifications, languages.
///
/// One optional row per Person — created on first save (see
/// PersonResumeService.UpsertAsync), same "created lazily, one row per
/// Person" rationale as PersonProfile.
/// </summary>
public sealed class PersonResume
{
    public Guid PersonResumeId { get; set; }

    public Guid PersonId { get; set; }

    public string? Address { get; set; }

    public string? LinkedInUrl { get; set; }

    /// <summary>"Objetivo profesional" — what the person is looking for in
    /// their career.</summary>
    public string? ProfessionalObjective { get; set; }

    /// <summary>"Perfil / resumen profesional" — a short overview
    /// summarizing the person's career so far.</summary>
    public string? ProfessionalSummary { get; set; }

    /// <summary>JSON-serialized string[] — same convention as
    /// JobRole.TechnicalSkillsJson etc.: always read/written as a whole
    /// list, no separate child table.</summary>
    public string SkillsJson { get; set; } = "[]";

    /// <summary>JSON string[] — one entry per work-experience item, e.g.
    /// "Empresa X — Puesto (2020-2023): logro o responsabilidad clave".</summary>
    public string WorkExperienceJson { get; set; } = "[]";

    /// <summary>JSON string[] — one entry per education item, e.g.
    /// "Universidad X — Ingeniería en Sistemas (2015-2019)".</summary>
    public string EducationJson { get; set; } = "[]";

    /// <summary>JSON string[] — certifications the person holds.</summary>
    public string CertificationsJson { get; set; } = "[]";

    /// <summary>JSON string[] — languages the person speaks (e.g. "Inglés
    /// avanzado").</summary>
    public string LanguagesJson { get; set; } = "[]";

    /// <summary>The raw résumé/CV text the employee/HRAdmin pasted in —
    /// kept for audit and so "Extraer con IA" can be re-run even after the
    /// structured fields above are hand-edited during review.</summary>
    public string? RawResumeText { get; set; }

    /// <summary>The Azure OpenAI deployment/model that last extracted from
    /// <see cref="RawResumeText"/> (e.g. "gpt-5-mini"), for audit purposes.</summary>
    public string? ExtractionModel { get; set; }

    public DateTime? ExtractedDate { get; set; }

    public DateTime CreatedDate { get; set; }

    public DateTime UpdatedDate { get; set; }

    /*
     * Navigation properties
     */

    public Person Person { get; set; } = null!;
}
