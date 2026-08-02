using System.Net;
using System.Text.Json;
using HumanOS.Contracts.OrgStructure;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>POST /tenants/{tenantId}/job-roles — creates an org-authored
/// Job Role ("puesto") template inside a Department.</summary>
public sealed class CreateJobRoleFunction
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly JobRoleService _jobRoleService;

    public CreateJobRoleFunction(JobRoleService jobRoleService)
    {
        _jobRoleService = jobRoleService;
    }

    [Function("CreateJobRole")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "tenants/{tenantId:guid}/job-roles")]
        HttpRequestData request,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        SaveJobRoleRequest? body;
        try
        {
            body = await JsonSerializer.DeserializeAsync<SaveJobRoleRequest>(request.Body, JsonOptions, cancellationToken);
        }
        catch (JsonException)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidJson", "The request body contains invalid JSON.", cancellationToken);
        }

        if (body is null || string.IsNullOrWhiteSpace(body.Title))
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "TitleRequired", "The job role title is required.", cancellationToken);
        }

        if (body.DepartmentId == Guid.Empty)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "DepartmentIdRequired", "A departmentId is required.", cancellationToken);
        }

        try
        {
            var jobRole = await _jobRoleService.CreateAsync(tenantId, body, cancellationToken);
            return await FunctionResponseFactory.CreatedResponseAsync(request, jobRole, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidDepartment", ex.Message, cancellationToken);
        }
    }
}
