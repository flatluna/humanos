using System.Net;
using HumanOS.Data;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.AzureFunctions.Api;

public sealed class StructuredLearningDocumentSummaryDto
{
    public Guid StructuredLearningDocumentId { get; set; }

    public Guid TenantId { get; set; }

    public string DocumentName { get; set; } = string.Empty;

    public string? ExecutiveSummary { get; set; }

    public int SectionCount { get; set; }

    public DateTime CreatedDate { get; set; }

    /// <summary>First illustration generated for this document (any
    /// section), if any — used as a lightweight catalog cover thumbnail.</summary>
    public Guid? CoverIllustrationId { get; set; }

    public string? Category { get; set; }

    public string? TargetSkill { get; set; }

    /// <summary>"YoungLearner" (EngramsIQ Family market — parents/kids/young
    /// people educating at home) or "Standard"/null (Enterprise). Lets the
    /// catalog card show which market this course belongs to (2026-10-08).</summary>
    public string? PedagogicalAudience { get; set; }

    /// <summary>"es"/"en" — see StructuredLearningLanguageHeuristic.</summary>
    public string Language { get; set; } = "es";

    /// <summary>True when this document is available in the public student
    /// catalog. Lets the Studio catalog's "Mis cursos / Globales" filter
    /// tell a tenant-owned draft from a public one (2026-10-06).</summary>
    public bool IsGlobal { get; set; }
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

        // Same global-vs-tenant visibility rule as the Capability catalog
        // and the student-facing structured-learning catalog: a non-global
        // document is only visible to the tenant that owns it. No caller
        // tenant resolved -> only the public/official (IsGlobal) documents,
        // never every tenant's private drafts.
        var documents = await _dbContext.StructuredLearningDocuments
            .AsNoTracking()
            .Where(d => d.IsGlobal || (tenantId != null && d.TenantId == tenantId))
            .Select(d => new
            {
                d.StructuredLearningDocumentId,
                d.TenantId,
                d.DocumentName,
                d.ExecutiveSummary,
                d.CreatedDate,
                SectionCount = d.Sections.Count,
                CoverIllustrationId = _dbContext.StructuredLearningIllustrations
                    .Where(i => i.StructuredLearningDocumentSectionId == d.Sections
                        .OrderBy(s => s.SortOrder)
                        .Select(s => s.StructuredLearningDocumentSectionId)
                        .FirstOrDefault())
                    .OrderBy(i => i.CreatedDate)
                    .Select(i => (Guid?)i.StructuredLearningIllustrationId)
                    .FirstOrDefault(),
                d.Category,
                d.TargetSkill,
                d.PedagogicalAudience,
                d.ConfigSnapshotJson,
                d.IsGlobal
            })
            .OrderByDescending(d => d.CreatedDate)
            .ToListAsync(cancellationToken);

        var result = documents.Select(d => new StructuredLearningDocumentSummaryDto
        {
            StructuredLearningDocumentId = d.StructuredLearningDocumentId,
            TenantId = d.TenantId,
            DocumentName = d.DocumentName,
            ExecutiveSummary = d.ExecutiveSummary,
            CreatedDate = d.CreatedDate,
            SectionCount = d.SectionCount,
            CoverIllustrationId = d.CoverIllustrationId,
            Category = d.Category,
            TargetSkill = d.TargetSkill,
            PedagogicalAudience = d.PedagogicalAudience,
            Language = HumanOS.Services.StructuredLearningLanguageHeuristic.Resolve(d.ConfigSnapshotJson, d.ExecutiveSummary ?? d.DocumentName),
            IsGlobal = d.IsGlobal
        }).ToList();

        return await FunctionResponseFactory.SuccessResponseAsync(request, result, cancellationToken: cancellationToken);
    }
}
