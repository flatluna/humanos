using HumanOS.Data;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.AzureFunctions.Api;

public sealed class StructuredLearningKnowledgeFactPathDto
{
    public Guid ConnectionId { get; set; }
    public int StepOrder { get; set; }
    public Guid SourceEntityId { get; set; }
    public string SourceEntityName { get; set; } = string.Empty;
    public string RelationshipCategory { get; set; } = string.Empty;
    public string RelationshipLabel { get; set; } = string.Empty;
    public Guid TargetEntityId { get; set; }
    public string TargetEntityName { get; set; } = string.Empty;
    public string? Evidence { get; set; }
    public string EpistemicStatus { get; set; } = "explicit";
    public double? Confidence { get; set; }
}

public sealed class StructuredLearningKnowledgeFactReadDto
{
    public Guid KnowledgeFactId { get; set; }
    public string Statement { get; set; } = string.Empty;
    public string FactType { get; set; } = string.Empty;
    public string Importance { get; set; } = string.Empty;
    public string DerivationType { get; set; } = string.Empty;
    public string EpistemicStatus { get; set; } = string.Empty;
    public double Confidence { get; set; }
    public List<StructuredLearningKnowledgeFactPathDto> GraphPath { get; set; } = [];
}

public sealed class StructuredLearningKnowledgeFactsReadDto
{
    public Guid EngramId { get; set; }
    public int TotalFacts { get; set; }
    public int CoreFacts { get; set; }
    public int SupportingFacts { get; set; }
    public int AtomicFacts { get; set; }
    public int CompositeFacts { get; set; }
    public List<StructuredLearningKnowledgeFactReadDto> KnowledgeFacts { get; set; } = [];
}

public sealed class GetStructuredLearningKnowledgeFactsFunction
{
    private readonly HumanOsDbContext _dbContext;

    public GetStructuredLearningKnowledgeFactsFunction(HumanOsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [Function("GetStructuredLearningKnowledgeFacts")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "studio/structured-learning/sections/{sectionId:guid}/knowledge-facts")] HttpRequestData request,
        Guid sectionId,
        CancellationToken cancellationToken)
    {
        var facts = await _dbContext.StructuredLearningKnowledgeFacts
            .Where(fact => fact.EngramId == sectionId)
            .Select(fact => new StructuredLearningKnowledgeFactReadDto
            {
                KnowledgeFactId = fact.KnowledgeFactId,
                Statement = fact.Statement,
                FactType = fact.FactType,
                Importance = fact.Importance,
                DerivationType = fact.DerivationType,
                EpistemicStatus = fact.EpistemicStatus,
                Confidence = fact.Confidence,
                GraphPath = fact.GraphPathSteps
                    .OrderBy(step => step.StepOrder)
                    .Select(step => new StructuredLearningKnowledgeFactPathDto
                    {
                        ConnectionId = step.ConnectionId,
                        StepOrder = step.StepOrder,
                        SourceEntityId = step.Connection.SourceEntityId,
                        SourceEntityName = step.Connection.SourceEntity.Name,
                        RelationshipCategory = step.Connection.RelationshipCategory,
                        RelationshipLabel = step.Connection.RelationshipLabel,
                        TargetEntityId = step.Connection.TargetEntityId,
                        TargetEntityName = step.Connection.TargetEntity.Name,
                        Evidence = step.Connection.Evidence,
                        EpistemicStatus = step.Connection.EpistemicStatus,
                        Confidence = step.Connection.Confidence
                    })
                    .ToList()
            })
            .OrderByDescending(fact => fact.Importance == "Core")
            .ThenBy(fact => fact.Statement)
            .ToListAsync(cancellationToken);

        var response = new StructuredLearningKnowledgeFactsReadDto
        {
            EngramId = sectionId,
            TotalFacts = facts.Count,
            CoreFacts = facts.Count(fact => fact.Importance == "Core"),
            SupportingFacts = facts.Count(fact => fact.Importance == "Supporting"),
            AtomicFacts = facts.Count(fact => fact.DerivationType == "Atomic"),
            CompositeFacts = facts.Count(fact => fact.DerivationType == "Composite"),
            KnowledgeFacts = facts
        };

        return await FunctionResponseFactory.SuccessResponseAsync(request, response, cancellationToken: cancellationToken);
    }
}
