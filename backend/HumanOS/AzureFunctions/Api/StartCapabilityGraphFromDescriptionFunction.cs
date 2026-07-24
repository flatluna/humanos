using System.Net;
using System.Text.Json;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>
/// Starts a new V2 "PDF → CapabilityGraph" run from a plain-text idea/goal
/// description instead of an uploaded PDF (2026-07-21 — the "Texto/idea"
/// option in Capability Studio's wizard). Expands the description into a
/// textbook-style source document via IdeaToDocumentAgent, then runs the
/// exact same Curador → GraphArchitect pipeline a real PDF would — see
/// <see cref="PdfCapabilityGraphPipelineService.RunFromDescriptionAsync"/>.
/// Returns immediately (Stage.Running) — poll
/// <see cref="GetPdfCapabilityGraphStatusFunction"/> for progress and the
/// eventual Completed/Failed result, same as the PDF entry point (both use
/// the same in-memory run store, keyed by RunId).
/// </summary>
public sealed class StartCapabilityGraphFromDescriptionFunction
{
    private readonly PdfCapabilityGraphOrchestrator _orchestrator;

    public StartCapabilityGraphFromDescriptionFunction(PdfCapabilityGraphOrchestrator orchestrator)
    {
        _orchestrator = orchestrator;
    }

    public sealed class StartRequest
    {
        public Guid TenantId { get; set; }

        public Guid CapabilityDomainId { get; set; }

        public string CapabilityName { get; set; } = string.Empty;

        /// <summary>Short description of what the capability should teach
        /// (e.g. "Capacidad para que un niño aprenda a sumar y restar") —
        /// expanded into a full source document by IdeaToDocumentAgent.</summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>Optional: the student-facing topical Subject (Finanzas,
        /// Cocina, Matemáticas...) this capability belongs to. See
        /// GET /api/subjects. Distinct from CapabilityDomainId.</summary>
        public Guid? SubjectId { get; set; }

        /// <summary>Opt-in: supplements each node with CURRENT web findings
        /// via Grounding with Bing Search (see WebGroundingService).
        /// Defaults to false.</summary>
        public bool EnableWebEnrichment { get; set; }

        /// <summary>Optional: an existing Program to attach this new
        /// Capability to (appended to the end of its sequence) as soon as
        /// the Capability row is created — see StartPdfCapabilityGraphFunction's
        /// identical field for the full rationale.</summary>
        public Guid? ProgramId { get; set; }

        /// <summary>See StartPdfCapabilityGraphFunction's identical field.</summary>
        public int? ProgramSequenceNumber { get; set; }

        /// <summary>See StartPdfCapabilityGraphFunction's identical field.</summary>
        public string? CapabilityObjectives { get; set; }

        /// <summary>See StartPdfCapabilityGraphFunction's identical field.</summary>
        public string? CapabilityRequirements { get; set; }
    }

    [Function("StartCapabilityGraphFromDescription")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "studio/capability-graph/create-from-description")]
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
            string.IsNullOrWhiteSpace(body.Description))
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.BadRequest,
                "InvalidRequest",
                "'tenantId', 'capabilityDomainId', 'capabilityName' and 'description' are required.",
                cancellationToken);
        }

        try
        {
            var status = _orchestrator.StartFromDescription(
                body.Description, body.CapabilityName, body.CapabilityDomainId, body.TenantId, body.EnableWebEnrichment, body.SubjectId, body.ProgramId,
                body.ProgramSequenceNumber, body.CapabilityObjectives, body.CapabilityRequirements);

            return await FunctionResponseFactory.SuccessResponseAsync(request, status, cancellationToken: cancellationToken);
        }
        catch (PdfCapabilityGraphOrchestrator.ActiveRunConflictException ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.Conflict, "CapabilityGraphRunInProgress", ex.Message, cancellationToken);
        }
    }
}
