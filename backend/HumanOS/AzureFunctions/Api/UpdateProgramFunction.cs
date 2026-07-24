using System.Net;
using System.Text.Json;
using HumanOS.Contracts.Programs;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

public sealed class UpdateProgramFunction
{
    private readonly ProgramService _programService;

    public UpdateProgramFunction(ProgramService programService)
    {
        _programService = programService;
    }

    [Function("UpdateProgram")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "programs/{programId:guid}")]
        HttpRequestData request,
        Guid programId,
        CancellationToken cancellationToken)
    {
        SaveProgramRequest? body;
        try
        {
            body = await JsonSerializer.DeserializeAsync<SaveProgramRequest>(
                request.Body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
                cancellationToken);
        }
        catch (JsonException)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidRequest",
                "Request body is not valid JSON.", cancellationToken);
        }

        if (body is null || string.IsNullOrWhiteSpace(body.Name))
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidRequest",
                "'name' is required.", cancellationToken);
        }

        var program = await _programService.UpdateAsync(programId, body, cancellationToken);

        if (program is null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "ProgramNotFound",
                $"No program found with id {programId}.", cancellationToken);
        }

        return await FunctionResponseFactory.SuccessResponseAsync(request, program, cancellationToken: cancellationToken);
    }
}
