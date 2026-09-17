using System.Net;
using System.Text.Json;
using HumanOS.Agents;
using HumanOS.Contracts.People;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>
/// POST /people/{personId}/resume/extract — asks
/// <see cref="ResumeExtractionAgent"/> (Microsoft Agent Framework) to
/// extract structured résumé/CV fields from raw pasted text. Returns a
/// pure PROPOSAL — the EmployeeDetailPage dashboard shows it as a
/// before/after diff against the currently saved résumé for the HRAdmin
/// to accept/reject field by field; nothing here is ever saved directly
/// to PersonResume (see SavePersonResumeFunction for that).
/// </summary>
public sealed class ExtractPersonResumeFunction
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly ResumeExtractionAgent _agent;

    public ExtractPersonResumeFunction(ResumeExtractionAgent agent)
    {
        _agent = agent;
    }

    [Function("ExtractPersonResume")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "people/{personId:guid}/resume/extract")]
        HttpRequestData request,
        Guid personId,
        CancellationToken cancellationToken)
    {
        if (!_agent.IsConfigured)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.ServiceUnavailable,
                "ExtractionAgentNotConfigured",
                "The Resume extraction agent is not yet configured (missing Azure OpenAI settings).",
                cancellationToken);
        }

        ExtractPersonResumeRequest? body;
        try
        {
            body = await JsonSerializer.DeserializeAsync<ExtractPersonResumeRequest>(request.Body, JsonOptions, cancellationToken);
        }
        catch (JsonException)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidJson", "The request body contains invalid JSON.", cancellationToken);
        }

        if (body is null || string.IsNullOrWhiteSpace(body.ResumeText))
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "ResumeTextRequired", "The résumé text is required.", cancellationToken);
        }

        ResumeExtractionResult result;
        try
        {
            result = await _agent.ExtractAsync(body.ResumeText, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.UnprocessableEntity,
                "ExtractionFailed",
                "The Resume extraction agent could not process this résumé: " + ex.Message,
                cancellationToken);
        }

        var proposal = result.Proposal;
        var response = new ExtractPersonResumeResponse
        {
            StudentAcademicProfile = proposal.StudentAcademicProfile,
            Address = proposal.Address,
            LinkedInUrl = proposal.LinkedInUrl,
            ProfessionalObjective = proposal.ProfessionalObjective,
            ProfessionalSummary = proposal.ProfessionalSummary,
            Skills = proposal.Skills,
            WorkExperience = proposal.WorkExperience,
            Education = proposal.Education,
            Certifications = proposal.Certifications,
            Languages = proposal.Languages,
            ModelName = result.TokenUsage.ModelName,
            InputTokens = result.TokenUsage.InputTokens,
            OutputTokens = result.TokenUsage.OutputTokens,
        };

        return await FunctionResponseFactory.SuccessResponseAsync(request, response, cancellationToken: cancellationToken);
    }
}
