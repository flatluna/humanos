using System.Net;
using System.Text.Json;
using HumanOS.Data;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

public sealed class EditStructuredLearningSourceTextRequest
{
    /// <summary>The reviewer's literal replacement for the section's source
    /// text. Optional — if omitted, the source text is left unchanged and
    /// only <see cref="Instruction"/>/<see cref="ImageInstruction"/> steer
    /// the full Engram regeneration.</summary>
    public string? NewText { get; set; }

    /// <summary>General guidance for the AI about this specific Engram,
    /// applied across every regenerated step.</summary>
    public string? Instruction { get; set; }

    /// <summary>Guidance specifically for the illustration(s) this Engram
    /// generates (most importantly the Teaching/"Learn" step's image).</summary>
    public string? ImageInstruction { get; set; }

    /// <summary>When true, enables real-time web grounding via Perplexity AI to supplement the edit.</summary>
    public bool UsePerplexitySearch { get; set; }
}

public sealed class EditStructuredLearningSourceTextResponseDto
{
    public string Text { get; set; } = string.Empty;
    public int VersionNumber { get; set; }
    public StructuredLearningStepDetailDto? UpdatedTeachingStep { get; set; }
    /// <summary>EVERY step of this Engram after the full regeneration — the
    /// frontend must REPLACE its entire Steps array with this list, since
    /// every step got a brand-new id (the old ones were deleted, not
    /// updated in place).</summary>
    public List<StructuredLearningStepDetailDto> AllSteps { get; set; } = [];
    public List<string> WebSearchCitations { get; set; } = [];
}

/// <summary>
/// Structured Learning's "Edit the Engram's original source text" feature
/// (2026-11-11) — takes the reviewer's new/edited source text and/or
/// general AI instructions and/or image instructions, versions the source
/// text (original preserved forever as version 0), and fully regenerates
/// this one Engram (Hypothesis/Teaching/DeepDive/Recall/Production/
/// Assessment, illustrations, audio, Knowledge Graph/Facts) exactly as if
/// it were newly created. See
/// <see cref="StructuredLearningReviewService.EditSourceTextAsync"/>.
/// </summary>
public sealed class EditStructuredLearningSourceTextFunction
{
    private readonly StructuredLearningReviewService _service;
    private readonly HumanOsDbContext _dbContext;

    public EditStructuredLearningSourceTextFunction(StructuredLearningReviewService service, HumanOsDbContext dbContext)
    {
        _service = service;
        _dbContext = dbContext;
    }

    [Function("EditStructuredLearningSourceText")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "studio/structured-learning/sections/{sectionId:guid}/source-text/edit")]
        HttpRequestData request,
        Guid sectionId,
        CancellationToken cancellationToken)
    {
        EditStructuredLearningSourceTextRequest? body;
        try
        {
            body = await JsonSerializer.DeserializeAsync<EditStructuredLearningSourceTextRequest>(
                request.Body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }, cancellationToken);
        }
        catch (JsonException)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidJson", "The request body contains invalid JSON.", cancellationToken);
        }

        if (body is null || (string.IsNullOrWhiteSpace(body.NewText) && string.IsNullOrWhiteSpace(body.Instruction) && string.IsNullOrWhiteSpace(body.ImageInstruction)))
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "MissingFields", "Provide at least one of: new text, an AI instruction, or an image instruction.", cancellationToken);
        }

        try
        {
            var result = await _service.EditSourceTextAsync(
                _dbContext, sectionId, body.NewText, body.Instruction, body.ImageInstruction, body.UsePerplexitySearch, cancellationToken);
            return await FunctionResponseFactory.SuccessResponseAsync(request, new EditStructuredLearningSourceTextResponseDto
            {
                Text = result.Text,
                VersionNumber = result.VersionNumber,
                WebSearchCitations = result.WebSearchCitations,
                UpdatedTeachingStep = result.UpdatedTeachingStep is null ? null : ToDto(result.UpdatedTeachingStep),
                AllSteps = result.AllSteps.Select(ToDto).ToList()
            }, cancellationToken: cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidSourceTextEdit", ex.Message, cancellationToken);
        }
    }

    private static StructuredLearningStepDetailDto ToDto(StructuredLearningReviewService.StepResult step) => new()
    {
        StructuredLearningSectionStepId = step.StructuredLearningSectionStepId,
        StepType = step.StepType.ToString(),
        Content = step.Content,
        StepDataJson = step.StepDataJson,
        IsHumanApproved = step.IsHumanApproved,
        HumanApprovedAtUtc = step.HumanApprovedAtUtc,
        WebSearchCitations = step.WebSearchCitations
    };
}
