using System.Net;
using System.Text.Json;
using HumanOS.Contracts.OrgStructure;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>POST /tenants/{tenantId}/skills — creates a catalog Skill,
/// optionally linked to one or many Job Roles right away.</summary>
public sealed class CreateSkillFunction
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly SkillService _skillService;

    public CreateSkillFunction(SkillService skillService)
    {
        _skillService = skillService;
    }

    [Function("CreateSkill")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "tenants/{tenantId:guid}/skills")]
        HttpRequestData request,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        SaveSkillRequest? body;
        try
        {
            body = await JsonSerializer.DeserializeAsync<SaveSkillRequest>(request.Body, JsonOptions, cancellationToken);
        }
        catch (JsonException)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidJson", "The request body contains invalid JSON.", cancellationToken);
        }

        if (body is null || string.IsNullOrWhiteSpace(body.Name))
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "NameRequired", "The skill name is required.", cancellationToken);
        }

        try
        {
            var skill = await _skillService.CreateAsync(tenantId, body, cancellationToken);
            return await FunctionResponseFactory.CreatedResponseAsync(request, skill, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidSkill", ex.Message, cancellationToken);
        }
    }
}
