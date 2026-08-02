using HumanOS.Contracts.OrgStructure;
using HumanOS.Data;
using HumanOS.Models.OrgStructure;
using HumanOS.Models.People;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.Services;

/// <summary>Applies an HRAdmin-confirmed org-chart-import proposal (see
/// <see cref="HumanOS.Agents.OrgChartExtractionAgent"/> +
/// AzureFunctions/Api/ApplyOrgChartFunction.cs): creates the Departments
/// and JobRoles described in the request, reusing any existing Department
/// with the same Name for this tenant instead of duplicating it (so
/// re-running an import — e.g. after fixing a mistake — never creates
/// duplicate departments). Any detected person name is always kept as a
/// text note on the JobRole's Summary; when
/// <see cref="ApplyOrgChartRequest.CreateTestPersonRecords"/> is true it
/// ALSO creates a placeholder <see cref="Person"/> (+ <see cref="PersonProfile"/>
/// for the display name) for that name — see that flag's remarks for why
/// these are test/demo-only accounts (random AzureOid/AzureTid, can never
/// actually sign in) and not real employees.</summary>
public sealed class OrgChartImportService
{
    private readonly HumanOsDbContext _dbContext;

    public OrgChartImportService(HumanOsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ApplyOrgChartResponse> ApplyAsync(
        Guid tenantId,
        ApplyOrgChartRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Departments.Count == 0)
        {
            throw new InvalidOperationException("At least one department is required.");
        }

        var existingDepartments = await _dbContext.Departments
            .Where(d => d.TenantId == tenantId)
            .ToListAsync(cancellationToken);

        // Name -> DepartmentId, seeded with everything that already
        // exists for this tenant so re-running an import never creates
        // duplicates.
        var resolvedIds = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        foreach (var department in existingDepartments)
        {
            resolvedIds.TryAdd(department.Name.Trim(), department.DepartmentId);
        }

        var departmentsCreated = 0;
        var departmentsReused = 0;
        var now = DateTime.UtcNow;

        // Departments can reference each other as parents purely by Name
        // within the SAME request — resolve them in dependency order
        // (parents before children), looping until every node that CAN be
        // resolved has been, so any nesting depth is supported.
        var remaining = new List<ApplyOrgChartDepartment>(request.Departments);
        var safetyIterations = remaining.Count + 1;

        while (remaining.Count > 0 && safetyIterations-- > 0)
        {
            var progressedThisPass = false;

            foreach (var node in remaining.ToList())
            {
                var name = node.Name?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(name))
                {
                    remaining.Remove(node);
                    progressedThisPass = true;
                    continue;
                }

                Guid? parentId = null;
                if (!string.IsNullOrWhiteSpace(node.ParentName))
                {
                    if (!resolvedIds.TryGetValue(node.ParentName.Trim(), out var foundParentId))
                    {
                        // Parent not resolved yet — try again next pass.
                        continue;
                    }
                    parentId = foundParentId;
                }

                if (resolvedIds.ContainsKey(name))
                {
                    departmentsReused++;
                }
                else
                {
                    var department = new Department
                    {
                        DepartmentId = Guid.NewGuid(),
                        TenantId = tenantId,
                        ParentDepartmentId = parentId,
                        Name = name,
                        IsActive = true,
                        CreatedDate = now,
                        UpdatedDate = now,
                    };
                    _dbContext.Departments.Add(department);
                    resolvedIds[name] = department.DepartmentId;
                    departmentsCreated++;
                }

                remaining.Remove(node);
                progressedThisPass = true;
            }

            if (!progressedThisPass)
            {
                break;
            }
        }

        if (remaining.Count > 0)
        {
            var unresolved = string.Join(", ", remaining.Select(d => d.Name));
            throw new InvalidOperationException(
                $"No se pudo resolver el departamento padre de: {unresolved}. " +
                "Verifica que el nombre del padre coincida exactamente con otro departamento de esta importación.");
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        var jobRolesCreated = 0;
        var peopleCreated = 0;
        foreach (var deptNode in request.Departments)
        {
            var name = deptNode.Name?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(name) || !resolvedIds.TryGetValue(name, out var departmentId))
            {
                continue;
            }

            foreach (var roleNode in deptNode.JobRoles)
            {
                if (string.IsNullOrWhiteSpace(roleNode.Title))
                {
                    continue;
                }

                var detectedPersonName = string.IsNullOrWhiteSpace(roleNode.DetectedPersonName)
                    ? null
                    : roleNode.DetectedPersonName!.Trim();

                var jobRole = new JobRole
                {
                    JobRoleId = Guid.NewGuid(),
                    TenantId = tenantId,
                    DepartmentId = departmentId,
                    Title = roleNode.Title.Trim(),
                    LevelLabel = string.IsNullOrWhiteSpace(roleNode.LevelLabel) ? null : roleNode.LevelLabel.Trim(),
                    Summary = detectedPersonName is null ? null : $"Detectado en organigrama importado: {detectedPersonName}",
                    ResponsibilitiesJson = "[]",
                    IsActive = true,
                    CreatedDate = now,
                    UpdatedDate = now,
                };
                _dbContext.JobRoles.Add(jobRole);
                jobRolesCreated++;

                if (request.CreateTestPersonRecords && detectedPersonName is not null)
                {
                    var person = CreatePlaceholderPerson(tenantId, departmentId, jobRole.JobRoleId, now);
                    _dbContext.People.Add(person);
                    _dbContext.PersonProfiles.Add(new PersonProfile
                    {
                        PersonProfileId = Guid.NewGuid(),
                        PersonId = person.PersonId,
                        DisplayName = detectedPersonName,
                        PreferredLanguage = "es",
                        CreatedDate = now,
                        UpdatedDate = now,
                    });
                    peopleCreated++;
                }
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new ApplyOrgChartResponse
        {
            DepartmentsCreated = departmentsCreated,
            DepartmentsReused = departmentsReused,
            JobRolesCreated = jobRolesCreated,
            PeopleCreated = peopleCreated,
        };
    }

    /// <summary>Builds a TEST/DEMO-only Person: a real Person normally
    /// requires an already-authenticated Azure AD identity (AzureOid +
    /// AzureTid), which an org chart image can never provide. This
    /// generates a random, unique-but-fake identity so the row satisfies
    /// UX_Person_AzureTid_AzureOid — this person can never actually sign
    /// in, so only use this for demoing the import feature in
    /// non-production tenants.</summary>
    private static Person CreatePlaceholderPerson(Guid tenantId, Guid departmentId, Guid jobRoleId, DateTime now)
    {
        return new Person
        {
            PersonId = Guid.NewGuid(),
            TenantId = tenantId,
            AzureOid = $"orgchart-import-{Guid.NewGuid():N}",
            AzureTid = "orgchart-import-placeholder",
            Email = null,
            IsActive = true,
            DepartmentId = departmentId,
            JobRoleId = jobRoleId,
            OrgRole = PersonOrgRole.Employee,
            CreatedDate = now,
            UpdatedDate = now,
        };
    }
}
