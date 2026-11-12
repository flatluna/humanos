namespace HumanOS.Models.Processes;

using HumanOS.Models.OrgStructure;

/// <summary>
/// Many-to-many junction recording that a <see cref="ProcessSkillRequirement"/>
/// applies to a given <see cref="Department"/>.
/// Ignored when <see cref="ProcessSkillRequirement.AppliesToAllDepartments"/> is true.
/// </summary>
public sealed class ProcessSkillRequirementDepartment
{
    public Guid ProcessSkillRequirementId { get; set; }

    public Guid DepartmentId { get; set; }

    public ProcessSkillRequirement ProcessSkillRequirement { get; set; } = null!;

    public Department Department { get; set; } = null!;
}
