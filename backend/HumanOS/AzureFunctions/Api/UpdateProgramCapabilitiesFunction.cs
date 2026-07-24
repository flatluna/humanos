using System.Net;
using System.Text.Json;
using HumanOS.Contracts.Programs;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

public sealed class UpdateProgramCapabilitiesFunction
{
    private readonly ProgramService _programService;

    public UpdateProgramCapabilitiesFunction(ProgramService programService)
    {
        _programService = programService;
    }

    [Function("UpdateProgramCapabilities")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "programs/{programId:guid}/capabilities")]
        HttpRequestData request,
        Guid programId,
        CancellationToken cancellationToken)
    {
        UpdateProgramCapabilitiesRequest? body;
        try
        {
            body = await JsonSerializer.DeserializeAsync<UpdateProgramCapabilitiesRequest>(
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

        if (body is null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidRequest",
                "Request body is required.", cancellationToken);
        }

        var updated = await _programService.UpdateCapabilitiesAsync(programId, body, cancellationToken);

        if (!updated)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "ProgramNotFound",
                $"No program found with id {programId}.", cancellationToken);
        }

        var program = await _programService.GetByIdAsync(programId, cancellationToken);
        return await FunctionResponseFactory.SuccessResponseAsync(request, program, cancellationToken: cancellationToken);
    }
}
