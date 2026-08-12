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

        try
        {
            var result = await _engine.GetCurrentStateAsync(_dbContext, sectionId, cancellationToken);
            return await FunctionResponseFactory.SuccessResponseAsync(request, new GetStructuredLearningRecallStateResponse
            {
                Started = result.Started,
                NextQuestion = result.NextQuestion,
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
