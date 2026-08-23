using System.Net;
using System.Text.Json;
using HumanOS.Data;
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

        document.IsGlobal = body.IsGlobal;
        document.IsPublished = body.IsPublished;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return await FunctionResponseFactory.SuccessResponseAsync(
            request,
            new
            {
                document.StructuredLearningDocumentId,
                document.IsGlobal,
                document.IsPublished
            },
            cancellationToken: cancellationToken);
    }
}
