using HumanOS.Agents.Studio;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Web;

namespace HumanOS.AzureFunctions.Api;

/// <summary>
/// TEST FUNCTION — Calls WebGroundingService.SearchAsync directly so we can
/// inspect the RAW Bing Grounding output (text + citations) for a given
/// chapter topic/context, without running the full PDF pipeline.
/// Endpoint: GET /api/test/web-grounding?topic=...&context=...
/// This function can be deleted after diagnosis is complete.
/// </summary>
public sealed class TestWebGroundingFunction
{
    private readonly WebGroundingService _webGrounding;

    public TestWebGroundingFunction(WebGroundingService webGrounding)
    {
        _webGrounding = webGrounding;
    }

    [Function("TestWebGrounding")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "test/web-grounding")] HttpRequestData req,
        FunctionContext executionContext)
    {
        var logger = executionContext.GetLogger<TestWebGroundingFunction>();

        if (!_webGrounding.IsConfigured)
        {
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteAsJsonAsync(new { error = "WebGroundingService is not configured." });
            return errorResponse;
        }

        var query = HttpUtility.ParseQueryString(req.Url.Query);
        var topic = query["topic"] ?? "Text analysis techniques";
        var context = query["context"] ?? "Fundamentos de IA Generativa, Agentes y NLP";

        logger.LogInformation("TestWebGrounding: topic='{Topic}' context='{Context}'", topic, context);

        var result = await _webGrounding.SearchAsync(topic, context, executionContext.CancellationToken);

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(new
        {
            topic,
            context,
            textLength = result.Text.Length,
            text = result.Text,
            citationCount = result.Citations.Count,
            citations = result.Citations
        });
        return response;
    }
}
