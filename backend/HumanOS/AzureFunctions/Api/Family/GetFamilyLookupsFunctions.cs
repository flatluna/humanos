using System.Net;
using HumanOS.Services.Family;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api.Family;

/// <summary>
/// GET /family/lookups/countries
/// Global catalog of supported countries for the Subject Capability Recommender.
/// </summary>
public sealed class GetCountriesFunction
{
    private readonly FamilyCapabilityService _capabilityService;

    public GetCountriesFunction(FamilyCapabilityService capabilityService)
    {
        _capabilityService = capabilityService;
    }

    [Function("GetCountries")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "family/lookups/countries")]
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var list = await _capabilityService.ListCountriesAsync(cancellationToken);
        return await FunctionResponseFactory.SuccessResponseAsync(request, list, HttpStatusCode.OK, cancellationToken);
    }
}

/// <summary>
/// GET /family/lookups/education-system-types
/// Global catalog of education system types (Public, Montessori, IB, etc.).
/// </summary>
public sealed class GetEducationSystemTypesFunction
{
    private readonly FamilyCapabilityService _capabilityService;

    public GetEducationSystemTypesFunction(FamilyCapabilityService capabilityService)
    {
        _capabilityService = capabilityService;
    }

    [Function("GetEducationSystemTypes")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "family/lookups/education-system-types")]
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var list = await _capabilityService.ListEducationSystemTypesAsync(cancellationToken);
        return await FunctionResponseFactory.SuccessResponseAsync(request, list, HttpStatusCode.OK, cancellationToken);
    }
}

/// <summary>
/// GET /family/lookups/subjects
/// Global catalog of subjects: Academic (taught in school) and Character (life skills not formally taught).
/// </summary>
public sealed class GetSubjectsFunction
{
    private readonly FamilyCapabilityService _capabilityService;

    public GetSubjectsFunction(FamilyCapabilityService capabilityService)
    {
        _capabilityService = capabilityService;
    }

    [Function("GetFamilySubjects")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "family/lookups/subjects")]
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var list = await _capabilityService.ListSubjectsAsync(cancellationToken);
        return await FunctionResponseFactory.SuccessResponseAsync(request, list, HttpStatusCode.OK, cancellationToken);
    }
}
