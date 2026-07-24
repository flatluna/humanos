namespace HumanOS.Models.Motivations;

/// <summary>
/// Motivation — catalog of universal "what drives you" tags (Growth,
/// Curiosity, Independence...) picked during the Growth Plan's "Where You
/// Want to Go" step. Distinct from <see cref="HumanOS.Models.Goals.Goal"/>
/// (concrete things a person wants to achieve) — a Motivation is a
/// standing personal trait/driver, not something adopted/completed.
/// </summary>
public sealed class Motivation
{
    public Guid MotivationId { get; set; }

    /// <summary>Stable slug (e.g. "curiosity") used to match a catalog row
    /// from client code, independent of the localized display Name.</summary>
    public string Code { get; set; } = null!;

    public string Name { get; set; } = null!;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedDate { get; set; }

    public DateTime UpdatedDate { get; set; }

    public ICollection<MotivationTranslation> Translations { get; set; } = [];

    public ICollection<PersonMotivation> PersonMotivations { get; set; } = [];
}
