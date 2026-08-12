using System.Net;
using System.Text.Json;
using HumanOS.Data;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.AzureFunctions.Api;

public sealed class AddStructuredLearningSectionRequest
{
    public string ChapterTitle { get; set; } = string.Empty;

    public string Instruction { get; set; } = string.Empty;

    public string LanguageCode { get; set; } = "es";

    /// <summary>Existing section ids (within the same chapter) that must
    /// come BEFORE the new section — mirrors AddGraphNodeRequest.RequiresNodeIds.</summary>
    public List<string> RequiresSectionIds { get; set; } = [];

    /// <summary>Existing section ids (within the same chapter) that must
    /// come AFTER the new section — mirrors AddGraphNodeRequest.RequiredByNodeIds.</summary>
    public List<string> RequiredBySectionIds { get; set; } = [];

    /// <summary>"None" | "AiGenerated" (default) | "UploadDirect" |
    /// "AiGeneratedFromUpload" — see StructuredLearningPedagogyService
    /// .GenerateIllustrationsAsync's doc comment.</summary>
    public string IllustrationMode { get; set; } = "AiGenerated";

    /// <summary>Base64-encoded image (no data: URL prefix), required when
    /// IllustrationMode is "UploadDirect" or "AiGeneratedFromUpload".</summary>
    public string? UploadedImageBase64 { get; set; }

    public string? UploadedImageContentType { get; set; }

    /// <summary>Only meaningful when IllustrationMode is "AiGeneratedFromUpload" —
    /// describes HOW the reference image should appear in the generated
    /// scenes (e.g. "put our logo on the volunteers' t-shirts"), kept
    /// separate from Instruction (which only describes the section's content).</summary>
    public string? ReferenceImageUsageInstruction { get; set; }
}

/// <summary>
/// Structured Learning's "Edit mode" add-section feature (2026-08-11) —
/// mirrors AddCapabilityGraphNodeFunction (Graph's equivalent): generates
/// AND persists a brand-new section (with its 6 pedagogical steps)
/// synchronously within one request. See
/// StructuredLearningSectionAdditionService's doc comment.
/// </summary>
public sealed class AddStructuredLearningSectionFunction
{
    private readonly StructuredLearningSectionAdditionService _service;
    private readonly HumanOsDbContext _dbContext;

    public AddStructuredLearningSectionFunction(StructuredLearningSectionAdditionService service, HumanOsDbContext dbContext)
    {
        _service = service;
        _dbContext = dbContext;
    }

    [Function("AddStructuredLearningSection")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "studio/structured-learning/{documentId:guid}/sections/add")]
        HttpRequestData request,
        Guid documentId,
        CancellationToken cancellationToken)
    {
        if (!_service.IsConfigured)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.ServiceUnavailable, "StructuredLearningSectionAdditionNotConfigured",
                "Adding sections is not configured (missing Azure OpenAI settings).", cancellationToken);
        }

        AddStructuredLearningSectionRequest? body;
        try
        {
            body = await JsonSerializer.DeserializeAsync<AddStructuredLearningSectionRequest>(
                request.Body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }, cancellationToken);
        }
        catch (JsonException)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidJson", "The request body contains invalid JSON.", cancellationToken);
        }

        if (body is null || string.IsNullOrWhiteSpace(body.ChapterTitle) || string.IsNullOrWhiteSpace(body.Instruction))
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "MissingFields", "ChapterTitle and Instruction are both required.", cancellationToken);
        }

        var illustrationMode = string.IsNullOrWhiteSpace(body.IllustrationMode) ? "AiGenerated" : body.IllustrationMode;
        var needsUploadedImage = illustrationMode is "UploadDirect" or "AiGeneratedFromUpload";

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
                    request, HttpStatusCode.BadRequest, "InvalidBase64", "'UploadedImageBase64' is not valid base64.", cancellationToken);
            }
        }

        if (needsUploadedImage && uploadedImageBytes is null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "MissingFields",
                "UploadedImageBase64 is required when IllustrationMode is 'UploadDirect' or 'AiGeneratedFromUpload'.",
                cancellationToken);
        }

        var requiresSectionIds = new List<Guid>();
        var requiredBySectionIds = new List<Guid>();
        foreach (var (rawIds, parsed) in new[] { (body.RequiresSectionIds, requiresSectionIds), (body.RequiredBySectionIds, requiredBySectionIds) })
        {
            foreach (var rawId in rawIds)
            {
                if (!Guid.TryParse(rawId, out var parsedId))
                {
                    return await FunctionResponseFactory.ErrorResponseAsync(
                        request, HttpStatusCode.BadRequest, "InvalidSectionId", $"'{rawId}' is not a valid section id.", cancellationToken);
                }
                parsed.Add(parsedId);
            }
        }

        try
        {
            var newSectionId = await _service.AddSectionAsync(
                documentId, body.ChapterTitle, body.Instruction, body.LanguageCode,
                requiresSectionIds, requiredBySectionIds, illustrationMode, uploadedImageBytes, body.UploadedImageContentType,
                body.ReferenceImageUsageInstruction, cancellationToken);

            var section = await _dbContext.StructuredLearningDocumentSections
                .Include(s => s.Steps)
                .FirstOrDefaultAsync(s => s.StructuredLearningDocumentSectionId == newSectionId, cancellationToken);

            if (section is null)
            {
                return await FunctionResponseFactory.ErrorResponseAsync(
                    request, HttpStatusCode.InternalServerError, "SectionNotFoundAfterCreation", "The section was created but could not be re-read.", cancellationToken);
            }

            var dto = new StructuredLearningSectionDetailDto
            {
                StructuredLearningDocumentSectionId = section.StructuredLearningDocumentSectionId,
                SortOrder = section.SortOrder,
                ChapterTitle = section.ChapterTitle,
                SubchapterTitle = section.SubchapterTitle,
                TotalCharacters = section.TotalCharacters,
                TotalTokens = section.TotalTokens,
                PedagogyStatus = section.PedagogyStatus.ToString(),
                PedagogyErrorMessage = section.PedagogyErrorMessage,
                Steps = section.Steps
                    .OrderBy(st => st.SortOrder)
                    .Select(st => new StructuredLearningStepDetailDto
                    {
                        StructuredLearningSectionStepId = st.StructuredLearningSectionStepId,
                        StepType = st.StepType.ToString(),
                        Content = st.Content,
                        StepDataJson = st.StepDataJson
                    })
                    .ToList()
            };

            return await FunctionResponseFactory.SuccessResponseAsync(request, dto, cancellationToken: cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidAddSection", ex.Message, cancellationToken);
        }
    }
}
