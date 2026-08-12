using System.Net;
using HumanOS.Data;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.AzureFunctions.Api;

/// <summary>
/// Structured Learning's "Edit mode" delete-section feature (2026-08-11) —
/// mirrors DeleteCapabilityGraphNodeFunction (Graph's equivalent): permanently
/// deletes ONE section and everything under it (steps, illustrations, recall/
/// assessment progress — see StructuredLearningSectionDeletionService for the
/// exact cascade order). Exposed behind a type-to-confirm warning modal in the
/// UI (DeleteStructuredLearningSectionModal.tsx) — irreversible, never called
/// without explicit user confirmation.
/// </summary>
public sealed class DeleteStructuredLearningSectionFunction
{
    private readonly HumanOsDbContext _dbContext;
    private readonly StructuredLearningSectionDeletionService _deletionService;

    public DeleteStructuredLearningSectionFunction(HumanOsDbContext dbContext, StructuredLearningSectionDeletionService deletionService)
    {
        _dbContext = dbContext;
        _deletionService = deletionService;
    }

    [Function("DeleteStructuredLearningSection")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "delete",
            Route = "studio/structured-learning/{documentId:guid}/sections/{sectionId:guid}")]
        HttpRequestData request,
        Guid documentId,
        Guid sectionId,
        CancellationToken cancellationToken)
    {
        var sectionBelongsToDocument = await _dbContext.StructuredLearningDocumentSections
            .AsNoTracking()
            .AnyAsync(
                s => s.StructuredLearningDocumentSectionId == sectionId && s.StructuredLearningDocumentId == documentId,
                cancellationToken);

        if (!sectionBelongsToDocument)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.NotFound,
                "SectionNotFound",
                $"No section {sectionId} found in document {documentId}.",
                cancellationToken);
        }

        var deleted = await _deletionService.DeleteSectionAsync(documentId, sectionId, cancellationToken);

        if (!deleted)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.NotFound,
                "SectionNotFound",
                "The requested section was not found.",
                cancellationToken);
        }

        return request.CreateResponse(HttpStatusCode.NoContent);
    }
}
