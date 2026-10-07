using System.Net;
using System.Text.Json;
using HumanOS.Data;
using HumanOS.Models.People;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

public sealed class BuddySessionUsageRequest
{
    public int? DurationSeconds { get; set; }
}

/// <summary>
/// POST humanos/buddy/session-usage (2026-10-01) — the browser reports how
/// long a Buddy Realtime voice call lasted once it ends (see
/// BuddyVoiceModal's teardown), since Buddy connects browser-to-Azure
/// directly over WebRTC and the backend never otherwise sees the call's
/// real duration. See <see cref="BuddySessionUsage"/> and
/// <see cref="TenantBuddyUsageService"/>.
/// </summary>
public sealed class PostBuddySessionUsageFunction
{
    private readonly HumanOsDbContext _dbContext;
    private readonly PersonService _personService;

    public PostBuddySessionUsageFunction(HumanOsDbContext dbContext, PersonService personService)
    {
        _dbContext = dbContext;
        _personService = personService;
    }

    [Function("PostBuddySessionUsage")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "humanos/buddy/session-usage")]
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        BuddySessionUsageRequest? body;
        try
        {
            body = await JsonSerializer.DeserializeAsync<BuddySessionUsageRequest>(
                request.Body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
                cancellationToken);
        }
        catch (JsonException)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidJson", "The request body contains invalid JSON.", cancellationToken);
        }

        var durationSeconds = body?.DurationSeconds ?? 0;
        if (durationSeconds <= 0)
        {
            // Nothing meaningful to log (e.g. the call never actually connected).
            return await FunctionResponseFactory.SuccessResponseAsync(request, new { logged = false }, cancellationToken: cancellationToken);
        }

        Guid? personId = null;
        if (request.Headers.TryGetValues("X-Azure-OID", out var oidValues) && !string.IsNullOrWhiteSpace(oidValues.FirstOrDefault()) &&
            request.Headers.TryGetValues("X-Azure-TID", out var tidValues) && !string.IsNullOrWhiteSpace(tidValues.FirstOrDefault()))
        {
            var person = await _personService.GetByAzureIdentityAsync(oidValues.First(), tidValues.First(), cancellationToken);
            personId = person?.PersonId;
        }

        _dbContext.BuddySessionUsages.Add(new BuddySessionUsage
        {
            PersonId = personId,
            DurationSeconds = durationSeconds,
            CreatedDate = DateTime.UtcNow,
        });
        await _dbContext.SaveChangesAsync(cancellationToken);

        return await FunctionResponseFactory.SuccessResponseAsync(request, new { logged = true }, cancellationToken: cancellationToken);
    }
}
