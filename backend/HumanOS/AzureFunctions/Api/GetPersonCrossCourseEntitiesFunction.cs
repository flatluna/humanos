using System.Net;
using HumanOS.Data;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.AzureFunctions.Api;

public sealed class CrossCourseEntityOccurrenceDto
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

public sealed class CrossCourseEntityItemDto
{
    public Guid EntityId { get; set; }

    public string CanonicalName { get; set; } = string.Empty;

    public string EntityType { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string? OntologicalClass { get; set; }

    public List<string> Aliases { get; set; } = [];

    public int TotalOccurrences { get; set; }

    public int DistinctCoursesCount { get; set; }

    public List<CrossCourseEntityOccurrenceDto> Occurrences { get; set; } = [];
}

public sealed class CrossCourseCourseSummaryDto
{
    public Guid CourseId { get; set; }

    public string CourseName { get; set; } = string.Empty;

    public int EngramCount { get; set; }

    public int EntityCount { get; set; }
}

public sealed class CrossCourseEntitiesResponseDto
{
    public int TotalCourses { get; set; }

    public int TotalEngrams { get; set; }

    public int TotalEntities { get; set; }

    public int TotalOccurrences { get; set; }

    public List<CrossCourseCourseSummaryDto> Courses { get; set; } = [];

    public List<CrossCourseEntityItemDto> Entities { get; set; } = [];
}

/// <summary>
/// "Cruzar Conocimiento" — aggregates entities across a set of courses the
/// person is enrolled in, picked freely by the user (unlike
/// GetProgramEntitiesFunction, which is scoped to a curated Program).
/// GET /people/{personId}/cross-course-entities?courseIds=id1,id2,...
/// </summary>
public sealed class GetPersonCrossCourseEntitiesFunction
{
    private readonly HumanOsDbContext _dbContext;
    private readonly StructuredLearningDocumentEnrollmentService _enrollmentService;

    public GetPersonCrossCourseEntitiesFunction(
        HumanOsDbContext dbContext,
        StructuredLearningDocumentEnrollmentService enrollmentService)
    {
        _dbContext = dbContext;
        _enrollmentService = enrollmentService;
    }

    [Function("GetPersonCrossCourseEntities")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "people/{personId:guid}/cross-course-entities")]
        HttpRequestData request,
        Guid personId,
        CancellationToken cancellationToken)
    {
        var enrolled = await _enrollmentService.GetEnrolledDocumentsAsync(personId, cancellationToken);
        var enrolledIds = enrolled.Select(d => d.StructuredLearningDocumentId).ToHashSet();

        var query = System.Web.HttpUtility.ParseQueryString(request.Url.Query);
        var requestedIdsRaw = query["courseIds"];

        List<Guid> selectedIds;
        if (string.IsNullOrWhiteSpace(requestedIdsRaw))
        {
            selectedIds = [];
        }
        else
        {
            selectedIds = requestedIdsRaw
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(s => Guid.TryParse(s, out var g) ? g : (Guid?)null)
                .Where(g => g.HasValue)
                .Select(g => g!.Value)
                // Only allow courses the person is actually enrolled in.
                .Where(id => enrolledIds.Contains(id))
                .Distinct()
                .ToList();
        }

        if (selectedIds.Count == 0)
        {
            var emptyResponse = new CrossCourseEntitiesResponseDto
            {
                TotalCourses = 0,
                TotalEngrams = 0,
                TotalEntities = 0,
                TotalOccurrences = 0,
                Courses = [],
                Entities = []
            };
            return await FunctionResponseFactory.SuccessResponseAsync(request, emptyResponse, cancellationToken: cancellationToken);
        }

        var courses = await _dbContext.StructuredLearningDocuments
            .AsNoTracking()
            .Where(d => selectedIds.Contains(d.StructuredLearningDocumentId))
            .Select(d => new
            {
                CourseId = d.StructuredLearningDocumentId,
                CourseName = d.DocumentName,
                EngramCount = d.Sections.Count
            })
            .ToListAsync(cancellationToken);

        var links = await _dbContext.StructuredLearningEntitySections
            .AsNoTracking()
            .Where(l => selectedIds.Contains(l.Section.StructuredLearningDocumentId))
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

        var entityItems = links
            .GroupBy(l => l.EntityId)
            .Select(g =>
            {
                var first = g.First();
                var occs = g.Select(l => new CrossCourseEntityOccurrenceDto
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

                return new CrossCourseEntityItemDto
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
            // Cross-course concepts are the whole point here, so entities that
            // bridge 2+ selected courses are surfaced before single-course ones.
            .OrderByDescending(e => e.DistinctCoursesCount)
            .ThenByDescending(e => e.TotalOccurrences)
            .ThenBy(e => e.CanonicalName)
            .ToList();

        var courseSummaries = courses.Select(c => new CrossCourseCourseSummaryDto
        {
            CourseId = c.CourseId,
            CourseName = c.CourseName,
            EngramCount = c.EngramCount,
            EntityCount = links.Where(l => l.CourseId == c.CourseId).Select(l => l.EntityId).Distinct().Count()
        }).ToList();

        var responseDto = new CrossCourseEntitiesResponseDto
        {
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
