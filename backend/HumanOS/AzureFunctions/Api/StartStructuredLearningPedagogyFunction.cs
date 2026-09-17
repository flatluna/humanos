using System.Net;
using System.Text.Json;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>
/// Starts Step 3 of the Structured Learning Document pipeline: generates
/// the pedagogical sequence for every section of an already-extracted
/// document (Step 2) that isn't already Completed. New, additive endpoint
/// — does not affect Graph/Traditional or Step 2's own endpoints.
/// </summary>
public sealed class StartStructuredLearningPedagogyFunction
{
    private readonly StructuredLearningPedagogyOrchestrator _orchestrator;

    public StartStructuredLearningPedagogyFunction(StructuredLearningPedagogyOrchestrator orchestrator)
    {
        _orchestrator = orchestrator;
    }

    public sealed class StartRequest
    {
        public Guid StructuredLearningDocumentId { get; set; }

        public string? LanguageCode { get; set; }

        /// <summary>When true, reprocesses EVERY section (not just
        /// non-Completed ones) — use after an agent prompt/instructions
        /// change so already-succeeded sections pick up the new behavior.</summary>
        public bool ForceRegenerateAll { get; set; }

        /// <summary>Optional reference image used for AI-generated section
        /// illustrations across the whole document.</summary>
        public string? UploadedImageBase64 { get; set; }

        public string? UploadedImageContentType { get; set; }

        /// <summary>Only meaningful when UploadedImageBase64 is set and the
        /// image should be used as a visual reference during generation.</summary>
        public string? ReferenceImageUsageInstruction { get; set; }

        /// <summary>Optional override to enable/disable Perplexity AI search for each engram during pedagogy generation.
        /// When null, defaults to the setting saved in the document's ConfigSnapshotJson.</summary>
        public bool? UsePerplexitySearch { get; set; }
    }

    [Function("StartStructuredLearningPedagogy")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "studio/structured-learning/pedagogy/start")]
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        if (!_orchestrator.IsConfigured)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.ServiceUnavailable, "StudioAgentsNotConfigured",
                "The Structured Learning pedagogy agent is not yet configured (missing Azure OpenAI settings).",
                cancellationToken);
        }

        var body = await JsonSerializer.DeserializeAsync<StartRequest>(
            request.Body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }, cancellationToken);

        if (body is null || body.StructuredLearningDocumentId == Guid.Empty)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidRequest",
                "'structuredLearningDocumentId' is required.", cancellationToken);
        }

        var languageCode = string.IsNullOrWhiteSpace(body.LanguageCode) ? "es" : body.LanguageCode;

        byte[]? uploadedImageBytes = null;
        if (!string.IsNullOrWhiteSpace(body.UploadedImageBase64))
        {
            try
            {
                uploadedImageBytes = Convert.FromBase64String(body.UploadedImageBase64);
            }
            catch (FormatException)
            {
                return await FunctionResponseFactory.ErrorResponseAsync(
                    request, HttpStatusCode.BadRequest, "InvalidBase64", "'uploadedImageBase64' is not valid base64.", cancellationToken);
            }
        }

        try
        {
            var status = _orchestrator.Start(
                body.StructuredLearningDocumentId,
                languageCode,
                body.ForceRegenerateAll,
                uploadedImageBytes,
                body.UploadedImageContentType,
                body.ReferenceImageUsageInstruction,
                body.UsePerplexitySearch);
            return await FunctionResponseFactory.SuccessResponseAsync(request, status, cancellationToken: cancellationToken);
        }
        catch (StructuredLearningPedagogyOrchestrator.ActiveRunConflictException ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.Conflict, "ActiveRunConflict", ex.Message, cancellationToken);
        }
    }
}
