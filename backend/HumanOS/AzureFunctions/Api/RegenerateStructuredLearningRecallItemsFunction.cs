using System.Net;
using HumanOS.Data;
using HumanOS.Models.Capabilities.Studio;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.AzureFunctions.Api;

public sealed class RegenerateStructuredLearningRecallItemsResponse
{
    public int ItemCount { get; set; }
}

/// <summary>
/// One-time admin/dev regeneration endpoint (2026-10-26) — the REAL fix for
/// legacy Static-mode sections whose bank predates <c>CorrectAnswer</c>: a
/// blind "guess the answer from the section content alone" backfill (the
/// first attempt at this) turned out unreliable, since legacy items also
/// have no persisted <c>VisualObjectsDescription</c> — there is no ground
/// truth left to infer FROM, and the old question text itself may still
/// have pre-existing issues (e.g. referring to a "child in the picture"
/// instead of asking the student directly) that a blind backfill can never
/// fix either. This instead wipes the section's entire static bank and
/// regenerates it from scratch with the CURRENT (fixed) authoring
/// instructions — one coherent agent call per item produces its question,
/// VisualObjectsDescription, CorrectAnswer, and illustration all together,
/// so everything is guaranteed internally consistent. Safe to call
/// repeatedly; each call fully replaces the bank.
/// </summary>
public sealed class RegenerateStructuredLearningRecallItemsFunction
{
    private readonly StructuredLearningRecallEngine _engine;
    private readonly HumanOS.Agents.Studio.StructuredLearningRecallAgent _agent;
    private readonly HumanOsDbContext _dbContext;

    public RegenerateStructuredLearningRecallItemsFunction(
        StructuredLearningRecallEngine engine, HumanOS.Agents.Studio.StructuredLearningRecallAgent agent, HumanOsDbContext dbContext)
    {
        _engine = engine;
        _agent = agent;
        _dbContext = dbContext;
    }

    [Function("RegenerateStructuredLearningRecallItems")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "studio/structured-learning/sections/{sectionId:guid}/recall/regenerate-items")]
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

        // Clear the old bank AND any in-progress question/attempts for this
        // section first — the old static items are about to be replaced
        // wholesale, so any question currently "in flight" (stored on the
        // step/person-state) or counted attempt no longer corresponds to
        // anything real.
        await _dbContext.StructuredLearningDocumentSections
            .Where(s => s.StructuredLearningDocumentSectionId == sectionId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.StaticRecallItemsJson, (string?)null), cancellationToken);

        await _dbContext.StructuredLearningRecallAttempts
            .Where(a => a.StructuredLearningDocumentSectionId == sectionId)
            .ExecuteDeleteAsync(cancellationToken);

        await _dbContext.StructuredLearningSectionSteps
            .Where(s => s.StructuredLearningDocumentSectionId == sectionId && s.StepType == StructuredLearningStepType.Recall)
            .ExecuteUpdateAsync(
                s => s.SetProperty(x => x.CurrentRecallPrompt, (string?)null)
                      .SetProperty(x => x.CurrentRecallVisualObjectsDescription, (string?)null),
                cancellationToken);

        await _dbContext.StructuredLearningPersonSectionStates
            .Where(p => p.StructuredLearningDocumentSectionId == sectionId)
            .ExecuteUpdateAsync(
                p => p.SetProperty(x => x.CurrentRecallPrompt, (string?)null)
                      .SetProperty(x => x.CurrentRecallVisualObjectsDescription, (string?)null),
                cancellationToken);

        await _engine.EnsureStaticItemsAsync(_dbContext, sectionId, cancellationToken);

        var itemCount = await _dbContext.StructuredLearningDocumentSections
            .AsNoTracking()
            .Where(s => s.StructuredLearningDocumentSectionId == sectionId)
            .Select(s => s.StaticRecallItemsJson)
            .FirstOrDefaultAsync(cancellationToken);

        var count = string.IsNullOrWhiteSpace(itemCount)
            ? 0
            : (System.Text.Json.JsonSerializer.Deserialize<List<object>>(itemCount)?.Count ?? 0);

        return await FunctionResponseFactory.SuccessResponseAsync(request, new RegenerateStructuredLearningRecallItemsResponse
        {
            ItemCount = count
        }, cancellationToken: cancellationToken);
    }
}
