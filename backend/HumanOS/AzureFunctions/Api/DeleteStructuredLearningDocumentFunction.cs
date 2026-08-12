using System.Net;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>
/// Permanently deletes a structured-learning document and everything under it.
/// This is the actual course/document delete action, not a program membership unlink.
/// </summary>
public sealed class DeleteStructuredLearningDocumentFunction
{
    private readonly StructuredLearningDocumentDeletionService _deletionService;

    public DeleteStructuredLearningDocumentFunction(StructuredLearningDocumentDeletionService deletionService)
    {
        _deletionService = deletionService;
    }

    [Function("DeleteStructuredLearningDocument")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "studio/structured-learning/{documentId:guid}")]
        HttpRequestData request,
        Guid documentId,
        CancellationToken cancellationToken)
    {
        var deleted = await _deletionService.DeleteDocumentAsync(documentId, cancellationToken);

        if (!deleted)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.NotFound,
                "DocumentNotFound",
                $"No structured learning document found with id {documentId}.",
                cancellationToken);
        }

        return request.CreateResponse(HttpStatusCode.NoContent);
    }
}
