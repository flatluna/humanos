using System.Net;
using HumanOS.Data;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

public sealed class GetStructuredLearningRecallStateResponse
{
    public bool Started { get; set; }
    public string NextQuestion { get; set; } = string.Empty;
    public string? VisualObjectsDescription { get; set; }

    /// <summary>Static-mode card id (2026-10-26) — when non-null, fetch the
    /// image from <c>GET /illustrations/structured-learning/{id}/image</c>
    /// and show it alongside <see cref="NextQuestion"/>.</summary>
    public Guid? IllustrationId { get; set; }

    /// <summary>Short, literal correct answer to <see cref="NextQuestion"/>
    /// (2026-10-26) — embedded into the Realtime voice Agent's own session
    /// instructions so it can react INSTANTLY to the student's spoken
    /// answer itself, without waiting on a server round-trip. Null in
    /// Dynamic mode / for legacy Static items.</summary>
    public string? CorrectAnswer { get; set; }
    public int AttemptsUsedForItem { get; set; }
    public int ItemsMastered { get; set; }
    public int ItemsRequired { get; set; }
    public bool Complete { get; set; }
}

/// <summary>
/// Bootstrap-safe GET for Recall state (2026-08-10) — fixes a real 400
/// error: the frontend used to POST .../recall/attempt with no
/// StudentResponse on EVERY mount, assuming it was always the section's
/// very first turn, which broke on any remount after the first question
/// had already been asked. This endpoint never grades anything — see
/// <see cref="StructuredLearningRecallEngine.GetCurrentStateAsync"/>.
/// </summary>
public sealed class GetStructuredLearningRecallStateFunction
{
    private readonly StructuredLearningRecallEngine _engine;
    private readonly HumanOS.Agents.Studio.StructuredLearningRecallAgent _agent;
    private readonly HumanOsDbContext _dbContext;

    public GetStructuredLearningRecallStateFunction(
        StructuredLearningRecallEngine engine, HumanOS.Agents.Studio.StructuredLearningRecallAgent agent, HumanOsDbContext dbContext)
    {
        _engine = engine;
        _agent = agent;
        _dbContext = dbContext;
    }

    [Function("GetStructuredLearningRecallState")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "studio/structured-learning/sections/{sectionId:guid}/recall/state")]
        HttpRequestData request,
        Guid sectionId,
        CancellationToken cancellationToken)
    {
        if (!_agent.IsConfigured)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.ServiceUnavailable, "StructuredLearningRecallAgentNotConfigured",
                "StructuredLearningRecallAgent is not configured (missing Azure OpenAI settings).", cancellationToken);
        }

        var query = System.Web.HttpUtility.ParseQueryString(request.Url.Query);
        Guid? personId = Guid.TryParse(query["personId"], out var parsedPersonId) ? parsedPersonId : null;
        Guid? engramSessionId = Guid.TryParse(query["engramSessionId"], out var parsedSessionId) ? parsedSessionId : null;

        try
        {
            var result = await _engine.GetCurrentStateAsync(_dbContext, sectionId, personId, engramSessionId, cancellationToken);
            return await FunctionResponseFactory.SuccessResponseAsync(request, new GetStructuredLearningRecallStateResponse
            {
                Started = result.Started,
                NextQuestion = result.NextQuestion,
                VisualObjectsDescription = result.VisualObjectsDescription,
                IllustrationId = result.IllustrationId,
                CorrectAnswer = result.CorrectAnswer,
                AttemptsUsedForItem = result.AttemptsUsedForItem,
                ItemsMastered = result.ItemsMastered,
                ItemsRequired = result.ItemsRequired,
                Complete = result.Complete
            }, cancellationToken: cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidRecallState", ex.Message, cancellationToken);
        }
    }
}
