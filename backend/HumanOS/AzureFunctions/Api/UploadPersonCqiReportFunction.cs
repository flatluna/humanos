using System.Net;
using HumanOS.Agents;
using HumanOS.Services;
using HumanOS.Storage;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>
/// POST /people/{personId}/cqi-profile — uploads a CQI assessment report
/// PDF (raw bytes as the request body, same "no multipart parsing"
/// convention as UploadPersonProfilePhotoFunction), extracts its text
/// (<see cref="PdfTextExtractor"/>), asks
/// <see cref="CqiReportExtractionAgent"/> to extract the structured CQI
/// Workspace profile, and saves it — replacing any previously saved
/// profile for this person.
/// </summary>
public sealed class UploadPersonCqiReportFunction
{
    private readonly CqiReportExtractionAgent _agent;
    private readonly PersonCqiProfileService _service;

    public UploadPersonCqiReportFunction(CqiReportExtractionAgent agent, PersonCqiProfileService service)
    {
        _agent = agent;
        _service = service;
    }

    [Function("UploadPersonCqiReport")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "people/{personId:guid}/cqi-profile")]
        HttpRequestData request,
        Guid personId,
        CancellationToken cancellationToken)
    {
        if (!_agent.IsConfigured)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.ServiceUnavailable, "ExtractionAgentNotConfigured",
                "The CQI report extraction agent is not yet configured (missing Azure OpenAI settings).",
                cancellationToken);
        }

        var contentType = request.Headers.TryGetValues("Content-Type", out var values) ? values.First() : "application/pdf";
        if (!contentType.Contains("pdf", StringComparison.OrdinalIgnoreCase))
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "OnlyPdfAllowed", "Only PDF files are supported.", cancellationToken);
        }

        var fileName = request.Headers.TryGetValues("X-File-Name", out var fileNameValues)
            ? Uri.UnescapeDataString(fileNameValues.First())
            : "cqi-report.pdf";

        string reportText;
        try
        {
            using var memoryStream = new MemoryStream();
            await request.Body.CopyToAsync(memoryStream, cancellationToken);
            memoryStream.Position = 0;
            reportText = PdfTextExtractor.ExtractText(memoryStream);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidPdf", "The uploaded file could not be read as a PDF.", cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(reportText))
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.UnprocessableEntity, "NoTextFound",
                "No readable text was found in this PDF.", cancellationToken);
        }

        try
        {
            var extracted = await _agent.ExtractAsync(reportText, cancellationToken);
            var saved = await _service.SaveAsync(personId, extracted, fileName, _agent.DeploymentName, cancellationToken);
            return await FunctionResponseFactory.SuccessResponseAsync(request, saved, cancellationToken: cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.UnprocessableEntity, "ExtractionFailed",
                "The CQI extraction agent could not process this report: " + ex.Message, cancellationToken);
        }
    }
}
