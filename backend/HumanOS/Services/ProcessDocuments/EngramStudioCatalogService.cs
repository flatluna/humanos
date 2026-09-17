using HumanOS.Contracts.ProcessDocuments;
using HumanOS.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HumanOS.Services.ProcessDocuments;

/// <summary>
/// Retrieves lightweight metadata for candidate Classes belonging to Learning Paths
/// from the Engram Studio catalog.
/// Excludes Engram names, descriptions, and instructional contents.
/// </summary>
public sealed class EngramStudioCatalogService : IEngramStudioCatalogService
{
    private readonly IDbContextFactory<HumanOsDbContext> _dbContextFactory;

    public EngramStudioCatalogService(IDbContextFactory<HumanOsDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    private HumanOsDbContext CreateDbContext() =>
        _dbContextFactory.CreateDbContext();

    public async Task<IReadOnlyList<EngramStudioClassSummary>> GetAvailableClassesAsync(
        string? organizationId = null,
        string? language = null,
        CancellationToken cancellationToken = default)
    {
        using var dbContext = _dbContextFactory.CreateDbContext();

        Guid? tenantGuid = null;
        if (!string.IsNullOrWhiteSpace(organizationId) && Guid.TryParse(organizationId, out var parsed))
        {
            tenantGuid = parsed;
        }

        // Resolve real Tenant GUID if organizationId was an Azure AD TID or Person identity
        if (!tenantGuid.HasValue || !await dbContext.Tenants.AnyAsync(t => t.TenantId == tenantGuid.Value, cancellationToken))
        {
            if (!string.IsNullOrWhiteSpace(organizationId))
            {
                var tenant = await dbContext.Tenants
                    .AsNoTracking()
                    .FirstOrDefaultAsync(t => t.AzureTenantId == organizationId, cancellationToken);
                if (tenant != null)
                {
                    tenantGuid = tenant.TenantId;
                }
                else
                {
                    var person = await dbContext.People
                        .AsNoTracking()
                        .FirstOrDefaultAsync(p => (p.AzureTid == organizationId || p.AzureOid == organizationId) && p.TenantId != null, cancellationToken);
                    if (person?.TenantId != null)
                    {
                        tenantGuid = person.TenantId.Value;
                    }
                }
            }
        }

        var results = new List<EngramStudioClassSummary>();

        // 1. Retrieve published Capabilities linked to active Learning Programs
        var programCapabilitiesQuery = dbContext.ProgramCapabilities
            .AsNoTracking()
            .Where(pc => pc.Program.IsActive && pc.Capability.IsActive);

        // Organization isolation: public/official or owned by the tenant
        if (tenantGuid.HasValue)
        {
            programCapabilitiesQuery = programCapabilitiesQuery.Where(pc =>
                pc.Capability.OwnerTenantId == null || pc.Capability.OwnerTenantId == tenantGuid.Value);
        }
        else
        {
            programCapabilitiesQuery = programCapabilitiesQuery.Where(pc =>
                pc.Capability.OwnerTenantId == null && pc.Capability.OwnerPersonId == null);
        }

        // Language filter if provided
        if (!string.IsNullOrWhiteSpace(language))
        {
            var normalizedLang = language.Trim().ToLowerInvariant();
            programCapabilitiesQuery = programCapabilitiesQuery.Where(pc =>
                pc.Capability.ContentLanguageCode == null ||
                pc.Capability.ContentLanguageCode == normalizedLang);
        }

        var capabilitySummaries = await programCapabilitiesQuery
            .Select(pc => new EngramStudioClassSummary
            {
                LearningPathId = pc.Program.ProgramId.ToString(),
                LearningPathName = pc.Program.Name,
                ClassId = pc.Capability.CapabilityId.ToString(),
                ClassName = pc.Capability.Name,
                ClassDescription = pc.Capability.Description ?? pc.Capability.ExecutiveSummary ?? pc.Objectives ?? string.Empty,
                PublicationStatus = pc.Capability.IsActive ? "published" : "draft",
                Language = pc.Capability.ContentLanguageCode ?? "es",
                EngramCount = pc.Capability.CapabilityGraph != null ? pc.Capability.CapabilityGraph.Nodes.Count : 0,
                CapabilityOrTopicTags = new List<string>
                {
                    pc.Capability.Subject != null ? pc.Capability.Subject.Code : string.Empty,
                    pc.Capability.Category ?? string.Empty
                }.Where(t => !string.IsNullOrEmpty(t)).ToList(),
                OwnerTenantId = pc.Capability.OwnerTenantId,
                IsGlobal = pc.Capability.OwnerTenantId == null && pc.Capability.OwnerPersonId == null
            })
            .ToListAsync(cancellationToken);

        results.AddRange(capabilitySummaries);

        // 2. Standalone active Capabilities in Studio (even if not part of a Learning Program)
        var standaloneCapabilitiesQuery = dbContext.Capabilities
            .AsNoTracking()
            .Where(c => c.IsActive && !results.Select(r => r.ClassId).Contains(c.CapabilityId.ToString()));

        if (tenantGuid.HasValue)
        {
            standaloneCapabilitiesQuery = standaloneCapabilitiesQuery.Where(c =>
                c.OwnerTenantId == null || c.OwnerTenantId == tenantGuid.Value);
        }
        else
        {
            standaloneCapabilitiesQuery = standaloneCapabilitiesQuery.Where(c =>
                c.OwnerTenantId == null && c.OwnerPersonId == null);
        }

        var standaloneCapSummaries = await standaloneCapabilitiesQuery
            .Select(c => new EngramStudioClassSummary
            {
                LearningPathId = c.CapabilityId.ToString(),
                LearningPathName = c.Name,
                ClassId = c.CapabilityId.ToString(),
                ClassName = c.Name,
                ClassDescription = c.Description ?? c.ExecutiveSummary ?? c.Name,
                PublicationStatus = c.IsActive ? "published" : "draft",
                Language = c.ContentLanguageCode ?? "es",
                EngramCount = c.CapabilityGraph != null ? c.CapabilityGraph.Nodes.Count : 0,
                CapabilityOrTopicTags = new List<string>
                {
                    c.Subject != null ? c.Subject.Code : string.Empty,
                    c.Category ?? string.Empty,
                    c.TargetSkill ?? string.Empty
                }.Where(t => !string.IsNullOrEmpty(t)).ToList(),
                OwnerTenantId = c.OwnerTenantId,
                IsGlobal = c.OwnerTenantId == null && c.OwnerPersonId == null
            })
            .ToListAsync(cancellationToken);

        results.AddRange(standaloneCapSummaries);

        // 3. Retrieve published Structured Learning Documents linked to active Learning Programs
        var structuredMembershipsQuery = dbContext.StructuredLearningDocumentProgramMemberships
            .AsNoTracking()
            .Where(sm => sm.Program.IsActive && sm.Document.IsPublished);

        if (tenantGuid.HasValue)
        {
            structuredMembershipsQuery = structuredMembershipsQuery.Where(sm =>
                sm.Document.IsGlobal || sm.Document.TenantId == Guid.Empty || sm.Document.TenantId == tenantGuid.Value);
        }
        else
        {
            structuredMembershipsQuery = structuredMembershipsQuery.Where(sm =>
                sm.Document.IsGlobal || sm.Document.TenantId == Guid.Empty);
        }

        var structuredSummaries = await structuredMembershipsQuery
            .Select(sm => new EngramStudioClassSummary
            {
                LearningPathId = sm.Program.ProgramId.ToString(),
                LearningPathName = sm.Program.Name,
                ClassId = sm.Document.StructuredLearningDocumentId.ToString(),
                ClassName = sm.Document.DocumentName,
                ClassDescription = sm.Document.ExecutiveSummary ?? string.Empty,
                PublicationStatus = sm.Document.IsPublished ? "published" : "draft",
                Language = "es",
                EngramCount = sm.Document.Sections.Count,
                CapabilityOrTopicTags = new List<string>
                {
                    sm.Document.Category ?? string.Empty,
                    sm.Document.TargetSkill ?? string.Empty
                }.Where(t => !string.IsNullOrEmpty(t)).ToList(),
                OwnerTenantId = sm.Document.TenantId == Guid.Empty ? null : (Guid?)sm.Document.TenantId,
                IsGlobal = sm.Document.IsGlobal
            })
            .ToListAsync(cancellationToken);

        results.AddRange(structuredSummaries);

        // 4. Standalone published Structured Learning Documents (authored in Studio)
        var standaloneDocsQuery = dbContext.StructuredLearningDocuments
            .AsNoTracking()
            .Where(d => d.IsPublished && !results.Select(r => r.ClassId).Contains(d.StructuredLearningDocumentId.ToString()));

        if (tenantGuid.HasValue)
        {
            standaloneDocsQuery = standaloneDocsQuery.Where(d =>
                d.IsGlobal || d.TenantId == Guid.Empty || d.TenantId == tenantGuid.Value);
        }
        else
        {
            standaloneDocsQuery = standaloneDocsQuery.Where(d =>
                d.IsGlobal || d.TenantId == Guid.Empty);
        }

        var standaloneDocSummaries = await standaloneDocsQuery
            .Select(d => new EngramStudioClassSummary
            {
                LearningPathId = d.StructuredLearningDocumentId.ToString(),
                LearningPathName = d.DocumentName,
                ClassId = d.StructuredLearningDocumentId.ToString(),
                ClassName = d.DocumentName,
                ClassDescription = d.ExecutiveSummary ?? d.DocumentName,
                PublicationStatus = d.IsPublished ? "published" : "draft",
                Language = "es",
                EngramCount = d.Sections.Count,
                CapabilityOrTopicTags = new List<string>
                {
                    d.Category ?? string.Empty,
                    d.TargetSkill ?? string.Empty
                }.Where(t => !string.IsNullOrEmpty(t)).ToList(),
                OwnerTenantId = d.TenantId == Guid.Empty ? null : (Guid?)d.TenantId,
                IsGlobal = d.IsGlobal
            })
            .ToListAsync(cancellationToken);

        results.AddRange(standaloneDocSummaries);

        // 5. Tenant's official Skill Catalog (dbo.Skill) — In Human OS, every Skill represents a Course / Curriculum module
        if (tenantGuid.HasValue)
        {
            var catalogSkills = await dbContext.Skills
                .AsNoTracking()
                .Where(s => s.TenantId == tenantGuid.Value && s.IsActive)
                .ToListAsync(cancellationToken);

            foreach (var sk in catalogSkills)
            {
                var skillIdStr = sk.SkillId.ToString();
                if (results.Any(r => string.Equals(r.ClassId, skillIdStr, StringComparison.OrdinalIgnoreCase) ||
                                     string.Equals(r.ClassName, sk.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                results.Add(new EngramStudioClassSummary
                {
                    LearningPathId = $"path-{sk.Category}",
                    LearningPathName = sk.Category switch
                    {
                        "soft-skills" => "Soft Skills",
                        "department-processes" => "Department Processes",
                        "hr-processes" => "HR Processes",
                        _ => "Job Skills"
                    },
                    ClassId = skillIdStr,
                    ClassName = sk.Name,
                    ClassDescription = !string.IsNullOrWhiteSpace(sk.Description)
                        ? sk.Description
                        : $"Catalog Skill: {sk.Name} ({sk.Category})",
                    PublicationStatus = "published",
                    Language = "es",
                    EngramCount = 1,
                    CapabilityOrTopicTags = new List<string> { sk.Category },
                    OwnerTenantId = sk.TenantId,
                    IsGlobal = false
                });
            }
        }

        return results;
    }

    public async Task<IReadOnlyList<EngramStudioEngramSummary>> GetEngramsByClassIdsAsync(
        IEnumerable<string> classIds,
        string? organizationId = null,
        string? language = null,
        CancellationToken cancellationToken = default)
    {
        var targetIds = classIds
            .Where(id => Guid.TryParse(id, out _))
            .Select(Guid.Parse)
            .ToHashSet();

        if (targetIds.Count == 0)
        {
            return Array.Empty<EngramStudioEngramSummary>();
        }

        using var dbContext = _dbContextFactory.CreateDbContext();

        Guid? tenantGuid = null;
        if (!string.IsNullOrWhiteSpace(organizationId) && Guid.TryParse(organizationId, out var parsed))
        {
            tenantGuid = parsed;
        }

        if (!tenantGuid.HasValue || !await dbContext.Tenants.AnyAsync(t => t.TenantId == tenantGuid.Value, cancellationToken))
        {
            if (!string.IsNullOrWhiteSpace(organizationId))
            {
                var tenant = await dbContext.Tenants
                    .AsNoTracking()
                    .FirstOrDefaultAsync(t => t.AzureTenantId == organizationId, cancellationToken);
                if (tenant != null)
                {
                    tenantGuid = tenant.TenantId;
                }
                else
                {
                    var person = await dbContext.People
                        .AsNoTracking()
                        .FirstOrDefaultAsync(p => (p.AzureTid == organizationId || p.AzureOid == organizationId) && p.TenantId != null, cancellationToken);
                    if (person?.TenantId != null)
                    {
                        tenantGuid = person.TenantId.Value;
                    }
                }
            }
        }

        var results = new List<EngramStudioEngramSummary>();

        // 1. Structured Learning Document Sections (Engrams in Structured Learning Documents)
        var sectionsQuery = dbContext.StructuredLearningDocumentSections
            .AsNoTracking()
            .Where(s => targetIds.Contains(s.StructuredLearningDocumentId) && s.Document.IsPublished);

        if (tenantGuid.HasValue)
        {
            sectionsQuery = sectionsQuery.Where(s =>
                s.Document.IsGlobal || s.Document.TenantId == Guid.Empty || s.Document.TenantId == tenantGuid.Value);
        }
        else
        {
            sectionsQuery = sectionsQuery.Where(s =>
                s.Document.IsGlobal || s.Document.TenantId == Guid.Empty);
        }

        var sectionEngrams = await sectionsQuery
            .Select(s => new EngramStudioEngramSummary
            {
                EngramId = s.StructuredLearningDocumentSectionId.ToString(),
                ClassId = s.StructuredLearningDocumentId.ToString(),
                EngramName = !string.IsNullOrWhiteSpace(s.Capability) ? s.Capability
                           : (!string.IsNullOrWhiteSpace(s.SubchapterTitle) ? s.SubchapterTitle
                           : s.ChapterTitle),
                EngramDescription = !string.IsNullOrWhiteSpace(s.SubchapterTitle) && !string.IsNullOrWhiteSpace(s.Capability)
                           ? $"{s.ChapterTitle} — {s.SubchapterTitle}"
                           : s.ChapterTitle,
                PublicationStatus = s.Document.IsPublished ? "published" : "draft",
                Language = "es",
                OwnerTenantId = s.Document.TenantId == Guid.Empty ? null : (Guid?)s.Document.TenantId,
                IsGlobal = s.Document.IsGlobal,
                Tags = new List<string> { s.ChapterTitle }.Where(t => !string.IsNullOrEmpty(t)).ToList()
            })
            .ToListAsync(cancellationToken);

        results.AddRange(sectionEngrams);

        // 2. CapabilityGraphNodes (Engrams in Graph-based Capabilities)
        var nodesQuery = dbContext.CapabilityGraphNodes
            .AsNoTracking()
            .Where(n => (n.CapabilityGraph != null && targetIds.Contains(n.CapabilityGraph.CapabilityId)) ||
                        targetIds.Contains(n.CapabilityGraphId));

        if (tenantGuid.HasValue)
        {
            nodesQuery = nodesQuery.Where(n =>
                n.CapabilityGraph!.Capability.OwnerTenantId == null ||
                n.CapabilityGraph.Capability.OwnerTenantId == tenantGuid.Value);
        }
        else
        {
            nodesQuery = nodesQuery.Where(n =>
                n.CapabilityGraph!.Capability.OwnerTenantId == null &&
                n.CapabilityGraph.Capability.OwnerPersonId == null);
        }

        if (!string.IsNullOrWhiteSpace(language))
        {
            var normalizedLang = language.Trim().ToLowerInvariant();
            nodesQuery = nodesQuery.Where(n =>
                n.CapabilityGraph!.Capability.ContentLanguageCode == null ||
                n.CapabilityGraph.Capability.ContentLanguageCode == normalizedLang);
        }

        var nodeEngrams = await nodesQuery
            .Select(n => new EngramStudioEngramSummary
            {
                EngramId = n.CapabilityGraphNodeId.ToString(),
                ClassId = n.CapabilityGraph!.CapabilityId.ToString(),
                EngramName = n.Name,
                EngramDescription = n.Description ?? n.AcademicDefinition ?? n.Interpretation ?? string.Empty,
                PublicationStatus = n.CapabilityGraph.Capability.IsActive ? "published" : "draft",
                Language = n.CapabilityGraph.Capability.ContentLanguageCode ?? "es",
                OwnerTenantId = n.CapabilityGraph.Capability.OwnerTenantId,
                IsGlobal = n.CapabilityGraph.Capability.OwnerTenantId == null && n.CapabilityGraph.Capability.OwnerPersonId == null,
                Tags = new List<string> { n.NodeType.ToString() }
            })
            .ToListAsync(cancellationToken);

        results.AddRange(nodeEngrams);

        // 3. Traditional Capability Lessons (Engrams in Traditional courses)
        var lessonsQuery = dbContext.CapabilityLessons
            .AsNoTracking()
            .Where(l => l.CapabilityChapter != null && targetIds.Contains(l.CapabilityChapter.CapabilityId));

        if (tenantGuid.HasValue)
        {
            lessonsQuery = lessonsQuery.Where(l =>
                l.CapabilityChapter!.Capability.OwnerTenantId == null ||
                l.CapabilityChapter.Capability.OwnerTenantId == tenantGuid.Value);
        }
        else
        {
            lessonsQuery = lessonsQuery.Where(l =>
                l.CapabilityChapter!.Capability.OwnerTenantId == null &&
                l.CapabilityChapter.Capability.OwnerPersonId == null);
        }

        var lessonEngrams = await lessonsQuery
            .Select(l => new EngramStudioEngramSummary
            {
                EngramId = l.CapabilityLessonId.ToString(),
                ClassId = l.CapabilityChapter!.CapabilityId.ToString(),
                EngramName = l.Title,
                EngramDescription = l.CapabilityChapter != null ? $"{l.CapabilityChapter.Title} — {l.Title}" : l.Title,
                PublicationStatus = l.CapabilityChapter != null && l.CapabilityChapter.Capability != null && l.CapabilityChapter.Capability.IsActive ? "published" : "draft",
                Language = l.CapabilityChapter != null && l.CapabilityChapter.Capability != null ? (l.CapabilityChapter.Capability.ContentLanguageCode ?? "es") : "es",
                OwnerTenantId = l.CapabilityChapter != null && l.CapabilityChapter.Capability != null ? l.CapabilityChapter.Capability.OwnerTenantId : null,
                IsGlobal = l.CapabilityChapter != null && l.CapabilityChapter.Capability != null && l.CapabilityChapter.Capability.OwnerTenantId == null && l.CapabilityChapter.Capability.OwnerPersonId == null,
                Tags = new List<string> { l.CapabilityChapter != null ? l.CapabilityChapter.Title : string.Empty }.Where(t => !string.IsNullOrEmpty(t)).ToList()
            })
            .ToListAsync(cancellationToken);

        results.AddRange(lessonEngrams);

        // 4. Skills from dbo.Skill (Catalog Skills acting as classes)
        var remainingSkillIds = targetIds.Where(id => !results.Any(r => r.ClassId == id.ToString())).ToList();
        if (remainingSkillIds.Count > 0)
        {
            var matchingSkills = await dbContext.Skills
                .AsNoTracking()
                .Where(s => remainingSkillIds.Contains(s.SkillId))
                .ToListAsync(cancellationToken);

            foreach (var sk in matchingSkills)
            {
                results.Add(new EngramStudioEngramSummary
                {
                    EngramId = sk.SkillId.ToString(),
                    ClassId = sk.SkillId.ToString(),
                    EngramName = sk.Name,
                    EngramDescription = sk.Description ?? sk.Name,
                    PublicationStatus = "published",
                    Language = "es",
                    OwnerTenantId = sk.TenantId,
                    IsGlobal = false,
                    Tags = new List<string> { sk.Category }
                });
            }
        }

        return results;
    }

    public async Task<IReadOnlyList<EngramStudioLearningPathSummary>> GetAvailableLearningPathsAsync(
        string? organizationId = null,
        CancellationToken cancellationToken = default)
    {
        using var dbContext = _dbContextFactory.CreateDbContext();

        var programs = await dbContext.Programs
            .AsNoTracking()
            .Where(p => p.IsActive)
            .OrderBy(p => p.Name)
            .Select(p => new EngramStudioLearningPathSummary
            {
                LearningPathId = p.ProgramId.ToString(),
                LearningPathName = p.Name,
                Description = p.Description ?? p.Objectives
            })
            .ToListAsync(cancellationToken);

        return programs;
    }
}
