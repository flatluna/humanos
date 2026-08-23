using System.Net;
using System.Text.Json;
using HumanOS.Data;
using HumanOS.Services.DocumentClassification;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.AzureFunctions.Api;

public sealed class ConfirmStructuredLearningDocumentTypeRequest
{
    /// <summary>Must be one of HumanOS.Services.DocumentClassification.DocumentType's
    /// names (Contract/Law/Policy/Procedure/AcademicWithIndex/AcademicNarrative/
    /// ResearchPaper/Unknown) — the designer's final choice, whether it
    /// accepts the engine's recommendation or overrides it.</summary>
    public string DocumentType { get; set; } = string.Empty;
}

/// <summary>
/// Wizard confirmation step for the Document Type Detection Engine
/// (2026-08-17): the engine's recommendation (DetectedDocumentType) is
/// advisory only — this endpoint records what the designer actually
/// confirmed (accept as-is, or override), which is what downstream
/// chapter/engram extraction and pedagogy design must read.
/// </summary>
public sealed class ConfirmStructuredLearningDocumentTypeFunction
{
    private readonly HumanOsDbContext _dbContext;

    public ConfirmStructuredLearningDocumentTypeFunction(HumanOsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [Function("ConfirmStructuredLearningDocumentType")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "studio/structured-learning/{documentId:guid}/document-type/confirm")]
        HttpRequestData request,
        Guid documentId,
        CancellationToken cancellationToken)
    {
        ConfirmStructuredLearningDocumentTypeRequest? body;
        try
        {
            body = await JsonSerializer.DeserializeAsync<ConfirmStructuredLearningDocumentTypeRequest>(
                request.Body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }, cancellationToken);
        }
        catch (JsonException)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidJson", "The request body contains invalid JSON.", cancellationToken);
        }

        if (body is null || !Enum.TryParse<DocumentType>(body.DocumentType, out var parsedType))
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidDocumentType",
                "DocumentType must be one of: " + string.Join(", ", Enum.GetNames<DocumentType>()), cancellationToken);
        }

        var document = await _dbContext.StructuredLearningDocuments
            .FirstOrDefaultAsync(d => d.StructuredLearningDocumentId == documentId, cancellationToken);
        if (document is null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "DocumentNotFound", $"No document found with id '{documentId}'.", cancellationToken);
        }

        document.ConfirmedDocumentType = parsedType.ToString();
        document.DocumentTypeConfirmedByUser = true;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return await FunctionResponseFactory.SuccessResponseAsync(request, new
        {
            document.StructuredLearningDocumentId,
            document.ConfirmedDocumentType,
            document.DocumentTypeConfirmedByUser
        }, cancellationToken: cancellationToken);
    }
}
