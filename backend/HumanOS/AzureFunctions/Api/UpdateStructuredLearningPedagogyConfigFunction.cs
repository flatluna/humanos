using System.Net;
using System.Text.Json;
using HumanOS.Data;
using HumanOS.Models.Capabilities.Studio;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.AzureFunctions.Api;

/// <summary>
/// Saves the Step 3 pedagogical configuration (tone, Deep Dive, Recall
/// on/off + count, Production on/off, Assessment type + count) onto an
/// already-extracted <see cref="StructuredLearningDocument"/>, BEFORE
/// starting pedagogy generation — StructuredLearningPedagogyService reads
/// these fields directly off the document row.
/// </summary>
public sealed class UpdateStructuredLearningPedagogyConfigFunction
{
    private readonly HumanOsDbContext _dbContext;

    public UpdateStructuredLearningPedagogyConfigFunction(HumanOsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public sealed class UpdateRequest
    {
        public string? PedagogicalAudience { get; set; }

        public int? MaxDeepDiveCardsPerNode { get; set; }

        public bool IncludeProductionStep { get; set; } = true;

        public bool IncludeRecallStep { get; set; } = true;

        public int? RecallItemsRequired { get; set; }

        public bool IncludeAssessmentStep { get; set; } = true;

        public StructuredLearningAssessmentQuestionType AssessmentQuestionType { get; set; } = StructuredLearningAssessmentQuestionType.MultipleChoice;

        public int? AssessmentQuestionsPerRound { get; set; }
    }

    [Function("UpdateStructuredLearningPedagogyConfig")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "studio/structured-learning/{documentId:guid}/config")]
        HttpRequestData request,
        Guid documentId,
        CancellationToken cancellationToken)
    {
        var body = await JsonSerializer.DeserializeAsync<UpdateRequest>(
            request.Body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }, cancellationToken);

        if (body is null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidRequest", "A valid request body is required.", cancellationToken);
        }

        if (body.MaxDeepDiveCardsPerNode is < 1 or > 10)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidRequest", "'maxDeepDiveCardsPerNode' must be between 1 and 10.", cancellationToken);
        }

        if (body.RecallItemsRequired is < 1 or > 10)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidRequest", "'recallItemsRequired' must be between 1 and 10.", cancellationToken);
        }

        if (body.AssessmentQuestionsPerRound is < 1 or > 10)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidRequest", "'assessmentQuestionsPerRound' must be between 1 and 10.", cancellationToken);
        }

        var document = await _dbContext.StructuredLearningDocuments
            .FirstOrDefaultAsync(d => d.StructuredLearningDocumentId == documentId, cancellationToken);

        if (document is null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "DocumentNotFound", $"No document found with id '{documentId}'.", cancellationToken);
        }

        document.PedagogicalAudience = string.IsNullOrWhiteSpace(body.PedagogicalAudience) ? null : body.PedagogicalAudience;
        document.MaxDeepDiveCardsPerNode = body.MaxDeepDiveCardsPerNode;
        document.IncludeProductionStep = body.IncludeProductionStep;
        document.IncludeRecallStep = body.IncludeRecallStep;
        document.RecallItemsRequired = body.RecallItemsRequired;
        document.IncludeAssessmentStep = body.IncludeAssessmentStep;
        document.AssessmentQuestionType = body.AssessmentQuestionType;
        document.AssessmentQuestionsPerRound = body.AssessmentQuestionsPerRound;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return await FunctionResponseFactory.SuccessResponseAsync(request, new { ok = true }, cancellationToken: cancellationToken);
    }
}
