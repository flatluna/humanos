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

        /// <summary>Optional: the user's own description of what this
        /// Capability teaches. Saved directly to the Capability row in the
        /// database (unlike the CapabilityObjectives, which are scoped to a
        /// specific Program sequence position).</summary>
        public string? CapabilityDescription { get; set; }

        public string FileName { get; set; } = string.Empty;

        public string ContentBase64 { get; set; } = string.Empty;

        /// <summary>Optional: the student-facing topical Subject (Finanzas,
        /// Cocina, Matemáticas...) this capability belongs to. See
        /// GET /api/subjects. Distinct from CapabilityDomainId.</summary>
        public Guid? SubjectId { get; set; }

        /// <summary>Opt-in: supplements each chapter's topic with CURRENT
        /// web findings via Grounding with Bing Search before curating
        /// (see WebGroundingService). Defaults to false — never enabled
        /// unless the caller explicitly asks for it.</summary>
        public bool EnableWebEnrichment { get; set; }

        /// <summary>Optional: an existing Program to attach this new
        /// Capability to (appended to the end of its sequence) as soon as
        /// the Capability row is created — top-down flow (2026-07-23):
        /// create the Program first, then connect Capabilities to it, here
        /// or later from the Capability's own detail page.</summary>
        public Guid? ProgramId { get; set; }

        /// <summary>Optional: the designer's own explicit choice of
        /// sequence position (1-based, e.g. "#8") within ProgramId's
        /// sequence. Gaps are allowed — the designer may create #8 before
        /// #6 exists. Null falls back to auto-append (current max + 1).
        /// Ignored when ProgramId is null.</summary>
        public int? ProgramSequenceNumber { get; set; }

        /// <summary>Optional: what this Capability is meant to accomplish
        /// specifically within ProgramId's sequence (distinct from the
        /// Program's own overall objectives). Passed to GraphArchitectAgent
        /// as extra grounding context. Ignored when ProgramId is null.</summary>
        public string? CapabilityObjectives { get; set; }

        /// <summary>Optional: prerequisites a learner needs before starting
        /// this Capability within ProgramId's sequence. Ignored when
        /// ProgramId is null.</summary>
        public string? CapabilityRequirements { get; set; }

        /// <summary>"es" or "en" — the language ALL generated content for
        /// this capability (graph node names/descriptions, blueprint step
        /// text, illustration prompts) must be authored in, regardless of
        /// what language the uploaded PDF itself is written in. Defaults
        /// to "es" when omitted, for backward compatibility. See
        /// Capability.ContentLanguageCode.</summary>
        public string? LanguageCode { get; set; }

        /// <summary>Optional tone/persona directive for ALL generated
        /// learner-facing content on this capability. Null/omitted (the
        /// default) means "Standard" — zero prompt change. The only other
        /// supported value today is "YoungLearner" (K-12 early-childhood
        /// courses). See Capability.PedagogicalAudience.</summary>
        public string? PedagogicalAudience { get; set; }

        /// <summary>Formal/school-track opt-in (2026-07-29 feature):
        /// when true, the generated blueprint for every node includes an
        /// OPTIONAL 6th step, Examples, with 3+ example cards illustrated
        /// with emoji. False (default, omitted) leaves every generated
        /// blueprint identical to before this field existed. See
        /// Capability.IncludeExamplesStep.</summary>
        public bool IncludeExamplesStep { get; set; }

        /// <summary>Optional (2026-08-01 "cursos de empresa" feature):
        /// which employer training track this capability belongs to —
        /// "job-skills" | "soft-skills" | "department-processes" |
        /// "hr-processes". Null for capabilities not tied to any employer
        /// training track. See Capability.Category.</summary>
        public string? Category { get; set; }

        /// <summary>Optional: when set together with <see cref="TargetSkill"/>,
        /// HR is explicitly authoring this capability to close ONE specific
        /// required skill/process gap on this JobRole. See
        /// Capability.TargetJobRoleId.</summary>
        public Guid? TargetJobRoleId { get; set; }

        /// <summary>The exact required-skill/process string this capability
        /// is meant to satisfy. See Capability.TargetSkill.</summary>
        public string? TargetSkill { get; set; }

        /// <summary>Optional maximum number of Deep Dive example cards the
        /// AI may generate per node for this course. Must be between 1 and
        /// 10 when provided.</summary>
        public int? MaxDeepDiveCardsPerNode { get; set; }

        /// <summary>Opt-in (2026-08-03): when true, and the source PDF has
        /// a real detected chapter/subsection structure (e.g. a formal
        /// syllabus with numbered "Unidades"/subsections), forces the
        /// GraphArchitect to create EXACTLY one node per detected chapter/
        /// subsection, in the same order, with none skipped or merged —
        /// instead of its normal judgment-based node count. Use this for
        /// courses that must mirror an externally-mandated curriculum
        /// exactly. False (default) leaves the existing "design the graph
        /// from real cognitive complexity" behavior unchanged.</summary>
        public bool StrictSyllabusMode { get; set; }
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

            if (body.MaxDeepDiveCardsPerNode is < 1 or > 10)
            {
                return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.BadRequest,
                "InvalidRequest",
                "'maxDeepDiveCardsPerNode' must be between 1 and 10.",
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

        try
        {
            var status = _orchestrator.Start(
                pdfBytes, body.FileName, body.CapabilityDomainId, body.CapabilityName, body.TenantId, body.EnableWebEnrichment, body.SubjectId, body.ProgramId,
                body.ProgramSequenceNumber, body.CapabilityObjectives, body.CapabilityRequirements, body.CapabilityDescription,
                string.IsNullOrWhiteSpace(body.LanguageCode) ? "es" : body.LanguageCode.Trim().ToLowerInvariant(),
                body.PedagogicalAudience, body.IncludeExamplesStep, body.Category, body.TargetJobRoleId, body.TargetSkill, body.MaxDeepDiveCardsPerNode,
                body.StrictSyllabusMode);

            return await FunctionResponseFactory.SuccessResponseAsync(request, status, cancellationToken: cancellationToken);
        }
        catch (PdfCapabilityGraphOrchestrator.ActiveRunConflictException ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.Conflict, "CapabilityGraphRunInProgress", ex.Message, cancellationToken);
        }
    }
}
