using System.Net;
using System.Text.Json;
using HumanOS.Data;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.AzureFunctions.Api;

public sealed class UpdateStructuredLearningClassificationFunction
{
    private readonly HumanOsDbContext _dbContext;

    public UpdateStructuredLearningClassificationFunction(HumanOsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public sealed class UpdateRequest
    {
        public Guid? TopicId { get; set; }
    }

    [Function("UpdateStructuredLearningClassification")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "studio/structured-learning/{documentId:guid}/classification")]
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
                request, HttpStatusCode.NotFound, "StructuredLearningDocumentNotFound",
                "The requested structured learning document was not found.", cancellationToken);
        }

        document.TopicId = body.TopicId;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return await FunctionResponseFactory.SuccessResponseAsync(
            request,
            new { document.StructuredLearningDocumentId, document.TopicId },
            cancellationToken: cancellationToken);
    }
}
