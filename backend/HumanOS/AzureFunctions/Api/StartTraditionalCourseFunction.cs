using System.Net;
using System.Text.Json;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>
/// Starts a new Traditional Course run — Studio's second, simpler
/// course-creation method (linear Chapters -> Lessons of reading Cards,
/// ONE multiple-choice exam per chapter). Accepts EITHER an uploaded PDF
/// (FileName + ContentBase64) OR a short idea/description (Description) —
/// same "Modo A / Modo B" dual entry point as the Graph pipeline's
/// StartPdfCapabilityGraphFunction/StartCapabilityGraphFromDescriptionFunction,
/// merged into ONE endpoint here for simplicity. Returns immediately
/// (Stage.Running) — poll GetTraditionalCourseStatus for progress.
/// </summary>
public sealed class StartTraditionalCourseFunction
{
    private readonly TraditionalCoursePipelineOrchestrator _orchestrator;
    private readonly PersonService _personService;

    public StartTraditionalCourseFunction(TraditionalCoursePipelineOrchestrator orchestrator, PersonService personService)
    {
        _orchestrator = orchestrator;
        _personService = personService;
    }

    public sealed class StartRequest
    {
        public Guid TenantId { get; set; }

        public Guid CapabilityDomainId { get; set; }

        public string CapabilityName { get; set; } = string.Empty;

        public string? CapabilityDescription { get; set; }

        /// <summary>Required for the PDF entry point. Mutually exclusive with <see cref="Description"/>.</summary>
        public string? FileName { get; set; }

        public string? ContentBase64 { get; set; }

        /// <summary>Required for the "Texto/idea" entry point. Mutually exclusive with <see cref="FileName"/>/<see cref="ContentBase64"/>.</summary>
        public string? Description { get; set; }

        public Guid? SubjectId { get; set; }

        public bool EnableWebEnrichment { get; set; }

        public string? LanguageCode { get; set; }

        public string? PedagogicalAudience { get; set; }

        /// <summary>How many multiple-choice questions each chapter's final exam has. Defaults to 5. Must be between 1 and 20.</summary>
        public int QuestionsPerChapterExam { get; set; } = 5;

        public bool StrictSyllabusMode { get; set; }

        public List<CapabilityOutlineChapterDto>? ApprovedOutline { get; set; }

        /// <summary>Only used with the "Texto/idea" entry point together with an ApprovedOutline — see
        /// StartCapabilityGraphFromDescriptionFunction's identical field.</summary>
        public string? PreExpandedDocumentText { get; set; }

        public string? LogoImageBase64 { get; set; }

        /// <summary>Optional reference image for chapter/lesson/exam illustrations.</summary>
        public string? UploadedImageBase64 { get; set; }

        public string? UploadedImageContentType { get; set; }

        /// <summary>Only meaningful when UploadedImageBase64 is set.</summary>
        public string? ReferenceImageUsageInstruction { get; set; }

        public bool AcceptedLegalDisclosure { get; set; }

        public bool IsGlobal { get; set; }
    }

    [Function("StartTraditionalCourse")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "studio/traditional-course/create")]
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        if (!_orchestrator.IsConfigured)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.ServiceUnavailable, "StudioAgentsNotConfigured",
                "The Traditional Course pipeline agents are not yet configured (missing Azure OpenAI settings).",
                cancellationToken);
        }

        var body = await JsonSerializer.DeserializeAsync<StartRequest>(
            request.Body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }, cancellationToken);

        var hasPdf = body is not null && !string.IsNullOrWhiteSpace(body.FileName) && !string.IsNullOrWhiteSpace(body.ContentBase64);
        var hasDescription = body is not null && !string.IsNullOrWhiteSpace(body.Description);

        if (body is null || body.TenantId == Guid.Empty || body.CapabilityDomainId == Guid.Empty ||
            string.IsNullOrWhiteSpace(body.CapabilityName) || (!hasPdf && !hasDescription))
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidRequest",
                "'tenantId', 'capabilityDomainId', 'capabilityName' and either ('fileName' + 'contentBase64') or 'description' are required.",
                cancellationToken);
        }

        if (!body.AcceptedLegalDisclosure)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "LegalDisclosureNotAccepted",
                "'acceptedLegalDisclosure' must be true — the content-liability disclosure must be accepted before generation can start.",
                cancellationToken);
        }

        if (body.QuestionsPerChapterExam is < 1 or > 20)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidRequest",
                "'questionsPerChapterExam' must be between 1 and 20.", cancellationToken);
        }

        byte[]? pdfBytes = null;
        if (hasPdf)
        {
            try
            {
                pdfBytes = Convert.FromBase64String(body.ContentBase64!);
            }
            catch (FormatException)
            {
                return await FunctionResponseFactory.ErrorResponseAsync(
                    request, HttpStatusCode.BadRequest, "InvalidBase64", "'contentBase64' is not valid base64.", cancellationToken);
            }
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

        byte[]? uploadedReferenceImageBytes = null;
        if (!string.IsNullOrWhiteSpace(body.UploadedImageBase64))
        {
            try
            {
                uploadedReferenceImageBytes = Convert.FromBase64String(body.UploadedImageBase64);
            }
            catch (FormatException)
            {
                return await FunctionResponseFactory.ErrorResponseAsync(
                    request, HttpStatusCode.BadRequest, "InvalidBase64", "'uploadedImageBase64' is not valid base64.", cancellationToken);
            }
        }

        try
        {
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

            Guid? ownerPersonId = body.IsGlobal ? null : callerPersonId;
            Guid? ownerTenantId = body.IsGlobal ? null : callerTenantId;
            var consentAcceptedDate = DateTime.UtcNow;
            var languageCode = string.IsNullOrWhiteSpace(body.LanguageCode) ? "es" : body.LanguageCode.Trim().ToLowerInvariant();

            var status = hasPdf
                ? _orchestrator.Start(
                    pdfBytes!, body.FileName!, body.CapabilityDomainId, body.CapabilityName, body.TenantId, body.EnableWebEnrichment,
                    body.SubjectId, body.CapabilityDescription, languageCode, body.PedagogicalAudience, body.QuestionsPerChapterExam,
                    body.StrictSyllabusMode, body.ApprovedOutline, brandLogoBytes, uploadedReferenceImageBytes,
                    body.ReferenceImageUsageInstruction, ownerPersonId, ownerTenantId, consentAcceptedDate,
                    callerPersonId, callerEmail)
                : _orchestrator.StartFromDescription(
                    body.Description!, body.CapabilityName, body.CapabilityDomainId, body.TenantId, body.EnableWebEnrichment,
                    body.SubjectId, languageCode, body.PedagogicalAudience, body.QuestionsPerChapterExam, body.StrictSyllabusMode,
                    body.ApprovedOutline, body.PreExpandedDocumentText, brandLogoBytes, uploadedReferenceImageBytes,
                    body.ReferenceImageUsageInstruction, ownerPersonId, ownerTenantId, consentAcceptedDate,
                    callerPersonId, callerEmail);

            return await FunctionResponseFactory.SuccessResponseAsync(request, status, cancellationToken: cancellationToken);
        }
        catch (TraditionalCoursePipelineOrchestrator.ActiveRunConflictException ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.Conflict, "TraditionalCourseRunInProgress", ex.Message, cancellationToken);
        }
    }
}
