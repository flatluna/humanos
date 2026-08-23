using HumanOS.Data;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.AzureFunctions.Api;

/// <summary>Student-facing Structured Learning catalog. Shows documents
/// explicitly marked global, plus (2026-08-19) the caller's own tenant-owned
/// documents when a tenant identity is available — same global-vs-tenant
/// visibility rule as the Capability catalog.
/// </summary>
public sealed class GetStudentStructuredLearningDocumentsFunction
{
    private readonly HumanOsDbContext _dbContext;

    public GetStudentStructuredLearningDocumentsFunction(HumanOsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [Function("GetStudentStructuredLearningDocuments")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "catalog/structured-learning")]
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        Guid? callerTenantId = request.Headers.TryGetValues("X-Studio-Tenant-Id", out var studioTenantValues) &&
            Guid.TryParse(studioTenantValues.FirstOrDefault(), out var studioTenantId)
                ? studioTenantId
                : null;

        var documents = await _dbContext.StructuredLearningDocuments
            .AsNoTracking()
            .Where(d => d.IsPublished && (d.IsGlobal || (callerTenantId != null && d.TenantId == callerTenantId)))
            .Select(d => new
            {
                StructuredLearningDocumentId = d.StructuredLearningDocumentId,
                DocumentName = d.DocumentName,
                ExecutiveSummary = d.ExecutiveSummary,
                SubjectId = d.SubjectId,
                TopicId = d.TopicId,
                DomainName = _dbContext.Subjects
                    .Where(subject => subject.SubjectId == d.SubjectId)
                    .Select(subject => subject.Name)
                    .FirstOrDefault(),
                SubdomainName = _dbContext.StructuredLearningTopics
                    .Where(topic => topic.StructuredLearningTopicId == d.TopicId)
                    .Select(topic => topic.Name)
                    .FirstOrDefault(),
                SectionCount = d.Sections.Count,
                CreatedDate = d.CreatedDate,
                CoverIllustrationId = _dbContext.StructuredLearningIllustrations
                    .Where(i => i.StructuredLearningDocumentSectionId == d.Sections
                        .OrderBy(s => s.SortOrder)
                        .Select(s => s.StructuredLearningDocumentSectionId)
                        .FirstOrDefault())
                    .OrderBy(i => i.CreatedDate)
                    .Select(i => (Guid?)i.StructuredLearningIllustrationId)
                    .FirstOrDefault()
            })
            .OrderByDescending(d => d.CreatedDate)
            .ToListAsync(cancellationToken);

        return await FunctionResponseFactory.SuccessResponseAsync(request, documents, cancellationToken: cancellationToken);
    }
}