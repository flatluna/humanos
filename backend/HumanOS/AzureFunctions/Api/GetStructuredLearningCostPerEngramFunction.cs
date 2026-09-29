using System.Web;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>Per-engram cost breakdown for one Structured Learning Document —
/// see StructuredLearningCostService.GetPerEngramAsync's doc comment. Pass
/// ?sectionId=... to get back just that one engram's row (used by the
/// per-section edit page, which doesn't want to load the whole document's
/// breakdown just to show its own cost).</summary>
public sealed class GetStructuredLearningCostPerEngramFunction
{
    private readonly StructuredLearningCostService _costService;

    public GetStructuredLearningCostPerEngramFunction(StructuredLearningCostService costService)
    {
        _costService = costService;
    }

    [Function("GetStructuredLearningCostPerEngram")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "studio/structured-learning-costs/{documentId:guid}/per-engram")]
        HttpRequestData request,
        Guid documentId,
        CancellationToken cancellationToken)
    {
        var query = HttpUtility.ParseQueryString(request.Url.Query);
        Guid? onlySectionId = Guid.TryParse(query["sectionId"], out var parsedSectionId) ? parsedSectionId : null;

        var rows = await _costService.GetPerEngramAsync(documentId, onlySectionId, cancellationToken);
        return await FunctionResponseFactory.SuccessResponseAsync(request, rows, cancellationToken: cancellationToken);
    }
}
