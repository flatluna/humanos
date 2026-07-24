using System.Net;
using System.Text.Json;
using HumanOS.Contracts.Programs;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

public sealed class CreateProgramFunction
{
    private readonly ProgramService _programService;

    public CreateProgramFunction(ProgramService programService)
    {
        _programService = programService;
    }

    [Function("CreateProgram")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "programs")]
        HttpRequestData request,
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

        var program = await _programService.CreateAsync(body, cancellationToken);

        return await FunctionResponseFactory.CreatedResponseAsync(request, program, cancellationToken);
    }
}
