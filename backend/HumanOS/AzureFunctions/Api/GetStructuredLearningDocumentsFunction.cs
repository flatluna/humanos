using System.Net;
using HumanOS.Data;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.AzureFunctions.Api;

public sealed class StructuredLearningDocumentSummaryDto
{
    public Guid StructuredLearningDocumentId { get; set; }

    public string DocumentName { get; set; } = string.Empty;

    public string? ExecutiveSummary { get; set; }

    public int SectionCount { get; set; }

    public DateTime CreatedDate { get; set; }

    /// <summary>First illustration generated for this document (any
    /// section), if any — used as a lightweight catalog cover thumbnail.</summary>
    public Guid? CoverIllustrationId { get; set; }

    public string? Category { get; set; }

    public string? TargetSkill { get; set; }
}

/// <summary>
/// Lightweight read-only listing of every Structured Learning Document
/// (2026-08-10) — lets the catalog page ("Catálogo") show these as cards
/// the user can open, without requiring the full Capability-model
/// integration (no CapabilityId FK yet — see repo memory). Purely
/// additive: just reads the existing tables, no schema change.
/// </summary>
public sealed class GetStructuredLearningDocumentsFunction
{
    private readonly HumanOsDbContext _dbContext;

    public GetStructuredLearningDocumentsFunction(HumanOsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [Function("GetStructuredLearningDocuments")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "studio/structured-learning")]
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        // Studio's own authoring catalog — same global-vs-tenant visibility
        // rule as the student catalog (GetStudentStructuredLearningDocumentsFunction),
        // but without the IsPublished restriction (authors must see their
        // own drafts). Caller tenant comes from the signed launch JWT via
        // the X-Studio-Tenant-Id header (see capabilitystudio's
        // getIdentityHeaders()); an explicit ?tenantId= query param is
        // still honored for backward compatibility.
        Guid? callerTenantId = request.Headers.TryGetValues("X-Studio-Tenant-Id", out var studioTenantValues) &&
            Guid.TryParse(studioTenantValues.FirstOrDefault(), out var studioTenantId)
                ? studioTenantId
                : null;

        var query = System.Web.HttpUtility.ParseQueryString(request.Url.Query);
        var tenantId = callerTenantId ?? (Guid.TryParse(query["tenantId"], out var parsedTenantId) ? parsedTenantId : (Guid?)null);
        var documents = await _dbContext.StructuredLearningDocuments
            .AsNoTracking()
            .Where(d => d.IsGlobal || d.TenantId == Guid.Empty || (tenantId != null && d.TenantId == tenantId) || tenantId == null)
            .Select(d => new StructuredLearningDocumentSummaryDto
            {
                StructuredLearningDocumentId = d.StructuredLearningDocumentId,
                DocumentName = d.DocumentName,
                ExecutiveSummary = d.ExecutiveSummary,
                CreatedDate = d.CreatedDate,
                SectionCount = d.Sections.Count,
                CoverIllustrationId = _dbContext.StructuredLearningIllustrations
                    .Where(i => i.StructuredLearningDocumentSectionId == d.Sections
                        .OrderBy(s => s.SortOrder)
                        .Select(s => s.StructuredLearningDocumentSectionId)
                        .FirstOrDefault())
                    .OrderBy(i => i.CreatedDate)
                    .Select(i => (Guid?)i.StructuredLearningIllustrationId)
                    .FirstOrDefault(),
                Category = d.Category,
                TargetSkill = d.TargetSkill
            })
            .OrderByDescending(d => d.CreatedDate)
            .ToListAsync(cancellationToken);

        return await FunctionResponseFactory.SuccessResponseAsync(request, documents, cancellationToken: cancellationToken);
    }
}
