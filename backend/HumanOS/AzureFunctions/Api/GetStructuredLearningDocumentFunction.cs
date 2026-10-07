using System.Net;
using System.Text.Json;
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

    public string? VideoWizardStateJson { get; set; }

    public List<string>? WebSearchCitations { get; set; }
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

    public List<string>? WebSearchSources { get; set; }

    public bool HasApiData { get; set; }

    /// <summary>Estimated minutes for a learner to complete this Engram,
    /// built from the actual steps present (Before you start/Learn/Deep
    /// dive/In your role/Recall/Assessment) rather than raw token counts —
    /// each step type has its own realistic fixed duration, with Deep Dive
    /// scaled by its real card count (2026-09-27).</summary>
    public int EstimatedMinutesToComplete { get; set; }

    public List<StructuredLearningStepDetailDto> Steps { get; set; } = [];
}

public sealed class StructuredLearningDocumentDetailResponse
{
    public Guid StructuredLearningDocumentId { get; set; }

    public string DocumentName { get; set; } = string.Empty;

    public bool IsGlobal { get; set; }

    public bool IsPublished { get; set; }

    public Guid? SubjectId { get; set; }

    public Guid? CapabilityDomainId { get; set; }

    public Guid? TopicId { get; set; }

    public string? Category { get; set; }

    public string? TargetSkill { get; set; }

    public string? ExecutiveSummary { get; set; }

    public string? LanguageCode { get; set; }

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

    public bool ShowQaProfessorStep { get; set; }

    public bool ShowDeepDiveAgentChat { get; set; }

    public bool EnableQaProfessorVoice { get; set; }

    public bool EnableDeepDiveAgentVoice { get; set; }

    public bool IncludeWorkplaceApplicationStep { get; set; }

    public string RecallGenerationMode { get; set; } = string.Empty;

    public string AssessmentGenerationMode { get; set; } = string.Empty;

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
            CapabilityDomainId = document.CapabilityDomainId,
            TopicId = document.TopicId,
            Category = document.Category,
            TargetSkill = document.TargetSkill,
            ExecutiveSummary = document.ExecutiveSummary,
            LanguageCode = ResolveLanguageCode(document.ConfigSnapshotJson),
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
            ShowQaProfessorStep = document.ShowQaProfessorStep,
            ShowDeepDiveAgentChat = document.ShowDeepDiveAgentChat,
            EnableQaProfessorVoice = document.EnableQaProfessorVoice,
            EnableDeepDiveAgentVoice = document.EnableDeepDiveAgentVoice,
            IncludeWorkplaceApplicationStep = document.IncludeWorkplaceApplicationStep,
            RecallGenerationMode = document.RecallGenerationMode.ToString(),
            AssessmentGenerationMode = document.AssessmentGenerationMode.ToString(),
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
                    WebSearchSources = ExtractSourcesFromConfigSnapshot(s.ConfigSnapshotJson),
                    HasApiData = CheckHasApiData(s.ConfigSnapshotJson),
                    EstimatedMinutesToComplete = EstimateMinutesToComplete(s.Steps, document.RecallItemsRequired, document.AssessmentQuestionsPerRound),
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
                            TimelineJson = st.TimelineJson,
                            VideoWizardStateJson = st.VideoWizardStateJson,
                            WebSearchCitations = ExtractCitationsFromStepData(st.StepDataJson)
                        })
                        .ToList()
                })
                .ToList()
        };

        return await FunctionResponseFactory.SuccessResponseAsync(request, response, cancellationToken: cancellationToken);
    }

    /// <summary>Estimates learner completion time (minutes) for one Engram
    /// from the ACTUAL steps present, using a fixed realistic duration per
    /// step type instead of a token/word count (2026-09-27 fix — a designer
    /// correctly pointed out token-derived estimates don't match how long a
    /// human actually spends per step). Deep Dive scales by its real
    /// illustrated-panel count (a single card can be a multi-panel sequence,
    /// see CountDeepDivePanels). Recall/Assessment scale by the document's
    /// CONFIGURED item counts (known before the learner starts) instead of
    /// trying to predict real per-learner mistakes — each question already
    /// bakes in an average read+answer+retry-on-error buffer, so more
    /// configured questions realistically means more time regardless of how
    /// many the learner actually gets wrong (2026-09-28 follow-up fix).</summary>
    private static int EstimateMinutesToComplete(
        ICollection<Models.Capabilities.Studio.StructuredLearningSectionStep> steps,
        int? recallItemsRequired,
        int? assessmentQuestionsPerRound)
    {
        const int minutesPerRecallItem = 1; // read the prompt + answer + occasional retry
        const int minutesPerAssessmentQuestion = 2; // read + answer + review feedback on a miss

        var totalMinutes = 0;
        foreach (var step in steps)
        {
            totalMinutes += step.StepType switch
            {
                Models.Capabilities.Studio.StructuredLearningStepType.Hypothesis => 2, // "Before you start"
                Models.Capabilities.Studio.StructuredLearningStepType.Teaching => 6, // "Learn" — reading + narrated audio
                Models.Capabilities.Studio.StructuredLearningStepType.DeepDive => 2 * CountDeepDivePanels(step.StepDataJson),
                Models.Capabilities.Studio.StructuredLearningStepType.Production => 4, // "In your role"
                Models.Capabilities.Studio.StructuredLearningStepType.Recall => Math.Max(3, minutesPerRecallItem * (recallItemsRequired ?? 3)),
                Models.Capabilities.Studio.StructuredLearningStepType.Assessment => Math.Max(5, minutesPerAssessmentQuestion * (assessmentQuestionsPerRound ?? 3)),
                _ => 0,
            };
        }

        return Math.Max(totalMinutes, 3);
    }

    /// <summary>DeepDive's StepDataJson is persisted as a bare JSON array of
    /// ExampleCards (see StructuredLearningPedagogyService.BuildStepDataJson).
    /// A single card can itself be a multi-panel illustrated sequence (its
    /// own IllustrationIds/IllustrationPrompts array, e.g. a 4-step visual
    /// derivation) — counting only the number of CARDS badly undercounts
    /// that content, so this sums each card's panel count instead
    /// (2026-09-28 fix), defaulting to 1 panel per card when neither array
    /// is present.</summary>
    private static int CountDeepDivePanels(string? stepDataJson)
    {
        if (string.IsNullOrWhiteSpace(stepDataJson))
        {
            return 1;
        }

        try
        {
            using var json = JsonDocument.Parse(stepDataJson);
            if (json.RootElement.ValueKind != JsonValueKind.Array)
            {
                return 1;
            }

            var totalPanels = 0;
            foreach (var card in json.RootElement.EnumerateArray())
            {
                var panelCount = 1;
                if (card.TryGetProperty("IllustrationIds", out var ids) && ids.ValueKind == JsonValueKind.Array && ids.GetArrayLength() > 0)
                {
                    panelCount = ids.GetArrayLength();
                }
                else if (card.TryGetProperty("IllustrationPrompts", out var prompts) && prompts.ValueKind == JsonValueKind.Array && prompts.GetArrayLength() > 0)
                {
                    panelCount = prompts.GetArrayLength();
                }

                totalPanels += panelCount;
            }

            return Math.Max(1, totalPanels);
        }
        catch (JsonException)
        {
            return 1;
        }
    }

    private static string? ResolveLanguageCode(string? configSnapshotJson)
    {        if (string.IsNullOrWhiteSpace(configSnapshotJson))
        {
            return null;
        }

        try
        {
            using var json = JsonDocument.Parse(configSnapshotJson);
            if (json.RootElement.TryGetProperty("LanguageCode", out var language) ||
                json.RootElement.TryGetProperty("languageCode", out language))
            {
                return language.GetString();
            }
        }
        catch (JsonException)
        {
            // Ignore malformed historical snapshots; the field is optional.
        }

        return null;
    }

    private static List<string>? ExtractCitationsFromStepData(string? stepDataJson)
    {
        if (string.IsNullOrWhiteSpace(stepDataJson)) return null;
        try
        {
            using var doc = JsonDocument.Parse(stepDataJson);
            if (doc.RootElement.TryGetProperty("WebSearchCitations", out var prop) && prop.ValueKind == JsonValueKind.Array)
            {
                var list = new List<string>();
                foreach (var el in prop.EnumerateArray())
                {
                    var str = el.GetString();
                    if (!string.IsNullOrWhiteSpace(str)) list.Add(str);
                }
                return list.Count > 0 ? list : null;
            }
        }
        catch { }
        return null;
    }

    private static List<string>? ExtractSourcesFromConfigSnapshot(string? configSnapshotJson)
    {
        if (string.IsNullOrWhiteSpace(configSnapshotJson)) return null;
        try
        {
            using var doc = JsonDocument.Parse(configSnapshotJson);
            if ((doc.RootElement.TryGetProperty("WebSearchSources", out var prop) ||
                 doc.RootElement.TryGetProperty("webSearchSources", out prop)) && prop.ValueKind == JsonValueKind.Array)
            {
                var list = new List<string>();
                foreach (var el in prop.EnumerateArray())
                {
                    var str = el.GetString();
                    if (!string.IsNullOrWhiteSpace(str)) list.Add(str);
                }
                return list.Count > 0 ? list : null;
            }
        }
        catch { }
        return null;
    }

    private static bool CheckHasApiData(string? configSnapshotJson)
    {
        if (string.IsNullOrWhiteSpace(configSnapshotJson)) return false;
        try
        {
            using var doc = JsonDocument.Parse(configSnapshotJson);
            if ((doc.RootElement.TryGetProperty("HasApiData", out var prop) ||
                 doc.RootElement.TryGetProperty("hasApiData", out prop)) && prop.ValueKind == JsonValueKind.True)
            {
                return true;
            }
        }
        catch { }
        return false;
    }
}
