using System.Net;
using System.Text.Json;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>
/// POST /people/{personId}/tec-milenium/plan/accept-catalog-course — "Mientras estudias"
/// (2026-09-28). Student clicked "Inscribirme" on one agent-recommended catalog course;
/// enrolls them via whichever real service owns that catalog item (Capability vs
/// Structured Learning document — same split as NgoAutomateApp's EnrollMyCourseFunction
/// proxy). No auto-enroll on plan generation — only on this explicit accept action.
/// </summary>
public sealed class AcceptTecMileniumCatalogCourseFunction
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly CapabilityEnrollmentService _capabilityEnrollmentService;
    private readonly StructuredLearningDocumentEnrollmentService _structuredLearningDocumentEnrollmentService;

    public AcceptTecMileniumCatalogCourseFunction(
        CapabilityEnrollmentService capabilityEnrollmentService,
        StructuredLearningDocumentEnrollmentService structuredLearningDocumentEnrollmentService)
    {
        _capabilityEnrollmentService = capabilityEnrollmentService;
        _structuredLearningDocumentEnrollmentService = structuredLearningDocumentEnrollmentService;
    }

    public sealed class Request
    {
        public Guid CatalogItemId { get; set; }

        public string CatalogType { get; set; } = string.Empty;
    }

    [Function("AcceptTecMileniumCatalogCourse")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "people/{personId:guid}/tec-milenium/plan/accept-catalog-course")]
        HttpRequestData request,
        Guid personId,
        CancellationToken cancellationToken)
    {
        Request? body;
        try
        {
            body = await JsonSerializer.DeserializeAsync<Request>(request.Body, JsonOptions, cancellationToken);
        }
        catch (JsonException)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidJson", "The request body contains invalid JSON.", cancellationToken);
        }

        if (body is null || body.CatalogItemId == Guid.Empty)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "CatalogItemRequired", "A 'catalogItemId' is required.", cancellationToken);
        }

        try
        {
            if (string.Equals(body.CatalogType, "StructuredLearningDocument", StringComparison.OrdinalIgnoreCase))
            {
                var result = await _structuredLearningDocumentEnrollmentService.EnrollAsync(personId, body.CatalogItemId, cancellationToken);
                return await FunctionResponseFactory.SuccessResponseAsync(request, result, cancellationToken: cancellationToken);
            }
            else
            {
                var result = await _capabilityEnrollmentService.EnrollAsync(personId, body.CatalogItemId, cancellationToken);
                return await FunctionResponseFactory.SuccessResponseAsync(request, result, cancellationToken: cancellationToken);
            }
        }
        catch (InvalidOperationException ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "CatalogItemNotFound", ex.Message, cancellationToken);
        }
    }
}
