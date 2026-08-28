using System.Net;
using System.Text.Json;
using HumanOS.Data;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.AzureFunctions.Api;

/// <summary>PATCH /studio/structured-learning/{documentId}/name — renames the course title (DocumentName).</summary>
public sealed class UpdateStructuredLearningDocumentNameFunction
{
    private readonly HumanOsDbContext _dbContext;

    public UpdateStructuredLearningDocumentNameFunction(HumanOsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public sealed class UpdateRequest
    {
        public string DocumentName { get; set; } = string.Empty;
    }

    [Function("UpdateStructuredLearningDocumentName")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "studio/structured-learning/{documentId:guid}/name")]
        HttpRequestData request,
        Guid documentId,
        CancellationToken cancellationToken)
    {
        var body = await JsonSerializer.DeserializeAsync<UpdateRequest>(
            request.Body,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
            cancellationToken) ?? new UpdateRequest();

        var trimmedName = body.DocumentName.Trim();
        if (trimmedName.Length == 0)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidDocumentName",
                "The course title cannot be empty.", cancellationToken);
        }

        var document = await _dbContext.StructuredLearningDocuments
            .FirstOrDefaultAsync(d => d.StructuredLearningDocumentId == documentId, cancellationToken);

        if (document is null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "StructuredLearningDocumentNotFound",
                "The requested structured learning document was not found.", cancellationToken);
        }

        document.DocumentName = trimmedName;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return await FunctionResponseFactory.SuccessResponseAsync(
            request,
            new { document.StructuredLearningDocumentId, document.DocumentName },
            cancellationToken: cancellationToken);
    }
}
