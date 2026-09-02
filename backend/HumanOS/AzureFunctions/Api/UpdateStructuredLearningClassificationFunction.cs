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
        public Guid? SubjectId { get; set; }
        public Guid? TopicId { get; set; }
        public string? Category { get; set; }
        public string? TargetSkill { get; set; }
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

        document.SubjectId = body.SubjectId;
        document.TopicId = body.TopicId;
        document.Category = body.Category;
        document.TargetSkill = body.TargetSkill;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return await FunctionResponseFactory.SuccessResponseAsync(
            request,
            new { document.StructuredLearningDocumentId, document.SubjectId, document.TopicId, document.Category, document.TargetSkill },
            cancellationToken: cancellationToken);
    }
}
