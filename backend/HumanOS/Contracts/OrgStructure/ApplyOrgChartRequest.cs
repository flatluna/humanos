namespace HumanOS.Contracts.OrgStructure;

/// <summary>Body for POST /tenants/{tenantId}/org-chart/apply — the
/// HRAdmin-confirmed departments/job-roles proposal (after reviewing and
/// editing the AI extraction from OrgChartExtractionAgent) to create for
/// real.</summary>
public sealed class ApplyOrgChartRequest
{
    public List<ApplyOrgChartDepartment> Departments { get; set; } = [];

    /// <summary>When true, also creates a placeholder
    /// <see cref="HumanOS.Models.People.Person"/> record for every
    /// JobRole with a non-empty <see cref="ApplyOrgChartJobRole.DetectedPersonName"/>.
    /// These are TEST/DEMO records only — a Person normally requires a
    /// real, already-authenticated Azure AD identity, which an org chart
    /// image can never provide, so these placeholders get a randomly
    /// generated AzureOid/AzureTid and can never actually sign in. Use
    /// only in non-production tenants for demoing the feature.</summary>
    public bool CreateTestPersonRecords { get; set; }
}

public sealed class ApplyOrgChartDepartment
{
    public string Name { get; set; } = null!;

    /// <summary>Must match another Departments[].Name in this SAME
    /// request, or be null for a top-level department.</summary>
    public string? ParentName { get; set; }

    public List<ApplyOrgChartJobRole> JobRoles { get; set; } = [];
}

public sealed class ApplyOrgChartJobRole
{
    public string Title { get; set; } = null!;

    public string? LevelLabel { get; set; }

    /// <summary>The person's name detected in the org chart image for
    /// this position, kept as a text note on
    /// <see cref="HumanOS.Models.OrgStructure.JobRole.Summary"/>. Also
    /// used to create a placeholder <see cref="HumanOS.Models.People.Person"/>
    /// when <see cref="ApplyOrgChartRequest.CreateTestPersonRecords"/> is
    /// true — see that flag's remarks for why these are test-only
    /// accounts, never real logins.</summary>
    public string? DetectedPersonName { get; set; }
}

/// <summary>Response for POST /tenants/{tenantId}/org-chart/apply.</summary>
public sealed class ApplyOrgChartResponse
{
    public int DepartmentsCreated { get; set; }

    public int DepartmentsReused { get; set; }

    public int JobRolesCreated { get; set; }

    public int PeopleCreated { get; set; }
}
