namespace HumanOS.Models.OrgStructure;

/// <summary>
/// Many-to-many junction: which <see cref="JobRole"/>s include a given <see cref="ExperienceRequirement"/>.
/// </summary>
public sealed class JobRoleExperienceRequirement
{
    public Guid JobRoleId { get; set; }

    public JobRole JobRole { get; set; } = null!;

    public Guid ExperienceRequirementId { get; set; }

    public ExperienceRequirement ExperienceRequirement { get; set; } = null!;

    public DateTime CreatedDate { get; set; }
}
