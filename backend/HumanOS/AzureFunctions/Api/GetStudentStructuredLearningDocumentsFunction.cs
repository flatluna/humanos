using HumanOS.Data;
using HumanOS.Services;
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
    private readonly PersonService _personService;

    public GetStudentStructuredLearningDocumentsFunction(HumanOsDbContext dbContext, PersonService personService)
    {
        _dbContext = dbContext;
        _personService = personService;
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

        if (callerTenantId is null &&
            request.Headers.TryGetValues("X-Azure-OID", out var oidValues) &&
            request.Headers.TryGetValues("X-Azure-TID", out var tidValues) &&
            !string.IsNullOrWhiteSpace(oidValues.FirstOrDefault()) &&
            !string.IsNullOrWhiteSpace(tidValues.FirstOrDefault()))
        {
            var callerPerson = await _personService.GetByAzureIdentityAsync(
                oidValues.First(), tidValues.First(), cancellationToken);
            callerTenantId = callerPerson?.TenantId;
        }

        var documents = await _dbContext.StructuredLearningDocuments
            .AsNoTracking()
            .Where(d => d.IsPublished && (d.IsGlobal || (callerTenantId != null && d.TenantId == callerTenantId)))
            .Select(d => new
            {
                StructuredLearningDocumentId = d.StructuredLearningDocumentId,
                DocumentName = d.DocumentName,
                ExecutiveSummary = d.ExecutiveSummary,
                ConfigSnapshotJson = d.ConfigSnapshotJson,
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

        var response = documents.Select(d => new
        {
            d.StructuredLearningDocumentId,
            d.DocumentName,
            d.ExecutiveSummary,
            d.SubjectId,
            d.TopicId,
            d.DomainName,
            d.SubdomainName,
            d.SectionCount,
            d.CreatedDate,
            d.CoverIllustrationId,
            Language = StructuredLearningLanguageHeuristic.Resolve(d.ConfigSnapshotJson, d.ExecutiveSummary ?? d.DocumentName),
        });

        return await FunctionResponseFactory.SuccessResponseAsync(request, response, cancellationToken: cancellationToken);
    }
}