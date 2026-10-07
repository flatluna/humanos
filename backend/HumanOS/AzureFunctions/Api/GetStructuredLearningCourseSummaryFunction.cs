using System.Net;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>GET /studio/structured-learning/{documentId}/summary —
/// engrams/chapters/facts quick counts, see
/// <see cref="StructuredLearningCourseSummaryService"/>.</summary>
public sealed class GetStructuredLearningCourseSummaryFunction
{
    private readonly StructuredLearningCourseSummaryService _summaryService;

    public GetStructuredLearningCourseSummaryFunction(StructuredLearningCourseSummaryService summaryService)
    {
        _summaryService = summaryService;
    }

    [Function("GetStructuredLearningCourseSummary")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "studio/structured-learning/{documentId:guid}/summary")]
        HttpRequestData request,
        Guid documentId,
        CancellationToken cancellationToken)
    {
        var summary = await _summaryService.GetSummaryAsync(documentId, cancellationToken);
        if (summary is null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "not_found", "No se encontró este documento.", cancellationToken);
        }

        return await FunctionResponseFactory.SuccessResponseAsync(request, summary, cancellationToken: cancellationToken);
    }
}
