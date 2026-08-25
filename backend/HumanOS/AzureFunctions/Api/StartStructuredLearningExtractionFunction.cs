using System.Net;
using System.Text.Json;
using HumanOS.Models.Capabilities.Studio;
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

        /// <summary>Azure AD Object ID of the person creating this document —
        /// see StructuredLearningDocument.CreatedByOid's own doc comment.</summary>
        public string? CreatedByOid { get; set; }

        public string? CreatedByEmail { get; set; }

        /// <summary>Catalog visibility — see
        /// StructuredLearningDocument.IsGlobal's own doc comment. Defaults to
        /// false (Personal/tenant-only) when omitted.</summary>
        public bool IsGlobal { get; set; }

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

        // ---- Step 1 config (2026-08-14) — domain/subject + career-skill
        // linking and initial pedagogy defaults, all optional. See
        // StructuredLearningCreationOptions. ----

        public Guid? SubjectId { get; set; }

        /// <summary>Knowledge Graph topic scoping (2026-08-16) — e.g. the
        /// "Ley del IVA" Topic within SubjectId="Leyes". See
        /// StructuredLearningDocument.TopicId's own doc comment.</summary>
        public Guid? TopicId { get; set; }

        public Guid? CapabilityDomainId { get; set; }

        public string? Category { get; set; }

        public string? TargetSkill { get; set; }

        public string? PedagogicalAudience { get; set; }

        public int? MaxDeepDiveCardsPerNode { get; set; }

        public bool? IncludeProductionStep { get; set; }

        public bool? IncludeRecallStep { get; set; }

        public bool? IncludeHypothesisStep { get; set; }

        public int? RecallItemsRequired { get; set; }

        public bool? IncludeAssessmentStep { get; set; }

        public StructuredLearningAssessmentQuestionType? AssessmentQuestionType { get; set; }

        public int? AssessmentQuestionsPerRound { get; set; }

        /// <summary>Knowledge Graph opt-in (2026-08-14) — null keeps the default (both on).</summary>
        public bool? IncludeKnowledgeGraphEntities { get; set; }

        public bool? IncludeKnowledgeGraphConnections { get; set; }

        /// <summary>Document Type Detection Engine (2026-08-17, Paso 3a) —
        /// the designer's confirmed choice from the wizard's outline-preview
        /// step, as one of HumanOS.Services.DocumentClassification.DocumentType's
        /// names. Null means the designer never reviewed one; the backend
        /// then classifies the document itself as a recommendation only.</summary>
        public string? ConfirmedDocumentType { get; set; }

        /// <summary>Opt-in "Generar contenido con IA" (2026-08-17) — see
        /// StructuredLearningCreationOptions.ExpandSparseContent's own doc
        /// comment. False/omitted (default) never touches any section's text.</summary>
        public bool ExpandSparseContent { get; set; }

        /// <summary>"Main" or "Economy" — ignored when ExpandSparseContent is false.</summary>
        public string? ContentExpansionModel { get; set; }

        /// <summary>Opt-in Bing grounding for the content expansion —
        /// ignored when ExpandSparseContent is false.</summary>
        public bool UseWebGroundingForExpansion { get; set; }
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

        var creationOptions = new StructuredLearningCreationOptions
        {
            LanguageCode = languageCode,
            CreatedByOid = string.IsNullOrWhiteSpace(body.CreatedByOid) ? null : body.CreatedByOid,
            CreatedByEmail = string.IsNullOrWhiteSpace(body.CreatedByEmail) ? null : body.CreatedByEmail,
            IsGlobal = body.IsGlobal,
            SubjectId = body.SubjectId,
            TopicId = body.TopicId,
            CapabilityDomainId = body.CapabilityDomainId,
            Category = string.IsNullOrWhiteSpace(body.Category) ? null : body.Category,
            TargetSkill = string.IsNullOrWhiteSpace(body.TargetSkill) ? null : body.TargetSkill,
            PedagogicalAudience = string.IsNullOrWhiteSpace(body.PedagogicalAudience) ? null : body.PedagogicalAudience,
            MaxDeepDiveCardsPerNode = body.MaxDeepDiveCardsPerNode,
            IncludeProductionStep = body.IncludeProductionStep,
            IncludeRecallStep = body.IncludeRecallStep,
            IncludeHypothesisStep = body.IncludeHypothesisStep,
            RecallItemsRequired = body.RecallItemsRequired,
            IncludeAssessmentStep = body.IncludeAssessmentStep,
            AssessmentQuestionType = body.AssessmentQuestionType,
            AssessmentQuestionsPerRound = body.AssessmentQuestionsPerRound,
            IncludeKnowledgeGraphEntities = body.IncludeKnowledgeGraphEntities,
            IncludeKnowledgeGraphConnections = body.IncludeKnowledgeGraphConnections,
            ConfirmedDocumentType = string.IsNullOrWhiteSpace(body.ConfirmedDocumentType) ? null : body.ConfirmedDocumentType,
            ExpandSparseContent = body.ExpandSparseContent,
            ContentExpansionModel = body.ContentExpansionModel,
            UseWebGroundingForExpansion = body.UseWebGroundingForExpansion
        };

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

                status = _orchestrator.Start(pdfBytes, body.FileName!, body.DocumentName, body.TenantId, body.ApprovedOutline, creationOptions);
            }
            else
            {
                status = _orchestrator.StartFromDescription(
                    body.Description!, body.DocumentName, body.TenantId, body.ApprovedOutline,
                    body.PreExpandedDocumentText, languageCode, body.StrictSyllabusMode, creationOptions);
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
