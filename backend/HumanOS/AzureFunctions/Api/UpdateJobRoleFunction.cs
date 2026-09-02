using System.Net;
using System.Text.Json;
using HumanOS.Contracts.OrgStructure;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>PUT /job-roles/{jobRoleId} — edits a Job Role's title, level,
/// purpose/summary, responsibilities, required experience, and/or moves
/// it to another Department.</summary>
public sealed class UpdateJobRoleFunction
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly JobRoleService _jobRoleService;

    public UpdateJobRoleFunction(JobRoleService jobRoleService)
    {
        _jobRoleService = jobRoleService;
    }

    [Function("UpdateJobRole")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "job-roles/{jobRoleId:guid}")]
        HttpRequestData request,
        Guid jobRoleId,
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

        try
        {
            var jobRole = await _jobRoleService.UpdateAsync(jobRoleId, body, cancellationToken);

            if (jobRole is null)
            {
                return await FunctionResponseFactory.ErrorResponseAsync(
                    request, HttpStatusCode.NotFound, "JobRoleNotFound", "The requested job role was not found.", cancellationToken);
            }

            return await FunctionResponseFactory.SuccessResponseAsync(request, jobRole, cancellationToken: cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidJobRole", ex.Message, cancellationToken);
        }
        catch (Exception ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.InternalServerError, "UpdateFailed", ex.Message + (ex.InnerException != null ? " --> " + ex.InnerException.Message : string.Empty), cancellationToken);
        }
    }
}
