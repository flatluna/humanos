using System.Net;
using System.Text.Json;
using HumanOS.Contracts.OrgStructure;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>POST /tenants/{tenantId}/departments — creates an Área/
/// Departamento/Equipo node (HRAdmin-only action, enforced client-side
/// today — see GetMeFunction's trust-headers TODO).</summary>
public sealed class CreateDepartmentFunction
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly DepartmentService _departmentService;

    public CreateDepartmentFunction(DepartmentService departmentService)
    {
        _departmentService = departmentService;
    }

    [Function("CreateDepartment")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "tenants/{tenantId:guid}/departments")]
        HttpRequestData request,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        SaveDepartmentRequest? body;
        try
        {
            body = await JsonSerializer.DeserializeAsync<SaveDepartmentRequest>(request.Body, JsonOptions, cancellationToken);
        }
        catch (JsonException)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidJson", "The request body contains invalid JSON.", cancellationToken);
        }

        if (body is null || string.IsNullOrWhiteSpace(body.Name))
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "NameRequired", "The department name is required.", cancellationToken);
        }

        try
        {
            var department = await _departmentService.CreateAsync(tenantId, body, cancellationToken);
            return await FunctionResponseFactory.CreatedResponseAsync(request, department, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidParentDepartment", ex.Message, cancellationToken);
        }
    }
}
