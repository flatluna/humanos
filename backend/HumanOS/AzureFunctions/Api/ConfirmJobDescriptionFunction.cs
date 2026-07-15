using System.Net;
using HumanOS.Contracts.RoleExperience;
using HumanOS.Data;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.AzureFunctions.Api;

/// <summary>
/// Confirms a previously-extracted Job Description
/// (see ExtractJobDescriptionFunction) and points the employee's
/// PersonProfile at it. This is the "employee reviews and confirms"
/// half of the extract → review → confirm flow — nothing before this
/// call is treated as usable context for a Development Plan.
///
/// TODO: Accept employee corrections in the request body once the
/// review UI supports editing extracted fields before confirming;
/// today this confirms the extraction exactly as returned.
/// </summary>
public sealed class ConfirmJobDescriptionFunction
{
    private readonly HumanOsDbContext _dbContext;

    public ConfirmJobDescriptionFunction(HumanOsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [Function("ConfirmJobDescription")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "post",
            Route = "people/{personId:guid}/job-descriptions/{jobDescriptionId:guid}/confirm")]
        HttpRequestData request,
        Guid personId,
        Guid jobDescriptionId,
        CancellationToken cancellationToken)
    {
        var record = await _dbContext.JobDescriptions
            .SingleOrDefaultAsync(
                jd => jd.JobDescriptionId == jobDescriptionId && jd.PersonId == personId,
                cancellationToken);

        if (record is null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.NotFound,
                "JobDescriptionNotFound",
                "No extracted Job Description was found with that id for this person.",
                cancellationToken);
        }

        if (record.ExtractionStatus != "Extracted")
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.Conflict,
                "NotExtracted",
                $"This Job Description cannot be confirmed from status '{record.ExtractionStatus}'.",
                cancellationToken);
        }

        var profile = await _dbContext.PersonProfiles
            .SingleOrDefaultAsync(p => p.PersonId == personId, cancellationToken);

        if (profile is null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "PersonProfileNotFound", "No profile was found for this person.", cancellationToken);
        }

        var now = DateTime.UtcNow;
        record.ExtractionStatus = "Confirmed";
        record.ConfirmedDate = now;
        record.UpdatedDate = now;

        // The pointer from PersonProfile to its current Job Description
        // is only ever set here, on explicit employee confirmation —
        // never automatically from a raw extraction.
        profile.CurrentJobDescriptionId = record.JobDescriptionId;
        profile.UpdatedDate = now;

        await _dbContext.SaveChangesAsync(cancellationToken);

        var response = new ConfirmJobDescriptionResponse
        {
            JobDescriptionId = record.JobDescriptionId,
            PersonId = personId,
            ExtractionStatus = record.ExtractionStatus,
            ConfirmedDate = now,
        };

        return await FunctionResponseFactory.SuccessResponseAsync(request, response, cancellationToken: cancellationToken);
    }
}
