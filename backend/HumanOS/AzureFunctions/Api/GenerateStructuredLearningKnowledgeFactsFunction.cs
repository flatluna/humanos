using System.Net;
using HumanOS.Data;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.AzureFunctions.Api;

public sealed class GenerateStructuredLearningKnowledgeFactsResponseDto
{
    public Guid EngramId { get; set; }
    public int KnowledgeFactsCreated { get; set; }
    public int MissingGraphFactsCount { get; set; }
}

/// <summary>Manual "Regenerar Knowledge Facts" trigger for one Engram
/// (Studio Edit mode) — replays Step 5 (StructuredLearningKnowledgeFactGenerationService)
/// on demand, e.g. after tightening the generation agent's own instructions,
/// without re-running the whole pedagogy pipeline.</summary>
public sealed class GenerateStructuredLearningKnowledgeFactsFunction
{
    private readonly StructuredLearningKnowledgeFactGenerationService _generationService;
    private readonly HumanOsDbContext _dbContext;

    public GenerateStructuredLearningKnowledgeFactsFunction(
        StructuredLearningKnowledgeFactGenerationService generationService, HumanOsDbContext dbContext)
    {
        _generationService = generationService;
        _dbContext = dbContext;
    }

    [Function("GenerateStructuredLearningKnowledgeFacts")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "studio/structured-learning/sections/{sectionId:guid}/knowledge-facts/generate")]
        HttpRequestData request,
        Guid sectionId,
        CancellationToken cancellationToken)
    {
        if (!_generationService.IsConfigured)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.ServiceUnavailable, "KnowledgeFactGenerationNotConfigured",
                "Knowledge Fact generation is not configured (missing ClaudeConnectionAgentEndpoint/DeploymentName).", cancellationToken);
        }

        var sectionExists = await _dbContext.StructuredLearningDocumentSections
            .AsNoTracking()
            .AnyAsync(s => s.StructuredLearningDocumentSectionId == sectionId, cancellationToken);
        if (!sectionExists)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "SectionNotFound", "No StructuredLearningDocumentSection was found with that id.", cancellationToken);
        }

        var result = await _generationService.GenerateValidateAndPersistAsync(sectionId, cancellationToken);
        if (!result.GenerationSucceeded || !result.PersistenceSucceeded)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.UnprocessableEntity, "KnowledgeFactGenerationFailed",
                result.Errors.Count > 0 ? string.Join(' ', result.Errors) : "Knowledge Fact generation failed.", cancellationToken);
        }

        return await FunctionResponseFactory.SuccessResponseAsync(request, new GenerateStructuredLearningKnowledgeFactsResponseDto
        {
            EngramId = sectionId,
            KnowledgeFactsCreated = result.KnowledgeFactsCreated,
            MissingGraphFactsCount = result.MissingGraphFactsCount
        }, cancellationToken: cancellationToken);
    }
}
