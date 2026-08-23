using System.Net;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>
/// Reverts one video to the version it had before its last Trim/Cut edit
/// (2026-08-22) — see <see cref="StructuredLearningVideoOrchestrator.UndoLastEditAsync"/>.
/// One level of undo, no history stack.
/// </summary>
public sealed class UndoStructuredLearningVideoEditFunction
{
    private readonly StructuredLearningVideoOrchestrator _orchestrator;

    public UndoStructuredLearningVideoEditFunction(StructuredLearningVideoOrchestrator orchestrator)
    {
        _orchestrator = orchestrator;
    }

    [Function("UndoStructuredLearningVideoEdit")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "studio/structured-learning/videos/{illustrationId:guid}/undo")]
        HttpRequestData request,
        Guid illustrationId,
        CancellationToken cancellationToken)
    {
        try
        {
            await _orchestrator.UndoLastEditAsync(illustrationId, cancellationToken);
            return await FunctionResponseFactory.SuccessResponseAsync(request, new { undone = true }, cancellationToken: cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "NothingToUndo", ex.Message, cancellationToken);
        }
    }
}
