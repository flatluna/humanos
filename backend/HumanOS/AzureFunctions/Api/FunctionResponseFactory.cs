using System.Net;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

public static class FunctionResponseFactory
{
    public static async Task<HttpResponseData> ErrorResponseAsync(
        HttpRequestData request,
        HttpStatusCode statusCode,
        string error,
        string message,
        CancellationToken cancellationToken)
    {
        var response = request.CreateResponse(statusCode);
        await response.WriteAsJsonAsync(new { error, message }, cancellationToken);
        return response;
    }

    public static async Task<HttpResponseData> SuccessResponseAsync<T>(
        HttpRequestData request,
        T data,
        HttpStatusCode statusCode = HttpStatusCode.OK,
        CancellationToken cancellationToken = default)
    {
        var response = request.CreateResponse(statusCode);
        await response.WriteAsJsonAsync(data, cancellationToken);
        return response;
    }

    public static async Task<HttpResponseData> CreatedResponseAsync<T>(
        HttpRequestData request,
        T data,
        CancellationToken cancellationToken = default)
    {
        return await SuccessResponseAsync(request, data, HttpStatusCode.Created, cancellationToken);
    }
}
