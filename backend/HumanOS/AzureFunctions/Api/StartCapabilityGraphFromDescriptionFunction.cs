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
    private readonly PersonService _personService;

    public StartCapabilityGraphFromDescriptionFunction(PdfCapabilityGraphOrchestrator orchestrator, PersonService personService)
    {
        _orchestrator = orchestrator;
        _personService = personService;
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

        /// <summary>Optional: the user's own description of what this
        /// Capability teaches. Saved directly to the Capability row in the
        /// database. If not provided, uses Description as both the idea
        /// expansion input and the stored description.</summary>
        public string? CapabilityDescription { get; set; }

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

        /// <summary>See StartPdfCapabilityGraphFunction's identical field.</summary>
        public string? LanguageCode { get; set; }

        /// <summary>See StartPdfCapabilityGraphFunction's identical field.</summary>
        public string? PedagogicalAudience { get; set; }

        /// <summary>See StartPdfCapabilityGraphFunction's identical field.</summary>
        public bool IncludeExamplesStep { get; set; }

        /// <summary>See StartPdfCapabilityGraphFunction's identical field.</summary>
        public string? Category { get; set; }

        /// <summary>See StartPdfCapabilityGraphFunction's identical field.</summary>
        public Guid? TargetJobRoleId { get; set; }

        /// <summary>See StartPdfCapabilityGraphFunction's identical field.</summary>
        public string? TargetSkill { get; set; }

        /// <summary>See StartPdfCapabilityGraphFunction's identical field.</summary>
        public int? MaxDeepDiveCardsPerNode { get; set; }

        /// <summary>See StartPdfCapabilityGraphFunction's identical field.</summary>
        public bool StrictSyllabusMode { get; set; }

        /// <summary>See StartPdfCapabilityGraphFunction's identical field.</summary>
        public List<CapabilityOutlineChapterDto>? ApprovedOutline { get; set; }

        /// <summary>Optional (2026-08-07): the exact document
        /// IdeaToDocumentAgent expanded <see cref="Description"/> into during
        /// the outline preview call (PreviewCapabilityOutlineFunction's
        /// ExpandedDocumentText), when <see cref="ApprovedOutline"/> was
        /// approved from that preview. When provided, the pipeline reuses
        /// this text VERBATIM instead of calling IdeaToDocumentAgent again —
        /// required for ApprovedOutline's StartMarkers to still resolve,
        /// since a fresh expansion call would produce different (non-
        /// deterministic) text. Ignored when ApprovedOutline is null/empty
        /// (the normal "skip outline design" path expands fresh, same as
        /// before this field existed).</summary>
        public string? PreExpandedDocumentText { get; set; }

        /// <summary>See StartPdfCapabilityGraphFunction's identical field.</summary>
        public bool IncludeProductionStep { get; set; } = true;

        /// <summary>See StartPdfCapabilityGraphFunction's identical field.</summary>
        public int? RecallItemsRequired { get; set; }

        /// <summary>See StartPdfCapabilityGraphFunction's identical field.</summary>
        public int? AssessmentQuestionsPerRound { get; set; }

        /// <summary>See StartPdfCapabilityGraphFunction's identical field.</summary>
        public string? LogoImageBase64 { get; set; }

        /// <summary>See StartPdfCapabilityGraphFunction's identical field.</summary>
        public bool AcceptedLegalDisclosure { get; set; }

        /// <summary>See StartPdfCapabilityGraphFunction's identical field.</summary>
        public bool IsGlobal { get; set; }
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

        if (!body.AcceptedLegalDisclosure)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.BadRequest,
                "LegalDisclosureNotAccepted",
                "'acceptedLegalDisclosure' must be true — the content-liability disclosure must be accepted before generation can start.",
                cancellationToken);
        }

            if (body.MaxDeepDiveCardsPerNode is < 1 or > 10)
            {
                return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.BadRequest,
                "InvalidRequest",
                "'maxDeepDiveCardsPerNode' must be between 1 and 10.",
                cancellationToken);
            }

            if (body.RecallItemsRequired is < 1 or > 10)
            {
                return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.BadRequest,
                "InvalidRequest",
                "'recallItemsRequired' must be between 1 and 10.",
                cancellationToken);
            }

            if (body.AssessmentQuestionsPerRound is < 1 or > 10)
            {
                return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.BadRequest,
                "InvalidRequest",
                "'assessmentQuestionsPerRound' must be between 1 and 10.",
                cancellationToken);
            }

        byte[]? brandLogoBytes = null;
        if (!string.IsNullOrWhiteSpace(body.LogoImageBase64))
        {
            try
            {
                brandLogoBytes = Convert.FromBase64String(body.LogoImageBase64);
            }
            catch (FormatException)
            {
                return await FunctionResponseFactory.ErrorResponseAsync(
                    request, HttpStatusCode.BadRequest, "InvalidBase64", "'logoImageBase64' is not valid base64.", cancellationToken);
            }
        }

        try
        {
            // Ownership (2026-08-05): see StartPdfCapabilityGraphFunction's
            // identical best-effort identity resolution.
            Guid? callerPersonId = null;
            Guid? callerTenantId = null;
            string? callerEmail = null;
            if (request.Headers.TryGetValues("X-Azure-OID", out var oidValues) && !string.IsNullOrWhiteSpace(oidValues.FirstOrDefault()) &&
                request.Headers.TryGetValues("X-Azure-TID", out var tidValues) && !string.IsNullOrWhiteSpace(tidValues.FirstOrDefault()))
            {
                var callerPerson = await _personService.GetByAzureIdentityAsync(oidValues.First(), tidValues.First(), cancellationToken);
                if (callerPerson is not null)
                {
                    callerPersonId = callerPerson.PersonId;
                    callerTenantId = callerPerson.TenantId;
                    callerEmail = callerPerson.Email;
                }
            }

            // Studio sessions launched from a parent app with no HumanOS
            // Person record (e.g. an NGO admin) carry their tenant directly
            // via this header instead — see StartPdfCapabilityGraphFunction's
            // identical fallback.
            if (callerTenantId is null &&
                request.Headers.TryGetValues("X-Studio-Tenant-Id", out var studioTenantValues) &&
                Guid.TryParse(studioTenantValues.FirstOrDefault(), out var studioTenantId))
            {
                callerTenantId = studioTenantId;
            }

            // Global/personal visibility (2026-08-06): see
            // StartPdfCapabilityGraphFunction's identical IsGlobal handling,
            // including the "no owner left behind" guard (2026-10-06).
            if (!body.IsGlobal && callerPersonId is null && callerTenantId is null)
            {
                return await FunctionResponseFactory.ErrorResponseAsync(
                    request, HttpStatusCode.BadRequest, "CallerIdentityRequired",
                    "Creating a non-global (personal/tenant) capability requires a resolvable caller identity (X-Azure-OID/X-Azure-TID or X-Studio-Tenant-Id).",
                    cancellationToken);
            }

            Guid? ownerPersonId = body.IsGlobal ? null : callerPersonId;
            Guid? ownerTenantId = body.IsGlobal ? null : callerTenantId;

            // Legal disclosure consent (2026-08-06): stamped server-side
            // (UTC) — body.AcceptedLegalDisclosure only gates whether this
            // is recorded at all (already validated true above).
            var consentAcceptedDate = DateTime.UtcNow;

            var status = _orchestrator.StartFromDescription(
                body.Description, body.CapabilityName, body.CapabilityDomainId, body.TenantId, body.EnableWebEnrichment, body.SubjectId, body.ProgramId,
                body.ProgramSequenceNumber, body.CapabilityObjectives, body.CapabilityRequirements, body.CapabilityDescription,
                string.IsNullOrWhiteSpace(body.LanguageCode) ? "es" : body.LanguageCode.Trim().ToLowerInvariant(),
                body.PedagogicalAudience, body.IncludeExamplesStep, body.Category, body.TargetJobRoleId, body.TargetSkill, body.MaxDeepDiveCardsPerNode,
                body.StrictSyllabusMode, body.ApprovedOutline, body.PreExpandedDocumentText, body.IncludeProductionStep, body.RecallItemsRequired, body.AssessmentQuestionsPerRound, brandLogoBytes,
                ownerPersonId, ownerTenantId, consentAcceptedDate, callerPersonId, callerEmail);

            return await FunctionResponseFactory.SuccessResponseAsync(request, status, cancellationToken: cancellationToken);
        }
        catch (PdfCapabilityGraphOrchestrator.ActiveRunConflictException ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.Conflict, "CapabilityGraphRunInProgress", ex.Message, cancellationToken);
        }
    }
}
