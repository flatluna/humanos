using System.Net;
using System.Text.Json;
using HumanOS.Contracts.People;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>
/// Creates a brand-new Human OS Tenant (customer company) + Person (the
/// signed-in admin) + PersonProfile in one onboarding step, for a real
/// MSAL identity that has never signed into Human OS before (see
/// GetMeFunction — this only runs after that returned 404).
///
/// The Azure OID/TID (identity) come from request headers, matching
/// GetMeFunction, and the email comes from the request body populated
/// by the frontend directly from the active MSAL account — never
/// independently invented server-side.
/// </summary>
public sealed class CreateOnboardingFunction
{
    private readonly TenantService _tenantService;
    private readonly PersonService _personService;
    private readonly PersonProfileService _personProfileService;

    public CreateOnboardingFunction(
        TenantService tenantService,
        PersonService personService,
        PersonProfileService personProfileService)
    {
        _tenantService = tenantService;
        _personService = personService;
        _personProfileService = personProfileService;
    }

    [Function("CreateOnboarding")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "onboarding")]
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

        CreateOnboardingRequest? onboardingRequest;

        try
        {
            onboardingRequest = await JsonSerializer.DeserializeAsync<CreateOnboardingRequest>(
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
            || string.IsNullOrWhiteSpace(onboardingRequest.CompanyName)
            || string.IsNullOrWhiteSpace(onboardingRequest.AdminFirstName)
            || string.IsNullOrWhiteSpace(onboardingRequest.AdminLastName))
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.BadRequest,
                "MissingRequiredFields",
                "CompanyName, AdminFirstName, and AdminLastName are required.",
                cancellationToken);
        }

        // This identity may already have onboarded — never create a
        // second Tenant/Person for the same real Azure identity.
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

        var tenant = await _tenantService.CreateAsync(
            onboardingRequest.CompanyName,
            null,
            null,
            onboardingRequest.CompanyAddress,
            onboardingRequest.CompanyEmail,
            onboardingRequest.CompanyPhone,
            azureTid,
            cancellationToken);

        var person = await _personService.CreateAsync(
            tenant.TenantId,
            azureOid,
            azureTid,
            onboardingRequest.Email,
            cancellationToken);

        await _personProfileService.CreateAsync(
            person.PersonId,
            onboardingRequest.AdminFirstName,
            onboardingRequest.AdminLastName,
            displayName: $"{onboardingRequest.AdminFirstName} {onboardingRequest.AdminLastName}".Trim(),
            phoneNumber: null,
            preferredLanguage: "en",
            countryCode: null,
            timeZone: null,
            profilePhotoUrl: null,
            dateOfBirth: null,
            occupation: null,
            company: onboardingRequest.CompanyName,
            biography: null,
            cancellationToken);

        var response = request.CreateResponse(HttpStatusCode.Created);
        await response.WriteAsJsonAsync(new OnboardingResponse
        {
            PersonId = person.PersonId,
            TenantId = tenant.TenantId,
        });

        return response;
    }
}
