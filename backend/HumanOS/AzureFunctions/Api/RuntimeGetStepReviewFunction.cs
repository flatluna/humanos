using System.Net;
using HumanOS.Data;
using HumanOS.Models.Capabilities.Graph;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>
/// Read-only "review a step I already did" endpoint — used when the
/// student clicks a completed (green) step in the UI's stepper. Never
/// mutates anything (no reactivation, no evidence writes, no status
/// change) via <see cref="InstructorRuntimeOrchestrator.GetStepReviewAsync"/>.
/// </summary>
public sealed class RuntimeGetStepReviewFunction
{
    private readonly InstructorRuntimeOrchestrator _orchestrator;
    private readonly HumanOsDbContext _dbContext;

    public RuntimeGetStepReviewFunction(InstructorRuntimeOrchestrator orchestrator, HumanOsDbContext dbContext)
    {
        _orchestrator = orchestrator;
        _dbContext = dbContext;
    }

    [Function("RuntimeGetStepReview")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "instructor-runtime/steps/review")]
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var query = System.Web.HttpUtility.ParseQueryString(request.Url.Query);

        if (!Guid.TryParse(query["learningSessionNodeId"], out var learningSessionNodeId))
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "MissingFields",
                "Query parameter learningSessionNodeId is required.", cancellationToken);
        }

        if (!Enum.TryParse<ExperienceStepType>(query["stepType"], ignoreCase: true, out var stepType))
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidStepType",
                "Query parameter stepType must be one of: Hypothesis, Teaching, Recall, Production, Assessment.", cancellationToken);
        }

        try
        {
            var review = await _orchestrator.GetStepReviewAsync(_dbContext, learningSessionNodeId, stepType, cancellationToken);
            var response = RuntimeGraphApiMappers.ToStepReviewDto(review);

            return await FunctionResponseFactory.SuccessResponseAsync(request, response, cancellationToken: cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "StepNotReviewable", ex.Message, cancellationToken);
        }
    }
}
