namespace HumanOS.Models.Processes;

using HumanOS.Models.OrgStructure;

/// <summary>
/// Many-to-many junction recording that a <see cref="ProcessDocumentRecord"/>
/// (a business process / SOP) must be followed by a given <see cref="Department"/>.
/// Ignored when <see cref="ProcessDocumentRecord.AppliesToAllDepartments"/> is true.
/// </summary>
public sealed class ProcessDocumentDepartment
{
    public Guid ProcessDocumentId { get; set; }

    public Guid DepartmentId { get; set; }

    public ProcessDocumentRecord ProcessDocument { get; set; } = null!;

    public Department Department { get; set; } = null!;
}
