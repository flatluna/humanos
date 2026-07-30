using System.Net;
using System.Text.Json;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>Body for PUT /api/people/{personId}/profile/language.</summary>
public sealed class UpdatePreferredLanguageRequest
{
    public string PreferredLanguage { get; set; } = null!;
}

/// <summary>
/// Lightweight, single-field counterpart to UpdatePersonProfileFunction —
/// used by the frontend's language switcher (see LanguageSwitcher.tsx) to
/// persist a EN/ES change to the account so it "sticks" across devices/
/// sessions, WITHOUT requiring the caller to resend the entire profile
/// (which the full PUT /people/{personId}/profile endpoint would
/// otherwise overwrite with whatever partial data the caller has in
/// memory at that moment).
/// </summary>
public sealed class UpdatePreferredLanguageFunction
{
    private readonly PersonProfileService _personProfileService;

    public UpdatePreferredLanguageFunction(PersonProfileService personProfileService)
    {
        _personProfileService = personProfileService;
    }

    [Function("UpdatePreferredLanguage")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "put",
            Route = "people/{personId:guid}/profile/language")]
        HttpRequestData request,
        Guid personId,
        CancellationToken cancellationToken)
    {
        // TODO: Derive PersonId from the validated Microsoft Entra token.
        UpdatePreferredLanguageRequest? req;

        try
        {
            req = await JsonSerializer.DeserializeAsync<UpdatePreferredLanguageRequest>(
                request.Body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
                cancellationToken);
        }
        catch (JsonException)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidJson", "The request body contains invalid JSON.", cancellationToken);
        }

        if (req is null || string.IsNullOrWhiteSpace(req.PreferredLanguage))
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "MissingPreferredLanguage", "PreferredLanguage is required.", cancellationToken);
        }

        try
        {
            var profile = await _personProfileService.UpdatePreferredLanguageAsync(
                personId, req.PreferredLanguage, cancellationToken);

            if (profile is null)
            {
                return await FunctionResponseFactory.ErrorResponseAsync(
                    request, HttpStatusCode.NotFound, "PersonProfileNotFound", "The requested person profile was not found.", cancellationToken);
            }

            var response = request.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new { profile.PersonId, profile.PreferredLanguage }, cancellationToken);
            return response;
        }
        catch (ArgumentException ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "UnsupportedLanguage", ex.Message, cancellationToken);
        }
    }
}
