using System.Net;
using System.Text.Json;
using HumanOS.Agents.Studio;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

public sealed class RefineOutlineTitlesRequest
{
    /// <summary>One of HumanOS.Services.DocumentClassification.DocumentType's
    /// names — the confirmed/detected type shown in the wizard.</summary>
    public string? DocumentType { get; set; }

    /// <summary>Optional free-text instruction from the designer for HOW
    /// titles should be phrased — see OutlineTitleRefinementRequest's own
    /// doc comment.</summary>
    public string? CustomInstruction { get; set; }

    /// <summary>The current outline exactly as returned by
    /// PreviewCapabilityOutline (or already hand-edited by the designer) —
    /// StartMarker/TotalCharacters/TotalTokens are echoed back unchanged;
    /// only Title fields are replaced.</summary>
    public List<CapabilityOutlineChapterDto> Chapters { get; set; } = [];
}

/// <summary>
/// Opt-in "Mejorar títulos con IA" endpoint (2026-08-17) — the ONLY LLM call
/// in the otherwise-free Paso 5 outline-preview flow, and only when the
/// designer explicitly clicks the button. Always ONE batched call
/// regardless of chapter/node count (see OutlineTitleRefinementAgent's own
/// doc comment) — never one call per article. Best-effort: on any failure,
/// returns the ORIGINAL chapters unchanged rather than blocking the wizard.
/// </summary>
public sealed class RefineOutlineTitlesFunction
{
    private readonly OutlineTitleRefinementAgent _agent;

    public RefineOutlineTitlesFunction(OutlineTitleRefinementAgent agent)
    {
        _agent = agent;
    }

    [Function("RefineOutlineTitles")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "studio/capability-graph/preview-outline/refine-titles")]
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        if (!_agent.IsConfigured)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.ServiceUnavailable, "OutlineTitleRefinementNotConfigured",
                "Outline title refinement is not configured (missing Azure OpenAI settings).", cancellationToken);
        }

        RefineOutlineTitlesRequest? body;
        try
        {
            body = await JsonSerializer.DeserializeAsync<RefineOutlineTitlesRequest>(
                request.Body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }, cancellationToken);
        }
        catch (JsonException)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidJson", "The request body contains invalid JSON.", cancellationToken);
        }

        if (body is null || body.Chapters.Count == 0)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidRequest", "'chapters' is required and must not be empty.", cancellationToken);
        }

        var refinementRequest = new OutlineTitleRefinementRequest
        {
            DocumentType = string.IsNullOrWhiteSpace(body.DocumentType) ? "Unknown" : body.DocumentType,
            CustomInstruction = string.IsNullOrWhiteSpace(body.CustomInstruction) ? null : body.CustomInstruction,
            Chapters = body.Chapters.Select((chapter, index) => new OutlineTitleRefinementChapterInput
            {
                Index = index,
                OriginalTitle = chapter.Title,
                OriginalNodeTitles = chapter.Nodes.Select(n => n.Title).ToList()
            }).ToList()
        };

        try
        {
            var refined = await _agent.RefineAsync(refinementRequest, cancellationToken);
            var refinedByIndex = refined.Chapters.ToDictionary(c => c.Index);

            for (var i = 0; i < body.Chapters.Count; i++)
            {
                if (!refinedByIndex.TryGetValue(i, out var refinedChapter))
                {
                    continue;
                }

                body.Chapters[i].Title = refinedChapter.RefinedTitle;
                for (var n = 0; n < body.Chapters[i].Nodes.Count; n++)
                {
                    body.Chapters[i].Nodes[n].Title = refinedChapter.RefinedNodeTitles[n];
                }
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Best-effort — see this function's own doc comment. Fall back
            // to the original, unrefined titles rather than failing the
            // wizard over a purely cosmetic enhancement.
            return await FunctionResponseFactory.SuccessResponseAsync(request, body.Chapters, cancellationToken: cancellationToken);
        }

        return await FunctionResponseFactory.SuccessResponseAsync(request, body.Chapters, cancellationToken: cancellationToken);
    }
}
