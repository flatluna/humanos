using System.Net;
using System.Text.Json;
using HumanOS.Contracts.People;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>
/// Creates a Person (+ PersonProfile) with NO Tenant, for an individual
/// learner signing up on Engram Academy without an organization — see
/// CreateOnboardingFunction for the company/Tenant-creating counterpart.
/// A person who signs up this way starts as a student with no Tenant;
/// Engram Studio (capability authoring) access is unlocked later once
/// the person reaches the "Creator" Human Evolution Layer, not through
/// this signup flow (see human-os-core-philosophy.md's six Evolution
/// Layers) — that gating is not implemented yet.
///
/// The Azure OID/TID (identity) come from request headers, matching
/// GetMeFunction/CreateOnboardingFunction, and the email comes from the
/// request body populated by the frontend directly from the active MSAL
/// account — never independently invented server-side.
/// </summary>
public sealed class CreateIndividualOnboardingFunction
{
    private readonly PersonService _personService;
    private readonly PersonProfileService _personProfileService;

    public CreateIndividualOnboardingFunction(
        PersonService personService,
        PersonProfileService personProfileService)
    {
        _personService = personService;
        _personProfileService = personProfileService;
    }

    [Function("CreateIndividualOnboarding")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "onboarding/individual")]
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        if (!request.Headers.TryGetValues("X-Azure-OID", out var oidValues) || string.IsNullOrWhiteSpace(oidValues.FirstOrDefault()))
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "MissingAzureOid", "The X-Azure-OID header is required.", cancellationToken);
        }

        if (!request.Headers.TryGetValues("X-Azure-TID", out var tidValues) || string.IsNullOrWhiteSpace(tidValues.FirstOrDefault()))
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "MissingAzureTid", "The X-Azure-TID header is required.", cancellationToken);
        }

        var azureOid = oidValues.First();
        var azureTid = tidValues.First();

        CreateIndividualOnboardingRequest? onboardingRequest;

        try
        {
            onboardingRequest = await JsonSerializer.DeserializeAsync<CreateIndividualOnboardingRequest>(
                request.Body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
                cancellationToken);
        }
        catch (JsonException)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidJson", "The request body contains invalid JSON.", cancellationToken);
        }

        if (onboardingRequest is null
            || string.IsNullOrWhiteSpace(onboardingRequest.FirstName)
            || string.IsNullOrWhiteSpace(onboardingRequest.LastName))
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.BadRequest,
                "MissingRequiredFields",
                "FirstName and LastName are required.",
                cancellationToken);
        }

        // This identity may already have onboarded — never create a
        // second Person for the same real Azure identity.
        var existingPerson = await _personService.GetByAzureIdentityAsync(azureOid, azureTid, cancellationToken);
        if (existingPerson is not null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.Conflict,
                "AlreadyOnboarded",
                "This identity has already completed onboarding.",
                cancellationToken);
        }

        var person = await _personService.CreateAsync(
            tenantId: null,
            azureOid,
            azureTid,
            onboardingRequest.Email,
            cancellationToken);

        await _personProfileService.CreateAsync(
            person.PersonId,
            onboardingRequest.FirstName,
            onboardingRequest.LastName,
            displayName: $"{onboardingRequest.FirstName} {onboardingRequest.LastName}".Trim(),
            phoneNumber: null,
            preferredLanguage: "en",
            countryCode: null,
            timeZone: null,
            profilePhotoUrl: null,
            dateOfBirth: null,
            occupation: null,
            company: null,
            biography: null,
            cancellationToken);

        var response = request.CreateResponse(HttpStatusCode.Created);
        await response.WriteAsJsonAsync(new OnboardingResponse
        {
            PersonId = person.PersonId,
            TenantId = null,
        });

        return response;
    }
}
