using System.Net;
using System.Text.Json;
using HumanOS.Contracts.People;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

public sealed class UpdateHumanProfileFunction
{
    private readonly HumanProfileService _humanProfileService;

    public UpdateHumanProfileFunction(HumanProfileService humanProfileService)
    {
        _humanProfileService = humanProfileService;
    }

    [Function("UpdateHumanProfile")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "put",
            Route = "people/{personId:guid}/human-profile")]
        HttpRequestData request,
        Guid personId,
        CancellationToken cancellationToken)
    {
        // TODO: Derive PersonId from the validated Microsoft Entra token.
        try
        {
            var req = await JsonSerializer.DeserializeAsync<UpsertHumanProfileRequest>(
                request.Body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
                cancellationToken);

            if (req is null)
            {
                return await CreateErrorResponse(request, HttpStatusCode.BadRequest, "RequestBodyRequired", "Request body is required.", cancellationToken);
            }

            var profile = await _humanProfileService.UpdateAsync(
                personId,
                req.MissionStatement,
                req.PrimaryGoal,
                req.LearningStyle,
                req.CurrentLifeStage,
                req.WeeklyAvailabilityHours,
                req.MotivationScore,
                req.ConfidenceScore,
                cancellationToken);

            if (profile is null)
            {
                return await CreateErrorResponse(request, HttpStatusCode.NotFound, "HumanProfileNotFound", "The requested human profile was not found.", cancellationToken);
            }

            var response = request.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(
                new
                {
                    profile.HumanProfileId,
                    profile.PersonId,
                    profile.MissionStatement,
                    profile.PrimaryGoal,
                    profile.LearningStyle,
                    profile.CurrentLifeStage,
                    profile.WeeklyAvailabilityHours,
                    profile.MotivationScore,
                    profile.ConfidenceScore,
                    profile.CreatedDate,
                    profile.UpdatedDate
                },
                cancellationToken);

            return response;
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return await CreateErrorResponse(request, HttpStatusCode.BadRequest, "InvalidHumanProfileValue", ex.Message, cancellationToken);
        }
    }

    private static async Task<HttpResponseData> CreateErrorResponse(
        HttpRequestData request,
        HttpStatusCode statusCode,
        string error,
        string message,
        CancellationToken cancellationToken)
    {
        var response = request.CreateResponse(statusCode);
        await response.WriteAsJsonAsync(new { error, message }, cancellationToken);
        return response;
    }
}
