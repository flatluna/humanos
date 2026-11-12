namespace HumanOS.Contracts.Family;

public sealed class CountryResponse
{
    public Guid CountryId { get; set; }

    public string Code { get; set; } = null!;

    public string NameEs { get; set; } = null!;

    public string NameEn { get; set; } = null!;

    public int DisplayOrder { get; set; }
}

public sealed class EducationSystemTypeResponse
{
    public Guid EducationSystemTypeId { get; set; }

    public string Code { get; set; } = null!;

    public string NameEs { get; set; } = null!;

    public string NameEn { get; set; } = null!;

    public int DisplayOrder { get; set; }
}

public sealed class SubjectResponse
{
    public Guid SubjectId { get; set; }

    public string Code { get; set; } = null!;

    public string NameEs { get; set; } = null!;

    public string NameEn { get; set; } = null!;

    /// <summary>
    /// 'Academic' or 'Character'
    /// </summary>
    public string SubjectCategory { get; set; } = null!;

    public int DisplayOrder { get; set; }
}

/// <summary>
/// Request to run the Subject Capability Recommender Agent for a specific
/// Country + EducationSystemType + Grade (Stage) + Subject combination.
/// </summary>
public sealed class GenerateSubjectCapabilityRecommendationsRequest
{
    public string CountryCode { get; set; } = null!;

    public string EducationSystemTypeCode { get; set; } = null!;

    public string StageKey { get; set; } = null!;

    public string SubjectCode { get; set; } = null!;

    /// <summary>
    /// Optional: when set to 'es' or 'en', the course is generated in that single language only
    /// (both the Es and En fields are filled with the same text in that language) to avoid making
    /// the LLM produce a redundant translation nobody will use. Null/omitted = legacy full
    /// bilingual generation (both languages genuinely translated).
    /// </summary>
    public string? Language { get; set; }
}

/// <summary>
/// Request to edit the text fields of an existing (still-pending) capability recommendation,
/// before it's sent to HumanOS Studio.
/// </summary>
public sealed class UpdateCapabilityRecommendationRequest
{
    public string NameEs { get; set; } = null!;

    public string NameEn { get; set; } = null!;

    public string DescriptionEs { get; set; } = null!;

    public string DescriptionEn { get; set; } = null!;

    public string? WhyItMattersEs { get; set; }

    public string? WhyItMattersEn { get; set; }

    public List<string> MicroSkillsCoveredEs { get; set; } = [];

    public List<string> MicroSkillsCoveredEn { get; set; } = [];

    /// <summary>Prerequisites/requirements a student should already have before starting this course.</summary>
    public string? CourseRequirementsEs { get; set; }

    public string? CourseRequirementsEn { get; set; }

    /// <summary>Short 4-8 item chapter/topic-title preview, shown before sending to Studio.</summary>
    public List<string> CourseOutlineEs { get; set; } = [];

    public List<string> CourseOutlineEn { get; set; } = [];
}

/// <summary>
/// A single AI-recommended capability, pending hand-off to HumanOS Studio
/// (ArquitectoAgent pipeline) to generate its full Engrams.
/// </summary>
public sealed class CapabilityRecommendationResponse
{
    public Guid RecommendationId { get; set; }

    public string Code { get; set; } = null!;

    public string CountryCode { get; set; } = null!;

    public string EducationSystemTypeCode { get; set; } = null!;

    public string StageKey { get; set; } = null!;

    public string SubjectCode { get; set; } = null!;

    /// <summary>Human-readable age range for the selected grade/stage, e.g. '6 - 7 años'. Comes from
    /// family.HumanDevelopmentStages.AgeRange — not AI-generated, so it's never hallucinated.</summary>
    public string? AgeRange { get; set; }

    /// <summary>
    /// 'es' | 'en' — the single language this recommendation's real content was generated in
    /// (Es/En fields hold the same text duplicated). Null for legacy rows generated before this
    /// field existed (true bilingual content, both languages genuinely translated).
    /// </summary>
    public string? Language { get; set; }

    public string NameEs { get; set; } = null!;

    public string NameEn { get; set; } = null!;

    public string DescriptionEs { get; set; } = null!;

    public string DescriptionEn { get; set; } = null!;

    public string? WhyItMattersEs { get; set; }

    public string? WhyItMattersEn { get; set; }

    public List<string> MicroSkillsCoveredEs { get; set; } = [];

    public List<string> MicroSkillsCoveredEn { get; set; } = [];

    /// <summary>Prerequisites/requirements a student should already have before starting this course.</summary>
    public string? CourseRequirementsEs { get; set; }

    public string? CourseRequirementsEn { get; set; }

    /// <summary>Short 4-8 item chapter/topic-title preview, shown before sending to Studio.</summary>
    public List<string> CourseOutlineEs { get; set; } = [];

    public List<string> CourseOutlineEn { get; set; } = [];

    /// <summary>
    /// Sources/references the AI agent actually used (e.g. advanced teaching methods from
    /// countries like Japan, Finland, Singapore, or real URLs found via live web search). Each
    /// entry is one human-readable line. Empty when no source grounded this recommendation.
    /// </summary>
    public List<string> Sources { get; set; } = [];

    /// <summary>
    /// 'PendingEngramGeneration' | 'SentToStudio' | 'Generated'
    /// </summary>
    public string GenerationStatus { get; set; } = "PendingEngramGeneration";

    /// <summary>
    /// Ready-to-use idea/topic draft text for Studio's "description" field
    /// (NewCapabilityPage / startCapabilityGraphFromDescription).
    /// </summary>
    public string? StudioIdeaDraftEs { get; set; }

    public string? StudioIdeaDraftEn { get; set; }

    public string? StudioRunId { get; set; }

    /// <summary>The Studio-side StructuredLearningDocumentId, once Studio has created the course/Engrams
    /// for this recommendation — set by FamilyCapabilityService.SyncStudioStatusAsync. Null until then.</summary>
    public Guid? StudioDocumentId { get; set; }

    public DateTime CreatedDate { get; set; }
}
