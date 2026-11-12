using System.Net;
using System.Text.Json;
using HumanOS.Data;
using HumanOS.Models.Capabilities.Studio;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.AzureFunctions.Api;

public sealed class UpdateStructuredLearningVisibilityFunction
{
    private readonly HumanOsDbContext _dbContext;

    public UpdateStructuredLearningVisibilityFunction(HumanOsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public sealed class UpdateRequest
    {
        public bool IsGlobal { get; set; }
        public bool IsPublished { get; set; }
        public bool? ShowKnowledgeGraphToLearners { get; set; }
    }

    [Function("UpdateStructuredLearningVisibility")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "patch",
            Route = "studio/structured-learning/{documentId:guid}/visibility")]
        HttpRequestData request,
        Guid documentId,
        CancellationToken cancellationToken)
    {
        var body = await JsonSerializer.DeserializeAsync<UpdateRequest>(
            request.Body,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
            cancellationToken) ?? new UpdateRequest();

        var document = await _dbContext.StructuredLearningDocuments
            .FirstOrDefaultAsync(d => d.StructuredLearningDocumentId == documentId, cancellationToken);

        if (document is null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.NotFound,
                "StructuredLearningDocumentNotFound",
                "The requested structured learning document was not found.",
                cancellationToken);
        }

        var oldIsGlobal = document.IsGlobal;
        var oldIsPublished = document.IsPublished;

        document.IsGlobal = body.IsGlobal;
        document.IsPublished = body.IsPublished;
        if (body.ShowKnowledgeGraphToLearners.HasValue)
        {
            document.ShowKnowledgeGraphToLearners = body.ShowKnowledgeGraphToLearners.Value;
        }

        // Audit trail (2026-11-10): the shared dev database has no other
        // change history, and IsGlobal has been observed flipping back
        // unexpectedly — record who/when changed it so a recurrence can be
        // diagnosed instead of silently re-investigated from scratch.
        if (oldIsGlobal != document.IsGlobal || oldIsPublished != document.IsPublished)
        {
            request.Headers.TryGetValues("X-Azure-OID", out var oidValues);
            request.Headers.TryGetValues("X-Azure-Email", out var emailValues);
            request.Headers.TryGetValues("X-Studio-Tenant-Id", out var tenantValues);
            Guid.TryParse(tenantValues?.FirstOrDefault(), out var changedByTenantId);

            _dbContext.StructuredLearningDocumentVisibilityAudits.Add(new StructuredLearningDocumentVisibilityAudit
            {
                StructuredLearningDocumentId = documentId,
                OldIsGlobal = oldIsGlobal,
                NewIsGlobal = document.IsGlobal,
                OldIsPublished = oldIsPublished,
                NewIsPublished = document.IsPublished,
                ChangedByOid = oidValues?.FirstOrDefault(),
                ChangedByEmail = emailValues?.FirstOrDefault(),
                ChangedByTenantId = changedByTenantId == Guid.Empty ? null : changedByTenantId,
                ChangedAtUtc = DateTime.UtcNow
            });
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return await FunctionResponseFactory.SuccessResponseAsync(
            request,
            new
            {
                document.StructuredLearningDocumentId,
                document.IsGlobal,
                document.IsPublished,
                document.ShowKnowledgeGraphToLearners
            },
            cancellationToken: cancellationToken);
    }
}
