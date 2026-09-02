using System.Text.Json;
using HumanOS.Contracts.OrgStructure;
using HumanOS.Data;
using HumanOS.Models.OrgStructure;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.Services;

/// <summary>CRUD for the tenant's reusable Skill catalog (2026-08-01
/// "cierra el ciclo" feature) — lets HR author a training requirement
/// ONCE (e.g. "Prevención de Acoso Sexual Laboral", category
/// "hr-processes") and link it to one or MANY JobRoles at a time, instead
/// of retyping the same string into every role's free-text list. Powers
/// the enterprise console's "Catálogo de Skills" page.</summary>
public sealed class SkillService
{
    /// <summary>The 4 career-plan tracks — same values used everywhere
    /// else in this feature (JobRoleResponse's 4 list fields,
    /// PersonCareerPlanService.AutoMatchCapabilitiesAsync, Capability.Category).</summary>
    public static readonly string[] ValidCategories =
    [
        "job-skills",
        "soft-skills",
        "department-processes",
        "hr-processes",
    ];

    private readonly HumanOsDbContext _dbContext;

    public SkillService(HumanOsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>Lightweight projection for callers that only need to let
    /// someone pick a skill by name (2026-08-03 — Capability Studio's
    /// target-skill combobox was pulling the FULL catalog payload,
    /// Description/timestamps/per-Job-Role title+department included,
    /// just to render a name and a count). Single query, no
    /// JobRoleSkills join needed beyond a count.</summary>
    public async Task<IReadOnlyList<SkillSummaryResponse>> GetSummaryByTenantAsync(
        Guid tenantId,
        string? category = null,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Skills
            .AsNoTracking()
            .Where(s => s.TenantId == tenantId && s.IsActive);

        if (!string.IsNullOrWhiteSpace(category))
        {
            query = query.Where(s => s.Category == category);
        }

        return await query
            .OrderBy(s => s.Category)
            .ThenBy(s => s.Name)
            .Select(s => new SkillSummaryResponse
            {
                SkillId = s.SkillId,
                Name = s.Name,
                JobRoleCount = s.JobRoleSkills.Count,
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SkillResponse>> GetByTenantAsync(
        Guid tenantId,
        string? category = null,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Skills
            .AsNoTracking()
            .Where(s => s.TenantId == tenantId && s.IsActive);

        if (!string.IsNullOrWhiteSpace(category))
        {
            query = query.Where(s => s.Category == category);
        }

        var skills = await query
            .OrderBy(s => s.Category)
            .ThenBy(s => s.Name)
            .ToListAsync(cancellationToken);

        var skillIds = skills.Select(s => s.SkillId).ToList();

        var links = await _dbContext.JobRoleSkills
            .AsNoTracking()
            .Where(jrs => skillIds.Contains(jrs.SkillId))
            .Select(jrs => new
            {
                jrs.SkillId,
                jrs.JobRoleId,
                JobRoleTitle = jrs.JobRole.Title,
                DepartmentName = jrs.JobRole.Department.Name,
            })
            .ToListAsync(cancellationToken);

        var linksBySkill = links
            .GroupBy(l => l.SkillId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<SkillJobRoleLinkResponse>)g
                    .Select(l => new SkillJobRoleLinkResponse
                    {
                        JobRoleId = l.JobRoleId,
                        JobRoleTitle = l.JobRoleTitle,
                        DepartmentName = l.DepartmentName,
                    })
                    .OrderBy(l => l.JobRoleTitle)
                    .ToList());

        var matches = await GetMatchedCapabilitiesAsync(skills, cancellationToken);

        return skills
            .Select(s => ToResponse(s, linksBySkill.GetValueOrDefault(s.SkillId, []), matches.GetValueOrDefault(s.SkillId)))
            .ToList();
    }

    /// <summary>For each given Skill, finds the active course (Structured
    /// Learning document or Capability graph, if any) that already satisfies it —
    /// same matching logic as PersonCareerPlanService:
    /// (1) Structured Learning Document: TargetSkill or DocumentName matches
    /// this skill's Name (case-insensitive & whitespace-normalized);
    /// (2) Capability Graph: TargetSkill or Name matches.
    /// Powers the "Curso creado" vs "Generar curso" indicator on the Skill
    /// catalog page.</summary>
    public async Task<Dictionary<Guid, (Guid CourseId, string CourseName, string CourseType)?>> GetMatchedCapabilitiesAsync(
        IReadOnlyList<Skill> skills,
        CancellationToken cancellationToken = default)
    {
        var result = new Dictionary<Guid, (Guid, string, string)?>();
        if (skills.Count == 0)
        {
            return result;
        }

        static string Normalize(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return string.Empty;
            return System.Text.RegularExpressions.Regex.Replace(s.Trim(), @"\s+", " ");
        }

        var structuredDocuments = await _dbContext.StructuredLearningDocuments
            .AsNoTracking()
            .Select(d => new { d.StructuredLearningDocumentId, Name = d.DocumentName, d.Category, d.TargetSkill })
            .ToListAsync(cancellationToken);

        var activeCapabilities = await _dbContext.Capabilities
            .AsNoTracking()
            .Where(c => c.IsActive)
            .Select(c => new { c.CapabilityId, c.Name, c.Category, c.TargetSkill })
            .ToListAsync(cancellationToken);

        foreach (var skill in skills)
        {
            var skillNorm = Normalize(skill.Name);
            if (string.IsNullOrEmpty(skillNorm)) continue;

            // 1. Structured Learning Documents (primary Studio authoring format)
            var explicitDoc = structuredDocuments.FirstOrDefault(
                d => d.TargetSkill is not null
                    && (d.Category is null || d.Category == skill.Category)
                    && string.Equals(Normalize(d.TargetSkill), skillNorm, StringComparison.OrdinalIgnoreCase));

            var docMatch = explicitDoc ?? structuredDocuments.FirstOrDefault(
                d => (d.Category is null || d.Category == skill.Category)
                    && string.Equals(Normalize(d.Name), skillNorm, StringComparison.OrdinalIgnoreCase));

            docMatch ??= structuredDocuments.FirstOrDefault(
                d => (d.Category is null || d.Category == skill.Category)
                    && !string.IsNullOrEmpty(Normalize(d.TargetSkill))
                    && (Normalize(d.TargetSkill).Contains(skillNorm, StringComparison.OrdinalIgnoreCase)
                        || skillNorm.Contains(Normalize(d.TargetSkill), StringComparison.OrdinalIgnoreCase)));

            docMatch ??= structuredDocuments.FirstOrDefault(
                d => (d.Category is null || d.Category == skill.Category)
                    && !string.IsNullOrEmpty(Normalize(d.Name))
                    && (Normalize(d.Name).Contains(skillNorm, StringComparison.OrdinalIgnoreCase)
                        || skillNorm.Contains(Normalize(d.Name), StringComparison.OrdinalIgnoreCase)));

            if (docMatch is not null)
            {
                result[skill.SkillId] = (docMatch.StructuredLearningDocumentId, docMatch.Name, "structured-learning");
                continue;
            }

            // 2. Capability Graph courses
            var explicitCap = activeCapabilities.FirstOrDefault(
                c => c.TargetSkill is not null
                    && (c.Category is null || c.Category == skill.Category)
                    && string.Equals(Normalize(c.TargetSkill), skillNorm, StringComparison.OrdinalIgnoreCase));

            var capMatch = explicitCap ?? activeCapabilities.FirstOrDefault(
                c => (c.Category is null || c.Category == skill.Category)
                    && string.Equals(Normalize(c.Name), skillNorm, StringComparison.OrdinalIgnoreCase));

            if (capMatch is not null)
            {
                result[skill.SkillId] = (capMatch.CapabilityId, capMatch.Name, "graph");
            }
        }

        return result;
    }

    public async Task<SkillResponse> CreateAsync(
        Guid tenantId,
        SaveSkillRequest request,
        CancellationToken cancellationToken = default)
    {
        var category = ValidateCategory(request.Category);
        var name = ValidateName(request.Name);

        var duplicate = await _dbContext.Skills.AnyAsync(
            s => s.TenantId == tenantId && s.Category == category && s.IsActive && s.Name.ToLower() == name.ToLower(),
            cancellationToken);
        if (duplicate)
        {
            throw new InvalidOperationException($"A skill named \"{name}\" already exists in this category.");
        }

        var jobRoleIds = await ValidateJobRoleIdsAsync(tenantId, request.JobRoleIds, cancellationToken);

        var now = DateTime.UtcNow;
        var skill = new Skill
        {
            SkillId = Guid.NewGuid(),
            TenantId = tenantId,
            Category = category,
            Name = name,
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            IsActive = true,
            CreatedDate = now,
            UpdatedDate = now,
        };
        _dbContext.Skills.Add(skill);

        foreach (var jobRoleId in jobRoleIds)
        {
            _dbContext.JobRoleSkills.Add(new JobRoleSkill { JobRoleId = jobRoleId, SkillId = skill.SkillId, CreatedDate = now });
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return await GetByIdOrThrowAsync(skill.SkillId, cancellationToken);
    }

    public async Task<SkillResponse?> UpdateAsync(
        Guid skillId,
        SaveSkillRequest request,
        CancellationToken cancellationToken = default)
    {
        var skill = await _dbContext.Skills.SingleOrDefaultAsync(s => s.SkillId == skillId, cancellationToken);
        if (skill is null)
        {
            return null;
        }

        var category = ValidateCategory(request.Category);
        var name = ValidateName(request.Name);

        var duplicate = await _dbContext.Skills.AnyAsync(
            s => s.TenantId == skill.TenantId && s.Category == category && s.IsActive
                 && s.SkillId != skillId && s.Name.ToLower() == name.ToLower(),
            cancellationToken);
        if (duplicate)
        {
            throw new InvalidOperationException($"A skill named \"{name}\" already exists in this category.");
        }

        var jobRoleIds = await ValidateJobRoleIdsAsync(skill.TenantId, request.JobRoleIds, cancellationToken);

        var currentLinks = await _dbContext.JobRoleSkills
            .Where(jrs => jrs.SkillId == skillId)
            .ToListAsync(cancellationToken);

        var currentRoleIds = currentLinks.Select(l => l.JobRoleId).ToHashSet();
        var requestedRoleIds = jobRoleIds.ToHashSet();

        foreach (var link in currentLinks.Where(l => !requestedRoleIds.Contains(l.JobRoleId)))
        {
            _dbContext.JobRoleSkills.Remove(link);
        }

        var now = DateTime.UtcNow;
        foreach (var jobRoleId in requestedRoleIds.Where(id => !currentRoleIds.Contains(id)))
        {
            _dbContext.JobRoleSkills.Add(new JobRoleSkill { JobRoleId = jobRoleId, SkillId = skillId, CreatedDate = now });
        }

        skill.Category = category;
        skill.Name = name;
        skill.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        skill.UpdatedDate = now;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return await GetByIdOrThrowAsync(skillId, cancellationToken);
    }

    /// <summary>Hard-deletes the Skill (and, via cascade, every JobRole
    /// link to it) — this only removes the CATALOG entry, never touches
    /// any JobRole's own free-text list.</summary>
    public async Task<bool> DeleteAsync(Guid skillId, CancellationToken cancellationToken = default)
    {
        var skill = await _dbContext.Skills.SingleOrDefaultAsync(s => s.SkillId == skillId, cancellationToken);
        if (skill is null)
        {
            return false;
        }

        _dbContext.Skills.Remove(skill);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <summary>Promotes each category's free-text list on a JobRole into
    /// the reusable Skill catalog (2026-08-01 "cierra el ciclo" feature):
    /// finds-or-creates a Skill row (matched by tenant+category+name,
    /// case-insensitive) for every name currently in the role's list,
    /// links it to this role, and unlinks any catalog skill previously
    /// linked to this role in that category whose name is no longer
    /// present — never deletes the Skill row itself (it may still be
    /// linked to other roles). Called by JobRoleService on every
    /// create/update (so HR's existing free-text textareas keep working
    /// unchanged while automatically building up the catalog) and by
    /// <see cref="SyncCatalogForTenantAsync"/> to backfill roles that
    /// existed before this feature shipped. Does NOT call
    /// SaveChangesAsync — the caller controls when changes are
    /// persisted (JobRoleService batches this with its own role save).</summary>
    public async Task ReconcileForJobRoleAsync(JobRole jobRole, CancellationToken cancellationToken = default)
    {
        var categoryLists = new (string Category, List<string> Names)[]
        {
            ("job-skills", JsonSerializer.Deserialize<List<string>>(jobRole.TechnicalSkillsJson) ?? []),
            ("soft-skills", JsonSerializer.Deserialize<List<string>>(jobRole.SoftSkillsJson) ?? []),
            ("department-processes", JsonSerializer.Deserialize<List<string>>(jobRole.DepartmentProcessesJson) ?? []),
            ("hr-processes", JsonSerializer.Deserialize<List<string>>(jobRole.HrProcessesJson) ?? []),
        };

        var existingLinks = await _dbContext.JobRoleSkills
            .Where(jrs => jrs.JobRoleId == jobRole.JobRoleId)
            .Include(jrs => jrs.Skill)
            .ToListAsync(cancellationToken);

        var now = DateTime.UtcNow;

        foreach (var (category, names) in categoryLists)
        {
            var trimmedNames = names.Select(n => n.Trim()).Where(n => n.Length > 0).ToList();
            var currentLinksForCategory = existingLinks.Where(l => l.Skill.Category == category).ToList();

            foreach (var link in currentLinksForCategory)
            {
                if (!trimmedNames.Any(n => string.Equals(n, link.Skill.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    _dbContext.JobRoleSkills.Remove(link);
                }
            }

            foreach (var name in trimmedNames)
            {
                if (currentLinksForCategory.Any(l => string.Equals(l.Skill.Name, name, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                // FirstOrDefault (not SingleOrDefault): the catalog can already contain
                // more than one Skill row for the same tenant+category+name (legacy
                // duplicates with no unique constraint enforcing this) — picking the
                // first match keeps saves working instead of throwing "Sequence
                // contains more than one element" on every job role that references one
                // of those already-duplicated names.
                var skill = await _dbContext.Skills.FirstOrDefaultAsync(
                    s => s.TenantId == jobRole.TenantId && s.Category == category && s.Name.ToLower() == name.ToLower(),
                    cancellationToken);

                if (skill is null)
                {
                    skill = new Skill
                    {
                        SkillId = Guid.NewGuid(),
                        TenantId = jobRole.TenantId,
                        Category = category,
                        Name = name,
                        IsActive = true,
                        CreatedDate = now,
                        UpdatedDate = now,
                    };
                    _dbContext.Skills.Add(skill);
                }

                _dbContext.JobRoleSkills.Add(new JobRoleSkill
                {
                    JobRoleId = jobRole.JobRoleId,
                    SkillId = skill.SkillId,
                    CreatedDate = now,
                });
            }
        }
    }

    /// <summary>One-time/on-demand backfill: reconciles EVERY active
    /// JobRole in the tenant against the catalog (see
    /// <see cref="ReconcileForJobRoleAsync"/>) in a single SaveChanges —
    /// needed because roles created/last-saved before this feature
    /// shipped never triggered the per-save reconciliation, so their
    /// existing TechnicalSkills/SoftSkills/etc. lists wouldn't otherwise
    /// show up in the catalog until someone re-saves that role. Exposed
    /// via a "Sincronizar desde Roles y Puestos" button on the catalog
    /// page and safe to re-run any time (idempotent).</summary>
    public async Task<int> SyncCatalogForTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var jobRoles = await _dbContext.JobRoles
            .Where(r => r.TenantId == tenantId && r.IsActive)
            .ToListAsync(cancellationToken);

        foreach (var jobRole in jobRoles)
        {
            await ReconcileForJobRoleAsync(jobRole, cancellationToken);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return jobRoles.Count;
    }

    private async Task<SkillResponse> GetByIdOrThrowAsync(Guid skillId, CancellationToken cancellationToken)
    {
        var skill = await _dbContext.Skills.AsNoTracking().SingleAsync(s => s.SkillId == skillId, cancellationToken);
        var links = await _dbContext.JobRoleSkills
            .AsNoTracking()
            .Where(jrs => jrs.SkillId == skillId)
            .Select(jrs => new SkillJobRoleLinkResponse
            {
                JobRoleId = jrs.JobRoleId,
                JobRoleTitle = jrs.JobRole.Title,
                DepartmentName = jrs.JobRole.Department.Name,
            })
            .OrderBy(l => l.JobRoleTitle)
            .ToListAsync(cancellationToken);

        var matches = await GetMatchedCapabilitiesAsync([skill], cancellationToken);

        return ToResponse(skill, links, matches.GetValueOrDefault(skill.SkillId));
    }

    private async Task<List<Guid>> ValidateJobRoleIdsAsync(Guid tenantId, List<Guid>? jobRoleIds, CancellationToken cancellationToken)
    {
        if (jobRoleIds is null || jobRoleIds.Count == 0)
        {
            return [];
        }

        var distinctIds = jobRoleIds.Distinct().ToList();
        var validCount = await _dbContext.JobRoles
            .CountAsync(r => distinctIds.Contains(r.JobRoleId) && r.TenantId == tenantId, cancellationToken);

        if (validCount != distinctIds.Count)
        {
            throw new InvalidOperationException("One or more job roles do not belong to this tenant.");
        }

        return distinctIds;
    }

    private static string ValidateCategory(string? category)
    {
        if (category is null || !ValidCategories.Contains(category))
        {
            throw new InvalidOperationException(
                $"Category must be one of: {string.Join(", ", ValidCategories)}.");
        }

        return category;
    }

    private static string ValidateName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidOperationException("Skill name is required.");
        }

        return name.Trim();
    }

    private static SkillResponse ToResponse(
        Skill skill,
        IReadOnlyList<SkillJobRoleLinkResponse> jobRoles,
        (Guid CourseId, string CourseName, string CourseType)? matchedCapability = null) => new()
    {
        SkillId = skill.SkillId,
        TenantId = skill.TenantId,
        Category = skill.Category,
        Name = skill.Name,
        Description = skill.Description,
        IsActive = skill.IsActive,
        DocumentStoragePath = skill.DocumentStoragePath,
        DocumentFileName = skill.DocumentFileName,
        DocumentUrl = !string.IsNullOrEmpty(skill.DocumentStoragePath) ? $"/skills/{skill.SkillId}/document" : null,
        JobRoles = jobRoles,
        CreatedDate = skill.CreatedDate,
        UpdatedDate = skill.UpdatedDate,
        MatchedCapabilityId = matchedCapability?.CourseId,
        MatchedCapabilityName = matchedCapability?.CourseName,
        MatchedCourseType = matchedCapability?.CourseType,
    };

    public async Task<Skill?> GetEntityByIdAsync(Guid skillId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Skills.SingleOrDefaultAsync(s => s.SkillId == skillId, cancellationToken);
    }

    public async Task<SkillResponse?> UpdateDocumentPathAsync(
        Guid skillId,
        string? storagePath,
        string? fileName,
        CancellationToken cancellationToken = default)
    {
        var skill = await _dbContext.Skills.SingleOrDefaultAsync(s => s.SkillId == skillId, cancellationToken);
        if (skill is null) return null;

        skill.DocumentStoragePath = storagePath;
        skill.DocumentFileName = fileName;
        skill.UpdatedDate = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return await GetByIdOrThrowAsync(skillId, cancellationToken);
    }

    // ── Shared with JobRoleService/PersonCareerPlanService ────────────────

    /// <summary>All catalog skill NAMES currently linked to one JobRole,
    /// grouped by category — used to UNION into JobRoleResponse's
    /// TechnicalSkills/SoftSkills/DepartmentProcesses/HrProcesses (and
    /// into PersonCareerPlanService's "required" gap-analysis context) on
    /// top of that role's own free-text JSON lists, so a Skill linked
    /// from the catalog shows up everywhere without HR re-typing it.</summary>
    public static async Task<Dictionary<string, List<string>>> GetCatalogNamesByCategoryAsync(
        HumanOsDbContext dbContext,
        Guid jobRoleId,
        CancellationToken cancellationToken = default)
    {
        var rows = await dbContext.JobRoleSkills
            .AsNoTracking()
            .Where(jrs => jrs.JobRoleId == jobRoleId && jrs.Skill.IsActive)
            .Select(jrs => new { jrs.Skill.Category, jrs.Skill.Name })
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(r => r.Category)
            .ToDictionary(g => g.Key, g => g.Select(r => r.Name).ToList());
    }

    /// <summary>Batched version of <see cref="GetCatalogNamesByCategoryAsync"/>
    /// for endpoints that return MANY job roles at once (avoids N+1
    /// queries) — keyed by JobRoleId, then by category.</summary>
    public static async Task<Dictionary<Guid, Dictionary<string, List<string>>>> GetCatalogNamesByRolesAsync(
        HumanOsDbContext dbContext,
        IEnumerable<Guid> jobRoleIds,
        CancellationToken cancellationToken = default)
    {
        var ids = jobRoleIds.ToList();
        if (ids.Count == 0)
        {
            return [];
        }

        var rows = await dbContext.JobRoleSkills
            .AsNoTracking()
            .Where(jrs => ids.Contains(jrs.JobRoleId) && jrs.Skill.IsActive)
            .Select(jrs => new { jrs.JobRoleId, jrs.Skill.Category, jrs.Skill.Name })
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(r => r.JobRoleId)
            .ToDictionary(
                g => g.Key,
                g => g.GroupBy(r => r.Category).ToDictionary(gg => gg.Key, gg => gg.Select(r => r.Name).ToList()));
    }

    /// <summary>Merges a role's free-text JSON list with any catalog
    /// skill names for that same category, deduped case-insensitively
    /// (first-seen casing wins). Pure/no I/O — callers fetch the catalog
    /// dictionary once via the methods above.</summary>
    public static List<string> UnionWithCatalog(List<string> jsonList, Dictionary<string, List<string>>? catalogByCategory, string category)
    {
        if (catalogByCategory is null || !catalogByCategory.TryGetValue(category, out var catalogNames) || catalogNames.Count == 0)
        {
            return jsonList;
        }

        var result = new List<string>(jsonList);
        var seen = new HashSet<string>(jsonList.Select(s => s.Trim()), StringComparer.OrdinalIgnoreCase);
        foreach (var name in catalogNames)
        {
            var trimmed = name.Trim();
            if (seen.Add(trimmed))
            {
                result.Add(trimmed);
            }
        }

        return result;
    }
}
