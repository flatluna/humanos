using System.Net;
using System.Text.Json;
using System.Web;
using HumanOS.Contracts.Studio;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace HumanOS.AzureFunctions.Api;

public sealed class StructuredLearningWorkplaceApplicationFunction
{
    private readonly StructuredLearningWorkplaceApplicationService _service;
    private readonly ILogger<StructuredLearningWorkplaceApplicationFunction> _logger;

    public StructuredLearningWorkplaceApplicationFunction(
        StructuredLearningWorkplaceApplicationService service,
        ILogger<StructuredLearningWorkplaceApplicationFunction> logger)
    {
        _service = service;
        _logger = logger;
    }

    [Function("GetStructuredLearningWorkplaceApplication")]
    public async Task<HttpResponseData> RunGetAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "studio/structured-learning/sections/{sectionId:guid}/workplace-application")]
        HttpRequestData request,
        Guid sectionId,
        CancellationToken cancellationToken)
    {
        var query = HttpUtility.ParseQueryString(request.Url.Query);
        Guid? personId = null;
        if (Guid.TryParse(query["personId"] ?? query["studentId"] ?? query["employeeId"], out var parsedId))
        {
            personId = parsedId;
        }

        var language = query["languageCode"] ?? query["language"] ?? query["targetLanguage"];
        var force = string.Equals(query["force"], "true", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(query["regenerate"], "true", StringComparison.OrdinalIgnoreCase);

        try
        {
            var result = await _service.GetOrCreateAsync(sectionId, personId, language, force, cancellationToken);
            return await FunctionResponseFactory.SuccessResponseAsync(request, result, cancellationToken: cancellationToken);
        }
        catch (KeyNotFoundException ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "SectionNotFound", ex.Message, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get/generate workplace application for section {SectionId}", sectionId);
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.InternalServerError, "WorkplaceApplicationFailed", ex.Message, cancellationToken);
        }
    }

    public sealed class WorkplaceChatRequest
    {
        public Guid? PersonId { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? SessionId { get; set; }
        public string? LanguageCode { get; set; }
    }

    [Function("PostStructuredLearningWorkplaceChat")]
    public async Task<HttpResponseData> RunChatAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "studio/structured-learning/sections/{sectionId:guid}/workplace-application/chat")]
        HttpRequestData request,
        Guid sectionId,
        CancellationToken cancellationToken)
    {
        var body = await JsonSerializer.DeserializeAsync<WorkplaceChatRequest>(
            request.Body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }, cancellationToken);

        if (body is null || string.IsNullOrWhiteSpace(body.Message))
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidRequest", "'message' is required.", cancellationToken);
        }

        try
        {
            var result = await _service.ChatWithMentorAsync(
                sectionId,
                body.PersonId,
                body.Message,
                body.SessionId,
                body.LanguageCode,
                cancellationToken);

            return await FunctionResponseFactory.SuccessResponseAsync(request, result, cancellationToken: cancellationToken);
        }
        catch (KeyNotFoundException ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "SectionNotFound", ex.Message, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process workplace chat for section {SectionId}", sectionId);
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.InternalServerError, "WorkplaceChatFailed", ex.Message, cancellationToken);
        }
    }
}
