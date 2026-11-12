using System.Net;
using System.Text.Json;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

public sealed class RegenerateStructuredLearningLessonIllustrationRequestDto
{
    public string Instructions { get; set; } = string.Empty;

    /// <summary>Optional company brand logo, base64-encoded PNG bytes
    /// (2026-11-11) — when supplied, the model is instructed to weave this
    /// ACTUAL uploaded logo into the generated scene instead of guessing
    /// one from the text prompt. Null/empty means no logo reference.</summary>
    public string? LogoImageBase64 { get; set; }

    /// <summary>Optional ID of one of this Engram's own existing source
    /// images (e.g. a real video frame) to use as an image-to-image
    /// reference for the regenerated illustration (2026-11-12). Must belong
    /// to this section and have Purpose = SourceDocument, or the request
    /// fails. Takes precedence over <see cref="LogoImageBase64"/>.</summary>
    public Guid? ReferenceSourceIllustrationId { get; set; }
}

public sealed class RegenerateStructuredLearningLessonIllustrationResponseDto
{
    public Guid SectionId { get; set; }
    public Guid IllustrationId { get; set; }

    /// <summary>The Teaching step's narration text after this regeneration
    /// (2026-11-11) — identical to what it was before unless the new image's
    /// scene required patching a scene-descriptive sentence (see
    /// StructuredLearningLessonNarrationSyncAgent). The frontend should
    /// replace its displayed Content with this value and re-fetch the
    /// section's audio so the narration, audio, and image never drift out
    /// of sync with each other.</summary>
    public string Content { get; set; } = string.Empty;
}

/// <summary>
/// "Regenerate this image" escape hatch for the Teaching step's single
/// lesson illustration (the "Learn" image shown when there are no Scenes/
/// Cards, 2026-11-11) — reviewer supplies free-text instructions, but the
/// backend ALWAYS prepends the Engram's own original lesson illustration
/// prompt (the source-faithful prompt generated from the full class text
/// when this Engram was first created) plus the full lesson text again, so
/// a reviewer's tweak ("make it simpler", "change the colors") can never
/// silently discard what the image was actually supposed to depict. See
/// StructuredLearningProfessorLectureService.RegenerateLessonIllustrationAsync.
/// </summary>
public sealed class RegenerateStructuredLearningLessonIllustrationFunction
{
    private readonly StructuredLearningProfessorLectureService _lectureService;

    public RegenerateStructuredLearningLessonIllustrationFunction(StructuredLearningProfessorLectureService lectureService)
    {
        _lectureService = lectureService;
    }

    [Function("RegenerateStructuredLearningLessonIllustration")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "studio/structured-learning/sections/{sectionId:guid}/professor-lecture/illustration/regenerate")]
        HttpRequestData request,
        Guid sectionId,
        CancellationToken cancellationToken)
    {
        RegenerateStructuredLearningLessonIllustrationRequestDto? body = null;
        try
        {
            body = await JsonSerializer.DeserializeAsync<RegenerateStructuredLearningLessonIllustrationRequestDto>(
                request.Body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }, cancellationToken);
        }
        catch (JsonException)
        {
            // Missing/invalid body falls through to the empty-instructions validation below.
        }

        if (body is null || string.IsNullOrWhiteSpace(body.Instructions))
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InstructionsRequired", "Instructions are required to regenerate the lesson illustration.", cancellationToken);
        }

        try
        {
            byte[]? logoImageBytes = null;
            if (!string.IsNullOrWhiteSpace(body.LogoImageBase64))
            {
                try
                {
                    logoImageBytes = Convert.FromBase64String(body.LogoImageBase64);
                }
                catch (FormatException)
                {
                    return await FunctionResponseFactory.ErrorResponseAsync(
                        request, HttpStatusCode.BadRequest, "InvalidLogoImage", "LogoImageBase64 is not valid base64.", cancellationToken);
                }
            }

            var (illustrationId, content) = await _lectureService.RegenerateLessonIllustrationAsync(
                sectionId, body.Instructions, logoImageBytes, body.ReferenceSourceIllustrationId, cancellationToken);

            return await FunctionResponseFactory.SuccessResponseAsync(request, new RegenerateStructuredLearningLessonIllustrationResponseDto
            {
                SectionId = sectionId,
                IllustrationId = illustrationId,
                Content = content
            }, cancellationToken: cancellationToken);
        }
        catch (KeyNotFoundException ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "SectionNotFound", ex.Message, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "IllustrationRegenerationUnavailable", ex.Message, cancellationToken);
        }
    }
}
