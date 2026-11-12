namespace HumanOS.Models.Processes;

using HumanOS.Models.OrgStructure;

/// <summary>
/// Many-to-many junction recording that a <see cref="ProcessSkillRequirement"/>
/// applies to a given <see cref="JobRole"/>.
/// Ignored when <see cref="ProcessSkillRequirement.AppliesToAllDepartments"/> is true.
/// </summary>
public sealed class ProcessSkillRequirementJobRole
{
    public Guid ProcessSkillRequirementId { get; set; }

    public Guid JobRoleId { get; set; }

    public ProcessSkillRequirement ProcessSkillRequirement { get; set; } = null!;

    public JobRole JobRole { get; set; } = null!;
}
