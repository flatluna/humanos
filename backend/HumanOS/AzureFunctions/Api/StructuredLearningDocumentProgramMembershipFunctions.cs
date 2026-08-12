using System.Net;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

public sealed class GetStructuredLearningDocumentProgramsFunction
{
    private readonly ProgramService _programService;

    public GetStructuredLearningDocumentProgramsFunction(ProgramService programService)
    {
        _programService = programService;
    }

    [Function("GetStructuredLearningDocumentPrograms")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "studio/structured-learning/{documentId:guid}/programs")]
        HttpRequestData request,
        Guid documentId,
        CancellationToken cancellationToken)
    {
        var memberships = await _programService.GetProgramsForStructuredLearningDocumentAsync(documentId, cancellationToken);
        return await FunctionResponseFactory.SuccessResponseAsync(request, memberships, cancellationToken: cancellationToken);
    }
}

public sealed class AddStructuredLearningDocumentToProgramFunction
{
    private readonly ProgramService _programService;

    public AddStructuredLearningDocumentToProgramFunction(ProgramService programService)
    {
        _programService = programService;
    }

    [Function("AddStructuredLearningDocumentToProgram")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "studio/structured-learning/{documentId:guid}/programs/{programId:guid}")]
        HttpRequestData request,
        Guid documentId,
        Guid programId,
        CancellationToken cancellationToken)
    {
        var attached = await _programService.AttachStructuredLearningDocumentAsync(programId, documentId, cancellationToken);

        if (!attached)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.NotFound,
                "StructuredLearningDocumentProgramNotFound",
                $"No program or structured-learning document was found for programId {programId} and documentId {documentId}.",
                cancellationToken);
        }

        return await FunctionResponseFactory.SuccessResponseAsync(request, new { programId, documentId }, cancellationToken: cancellationToken);
    }
}

public sealed class RemoveStructuredLearningDocumentFromProgramFunction
{
    private readonly ProgramService _programService;

    public RemoveStructuredLearningDocumentFromProgramFunction(ProgramService programService)
    {
        _programService = programService;
    }

    [Function("RemoveStructuredLearningDocumentFromProgram")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "studio/structured-learning/{documentId:guid}/programs/{programId:guid}")]
        HttpRequestData request,
        Guid documentId,
        Guid programId,
        CancellationToken cancellationToken)
    {
        var removed = await _programService.DetachStructuredLearningDocumentAsync(programId, documentId, cancellationToken);

        if (!removed)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.NotFound,
                "StructuredLearningDocumentProgramMembershipNotFound",
                $"No structured-learning/program membership was found for programId {programId} and documentId {documentId}.",
                cancellationToken);
        }

        return await FunctionResponseFactory.SuccessResponseAsync(request, new { programId, documentId }, cancellationToken: cancellationToken);
    }
}
