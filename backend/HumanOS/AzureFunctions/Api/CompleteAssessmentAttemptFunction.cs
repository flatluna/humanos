using System.Net;
using System.Text.Json;
using HumanOS.Contracts.Assessments;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

public sealed class CompleteAssessmentAttemptFunction
{
    private readonly AssessmentService _assessmentService;

    public CompleteAssessmentAttemptFunction(AssessmentService assessmentService)
    {
        _assessmentService = assessmentService;
    }

    [Function("CompleteAssessmentAttempt")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "put",
            Route = "people/{personId:guid}/assessments/{attemptId:guid}/complete")]
        HttpRequestData request,
        Guid personId,
        Guid attemptId,
        CancellationToken cancellationToken)
    {
        // TODO: Derive PersonId from the validated Microsoft Entra token.
        CompleteAssessmentAttemptRequest? body;

        try
        {
            body = await JsonSerializer.DeserializeAsync<CompleteAssessmentAttemptRequest>(
                request.Body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
                cancellationToken);
        }
        catch (JsonException)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidJson", "The request body contains invalid JSON.", cancellationToken);
        }

        if (body is null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "RequestBodyRequired", "A request body is required.", cancellationToken);
        }

        try
        {
            var score = (decimal)(body.Score ?? 0);

            var result = await _assessmentService.CompleteAttemptAsync(
                attemptId,
                score,
                body.AssistanceLevel,
                cancellationToken);

            if (result is null)
            {
                return await FunctionResponseFactory.ErrorResponseAsync(
                    request, HttpStatusCode.NotFound, "AssessmentAttemptNotFound", "The requested assessment attempt was not found.", cancellationToken);
            }

            return await FunctionResponseFactory.SuccessResponseAsync(request, result, cancellationToken: cancellationToken);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidValue", ex.Message, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.Conflict, "AttemptAlreadyCompleted", ex.Message, cancellationToken);
        }
    }
}
