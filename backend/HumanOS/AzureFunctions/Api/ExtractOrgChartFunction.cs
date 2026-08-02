using System.Net;
using HumanOS.Agents;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>POST /tenants/{tenantId}/org-chart/extract — reads an
/// uploaded org chart image (raw bytes in the body, Content-Type header
/// identifies the format — same "no multipart parsing" convention as
/// UploadProgramLogoFunction) and returns the AI-proposed department/
/// people hierarchy for the HRAdmin to review and edit before creating
/// anything for real (see ApplyOrgChartFunction).</summary>
public sealed class ExtractOrgChartFunction
{
    private const long MaxImageBytes = 10 * 1024 * 1024; // 10 MB

    private readonly OrgChartExtractionAgent _orgChartAgent;

    public ExtractOrgChartFunction(OrgChartExtractionAgent orgChartAgent)
    {
        _orgChartAgent = orgChartAgent;
    }

    [Function("ExtractOrgChart")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "tenants/{tenantId:guid}/org-chart/extract")]
        HttpRequestData request,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        _ = tenantId; // not needed for extraction itself — kept for route consistency with the other org-structure endpoints.

        if (!_orgChartAgent.IsConfigured)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.ServiceUnavailable,
                "AgentNotConfigured",
                "El agente de extracción de organigramas no está configurado en el backend (faltan credenciales de Azure OpenAI).",
                cancellationToken);
        }

        var contentType = request.Headers.TryGetValues("Content-Type", out var values)
            ? values.FirstOrDefault()
            : null;

        if (string.IsNullOrWhiteSpace(contentType) || !contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidContentType",
                "El archivo debe ser una imagen (image/png, image/jpeg, etc.).", cancellationToken);
        }

        using var memoryStream = new MemoryStream();
        await request.Body.CopyToAsync(memoryStream, cancellationToken);
        var imageBytes = memoryStream.ToArray();

        if (imageBytes.Length == 0)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "EmptyFile", "El archivo está vacío.", cancellationToken);
        }

        if (imageBytes.Length > MaxImageBytes)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "FileTooLarge", "La imagen no debe exceder 10 MB.", cancellationToken);
        }

        try
        {
            var result = await _orgChartAgent.ExtractAsync(imageBytes, contentType, cancellationToken);
            return await FunctionResponseFactory.SuccessResponseAsync(request, result, cancellationToken: cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.ServiceUnavailable, "AgentNotConfigured", ex.Message, cancellationToken);
        }
    }
}
