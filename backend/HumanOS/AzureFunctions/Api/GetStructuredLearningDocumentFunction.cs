using System.Net;
using HumanOS.Data;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.AzureFunctions.Api;

public sealed class StructuredLearningStepDetailDto
{
    public Guid StructuredLearningSectionStepId { get; set; }

    public string StepType { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public string? StepDataJson { get; set; }

    public string? PromptVersion { get; set; }

    public string? ProcessVersion { get; set; }

    public string? ModelName { get; set; }

    public string? PromptHash { get; set; }

    public string? ConfigSnapshotJson { get; set; }

    public bool IsHumanApproved { get; set; }

    public DateTime? HumanApprovedAtUtc { get; set; }
}

public sealed class StructuredLearningSectionDetailDto
{
    public Guid StructuredLearningDocumentSectionId { get; set; }

    public int SortOrder { get; set; }

    public string ChapterTitle { get; set; } = string.Empty;

    public string SubchapterTitle { get; set; } = string.Empty;

    public int TotalCharacters { get; set; }

    public int TotalTokens { get; set; }

    public string PromptVersion { get; set; } = string.Empty;

    public string ProcessVersion { get; set; } = string.Empty;

    public string? ModelName { get; set; }

    public string? PromptHash { get; set; }

    public string? ConfigSnapshotJson { get; set; }

    public string PedagogyStatus { get; set; } = string.Empty;

    public string? PedagogyErrorMessage { get; set; }

    public List<StructuredLearningStepDetailDto> Steps { get; set; } = [];
}

public sealed class StructuredLearningDocumentDetailResponse
{
    public Guid StructuredLearningDocumentId { get; set; }

    public string DocumentName { get; set; } = string.Empty;

    public string? PedagogicalAudience { get; set; }

    public string? PromptVersion { get; set; }

    public string? ProcessVersion { get; set; }

    public string? ModelName { get; set; }

    public string? PromptHash { get; set; }

    public string? ConfigSnapshotJson { get; set; }

    public int? MaxDeepDiveCardsPerNode { get; set; }

    public bool IncludeProductionStep { get; set; }

    public bool IncludeRecallStep { get; set; }

    public int? RecallItemsRequired { get; set; }

    public string AssessmentQuestionType { get; set; } = string.Empty;

    public int? AssessmentQuestionsPerRound { get; set; }

    public List<StructuredLearningSectionDetailDto> Sections { get; set; } = [];
}

/// <summary>Full detail of a Structured Learning Document — its pedagogical
/// config plus every section and whatever Step 3 content has been
/// generated for it so far (empty Steps list for sections still
/// NotStarted/Generating/Failed).</summary>
public sealed class GetStructuredLearningDocumentFunction
{
    private readonly HumanOsDbContext _dbContext;

    public GetStructuredLearningDocumentFunction(HumanOsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [Function("GetStructuredLearningDocument")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "studio/structured-learning/{documentId:guid}")]
        HttpRequestData request,
        Guid documentId,
        CancellationToken cancellationToken)
    {
        var document = await _dbContext.StructuredLearningDocuments
            .Include(d => d.Sections).ThenInclude(s => s.Steps)
            .FirstOrDefaultAsync(d => d.StructuredLearningDocumentId == documentId, cancellationToken);

        if (document is null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "DocumentNotFound", $"No document found with id '{documentId}'.", cancellationToken);
        }

        var response = new StructuredLearningDocumentDetailResponse
        {
            StructuredLearningDocumentId = document.StructuredLearningDocumentId,
            DocumentName = document.DocumentName,
            PedagogicalAudience = document.PedagogicalAudience,
            PromptVersion = document.PromptVersion,
            ProcessVersion = document.ProcessVersion,
            ModelName = document.ModelName,
            PromptHash = document.PromptHash,
            ConfigSnapshotJson = document.ConfigSnapshotJson,
            MaxDeepDiveCardsPerNode = document.MaxDeepDiveCardsPerNode,
            IncludeProductionStep = document.IncludeProductionStep,
            IncludeRecallStep = document.IncludeRecallStep,
            RecallItemsRequired = document.RecallItemsRequired,
            AssessmentQuestionType = document.AssessmentQuestionType.ToString(),
            AssessmentQuestionsPerRound = document.AssessmentQuestionsPerRound,
            Sections = document.Sections
                .OrderBy(s => s.SortOrder)
                .Select(s => new StructuredLearningSectionDetailDto
                {
                    StructuredLearningDocumentSectionId = s.StructuredLearningDocumentSectionId,
                    SortOrder = s.SortOrder,
                    ChapterTitle = s.ChapterTitle,
                    SubchapterTitle = s.SubchapterTitle,
                    TotalCharacters = s.TotalCharacters,
                    TotalTokens = s.TotalTokens,
                    PromptVersion = s.PromptVersion ?? string.Empty,
                    ProcessVersion = s.ProcessVersion ?? string.Empty,
                    ModelName = s.ModelName,
                    PromptHash = s.PromptHash,
                    ConfigSnapshotJson = s.ConfigSnapshotJson,
                    PedagogyStatus = s.PedagogyStatus.ToString(),
                    PedagogyErrorMessage = s.PedagogyErrorMessage,
                    Steps = s.Steps
                        .OrderBy(st => st.SortOrder)
                        .Select(st => new StructuredLearningStepDetailDto
                        {
                            StructuredLearningSectionStepId = st.StructuredLearningSectionStepId,
                            StepType = st.StepType.ToString(),
                            Content = st.Content,
                            StepDataJson = st.StepDataJson,
                            PromptVersion = st.PromptVersion,
                            ProcessVersion = st.ProcessVersion,
                            ModelName = st.ModelName,
                            PromptHash = st.PromptHash,
                            ConfigSnapshotJson = st.ConfigSnapshotJson,
                            IsHumanApproved = st.IsHumanApproved,
                            HumanApprovedAtUtc = st.HumanApprovedAtUtc
                        })
                        .ToList()
                })
                .ToList()
        };

        return await FunctionResponseFactory.SuccessResponseAsync(request, response, cancellationToken: cancellationToken);
    }
}
