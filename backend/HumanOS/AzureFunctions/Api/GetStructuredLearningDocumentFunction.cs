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

    public string? TimelineJson { get; set; }
}

public sealed class StructuredLearningSectionDetailDto
{
    public Guid StructuredLearningDocumentSectionId { get; set; }

    public int SortOrder { get; set; }

    public string ChapterTitle { get; set; } = string.Empty;

    public string SubchapterTitle { get; set; } = string.Empty;

    /// <summary>The section's actual extracted/generated source text — shown
    /// read-only in the studio so a designer can verify sparse-content
    /// expansion actually produced real content (2026-08-17).</summary>
    public string Text { get; set; } = string.Empty;

    public int TotalCharacters { get; set; }

    public int TotalTokens { get; set; }

    public string PromptVersion { get; set; } = string.Empty;

    public string ProcessVersion { get; set; } = string.Empty;

    public string? ModelName { get; set; }

    public string? PromptHash { get; set; }

    public string? ConfigSnapshotJson { get; set; }

    public string PedagogyStatus { get; set; } = string.Empty;

    public string? PedagogyErrorMessage { get; set; }

    /// <summary>Knowledge Graph (2026-08-15) — the entity name the model chose as this
    /// section's conceptual hub/root, used by the frontend to render the tree from a
    /// single logical root instead of guessing via degree count.</summary>
    public string? KnowledgeGraphRootEntityName { get; set; }

    public List<StructuredLearningStepDetailDto> Steps { get; set; } = [];
}

public sealed class StructuredLearningDocumentDetailResponse
{
    public Guid StructuredLearningDocumentId { get; set; }

    public string DocumentName { get; set; } = string.Empty;

    public bool IsGlobal { get; set; }

    public bool IsPublished { get; set; }

    public Guid? SubjectId { get; set; }

    public Guid? TopicId { get; set; }

    public string? ExecutiveSummary { get; set; }

    public Guid? CoverIllustrationId { get; set; }

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

    public bool IncludeAssessmentStep { get; set; }

    public string AssessmentQuestionType { get; set; } = string.Empty;

    public int? AssessmentQuestionsPerRound { get; set; }

    /// <summary>Document Type Detection Engine (2026-08-17) — the engine's
    /// recommendation, e.g. "Law"/"Procedure"/"Unknown".</summary>
    public string? DetectedDocumentType { get; set; }

    public double? DetectedDocumentTypeConfidence { get; set; }

    /// <summary>The designer's final choice after reviewing the
    /// recommendation — null until confirmed via
    /// ConfirmStructuredLearningDocumentType.</summary>
    public string? ConfirmedDocumentType { get; set; }

    public bool DocumentTypeConfirmedByUser { get; set; }

    public bool ShowKnowledgeGraphToLearners { get; set; }

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

        var firstSectionId = document.Sections.OrderBy(s => s.SortOrder).Select(s => s.StructuredLearningDocumentSectionId).FirstOrDefault();
        var coverIllustrationId = firstSectionId == default
            ? null
            : await _dbContext.StructuredLearningIllustrations
                .Where(i => i.StructuredLearningDocumentSectionId == firstSectionId)
                .OrderBy(i => i.CreatedDate)
                .Select(i => (Guid?)i.StructuredLearningIllustrationId)
                .FirstOrDefaultAsync(cancellationToken);

        var response = new StructuredLearningDocumentDetailResponse
        {
            StructuredLearningDocumentId = document.StructuredLearningDocumentId,
            DocumentName = document.DocumentName,
            IsGlobal = document.IsGlobal,
            IsPublished = document.IsPublished,
            SubjectId = document.SubjectId,
            TopicId = document.TopicId,
            ExecutiveSummary = document.ExecutiveSummary,
            CoverIllustrationId = coverIllustrationId,
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
            IncludeAssessmentStep = document.IncludeAssessmentStep,
            AssessmentQuestionType = document.AssessmentQuestionType.ToString(),
            AssessmentQuestionsPerRound = document.AssessmentQuestionsPerRound,
            DetectedDocumentType = document.DetectedDocumentType,
            DetectedDocumentTypeConfidence = document.DetectedDocumentTypeConfidence,
            ConfirmedDocumentType = document.ConfirmedDocumentType,
            DocumentTypeConfirmedByUser = document.DocumentTypeConfirmedByUser,
            ShowKnowledgeGraphToLearners = document.ShowKnowledgeGraphToLearners,
            Sections = document.Sections
                .OrderBy(s => s.SortOrder)
                .Select(s => new StructuredLearningSectionDetailDto
                {
                    StructuredLearningDocumentSectionId = s.StructuredLearningDocumentSectionId,
                    SortOrder = s.SortOrder,
                    ChapterTitle = s.ChapterTitle,
                    SubchapterTitle = s.SubchapterTitle,
                    Text = s.Text,
                    TotalCharacters = s.TotalCharacters,
                    TotalTokens = s.TotalTokens,
                    PromptVersion = s.PromptVersion ?? string.Empty,
                    ProcessVersion = s.ProcessVersion ?? string.Empty,
                    ModelName = s.ModelName,
                    PromptHash = s.PromptHash,
                    ConfigSnapshotJson = s.ConfigSnapshotJson,
                    PedagogyStatus = s.PedagogyStatus.ToString(),
                    PedagogyErrorMessage = s.PedagogyErrorMessage,
                    KnowledgeGraphRootEntityName = s.KnowledgeGraphRootEntityName,
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
                            HumanApprovedAtUtc = st.HumanApprovedAtUtc,
                            TimelineJson = st.TimelineJson
                        })
                        .ToList()
                })
                .ToList()
        };

        return await FunctionResponseFactory.SuccessResponseAsync(request, response, cancellationToken: cancellationToken);
    }
}
