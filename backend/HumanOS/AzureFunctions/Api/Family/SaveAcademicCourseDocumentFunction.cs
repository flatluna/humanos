using System.Net;
using System.Text.Json;
using HumanOS.Contracts.Family;
using HumanOS.Services.Family;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api.Family;

/// <summary>
/// POST /family/academic/courses
/// Persists (upsert, keyed by CourseCode) the complete academic course document authored by the
/// ProfesorExpert AI agent — this is what the "Aprobar y Guardar Curso en Base de Datos" button in
/// the Academic Course Generator UI actually calls.
/// </summary>
public sealed class SaveAcademicCourseDocumentFunction
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private readonly FamilyCapabilityService _capabilityService;

    public SaveAcademicCourseDocumentFunction(FamilyCapabilityService capabilityService)
    {
        _capabilityService = capabilityService;
    }

    [Function("SaveAcademicCourseDocument")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "family/academic/courses")]
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        SaveAcademicCourseDocumentRequest? body;
        try
        {
            body = await JsonSerializer.DeserializeAsync<SaveAcademicCourseDocumentRequest>(request.Body, JsonOptions, cancellationToken);
        }
        catch (JsonException ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidJson", $"Malformed JSON payload: {ex.Message}", cancellationToken);
        }

        if (body is null || string.IsNullOrWhiteSpace(body.CourseCode) || string.IsNullOrWhiteSpace(body.CourseDocumentMarkdown))
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "MissingRequiredFields",
                "CourseCode and CourseDocumentMarkdown are required.", cancellationToken);
        }

        try
        {
            var result = await _capabilityService.SaveAcademicCourseDocumentAsync(body, cancellationToken);
            return await FunctionResponseFactory.CreatedResponseAsync(request, result, cancellationToken);
        }
        catch (Exception ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.InternalServerError, "SaveFailed", ex.Message, cancellationToken);
        }
    }
}
