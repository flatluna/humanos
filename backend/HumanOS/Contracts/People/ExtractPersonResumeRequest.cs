namespace HumanOS.Contracts.People;

/// <summary>Body for POST /people/{personId}/resume/extract — the raw
/// pasted résumé/CV text to extract structured fields from.</summary>
public sealed class ExtractPersonResumeRequest
{
    public string ResumeText { get; set; } = null!;
}

/// <summary>Response for the same endpoint — ResumeExtractionAgent's
/// field-by-field proposal, plus token usage for observability. The
/// frontend diffs this against the CURRENTLY SAVED résumé itself
/// (client-side) to render the before/after review UI — nothing here is
/// ever saved automatically.</summary>
public sealed class ExtractPersonResumeResponse
{
    public string? Address { get; set; }

    public string? LinkedInUrl { get; set; }

    public string? ProfessionalObjective { get; set; }

    public string? ProfessionalSummary { get; set; }

    public List<string> Skills { get; set; } = [];

    public List<string> WorkExperience { get; set; } = [];

    public List<string> Education { get; set; } = [];

    public List<string> Certifications { get; set; } = [];

    public List<string> Languages { get; set; } = [];

    /// <summary>The Azure OpenAI deployment that produced this proposal
    /// (observability only).</summary>
    public string ModelName { get; set; } = string.Empty;

    public int InputTokens { get; set; }

    public int OutputTokens { get; set; }
}
