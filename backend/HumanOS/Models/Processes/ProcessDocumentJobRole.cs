namespace HumanOS.Models.Processes;

using HumanOS.Models.OrgStructure;

/// <summary>
/// Many-to-many junction recording that a <see cref="ProcessDocumentRecord"/>
/// (a business process / SOP) must be followed by a given <see cref="JobRole"/>.
/// Ignored when <see cref="ProcessDocumentRecord.AppliesToAllDepartments"/> is true.
/// </summary>
public sealed class ProcessDocumentJobRole
{
    public Guid ProcessDocumentId { get; set; }

    public Guid JobRoleId { get; set; }

    public ProcessDocumentRecord ProcessDocument { get; set; } = null!;

    public JobRole JobRole { get; set; } = null!;
}
