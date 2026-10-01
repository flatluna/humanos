using System.Net;
using System.Text.Json;
using HumanOS.Data;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.AzureFunctions.Api;

/// <summary>POST /people/{personId}/content-recommendations/{id}/send-email — "send email"
/// skill (2026-09-29): emails the student a previously saved recommendation report so they
/// can keep a copy, without regenerating it.</summary>
public sealed class SendContentRecommendationEmailFunction
{
    private readonly HumanOsDbContext _dbContext;
    private readonly ContentRecommendationService _service;
    private readonly TecMileniumNotificationEmailService _emailService;

    public SendContentRecommendationEmailFunction(
        HumanOsDbContext dbContext,
        ContentRecommendationService service,
        TecMileniumNotificationEmailService emailService)
    {
        _dbContext = dbContext;
        _service = service;
        _emailService = emailService;
    }

    [Function("SendContentRecommendationEmail")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "people/{personId:guid}/content-recommendations/{savedSearchId:guid}/send-email")]
        HttpRequestData request,
        Guid personId,
        Guid savedSearchId,
        CancellationToken cancellationToken)
    {
        if (!_emailService.IsConfiguredForArbitraryRecipient)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.ServiceUnavailable, "EmailNotConfigured",
                "Email sending is not configured (missing SendGrid settings).", cancellationToken);
        }

        var person = await _dbContext.People.AsNoTracking().FirstOrDefaultAsync(p => p.PersonId == personId, cancellationToken);
        if (person is null || string.IsNullOrWhiteSpace(person.Email))
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "PersonEmailNotFound", "This person has no email on file.", cancellationToken);
        }

        var saved = await _service.GetByIdAsync(personId, savedSearchId, cancellationToken);
        if (saved is null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "SavedSearchNotFound", "This saved search was not found.", cancellationToken);
        }

        var profile = await _dbContext.Set<HumanOS.Models.People.PersonProfile>()
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.PersonId == personId, cancellationToken);
        var personName = profile?.DisplayName
            ?? (profile is not null ? string.Join(' ', new[] { profile.FirstName, profile.LastName }.Where(s => !string.IsNullOrWhiteSpace(s))) : null);
        personName = string.IsNullOrWhiteSpace(personName) ? person.Email : personName;

        var proposal = JsonSerializer.Deserialize<HumanOS.Agents.ContentRecommendationProposal>(saved.ResultJson)
            ?? new HumanOS.Agents.ContentRecommendationProposal();

        await _emailService.SendContentRecommendationsAsync(
            person.Email,
            personName,
            proposal.Title,
            proposal.Summary,
            proposal.TrendsSummary,
            proposal.RecommendedItems
                .Select(i => new TecMileniumNotificationEmailService.RecommendedItem
                {
                    Title = i.Title,
                    Type = i.Type,
                    Reason = i.Reason,
                    Url = i.Url,
                })
                .ToList(),
            cancellationToken);

        return await FunctionResponseFactory.SuccessResponseAsync(request, new { sent = true }, cancellationToken: cancellationToken);
    }
}
