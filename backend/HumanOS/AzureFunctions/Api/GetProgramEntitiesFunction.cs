using System.Net;
using HumanOS.Data;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.AzureFunctions.Api;

public sealed class ProgramEntityOccurrenceDto
{
    public Guid CourseId { get; set; }

    public string CourseName { get; set; } = string.Empty;

    public Guid EngramId { get; set; }

    public string EngramTitle { get; set; } = string.Empty;

    public string? ChapterTitle { get; set; }

    public int SortOrder { get; set; }

    public string SourceText { get; set; } = string.Empty;

    public int? StartPosition { get; set; }

    public int? EndPosition { get; set; }
}

public sealed class ProgramEntityItemDto
{
    public Guid EntityId { get; set; }

    public string CanonicalName { get; set; } = string.Empty;

    public string EntityType { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string? OntologicalClass { get; set; }

    public List<string> Aliases { get; set; } = [];

    public int TotalOccurrences { get; set; }

    public int DistinctCoursesCount { get; set; }

    public List<ProgramEntityOccurrenceDto> Occurrences { get; set; } = [];
}

public sealed class ProgramCourseSummaryDto
{
    public Guid CourseId { get; set; }

    public string CourseName { get; set; } = string.Empty;

    public int EngramCount { get; set; }

    public int EntityCount { get; set; }
}

public sealed class ProgramEntitiesResponseDto
{
    public Guid ProgramId { get; set; }

    public string ProgramName { get; set; } = string.Empty;

    public string? ProgramCode { get; set; }

    public string? Description { get; set; }

    public int TotalCourses { get; set; }

    public int TotalEngrams { get; set; }

    public int TotalEntities { get; set; }

    public int TotalOccurrences { get; set; }

    public List<ProgramCourseSummaryDto> Courses { get; set; } = [];

    public List<ProgramEntityItemDto> Entities { get; set; } = [];
}

/// <summary>
/// Aggregates all entities that appear across ALL courses and ALL engrams of a
/// given Program, along with their occurrences, source quotes, and course breakdown.
/// </summary>
public sealed class GetProgramEntitiesFunction
{
    private readonly HumanOsDbContext _dbContext;

    public GetProgramEntitiesFunction(HumanOsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [Function("GetProgramEntities")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "programs/{programId:guid}/entities")]
        HttpRequestData request,
        Guid programId,
        CancellationToken cancellationToken)
    {
        var program = await _dbContext.Programs
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.ProgramId == programId, cancellationToken);

        if (program is null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.NotFound,
                "ProgramNotFound",
                $"No program found with id {programId}.",
                cancellationToken);
        }

        // 1. Gather all StructuredLearningDocument IDs connected to this Program
        var structuredDocIds = await _dbContext.StructuredLearningDocumentProgramMemberships
            .AsNoTracking()
            .Where(m => m.ProgramId == programId)
            .Select(m => m.StructuredLearningDocumentId)
            .ToListAsync(cancellationToken);

        var capDocIds = await _dbContext.ProgramCapabilities
            .AsNoTracking()
            .Where(pc => pc.ProgramId == programId && _dbContext.StructuredLearningDocuments.Any(d => d.StructuredLearningDocumentId == pc.CapabilityId))
            .Select(pc => pc.CapabilityId)
            .ToListAsync(cancellationToken);

        var allDocIds = structuredDocIds.Concat(capDocIds).Distinct().ToList();

        if (allDocIds.Count == 0)
        {
            var emptyResponse = new ProgramEntitiesResponseDto
            {
                ProgramId = program.ProgramId,
                ProgramName = program.Name,
                ProgramCode = program.Code,
                Description = program.Description,
                TotalCourses = 0,
                TotalEngrams = 0,
                TotalEntities = 0,
                TotalOccurrences = 0,
                Courses = [],
                Entities = []
            };
            return await FunctionResponseFactory.SuccessResponseAsync(request, emptyResponse, cancellationToken: cancellationToken);
        }

        // 2. Fetch course summaries
        var courses = await _dbContext.StructuredLearningDocuments
            .AsNoTracking()
            .Where(d => allDocIds.Contains(d.StructuredLearningDocumentId))
            .Select(d => new
            {
                CourseId = d.StructuredLearningDocumentId,
                CourseName = d.DocumentName,
                EngramCount = d.Sections.Count
            })
            .ToListAsync(cancellationToken);

        // 3. Query all entity-section occurrences for all engrams of these courses
        var links = await _dbContext.StructuredLearningEntitySections
            .AsNoTracking()
            .Where(l => allDocIds.Contains(l.Section.StructuredLearningDocumentId))
            .Select(l => new
            {
                EntityId = l.StructuredLearningEntityId,
                EntityName = l.Entity.Name,
                EntityType = l.Entity.EntityType,
                EntityDescription = l.Entity.Description,
                EntityOntologicalClass = l.Entity.OntologicalClass,
                EntityAliases = l.Entity.Aliases,
                CourseId = l.Section.StructuredLearningDocumentId,
                CourseName = l.Section.Document.DocumentName,
                EngramId = l.StructuredLearningDocumentSectionId,
                EngramTitle = l.Section.SubchapterTitle,
                ChapterTitle = l.Section.ChapterTitle,
                SortOrder = l.Section.SortOrder,
                l.SourceText,
                l.StartPosition,
                l.EndPosition
            })
            .ToListAsync(cancellationToken);

        // 4. Group by entity and construct aggregated DTOs
        var entityItems = links
            .GroupBy(l => l.EntityId)
            .Select(g =>
            {
                var first = g.First();
                var occs = g.Select(l => new ProgramEntityOccurrenceDto
                {
                    CourseId = l.CourseId,
                    CourseName = l.CourseName,
                    EngramId = l.EngramId,
                    EngramTitle = l.EngramTitle,
                    ChapterTitle = l.ChapterTitle,
                    SortOrder = l.SortOrder,
                    SourceText = l.SourceText,
                    StartPosition = l.StartPosition,
                    EndPosition = l.EndPosition
                }).ToList();

                return new ProgramEntityItemDto
                {
                    EntityId = g.Key,
                    CanonicalName = first.EntityName,
                    EntityType = first.EntityType,
                    Description = first.EntityDescription,
                    OntologicalClass = first.EntityOntologicalClass,
                    Aliases = first.EntityAliases ?? [],
                    TotalOccurrences = occs.Count,
                    DistinctCoursesCount = occs.Select(o => o.CourseId).Distinct().Count(),
                    Occurrences = occs
                };
            })
            .OrderByDescending(e => e.TotalOccurrences)
            .ThenBy(e => e.CanonicalName)
            .ToList();

        var courseSummaries = courses.Select(c => new ProgramCourseSummaryDto
        {
            CourseId = c.CourseId,
            CourseName = c.CourseName,
            EngramCount = c.EngramCount,
            EntityCount = links.Where(l => l.CourseId == c.CourseId).Select(l => l.EntityId).Distinct().Count()
        }).ToList();

        var responseDto = new ProgramEntitiesResponseDto
        {
            ProgramId = program.ProgramId,
            ProgramName = program.Name,
            ProgramCode = program.Code,
            Description = program.Description,
            TotalCourses = courses.Count,
            TotalEngrams = courses.Sum(c => c.EngramCount),
            TotalEntities = entityItems.Count,
            TotalOccurrences = links.Count,
            Courses = courseSummaries,
            Entities = entityItems
        };

        return await FunctionResponseFactory.SuccessResponseAsync(request, responseDto, cancellationToken: cancellationToken);
    }
}
