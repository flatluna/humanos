using System.Net;
using System.Text.Json;
using HumanOS.Contracts.OrgStructure;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>PUT /departments/{departmentId} — renames/re-parents a
/// Department node.</summary>
public sealed class UpdateDepartmentFunction
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly DepartmentService _departmentService;

    public UpdateDepartmentFunction(DepartmentService departmentService)
    {
        _departmentService = departmentService;
    }

    [Function("UpdateDepartment")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "departments/{departmentId:guid}")]
        HttpRequestData request,
        Guid departmentId,
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
            var department = await _departmentService.UpdateAsync(departmentId, body, cancellationToken);

            if (department is null)
            {
                return await FunctionResponseFactory.ErrorResponseAsync(
                    request, HttpStatusCode.NotFound, "DepartmentNotFound", "The requested department was not found.", cancellationToken);
            }

            return await FunctionResponseFactory.SuccessResponseAsync(request, department, cancellationToken: cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidDepartmentUpdate", ex.Message, cancellationToken);
        }
    }
}
