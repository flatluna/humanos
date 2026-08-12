using System.Net;
using HumanOS.Data;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>Structured Learning's "Edit mode" — removes ONE Deep Dive card
/// (by index), no LLM call. Mirrors DeleteDeepDiveCardFunction (Graph's
/// equivalent).</summary>
public sealed class DeleteStructuredLearningDeepDiveCardFunction
{
    private readonly StructuredLearningReviewService _service;
    private readonly HumanOsDbContext _dbContext;

    public DeleteStructuredLearningDeepDiveCardFunction(StructuredLearningReviewService service, HumanOsDbContext dbContext)
    {
        _service = service;
        _dbContext = dbContext;
    }

    [Function("DeleteStructuredLearningDeepDiveCard")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "studio/structured-learning/sections/{sectionId:guid}/deepdive/cards/{cardIndex:int}")]
        HttpRequestData request,
        Guid sectionId,
        int cardIndex,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _service.DeleteExampleCardAsync(_dbContext, sectionId, cardIndex, cancellationToken);
            return await FunctionResponseFactory.SuccessResponseAsync(request, new StructuredLearningStepDetailDto
            {
                StructuredLearningSectionStepId = result.StructuredLearningSectionStepId,
                StepType = result.StepType.ToString(),
                Content = result.Content,
                StepDataJson = result.StepDataJson
            }, cancellationToken: cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidCardDelete", ex.Message, cancellationToken);
        }
    }
}
