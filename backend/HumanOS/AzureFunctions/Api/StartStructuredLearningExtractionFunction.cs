using System.Net;
using System.Text.Json;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>
/// Starts Step 2 of the new "Structured Learning Document" Studio pipeline:
/// takes the document + APPROVED outline from the SAME wizard flow already
/// used by the Graph/Traditional pipelines (Material type -&gt; Upload
/// content -&gt; Review -&gt; Learning path design) and runs deterministic,
/// no-LLM extraction into <see cref="StructuredLearningDocumentSection"/>
/// rows (text, char/token counts, embedding vector, best-effort page
/// range). New, additive endpoint — does not affect the existing Graph or
/// Traditional Course start endpoints.
/// </summary>
public sealed class StartStructuredLearningExtractionFunction
{
    private readonly StructuredLearningExtractionOrchestrator _orchestrator;

    public StartStructuredLearningExtractionFunction(StructuredLearningExtractionOrchestrator orchestrator)
    {
        _orchestrator = orchestrator;
    }

    public sealed class StartRequest
    {
        public Guid TenantId { get; set; }

        public string DocumentName { get; set; } = string.Empty;

        /// <summary>Required for the PDF entry point. Mutually exclusive with <see cref="Description"/>.</summary>
        public string? FileName { get; set; }

        public string? ContentBase64 { get; set; }

        /// <summary>Required for the "Texto/idea" entry point. Mutually exclusive with <see cref="FileName"/>/<see cref="ContentBase64"/>.</summary>
        public string? Description { get; set; }

        public string? LanguageCode { get; set; }

        public bool StrictSyllabusMode { get; set; }

        /// <summary>The Paso 5 "Learning path design" outline the client already approved — required for Step 2.</summary>
        public List<CapabilityOutlineChapterDto>? ApprovedOutline { get; set; }

        /// <summary>Only used with the "Texto/idea" entry point — see
        /// StartCapabilityGraphFromDescriptionFunction's identical field.</summary>
        public string? PreExpandedDocumentText { get; set; }
    }

    [Function("StartStructuredLearningExtraction")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "studio/structured-learning/extract")]
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        if (!_orchestrator.IsConfigured)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.ServiceUnavailable, "StudioAgentsNotConfigured",
                "The Structured Learning Document extraction step is not yet configured (missing Azure OpenAI embedding settings).",
                cancellationToken);
        }

        var body = await JsonSerializer.DeserializeAsync<StartRequest>(
            request.Body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }, cancellationToken);

        var hasPdf = body is not null && !string.IsNullOrWhiteSpace(body.FileName) && !string.IsNullOrWhiteSpace(body.ContentBase64);
        var hasDescription = body is not null && !string.IsNullOrWhiteSpace(body.Description);

        if (body is null || body.TenantId == Guid.Empty || string.IsNullOrWhiteSpace(body.DocumentName) ||
            (!hasPdf && !hasDescription))
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidRequest",
                "'tenantId', 'documentName' and either ('fileName' + 'contentBase64') or 'description' are required.",
                cancellationToken);
        }

        if (body.ApprovedOutline is not { Count: > 0 })
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "MissingApprovedOutline",
                "'approvedOutline' (from Paso 5 'Learning path design') is required for Step 2.", cancellationToken);
        }

        var languageCode = string.IsNullOrWhiteSpace(body.LanguageCode) ? "es" : body.LanguageCode;

        try
        {
            StructuredLearningExtractionRunStatus status;
            if (hasPdf)
            {
                byte[] pdfBytes;
                try
                {
                    pdfBytes = Convert.FromBase64String(body.ContentBase64!);
                }
                catch (FormatException)
                {
                    return await FunctionResponseFactory.ErrorResponseAsync(
                        request, HttpStatusCode.BadRequest, "InvalidBase64", "'contentBase64' is not valid base64.", cancellationToken);
                }

                status = _orchestrator.Start(pdfBytes, body.FileName!, body.TenantId, body.ApprovedOutline);
            }
            else
            {
                status = _orchestrator.StartFromDescription(
                    body.Description!, body.DocumentName, body.TenantId, body.ApprovedOutline,
                    body.PreExpandedDocumentText, languageCode, body.StrictSyllabusMode);
            }

            return await FunctionResponseFactory.SuccessResponseAsync(request, status, cancellationToken: cancellationToken);
        }
        catch (StructuredLearningExtractionOrchestrator.ActiveRunConflictException ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.Conflict, "ActiveRunConflict", ex.Message, cancellationToken);
        }
    }
}
