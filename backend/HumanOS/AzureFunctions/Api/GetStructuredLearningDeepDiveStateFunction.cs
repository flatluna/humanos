using System.Net;
using HumanOS.Data;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

public sealed class GetStructuredLearningDeepDiveStateResponse
{
    public string NextQuestion { get; set; } = string.Empty;
    public int CardIndex { get; set; }
    public int TotalCards { get; set; }
    public bool Complete { get; set; }
}

/// <summary>
/// Bootstrap-safe GET for Deep Dive state (2026-08-10) — same "never
/// grades, safe on every mount" contract as
/// <see cref="GetStructuredLearningRecallStateFunction"/>. See
/// <see cref="StructuredLearningDeepDiveEngine.GetCurrentStateAsync"/>.
/// </summary>
public sealed class GetStructuredLearningDeepDiveStateFunction
{
    private readonly StructuredLearningDeepDiveEngine _engine;
    private readonly HumanOsDbContext _dbContext;

    public GetStructuredLearningDeepDiveStateFunction(StructuredLearningDeepDiveEngine engine, HumanOsDbContext dbContext)
    {
        _engine = engine;
        _dbContext = dbContext;
    }

    [Function("GetStructuredLearningDeepDiveState")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "studio/structured-learning/sections/{sectionId:guid}/deepdive/state")]
        HttpRequestData request,
        Guid sectionId,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _engine.GetCurrentStateAsync(_dbContext, sectionId, cancellationToken);
            return await FunctionResponseFactory.SuccessResponseAsync(request, new GetStructuredLearningDeepDiveStateResponse
            {
                NextQuestion = result.Message,
                CardIndex = result.CardIndex,
                TotalCards = result.TotalCards,
                Complete = result.Complete
            }, cancellationToken: cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidDeepDiveState", ex.Message, cancellationToken);
        }
    }
}
