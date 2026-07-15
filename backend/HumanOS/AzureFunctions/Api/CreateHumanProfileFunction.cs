using System.Net;
using System.Text.Json;
using HumanOS.Contracts.People;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

public sealed class CreateHumanProfileFunction
{
    private readonly HumanProfileService _humanProfileService;

    public CreateHumanProfileFunction(HumanProfileService humanProfileService)
    {
        _humanProfileService = humanProfileService;
    }

    [Function("CreateHumanProfile")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "post",
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

            var profile = await _humanProfileService.CreateAsync(
                personId,
                req.MissionStatement,
                req.PrimaryGoal,
                req.LearningStyle,
                req.CurrentLifeStage,
                req.WeeklyAvailabilityHours,
                req.MotivationScore,
                req.ConfidenceScore,
                cancellationToken);

            var response = request.CreateResponse(HttpStatusCode.Created);
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
        catch (KeyNotFoundException)
        {
            return await CreateErrorResponse(request, HttpStatusCode.NotFound, "PersonNotFound", "The requested person was not found.", cancellationToken);
        }
        catch (InvalidOperationException)
        {
            return await CreateErrorResponse(request, HttpStatusCode.Conflict, "HumanProfileAlreadyExists", "A human profile already exists for this person.", cancellationToken);
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
