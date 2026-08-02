using System.Net;
using System.Text.Json;
using HumanOS.Contracts.OrgStructure;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>POST /tenants/{tenantId}/org-chart/apply — creates the
/// Departments and JobRoles from an HRAdmin-confirmed org-chart-import
/// proposal (see ExtractOrgChartFunction/OrgChartExtractionAgent).</summary>
public sealed class ApplyOrgChartFunction
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly OrgChartImportService _importService;

    public ApplyOrgChartFunction(OrgChartImportService importService)
    {
        _importService = importService;
    }

    [Function("ApplyOrgChart")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "tenants/{tenantId:guid}/org-chart/apply")]
        HttpRequestData request,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        ApplyOrgChartRequest? body;
        try
        {
            body = await JsonSerializer.DeserializeAsync<ApplyOrgChartRequest>(request.Body, JsonOptions, cancellationToken);
        }
        catch (JsonException)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidJson", "The request body contains invalid JSON.", cancellationToken);
        }

        if (body is null || body.Departments.Count == 0)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "DepartmentsRequired", "At least one department is required.", cancellationToken);
        }

        try
        {
            var result = await _importService.ApplyAsync(tenantId, body, cancellationToken);
            return await FunctionResponseFactory.SuccessResponseAsync(request, result, cancellationToken: cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidOrgChartImport", ex.Message, cancellationToken);
        }
    }
}
