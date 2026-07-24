using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

public static class FunctionResponseFactory
{
    /// <summary>The isolated-worker's default `WriteAsJsonAsync` preserves C#'s
    /// PascalCase property names as-is (confirmed via curl: GetGoals returned
    /// {"GoalId":...,"Code":...}), which doesn't match the camelCase every
    /// frontend (human-os-web, humanlearn, capabilitystudio) assumes for
    /// response DTOs. Serializing explicitly with these options instead of
    /// relying on WriteAsJsonAsync's built-in serializer fixes this without
    /// depending on worker-SDK-version-specific `WorkerOptions.Serializer`
    /// wiring.</summary>
    private static readonly JsonSerializerOptions CamelCaseOptions = new(JsonSerializerDefaults.Web);

    public static async Task<HttpResponseData> ErrorResponseAsync(
        HttpRequestData request,
        HttpStatusCode statusCode,
        string error,
        string message,
        CancellationToken cancellationToken)
    {
        var response = request.CreateResponse(statusCode);
        response.Headers.Add("Content-Type", "application/json; charset=utf-8");
        await response.WriteStringAsync(
            JsonSerializer.Serialize(new { error, message }, CamelCaseOptions),
            cancellationToken);
        return response;
    }

    public static async Task<HttpResponseData> SuccessResponseAsync<T>(
        HttpRequestData request,
        T data,
        HttpStatusCode statusCode = HttpStatusCode.OK,
        CancellationToken cancellationToken = default)
    {
        var response = request.CreateResponse(statusCode);
        response.Headers.Add("Content-Type", "application/json; charset=utf-8");
        await response.WriteStringAsync(JsonSerializer.Serialize(data, CamelCaseOptions), cancellationToken);
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
