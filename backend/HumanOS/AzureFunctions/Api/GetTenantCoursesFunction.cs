using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>GET /tenants/{tenantId}/courses — one card per course actually
/// created by this tenant (Capability.OwnerTenantId or
/// StructuredLearningDocument.TenantId), each flagged with IsGlobal — see
/// <see cref="TenantCoursesService"/> doc comment for exact scope.</summary>
public sealed class GetTenantCoursesFunction
{
    private readonly TenantCoursesService _coursesService;

    public GetTenantCoursesFunction(TenantCoursesService coursesService)
    {
        _coursesService = coursesService;
    }

    [Function("GetTenantCourses")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "tenants/{tenantId:guid}/courses")]
        HttpRequestData request,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var courses = await _coursesService.GetTenantCoursesAsync(tenantId, cancellationToken);
        return await FunctionResponseFactory.SuccessResponseAsync(request, courses, cancellationToken: cancellationToken);
    }
}
