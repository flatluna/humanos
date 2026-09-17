using System.Net;
using System.Text.Json;
using HumanOS.Contracts.People;
using HumanOS.Models.People;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>PUT /people/{personId}/org-assignment — an HRAdmin assigning/
/// changing a person's Department, JobRole, Manager, and/or permission
/// tier (see PersonOrgAssignmentRequest's doc comment).</summary>
public sealed class UpdatePersonOrgAssignmentFunction
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly PersonService _personService;

    public UpdatePersonOrgAssignmentFunction(PersonService personService)
    {
        _personService = personService;
    }

    [Function("UpdatePersonOrgAssignment")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "people/{personId:guid}/org-assignment")]
        HttpRequestData request,
        Guid personId,
        CancellationToken cancellationToken)
    {
        PersonOrgAssignmentRequest? body;
        try
        {
            body = await JsonSerializer.DeserializeAsync<PersonOrgAssignmentRequest>(request.Body, JsonOptions, cancellationToken);
        }
        catch (JsonException)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidJson", "The request body contains invalid JSON.", cancellationToken);
        }

        if (body is null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "RequestBodyRequired", "A request body is required.", cancellationToken);
        }

        PersonOrgRole? orgRole = null;
        if (!string.IsNullOrWhiteSpace(body.OrgRole))
        {
            if (!Enum.TryParse<PersonOrgRole>(body.OrgRole, ignoreCase: true, out var parsedRole))
            {
                return await FunctionResponseFactory.ErrorResponseAsync(
                    request, HttpStatusCode.BadRequest, "InvalidOrgRole", "OrgRole must be Employee, Manager, or HRAdmin.", cancellationToken);
            }
            orgRole = parsedRole;
        }

        try
        {
            var person = await _personService.UpdateOrgAssignmentAsync(
                personId,
                body.DepartmentId,
                body.JobRoleId,
                body.ManagerId,
                orgRole,
                body.Email,
                body.DisplayName,
                body.PhoneNumber,
                body.Address,
                body.LinkedInUrl,
                cancellationToken);

            if (person is null)
            {
                return await FunctionResponseFactory.ErrorResponseAsync(
                    request, HttpStatusCode.NotFound, "PersonNotFound", "The requested person was not found.", cancellationToken);
            }

            return await FunctionResponseFactory.SuccessResponseAsync(request, person, cancellationToken: cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidOrgAssignment", ex.Message, cancellationToken);
        }
    }
}
