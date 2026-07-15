using System.Net;
using System.Text.Json;
using HumanOS.Agents;
using HumanOS.Contracts.RoleExperience;
using HumanOS.Data;
using HumanOS.Models.JobDescriptions;
using HumanOS.Storage;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.AzureFunctions.Api;

/// <summary>
/// Extracts structured Job Description fields from a previously-uploaded
/// PDF (see UploadRoleDocumentFunction, documentType: "job-description")
/// using a real LLM via <see cref="JobDescriptionExtractionAgent"/>
/// (Microsoft Agent Framework). Saves the result as a provisional
/// <see cref="JobDescriptionRecord"/> — nothing here is confirmed as
/// usable context until ConfirmJobDescriptionFunction is called.
///
/// TODO: Derive PersonId from the validated Microsoft Entra token
/// instead of trusting the route value, once Entra auth is wired up.
/// </summary>
public sealed class ExtractJobDescriptionFunction
{
    private readonly HumanOsDbContext _dbContext;
    private readonly RoleDocumentStorageService _roleDocumentStorageService;
    private readonly JobDescriptionExtractionAgent _extractionAgent;

    public ExtractJobDescriptionFunction(
        HumanOsDbContext dbContext,
        RoleDocumentStorageService roleDocumentStorageService,
        JobDescriptionExtractionAgent extractionAgent)
    {
        _dbContext = dbContext;
        _roleDocumentStorageService = roleDocumentStorageService;
        _extractionAgent = extractionAgent;
    }

    [Function("ExtractJobDescription")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "post",
            Route = "people/{personId:guid}/job-descriptions/extract")]
        HttpRequestData request,
        Guid personId,
        CancellationToken cancellationToken)
    {
        if (!_roleDocumentStorageService.IsConfigured)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.ServiceUnavailable,
                "DataLakeNotConfigured",
                "Document storage is not yet configured for this environment.",
                cancellationToken);
        }

        if (!_extractionAgent.IsConfigured)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.ServiceUnavailable,
                "ExtractionAgentNotConfigured",
                "The Job Description extraction agent is not yet configured (missing Azure OpenAI settings).",
                cancellationToken);
        }

        var person = await _dbContext.People
            .AsNoTracking()
            .SingleOrDefaultAsync(p => p.PersonId == personId, cancellationToken);

        if (person is null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "PersonNotFound", "No person was found with that id.", cancellationToken);
        }

        ExtractJobDescriptionRequest? extractRequest;

        try
        {
            extractRequest = await JsonSerializer.DeserializeAsync<ExtractJobDescriptionRequest>(
                request.Body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
                cancellationToken);
        }
        catch (JsonException)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidJson", "The request body contains invalid JSON.", cancellationToken);
        }

        if (extractRequest is null || string.IsNullOrWhiteSpace(extractRequest.StoragePath))
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "StoragePathRequired", "storagePath is required.", cancellationToken);
        }

        JobDescriptionExtractionResult extraction;

        try
        {
            await using var pdfStream = await _roleDocumentStorageService.DownloadJobDescriptionAsync(
                person.TenantId, extractRequest.StoragePath, cancellationToken);

            var text = PdfTextExtractor.ExtractText(pdfStream);

            extraction = await _extractionAgent.ExtractAsync(text, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var failedRecord = new JobDescriptionRecord
            {
                TenantId = person.TenantId,
                PersonId = personId,
                SourceStoragePath = extractRequest.StoragePath,
                SourceFileName = extractRequest.FileName,
                SourceUploadedDate = DateTime.UtcNow,
                JobTitle = string.Empty,
                ExtractionStatus = "Failed",
                ExtractionModel = _extractionAgent.DeploymentName,
                RawExtractionJson = ex.Message,
                CreatedDate = DateTime.UtcNow,
                UpdatedDate = DateTime.UtcNow,
            };
            _dbContext.JobDescriptions.Add(failedRecord);
            await _dbContext.SaveChangesAsync(cancellationToken);

            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.UnprocessableEntity,
                "ExtractionFailed",
                "The Job Description document could not be read or extracted.",
                cancellationToken);
        }

        var now = DateTime.UtcNow;
        var record = new JobDescriptionRecord
        {
            TenantId = person.TenantId,
            PersonId = personId,
            SourceStoragePath = extractRequest.StoragePath,
            SourceFileName = extractRequest.FileName,
            SourceUploadedDate = now,
            JobTitle = extraction.JobTitle,
            RolePurpose = extraction.RolePurpose,
            RoleSummary = extraction.RoleSummary,
            PrimaryResponsibilitiesJson = JsonSerializer.Serialize(extraction.PrimaryResponsibilities),
            ExpectedOutcomesJson = JsonSerializer.Serialize(extraction.ExpectedOutcomes),
            RequiredExperience = extraction.RequiredExperience,
            ToolsMentionedJson = JsonSerializer.Serialize(extraction.ToolsMentioned),
            SuggestedProfessionalLevel = extraction.SuggestedProfessionalLevel,
            ExtractionStatus = "Extracted",
            ExtractionModel = _extractionAgent.DeploymentName,
            RawExtractionJson = JsonSerializer.Serialize(extraction),
            ExtractedDate = now,
            CreatedDate = now,
            UpdatedDate = now,
        };

        _dbContext.JobDescriptions.Add(record);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var response = new JobDescriptionExtractionResponse
        {
            JobDescriptionId = record.JobDescriptionId,
            ExtractionStatus = record.ExtractionStatus,
            JobTitle = record.JobTitle,
            RolePurpose = record.RolePurpose,
            RoleSummary = record.RoleSummary,
            PrimaryResponsibilities = extraction.PrimaryResponsibilities,
            ExpectedOutcomes = extraction.ExpectedOutcomes,
            RequiredExperience = record.RequiredExperience,
            ToolsMentioned = extraction.ToolsMentioned,
            SuggestedProfessionalLevel = record.SuggestedProfessionalLevel,
            ExtractionModel = record.ExtractionModel ?? string.Empty,
            ExtractedDate = record.ExtractedDate ?? now,
        };

        return await FunctionResponseFactory.CreatedResponseAsync(request, response, cancellationToken);
    }
}
