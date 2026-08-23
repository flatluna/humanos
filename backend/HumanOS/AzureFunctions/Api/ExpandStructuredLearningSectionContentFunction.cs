using System.Net;
using System.Text.Json;
using HumanOS.Agents.Studio;
using HumanOS.Data;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;
using SharpToken;

namespace HumanOS.AzureFunctions.Api;

public sealed class ExpandStructuredLearningSectionContentRequest
{
    /// <summary>"Main" (default) or "Economy" — which configured Azure
    /// OpenAI deployment SectionContentExpansionAgent uses.</summary>
    public string? ModelChoice { get; set; }

    /// <summary>Opt-in Bing grounding for the expansion — best-effort,
    /// silently skipped if WebGroundingService isn't configured.</summary>
    public bool UseWebGrounding { get; set; }
}

public sealed class ExpandStructuredLearningSectionContentResponse
{
    public Guid StructuredLearningDocumentSectionId { get; set; }
    public int TotalCharacters { get; set; }
    public int TotalTokens { get; set; }
}

/// <summary>
/// Retroactive "Generar contenido con IA" for ONE already-persisted section
/// (2026-08-17, Studio Edit mode) — for a document created WITHOUT opting
/// into ExpandSparseContent at Step 2, this lets the designer expand one
/// sparse engram's real Text afterward, so the Knowledge Graph/Facts
/// (regenerated separately via the existing "Regenerar Grafo" button) and
/// Generate Pedagogy have real content to work from instead of a bare
/// outline entry. Re-embeds the section since Text changed.
/// </summary>
public sealed class ExpandStructuredLearningSectionContentFunction
{
    private readonly HumanOsDbContext _dbContext;
    private readonly SectionContentExpansionAgent _contentExpansion;
    private readonly WebGroundingService _webGrounding;
    private readonly CapabilityEmbeddingService _embeddingService;
    private readonly GptEncoding _tokenEncoding = GptEncoding.GetEncoding("cl100k_base");

    public ExpandStructuredLearningSectionContentFunction(
        HumanOsDbContext dbContext,
        SectionContentExpansionAgent contentExpansion,
        WebGroundingService webGrounding,
        CapabilityEmbeddingService embeddingService)
    {
        _dbContext = dbContext;
        _contentExpansion = contentExpansion;
        _webGrounding = webGrounding;
        _embeddingService = embeddingService;
    }

    [Function("ExpandStructuredLearningSectionContent")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "studio/structured-learning/sections/{sectionId:guid}/expand-content")]
        HttpRequestData request,
        Guid sectionId,
        CancellationToken cancellationToken)
    {
        if (!_contentExpansion.IsConfigured)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.ServiceUnavailable, "ContentExpansionNotConfigured",
                "Section content expansion is not configured (missing Azure OpenAI settings).", cancellationToken);
        }

        ExpandStructuredLearningSectionContentRequest? body;
        try
        {
            body = await JsonSerializer.DeserializeAsync<ExpandStructuredLearningSectionContentRequest>(
                request.Body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }, cancellationToken) ?? new();
        }
        catch (JsonException)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidJson", "The request body contains invalid JSON.", cancellationToken);
        }

        var section = await _dbContext.StructuredLearningDocumentSections
            .Include(s => s.Document)
            .FirstOrDefaultAsync(s => s.StructuredLearningDocumentSectionId == sectionId, cancellationToken);
        if (section is null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "SectionNotFound", $"No section found with id '{sectionId}'.", cancellationToken);
        }

        string? groundingFindings = null;
        if (body.UseWebGrounding && _webGrounding.IsConfigured)
        {
            try
            {
                var grounding = await _webGrounding.SearchAsync(section.SubchapterTitle, section.ChapterTitle, cancellationToken);
                groundingFindings = grounding.Text;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Best-effort — proceed without grounding findings.
            }
        }

        var modelChoice = string.Equals(body.ModelChoice, "Economy", StringComparison.OrdinalIgnoreCase)
            ? ContentExpansionModelChoice.Economy
            : ContentExpansionModelChoice.Main;

        string languageCode = "es";
        if (!string.IsNullOrWhiteSpace(section.Document.ConfigSnapshotJson))
        {
            try
            {
                var snapshot = JsonSerializer.Deserialize<JsonElement>(section.Document.ConfigSnapshotJson);
                if (snapshot.TryGetProperty("LanguageCode", out var language) && !string.IsNullOrWhiteSpace(language.GetString()))
                {
                    languageCode = language.GetString()!;
                }
            }
            catch (JsonException) { }
        }

        try
        {
            var expansion = await _contentExpansion.ExpandAsync(
                section.SubchapterTitle,
                section.ChapterTitle,
                section.Document.DocumentName,
                languageCode,
                modelChoice,
                groundingFindings,
                cancellationToken);

            if (string.IsNullOrWhiteSpace(expansion.ExpandedText))
            {
                return await FunctionResponseFactory.ErrorResponseAsync(
                    request, HttpStatusCode.UnprocessableEntity, "ContentExpansionEmpty",
                    "The content expansion agent returned no text.", cancellationToken);
            }

            section.Text = expansion.ExpandedText;
            section.TotalCharacters = expansion.ExpandedText.Length;
            section.TotalTokens = _tokenEncoding.Encode(expansion.ExpandedText).Count;
            section.Embedding = new Microsoft.Data.SqlTypes.SqlVector<float>(
                await _embeddingService.GenerateEmbeddingAsync(expansion.ExpandedText, cancellationToken));

            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.UnprocessableEntity, "ContentExpansionFailed", ex.Message, cancellationToken);
        }

        return await FunctionResponseFactory.SuccessResponseAsync(request, new ExpandStructuredLearningSectionContentResponse
        {
            StructuredLearningDocumentSectionId = section.StructuredLearningDocumentSectionId,
            TotalCharacters = section.TotalCharacters,
            TotalTokens = section.TotalTokens
        }, cancellationToken: cancellationToken);
    }
}
