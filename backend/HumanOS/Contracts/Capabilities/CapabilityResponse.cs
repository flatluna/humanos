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

    public DateTime CreatedDate { get; set; }

    public DateTime UpdatedDate { get; set; }
}
