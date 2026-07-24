namespace HumanOS.Models.Programs;

using HumanOS.Models.Capabilities;

/// <summary>
/// A "Program" (curated learning path) that groups an ordered sequence of
/// existing Capabilities toward a specific outcome (e.g. "Programa de
/// Inglés hacia Conversación"). A Capability's membership in a Program is
/// always OPTIONAL — a Capability can exist standalone (as today) and/or
/// belong to zero, one, or several Programs via <see cref="ProgramCapability"/>.
/// Named "LearningProgram" in C# (not "Program") to avoid colliding with
/// the isolated-worker's own top-level-statement-generated Program class
/// in Program.cs — the SQL table itself is still named "Program".
/// </summary>
public sealed class LearningProgram
{
    public Guid ProgramId { get; set; }

    public string Code { get; set; } = null!;

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    /// <summary>What a learner will achieve by completing this Program
    /// (free text, shown as-is on the Program's detail/landing view).</summary>
    public string? Objectives { get; set; }

    /// <summary>What a learner needs before starting this Program (prior
    /// level, time commitment, prerequisites, etc.), free text.</summary>
    public string? Requirements { get; set; }

    /// <summary>Blob StoragePath (Azure Data Lake, "program-logos"
    /// container) of the Program's logo/cover image. Null when no logo has
    /// been uploaded yet.</summary>
    public string? LogoStoragePath { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedDate { get; set; }

    public DateTime UpdatedDate { get; set; }

    public ICollection<ProgramTranslation> Translations { get; set; } = [];

    public ICollection<ProgramCapability> ProgramCapabilities { get; set; } = [];
}
