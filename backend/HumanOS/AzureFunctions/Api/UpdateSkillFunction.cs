using System.Net;
using System.Text.Json;
using HumanOS.Contracts.OrgStructure;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>PUT /skills/{skillId} — renames/re-describes a catalog Skill
/// and/or replaces the full set of Job Roles it's linked to.</summary>
public sealed class UpdateSkillFunction
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly SkillService _skillService;

    public UpdateSkillFunction(SkillService skillService)
    {
        _skillService = skillService;
    }

    [Function("UpdateSkill")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "skills/{skillId:guid}")]
        HttpRequestData request,
        Guid skillId,
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
            var skill = await _skillService.UpdateAsync(skillId, body, cancellationToken);

            if (skill is null)
            {
                return await FunctionResponseFactory.ErrorResponseAsync(
                    request, HttpStatusCode.NotFound, "SkillNotFound", "The requested skill was not found.", cancellationToken);
            }

            return await FunctionResponseFactory.SuccessResponseAsync(request, skill, cancellationToken: cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidSkill", ex.Message, cancellationToken);
        }
    }
}
