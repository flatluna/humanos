namespace HumanOS.Contracts.Capabilities;

using HumanOS.Agents.Studio;

public sealed class CapabilityResponse
{
    public Guid CapabilityId { get; set; }

    public Guid CapabilityDomainId { get; set; }

    public string DomainCode { get; set; } = null!;

    public Guid? SubjectId { get; set; }

    public string? SubjectCode { get; set; }

    public string Code { get; set; } = null!;

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public bool IsActive { get; set; }

    /// <summary>"GraphMemoryParadox" | "TraditionalChapters" — which
    /// course-creation method produced this capability, so the frontend
    /// knows which detail/preview page to route to. See
    /// Capability.LearningMode.</summary>
    public string LearningMode { get; set; } = "GraphMemoryParadox";

    /// <summary>"es" or "en" — the language this capability's entire
    /// generated content was authored in. See Capability.ContentLanguageCode.</summary>
    public string ContentLanguageCode { get; set; } = "es";

    /// <summary>Optional tone/persona directive for ALL generated
    /// learner-facing content on this capability. Null means "Standard".
    /// The only other supported value today is "YoungLearner" (K-12
    /// early-childhood courses). See Capability.PedagogicalAudience.</summary>
    public string? PedagogicalAudience { get; set; }

    /// <summary>Formal/school-track opt-in: when true, this capability's
    /// generated blueprints include an OPTIONAL 6th step, Examples, with
    /// example cards. See Capability.IncludeExamplesStep.</summary>
    public bool IncludeExamplesStep { get; set; }

    /// <summary>"job-skills" | "soft-skills" | "department-processes" |
    /// "hr-processes" | null. See Capability.Category.</summary>
    public string? Category { get; set; }

    /// <summary>See Capability.TargetJobRoleId.</summary>
    public Guid? TargetJobRoleId { get; set; }

    /// <summary>See Capability.TargetSkill.</summary>
    public string? TargetSkill { get; set; }

    public int LevelCount { get; set; }

    public int ModuleCount { get; set; }

    /// <summary>Total CapabilityGraphNode count for this capability's graph
    /// (0 when no graph has been generated yet). Shown on the student's
    /// course card as a "total nodes" stat (2026-07-21).</summary>
    public int NodeCount { get; set; }

    /// <summary>True when a course-level cover image has been generated and
    /// stored (see CapabilityGraph.CoverImageStoragePath) — lets the
    /// frontend know it can request GET /capabilities/{id}/cover-image
    /// instead of falling back to a placeholder.</summary>
    public bool HasCoverImage { get; set; }

    /// <summary>Short, student-facing "what you'll learn" teaser derived
    /// from CapabilityGraph.ExecutiveSummary (truncated). Null when no
    /// graph has been generated yet. Shown on the student's course card
    /// INSTEAD OF the raw Description field, which for PDF-generated
    /// capabilities is just an internal note ("Generado automáticamente a
    /// partir de un PDF...") and not meant for learners (2026-07-21).</summary>
    public string? LearningSummary { get; set; }

    /// <summary>Which Human Evolution Layers this capability's levels
    /// cover (e.g. ["Foundation", "Exploration", "Mastery"]).</summary>
    public List<HumanEvolutionLayer> Levels { get; set; } = [];

    /// <summary>See Capability.OwnerPersonId. Null for the public/official
    /// catalog.</summary>
    public Guid? OwnerPersonId { get; set; }

    /// <summary>See Capability.OwnerTenantId. Null for a public capability
    /// or a Consumer's personal one.</summary>
    public Guid? OwnerTenantId { get; set; }

    public DateTime CreatedDate { get; set; }

    public DateTime UpdatedDate { get; set; }
}
