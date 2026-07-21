using System.Net;
using System.Text.Json;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>
/// Starts a new V2 "PDF → CapabilityGraph" run: uploads a real PDF and
/// kicks off the full pipeline (extract text, detect chapters, curate,
/// design the graph, generate illustrations, persist, design + validate
/// every node's Memory Paradox blueprint). Returns immediately
/// (Stage.Running) — poll <see cref="GetPdfCapabilityGraphStatusFunction"/>
/// for progress and the eventual Completed/Failed result. Same
/// JSON+base64-body convention as the existing
/// ExtractCapabilityMaterialPdfFunction (no multipart/form-data parsing
/// elsewhere in this codebase, so this matches the established
/// convention rather than introducing a new one).
/// </summary>
public sealed class StartPdfCapabilityGraphFunction
{
    private readonly PdfCapabilityGraphOrchestrator _orchestrator;

    public StartPdfCapabilityGraphFunction(PdfCapabilityGraphOrchestrator orchestrator)
    {
        _orchestrator = orchestrator;
    }

    public sealed class StartRequest
    {
        public Guid TenantId { get; set; }

        public Guid CapabilityDomainId { get; set; }

        public string CapabilityName { get; set; } = string.Empty;

        public string FileName { get; set; } = string.Empty;

        public string ContentBase64 { get; set; } = string.Empty;

        /// <summary>Opt-in: supplements each chapter's topic with CURRENT
        /// web findings via Grounding with Bing Search before curating
        /// (see WebGroundingService). Defaults to false — never enabled
        /// unless the caller explicitly asks for it.</summary>
        public bool EnableWebEnrichment { get; set; }
    }

    [Function("StartPdfCapabilityGraph")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "studio/capability-graph/create-from-pdf")]
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        if (!_orchestrator.IsConfigured)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.ServiceUnavailable,
                "StudioAgentsNotConfigured",
                "The Human OS Studio pipeline agents are not yet configured (missing Azure OpenAI settings).",
                cancellationToken);
        }

        var body = await JsonSerializer.DeserializeAsync<StartRequest>(
            request.Body,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
            cancellationToken);

        if (body is null ||
            body.TenantId == Guid.Empty ||
            body.CapabilityDomainId == Guid.Empty ||
            string.IsNullOrWhiteSpace(body.CapabilityName) ||
            string.IsNullOrWhiteSpace(body.FileName) ||
            string.IsNullOrWhiteSpace(body.ContentBase64))
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.BadRequest,
                "InvalidRequest",
                "'tenantId', 'capabilityDomainId', 'capabilityName', 'fileName' and 'contentBase64' are required.",
                cancellationToken);
        }

        byte[] pdfBytes;
        try
        {
            pdfBytes = Convert.FromBase64String(body.ContentBase64);
        }
        catch (FormatException)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidBase64", "'contentBase64' is not valid base64.", cancellationToken);
        }

        var status = _orchestrator.Start(
            pdfBytes, body.FileName, body.CapabilityDomainId, body.CapabilityName, body.TenantId, body.EnableWebEnrichment);

        return await FunctionResponseFactory.SuccessResponseAsync(request, status, cancellationToken: cancellationToken);
    }
}
