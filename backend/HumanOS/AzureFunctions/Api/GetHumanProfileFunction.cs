using System.Net;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

public sealed class GetHumanProfileFunction
{
    private readonly HumanProfileService _humanProfileService;

    public GetHumanProfileFunction(HumanProfileService humanProfileService)
    {
        _humanProfileService = humanProfileService;
    }

    [Function("GetHumanProfile")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "get",
            Route = "people/{personId:guid}/human-profile")]
        HttpRequestData request,
        Guid personId,
        CancellationToken cancellationToken)
    {
        // TODO: Derive PersonId from the validated Microsoft Entra token.
        var profile = await _humanProfileService.GetByPersonIdAsync(personId, cancellationToken);

        if (profile is null)
        {
            var notFound = request.CreateResponse(HttpStatusCode.NotFound);
            await notFound.WriteAsJsonAsync(
                new { error = "HumanProfileNotFound", message = "The requested human profile was not found." },
                cancellationToken);
            return notFound;
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
}
