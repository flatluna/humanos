using System.Text.Json;
using HumanOS.Agents.Studio;
using HumanOS.Data;
using HumanOS.Models.Capabilities.Studio;
using HumanOS.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HumanOS.Services;

/// <summary>
/// Step 3 of the Structured Learning Document pipeline: generates the
/// pedagogical sequence (Hypothesis/Teaching/DeepDive?/Recall?/Production?/
/// Assessment) for every section of a document that isn't already
/// Completed — sections are processed with BOUNDED concurrency (never
/// unlimited, to respect Azure OpenAI rate limits, same rationale as
/// TraditionalCoursePipelineService's MaxConcurrentChapters), each result
/// checked against <see cref="StructuredLearningSectionEvaluator"/> (written
/// BEFORE this agent existed) before being persisted.
///
/// Resumable by design: only queries sections where
/// <see cref="StructuredLearningDocumentSection.PedagogyStatus"/> !=
/// Completed, so re-running after a crash/interruption (or after a Failed
/// section) never re-pays for sections that already succeeded.
/// </summary>
/// <summary>Per-section illustration-mode override for Edit mode's "Add
/// section" feature (2026-08-11) — see
/// StructuredLearningPedagogyService.GenerateIllustrationsAsync's doc
/// comment for what each Mode value does.</summary>
public sealed class SectionIllustrationOverride
{
    public Guid SectionId { get; init; }

    public string Mode { get; init; } = "AiGenerated";

    public byte[]? UploadedImageBytes { get; init; }

    public string? UploadedImageContentType { get; init; }

    /// <summary>The professor's own free-text instruction for the new
    /// section, forwarded as extra illustration guidance ONLY when
    /// <see cref="Mode"/> is "AiGeneratedFromUpload" — the section's
    /// illustration prompts are designed by StructuredLearningSectionDesignerAgent
    /// from the section's Text alone, with no visibility into the fact a
    /// reference image was supplied, so any instruction about how to use it
    /// (e.g. "put our logo on the volunteers' t-shirts") would otherwise be
    /// silently dropped.</summary>
    public string? ReferenceImageUsageInstruction { get; init; }
}

public sealed class StructuredLearningPedagogyService
{
    private const int DefaultMaxConcurrentSections = 3;

    private readonly StructuredLearningSectionDesignerAgent _designer;
    private readonly IDbContextFactory<HumanOsDbContext> _dbContextFactory;
    private readonly IConfiguration _configuration;
    private readonly GraphIllustrationImageService _imageService;
    private readonly CapabilityGraphIllustrationStorageService _illustrationStorage;
    private readonly HumanOS.Agents.Studio.PdfImageDescriptionAgent _imageDescriptionAgent;
    private readonly StructuredLearningGenerationUsageService _usageService;
    private readonly StructuredLearningKnowledgeFactGenerationService _knowledgeFactGeneration;
    private readonly StructuredLearningProfessorLectureAgent _professorLectureAgent;
    private readonly AzureSpeechService _speechService;
    private readonly PerplexitySearchService _perplexityService;
    private readonly IServiceScopeFactory _scopeFactory;

    public StructuredLearningPedagogyService(
        StructuredLearningSectionDesignerAgent designer,
        IDbContextFactory<HumanOsDbContext> dbContextFactory,
        IConfiguration configuration,
        GraphIllustrationImageService imageService,
        HumanOS.Agents.Studio.PdfImageDescriptionAgent imageDescriptionAgent,
        StructuredLearningGenerationUsageService usageService,
        StructuredLearningKnowledgeFactGenerationService knowledgeFactGeneration,
        StructuredLearningProfessorLectureAgent professorLectureAgent,
        AzureSpeechService speechService,
        PerplexitySearchService perplexityService,
        IServiceScopeFactory scopeFactory)
    {
        _designer = designer;
        _dbContextFactory = dbContextFactory;
        _configuration = configuration;
        _imageService = imageService;
        _imageDescriptionAgent = imageDescriptionAgent;
        _usageService = usageService;
        _knowledgeFactGeneration = knowledgeFactGeneration;
        _professorLectureAgent = professorLectureAgent;
        _speechService = speechService;
        _perplexityService = perplexityService;
        _scopeFactory = scopeFactory;
        // Same rationale as TraditionalCoursePipelineService: this service
        // only needs IConfiguration, so it's constructed directly rather
        // than injected (it's registered Scoped in DI, incompatible with
        // this Singleton's lifetime).
        _illustrationStorage = new CapabilityGraphIllustrationStorageService(configuration);
    }

    public bool IsConfigured => _designer.IsConfigured;

    public int MaxConcurrentSections
    {
        get
        {
            var raw = _configuration["MaxConcurrentStructuredLearningSections"];
            return int.TryParse(raw, out var value) && value > 0 ? value : DefaultMaxConcurrentSections;
        }
    }

    /// <param name="reportProgress">(step description, completed count, total count, titles of sections currently in flight).</param>
    /// <param name="forceRegenerateAll">When true, reprocesses EVERY section
    /// regardless of PedagogyStatus (not just non-Completed ones) — needed
    /// after an agent prompt/instructions change, so already-succeeded
    /// sections pick up the new behavior instead of being skipped as
    /// already-done.</param>
    /// <param name="fromEngramNumber">Optional 1-based inclusive start position
    /// (by SortOrder) — lets a designer restart/regenerate only a specific
    /// range of engrams (e.g. "1 to 10") instead of the whole document.
    /// Null means from the first engram.</param>
    /// <param name="toEngramNumber">Optional 1-based inclusive end position
    /// (by SortOrder). Null means through the last engram.</param>
    public async Task<StructuredLearningPedagogyResult> RunAsync(
        Guid documentId,
        string languageCode,
        Action<string, int, int, IReadOnlyList<string>> reportProgress,
        CancellationToken cancellationToken,
        bool forceRegenerateAll = false,
        byte[]? uploadedImageBytes = null,
        string? uploadedImageContentType = null,
        string? referenceImageUsageInstruction = null,
        SectionIllustrationOverride? illustrationOverride = null,
        bool? usePerplexitySearch = null,
        int? fromEngramNumber = null,
        int? toEngramNumber = null)
    {
        StructuredLearningDocument document;
        List<StructuredLearningDocumentSection> pendingSections;

        await using (var readContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            document = await readContext.StructuredLearningDocuments.FindAsync([documentId], cancellationToken)
                ?? throw new InvalidOperationException($"No se encontró el documento '{documentId}'.");

            var sectionsQuery = readContext.StructuredLearningDocumentSections
                .Where(s => s.StructuredLearningDocumentId == documentId);
            if (!forceRegenerateAll)
            {
                sectionsQuery = sectionsQuery.Where(s => s.PedagogyStatus != StructuredLearningPedagogyStatus.Completed);
            }

            var orderedSections = await sectionsQuery
                .OrderBy(s => s.SortOrder)
                .ToListAsync(cancellationToken);

            // Engram range filter (2026-09-19) applies AFTER ordering, using the
            // section's 1-based position in the document's own SortOrder
            // sequence — not the raw SortOrder value itself, since SortOrder can
            // start at any base or have gaps. "Engram 1 to 10" always means the
            // first ten engrams in the document, regardless of forceRegenerateAll.
            if (fromEngramNumber is > 0 || toEngramNumber is > 0)
            {
                var from = fromEngramNumber is > 0 ? fromEngramNumber.Value : 1;
                var to = toEngramNumber is > 0 ? toEngramNumber.Value : orderedSections.Count;
                pendingSections = orderedSections
                    .Select((section, index) => (section, position: index + 1))
                    .Where(x => x.position >= from && x.position <= to)
                    .Select(x => x.section)
                    .ToList();
            }
            else
            {
                pendingSections = orderedSections;
            }
        }

        var effectiveUsePerplexitySearch = usePerplexitySearch ?? ReadUsePerplexitySearch(document.ConfigSnapshotJson);

        var processVersion = "structured-learning-pedagogy-v1";
        var promptVersion = StructuredLearningSectionDesignerAgent.SourceFaithfulContractVersion;
        var configSnapshot = JsonSerializer.Serialize(new
        {
            processVersion,
            promptVersion,
            maxConcurrentSections = MaxConcurrentSections,
            forceRegenerateAll,
            documentId,
            languageCode,
            usePerplexitySearch = effectiveUsePerplexitySearch,
            pedagogicalAudience = document.PedagogicalAudience,
            includeRecallStep = document.IncludeRecallStep,
            includeProductionStep = document.IncludeProductionStep,
            includeAssessmentStep = document.IncludeAssessmentStep,
            maxDeepDiveCardsPerNode = document.MaxDeepDiveCardsPerNode,
            assessmentQuestionType = document.AssessmentQuestionType,
            assessmentQuestionsPerRound = document.AssessmentQuestionsPerRound ?? 2
        });

        await using (var writeContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            await writeContext.StructuredLearningDocuments
                .Where(d => d.StructuredLearningDocumentId == documentId)
                .ExecuteUpdateAsync(
                    d => d.SetProperty(x => x.PromptVersion, promptVersion)
                          .SetProperty(x => x.ProcessVersion, processVersion)
                          .SetProperty(x => x.ModelName, "azure-openai")
                          .SetProperty(x => x.PromptHash, HashString(configSnapshot))
                          .SetProperty(x => x.ConfigSnapshotJson, configSnapshot),
                    cancellationToken);
        }

        var totalSections = pendingSections.Count;
        var completedCount = 0;
        var failedCount = 0;
        var totalInputTokens = 0;
        var totalOutputTokens = 0;
        var activeTitles = new List<string>();
        var progressLock = new object();

        reportProgress($"Generating pedagogy for {totalSections} pending sections", 0, totalSections, []);

        if (totalSections == 0)
        {
            return new StructuredLearningPedagogyResult { StructuredLearningDocumentId = documentId, SectionsCompleted = 0, SectionsFailed = 0 };
        }

        using var semaphore = new SemaphoreSlim(MaxConcurrentSections);
        var tasks = pendingSections.Select(async section =>
        {
            await semaphore.WaitAsync(cancellationToken);
            lock (progressLock) { activeTitles.Add(section.SubchapterTitle); }
            try
            {
                SectionIllustrationOverride? perSectionOverride = illustrationOverride;
                if (uploadedImageBytes is { Length: > 0 })
                {
                    perSectionOverride = new SectionIllustrationOverride
                    {
                        SectionId = section.StructuredLearningDocumentSectionId,
                        Mode = "AiGeneratedFromUpload",
                        UploadedImageBytes = uploadedImageBytes,
                        UploadedImageContentType = uploadedImageContentType ?? "image/png",
                        ReferenceImageUsageInstruction = referenceImageUsageInstruction,
                    };
                }

                var (succeeded, inputTokens, outputTokens) = await ProcessSectionAsync(
                    document, section, languageCode, perSectionOverride, effectiveUsePerplexitySearch, cancellationToken);
                lock (progressLock)
                {
                    activeTitles.Remove(section.SubchapterTitle);
                    if (succeeded) completedCount++; else failedCount++;
                    totalInputTokens += inputTokens;
                    totalOutputTokens += outputTokens;
                    reportProgress(
                        $"{(succeeded ? "Completed" : "Failed")}: {section.SubchapterTitle}",
                        completedCount + failedCount, totalSections, [.. activeTitles]);
                }
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);

        return new StructuredLearningPedagogyResult
        {
            StructuredLearningDocumentId = documentId,
            SectionsCompleted = completedCount,
            SectionsFailed = failedCount,
            TotalInputTokens = totalInputTokens,
            TotalOutputTokens = totalOutputTokens
        };
    }

    private async Task<(bool Succeeded, int InputTokens, int OutputTokens)> ProcessSectionAsync(
        StructuredLearningDocument document,
        StructuredLearningDocumentSection section,
        string languageCode,
        SectionIllustrationOverride? illustrationOverride,
        bool usePerplexitySearch,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        // Mark Generating immediately so a concurrent status poll (or a
        // resumed run started mid-flight) sees this section as in-progress,
        // not NotStarted.
        await dbContext.StructuredLearningDocumentSections
            .Where(s => s.StructuredLearningDocumentSectionId == section.StructuredLearningDocumentSectionId)
            .ExecuteUpdateAsync(
                s => s.SetProperty(x => x.PedagogyStatus, StructuredLearningPedagogyStatus.Generating),
                cancellationToken);

        var includeDeepDiveStep = document.MaxDeepDiveCardsPerNode is > 0;
        var includeHypothesisStep = ReadIncludeHypothesisStep(document.ConfigSnapshotJson);

        // Snapshot every existing engram artifact BEFORE regenerating
        // anything (2026-09-19 "atomic restart" fix) — a restart must wipe
        // ALL of a section's prior generated content (cards/steps,
        // illustrations, hypothesis answers, assessment/recall history),
        // not just Steps, so nothing stale survives a regeneration. Ids are
        // captured now (not deleted yet) so old content stays intact if the
        // AI generation below fails — the actual delete-old+insert-new swap
        // happens together, atomically, in one transaction near the end.
        var oldIllustrationIds = await dbContext.StructuredLearningIllustrations
            .Where(i => i.StructuredLearningDocumentSectionId == section.StructuredLearningDocumentSectionId)
            .Select(i => i.StructuredLearningIllustrationId)
            .ToListAsync(cancellationToken);
        var oldHypothesisResponseIds = await dbContext.StructuredLearningHypothesisResponses
            .Where(h => h.StructuredLearningDocumentSectionId == section.StructuredLearningDocumentSectionId)
            .Select(h => h.StructuredLearningHypothesisResponseId)
            .ToListAsync(cancellationToken);
        var oldAssessmentRoundIds = await dbContext.StructuredLearningAssessmentRounds
            .Where(r => r.StructuredLearningDocumentSectionId == section.StructuredLearningDocumentSectionId)
            .Select(r => r.StructuredLearningAssessmentRoundId)
            .ToListAsync(cancellationToken);
        var oldRecallAttemptIds = await dbContext.StructuredLearningRecallAttempts
            .Where(r => r.StructuredLearningDocumentSectionId == section.StructuredLearningDocumentSectionId)
            .Select(r => r.StructuredLearningRecallAttemptId)
            .ToListAsync(cancellationToken);

        var sectionOverride = illustrationOverride?.SectionId == section.StructuredLearningDocumentSectionId ? illustrationOverride : null;
        var illustrationGuidance = sectionOverride?.Mode == "AiGeneratedFromUpload" ? sectionOverride.ReferenceImageUsageInstruction : null;

        string? perplexityFindings = null;
        List<string> perplexityCitations = [];

        if (usePerplexitySearch && _perplexityService.IsConfigured)
        {
            try
            {
                var queryParts = new List<string>();
                if (!string.IsNullOrWhiteSpace(section.ChapterTitle)) queryParts.Add(section.ChapterTitle);
                if (!string.IsNullOrWhiteSpace(section.SubchapterTitle) && !section.SubchapterTitle.Equals(section.ChapterTitle, StringComparison.OrdinalIgnoreCase))
                {
                    queryParts.Add(section.SubchapterTitle);
                }
                if (!string.IsNullOrWhiteSpace(section.Capability)) queryParts.Add(section.Capability);

                var searchQuery = string.Join(" - ", queryParts);
                if (string.IsNullOrWhiteSpace(searchQuery))
                {
                    searchQuery = document.DocumentName;
                }

                Console.WriteLine($"[Pedagogy] Engram {section.StructuredLearningDocumentSectionId}: Searching Perplexity with query: \"{searchQuery}\"");
                var searchResult = await _perplexityService.SearchAsync(searchQuery, cancellationToken);
                if (!string.IsNullOrWhiteSpace(searchResult.Answer))
                {
                    perplexityFindings = searchResult.Answer;
                    perplexityCitations = searchResult.Citations ?? [];
                    Console.WriteLine($"[Pedagogy] Engram {section.StructuredLearningDocumentSectionId}: Perplexity returned {perplexityCitations.Count} citations.");
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Console.WriteLine($"[Pedagogy] Engram {section.StructuredLearningDocumentSectionId}: Perplexity search failed, continuing without web grounding: {ex.Message}");
            }
        }

        try
        {
            var knowledgeFacts = await LoadKnowledgeFactsAsync(dbContext, section.StructuredLearningDocumentSectionId, cancellationToken);

            // Knowledge Facts are only ever a QUALITY ENHANCEMENT, never a
            // hard requirement — a designer can legitimately combine any
            // mix of options (Hypothesis on/off, Knowledge Graph on/off,
            // Recall/Production/Assessment on/off), and pedagogy must
            // still complete for every combination instead of hard-failing
            // a whole section just because one optional piece didn't come
            // through. When IncludeKnowledgeGraphEntities is false, a
            // section legitimately having zero facts is expected. When it's
            // true but facts still can't be produced (e.g. auto-recovery
            // below fails, or the section has too little content to derive
            // Core facts from), we now log a warning and fall back to
            // grounding pedagogy in the section's own Text alone — exactly
            // like before Knowledge Facts existed — rather than blocking
            // the designer's whole course (2026-08-23 fix; previously this
            // threw "No persisted Knowledge Facts..." and marked the
            // section Failed, even though Hypothesis-only, Recall-only, or
            // any other combination has nothing to do with Knowledge Facts).
            if (document.IncludeKnowledgeGraphEntities)
            {
                // Auto-recovery: the designer very often clicks "Generate
                // Pedagogy" before ever running Knowledge Facts for a
                // section — try generating them once here first, so
                // pedagogy just works as long as the section's own
                // Knowledge Graph (entities/connections) already exists.
                if (knowledgeFacts.Count == 0 && _knowledgeFactGeneration.IsConfigured)
                {
                    try
                    {
                        await _knowledgeFactGeneration.GenerateValidateAndPersistAsync(section.StructuredLearningDocumentSectionId, cancellationToken);
                        knowledgeFacts = await LoadKnowledgeFactsAsync(dbContext, section.StructuredLearningDocumentSectionId, cancellationToken);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        Console.WriteLine($"[Pedagogy] Engram {section.StructuredLearningDocumentSectionId}: Knowledge Facts auto-recovery failed, falling back to Text-only grounding: {ex.Message}");
                    }
                }

                var coreFactCount = knowledgeFacts.Count(fact => string.Equals(fact.Importance, "Core", StringComparison.OrdinalIgnoreCase));
                if (knowledgeFacts.Count == 0 || coreFactCount == 0 || knowledgeFacts.Any(fact => fact.KnowledgeFactId == Guid.Empty || fact.GraphPath.Count == 0))
                {
                    var reason = knowledgeFacts.Count == 0
                        ? "no persisted Knowledge Facts are available"
                        : coreFactCount == 0
                            ? "no persisted Core Knowledge Fact is available"
                            : "a persisted Knowledge Fact has no resolvable Graph Path";
                    Console.WriteLine($"[Pedagogy] Engram {section.StructuredLearningDocumentSectionId}: {reason} for this Engram; continuing with Text-only grounding instead of failing the section.");
                    knowledgeFacts = [];
                }
            }

            var coreFactCountForLog = knowledgeFacts.Count(fact => string.Equals(fact.Importance, "Core", StringComparison.OrdinalIgnoreCase));
            var pathStepCount = knowledgeFacts.Sum(fact => fact.GraphPath.Count);
            Console.WriteLine($"[Pedagogy] Engram {section.StructuredLearningDocumentSectionId}: loaded {knowledgeFacts.Count} Knowledge Facts ({coreFactCountForLog} Core, {knowledgeFacts.Count - coreFactCountForLog} Supporting) and {pathStepCount} GraphPathSteps from SQL.");
            var designStopwatch = System.Diagnostics.Stopwatch.StartNew();

            // Occasionally the model's structured-output JSON is malformed
            // (e.g. writes an array/object into a field the schema declares
            // as a plain string, such as a DeepDive step's Content) — that
            // throws a raw JsonException from inside DesignAsync/RunAsync
            // rather than an empty/invalid Steps list. Treat that the same
            // as any other validation failure so it feeds into the SAME
            // retry loop below instead of crashing the whole section on the
            // very first attempt.
            async Task<(StructuredLearningSectionDesignerAgent.DesignResult? Design, List<string> Failures)> TryDesignAsync()
            {
                try
                {
                    var attemptDesign = await _designer.DesignAsync(
                        section.ChapterTitle,
                        section.SubchapterTitle,
                        section.Text,
                        languageCode,
                        document.PedagogicalAudience,
                        includeDeepDiveStep,
                        document.MaxDeepDiveCardsPerNode,
                        includeHypothesisStep,
                        document.IncludeRecallStep,
                        document.IncludeProductionStep,
                        document.IncludeAssessmentStep,
                        cancellationToken,
                        illustrationGuidance,
                        knowledgeFacts,
                        perplexityFindings,
                        document.CourseLevel);
                    return (attemptDesign, ValidateGeneratedSteps(attemptDesign.Response.Steps, document, includeDeepDiveStep, includeHypothesisStep, section.Text, attemptDesign.Response.Capability));
                }
                catch (JsonException ex)
                {
                    return (null, [$"Malformed model response: {ex.Message}"]);
                }
            }

            var (design, failures) = await TryDesignAsync();
            var steps = design?.Response.Steps ?? [];

            // Retry up to 2 more times (3 attempts total) — occasionally the
            // model drops/reorders a step (e.g. skips DeepDive), writes
            // ExampleCards too short despite the explicit instructions, or
            // returns malformed JSON (see TryDesignAsync above). Some topics
            // (e.g. terse math derivations) miss on the first retry too, so
            // a single retry wasn't always enough; token usage across every
            // successful attempt is accumulated onto the final result.
            const int maxAttempts = 3;
            for (var attempt = 2; failures.Count > 0 && attempt <= maxAttempts; attempt++)
            {
                var (retryDesign, retryFailures) = await TryDesignAsync();
                if (retryDesign is null)
                {
                    failures = retryFailures;
                    continue;
                }

                design = new StructuredLearningSectionDesignerAgent.DesignResult
                {
                    Response = retryDesign.Response,
                    TokenUsage = new AgentTokenUsage
                    {
                        AgentName = retryDesign.TokenUsage.AgentName,
                        ModelName = retryDesign.TokenUsage.ModelName,
                        InputTokens = (design?.TokenUsage.InputTokens ?? 0) + retryDesign.TokenUsage.InputTokens,
                        OutputTokens = (design?.TokenUsage.OutputTokens ?? 0) + retryDesign.TokenUsage.OutputTokens,
                        CachedInputTokens = (design?.TokenUsage.CachedInputTokens ?? 0) + retryDesign.TokenUsage.CachedInputTokens
                    }
                };
                steps = retryDesign.Response.Steps;
                failures = retryFailures;
            }

            if (design is null)
            {
                await MarkFailedAsync(dbContext, section.StructuredLearningDocumentSectionId, string.Join(" | ", failures), cancellationToken);
                return (false, 0, 0);
            }

            designStopwatch.Stop();
            try
            {
                await _usageService.RecordAsync(
                    document.StructuredLearningDocumentId, section.StructuredLearningDocumentSectionId, "Pedagogy",
                    "StructuredLearningSectionDesigner", design.TokenUsage.ModelName, design.TokenUsage.InputTokens,
                    design.TokenUsage.OutputTokens, design.TokenUsage.CachedInputTokens, designStopwatch.ElapsedMilliseconds, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Usage logging must never break pedagogy generation itself.
            }

            if (failures.Count > 0)
            {
                await MarkFailedAsync(dbContext, section.StructuredLearningDocumentSectionId, string.Join(" | ", failures), cancellationToken);
                return (false, design.TokenUsage.InputTokens, design.TokenUsage.OutputTokens);
            }

            await GenerateIllustrationsAsync(dbContext, section.TenantId, section.StructuredLearningDocumentSectionId, steps, sectionOverride, cancellationToken);

            // Professor Lecture & Spoken Audio Generation (2026-09-06)
            // Creates the oral class script directly from the engram and synthesizes
            // the professor's speech to Azure Data Lake.
            await GenerateProfessorLectureAndSpeechAsync(
                dbContext, document, section, steps, languageCode, cancellationToken);

            // ATOMIC SWAP (2026-09-19): a "retry"/regenerate on a section that
            // already has old content persisted (steps/cards, illustrations,
            // hypothesis answers, assessment/recall history) must delete ALL of
            // it and insert the freshly generated replacement together, as one
            // transaction — either the whole old->new swap commits, or (on any
            // failure) nothing changes and the section keeps its previous
            // content instead of being left half-empty.
            //
            // Wrapped in the DbContext's own retrying execution strategy
            // (2026-09-20 fix): SqlServer's retrying strategy refuses plain
            // user-initiated BeginTransactionAsync calls ("does not support
            // user-initiated transactions"), which was silently failing
            // EVERY section (0 tokens, immediate exception) — regenerate
            // via CreateExecutionStrategy().ExecuteAsync so the transaction
            // participates in the same retry-safe execution the rest of
            // this DbContext uses.
            var strategy = dbContext.Database.CreateExecutionStrategy();
            var processVersion = "structured-learning-pedagogy-v1";
            await strategy.ExecuteAsync(async () =>
            {
                await using var swapTransaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

                if (oldAssessmentRoundIds.Count > 0)
                {
                    await dbContext.StructuredLearningAssessmentQuestionAttempts
                        .Where(a => oldAssessmentRoundIds.Contains(a.StructuredLearningAssessmentRoundId))
                        .ExecuteDeleteAsync(cancellationToken);
                    await dbContext.StructuredLearningAssessmentRounds
                        .Where(r => oldAssessmentRoundIds.Contains(r.StructuredLearningAssessmentRoundId))
                        .ExecuteDeleteAsync(cancellationToken);
                }
                if (oldRecallAttemptIds.Count > 0)
                {
                    await dbContext.StructuredLearningRecallAttempts
                        .Where(r => oldRecallAttemptIds.Contains(r.StructuredLearningRecallAttemptId))
                        .ExecuteDeleteAsync(cancellationToken);
                }
                if (oldHypothesisResponseIds.Count > 0)
                {
                    await dbContext.StructuredLearningHypothesisResponses
                        .Where(h => oldHypothesisResponseIds.Contains(h.StructuredLearningHypothesisResponseId))
                        .ExecuteDeleteAsync(cancellationToken);
                }
                if (oldIllustrationIds.Count > 0)
                {
                    await dbContext.StructuredLearningIllustrations
                        .Where(i => oldIllustrationIds.Contains(i.StructuredLearningIllustrationId))
                        .ExecuteDeleteAsync(cancellationToken);
                }
                await dbContext.StructuredLearningSectionSteps
                    .Where(s => s.StructuredLearningDocumentSectionId == section.StructuredLearningDocumentSectionId)
                    .ExecuteDeleteAsync(cancellationToken);
            var promptVersion = StructuredLearningSectionDesignerAgent.SourceFaithfulContractVersion;
            var modelName = design.TokenUsage.ModelName;
            var configSnapshot = JsonSerializer.Serialize(new
            {
                processVersion,
                promptVersion,
                includeDeepDiveStep,
                includeHypothesisStep,
                maxDeepDiveCards = document.MaxDeepDiveCardsPerNode,
                includeRecallStep = document.IncludeRecallStep,
                includeProductionStep = document.IncludeProductionStep,
                includeAssessmentStep = document.IncludeAssessmentStep,
                usePerplexitySearch,
                hasApiData = perplexityCitations.Count > 0,
                webSearchSources = perplexityCitations,
                webSearchService = perplexityCitations.Count > 0 ? "Perplexity AI" : null,
                webSearchSnippet = perplexityFindings != null && perplexityFindings.Length > 300 ? perplexityFindings[..300] + "..." : perplexityFindings,
                languageCode,
                pedagogicalAudience = document.PedagogicalAudience,
                illustrationGuidanceProvided = !string.IsNullOrWhiteSpace(illustrationGuidance)
            });

            var statements = (design.Response.Statements ?? [])
                .Select(dto => new StructuredLearningStatement
                {
                    StatementId = Guid.NewGuid(),
                    SectionId = section.StructuredLearningDocumentSectionId,
                    StatementType = dto.StatementType,
                    Statement = dto.Statement,
                    SourceText = dto.SourceText,
                    ConditionEntity = dto.ConditionEntity,
                    ConditionAttribute = dto.ConditionAttribute,
                    ConditionOperator = dto.ConditionOperator,
                    ConditionValue = dto.ConditionValue,
                    ConditionUnit = dto.ConditionUnit,
                    ActorEntity = dto.ActorEntity,
                    ActorType = dto.ActorType,
                    Action = dto.Action,
                    TargetEntity = dto.TargetEntity,
                    Modality = dto.Modality,
                    PossibleOutcomes = dto.PossibleOutcomes
                })
                .ToList();

            await dbContext.StructuredLearningDocumentSections
                .Where(s => s.StructuredLearningDocumentSectionId == section.StructuredLearningDocumentSectionId)
                .ExecuteUpdateAsync(
                    s => s.SetProperty(x => x.PromptVersion, promptVersion)
                          .SetProperty(x => x.ProcessVersion, processVersion)
                          .SetProperty(x => x.ModelName, modelName)
                          .SetProperty(x => x.ConfigSnapshotJson, configSnapshot)
                          .SetProperty(x => x.PromptHash, HashString(configSnapshot))
                          .SetProperty(x => x.Statements, statements),
                    cancellationToken);

            for (var i = 0; i < steps.Count; i++)
            {
                var step = steps[i];
                if (step.StepType == StructuredLearningStepType.Teaching && perplexityCitations.Count > 0)
                {
                    step.WebSearchCitations = perplexityCitations;
                }

                dbContext.StructuredLearningSectionSteps.Add(new StructuredLearningSectionStep
                {
                    StructuredLearningSectionStepId = Guid.NewGuid(),
                    StructuredLearningDocumentSectionId = section.StructuredLearningDocumentSectionId,
                    TenantId = section.TenantId,
                    StepType = step.StepType,
                    SortOrder = i,
                    Content = step.Content,
                    StepDataJson = BuildStepDataJson(step),
                    PromptVersion = promptVersion,
                    ProcessVersion = processVersion,
                    ModelName = modelName,
                    PromptHash = HashString(step.Content + "|" + step.StepType),
                    ConfigSnapshotJson = configSnapshot,
                    CreatedDate = DateTime.UtcNow
                });
            }

            await dbContext.StructuredLearningDocumentSections
                .Where(s => s.StructuredLearningDocumentSectionId == section.StructuredLearningDocumentSectionId)
                .ExecuteUpdateAsync(
                    s => s.SetProperty(x => x.PedagogyStatus, StructuredLearningPedagogyStatus.Completed)
                          .SetProperty(x => x.PedagogyErrorMessage, (string?)null),
                    cancellationToken);

                await dbContext.SaveChangesAsync(cancellationToken);
                await swapTransaction.CommitAsync(cancellationToken);
            });

            // Static Recall/Assessment generation-cost lever (2026-10-06) —
            // when the designer chose "Static" for either step, pre-generate
            // its up-to-3 questions right now, during authoring, so no
            // student ever triggers an LLM call for them at runtime (same
            // question persisted in SQL and served to everyone). No-op for
            // sections whose step isn't included, or whose mode is Dynamic.
            if ((document.IncludeRecallStep && document.RecallGenerationMode == StructuredLearningGenerationMode.Static)
                || (document.IncludeAssessmentStep && document.AssessmentGenerationMode == StructuredLearningGenerationMode.Static))
            {
                try
                {
                    using var staticScope = _scopeFactory.CreateScope();
                    await using var staticDbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

                    if (document.IncludeRecallStep && document.RecallGenerationMode == StructuredLearningGenerationMode.Static)
                    {
                        var recallEngine = staticScope.ServiceProvider.GetRequiredService<StructuredLearningRecallEngine>();
                        await recallEngine.EnsureStaticItemsAsync(staticDbContext, section.StructuredLearningDocumentSectionId, cancellationToken);
                    }

                    if (document.IncludeAssessmentStep && document.AssessmentGenerationMode == StructuredLearningGenerationMode.Static)
                    {
                        var assessmentEngine = staticScope.ServiceProvider.GetRequiredService<StructuredLearningAssessmentEngine>();
                        await assessmentEngine.EnsureStaticQuestionsAsync(staticDbContext, section.StructuredLearningDocumentSectionId, cancellationToken);
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // Never fail the whole section over the static-bank
                    // pre-generation step — Dynamic generation at runtime
                    // remains the safety net if this didn't finish.
                    Console.WriteLine($"[Pedagogy] Engram {section.StructuredLearningDocumentSectionId}: static Recall/Assessment generation failed, will fall back to Dynamic at runtime: {ex.Message}");
                }
            }

            return (true, design.TokenUsage.InputTokens, design.TokenUsage.OutputTokens);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var msg = ex.InnerException != null ? $"{ex.Message} --> {ex.InnerException.Message}" : ex.Message;
            await MarkFailedAsync(dbContext, section.StructuredLearningDocumentSectionId, msg, cancellationToken);
            return (false, 0, 0);
        }
    }

    private static bool ReadIncludeHypothesisStep(string? configSnapshotJson)
    {
        if (string.IsNullOrWhiteSpace(configSnapshotJson)) return true;

        try
        {
            using var json = JsonDocument.Parse(configSnapshotJson);
            return !json.RootElement.TryGetProperty("IncludeHypothesisStep", out var value)
                || value.ValueKind != JsonValueKind.False;
        }
        catch (JsonException)
        {
            return true;
        }
    }

    private static bool ReadUsePerplexitySearch(string? configSnapshotJson)
    {
        if (string.IsNullOrWhiteSpace(configSnapshotJson)) return false;

        try
        {
            using var json = JsonDocument.Parse(configSnapshotJson);
            if (json.RootElement.TryGetProperty("UsePerplexitySearch", out var value)
                || json.RootElement.TryGetProperty("usePerplexitySearch", out value))
            {
                return value.ValueKind == JsonValueKind.True;
            }
        }
        catch (JsonException)
        {
            return false;
        }

        return false;
    }

    private static async Task MarkFailedAsync(HumanOsDbContext dbContext, Guid sectionId, string errorMessage, CancellationToken cancellationToken)
    {
        await dbContext.StructuredLearningDocumentSections
            .Where(s => s.StructuredLearningDocumentSectionId == sectionId)
            .ExecuteUpdateAsync(
                s => s.SetProperty(x => x.PedagogyStatus, StructuredLearningPedagogyStatus.Failed)
                      .SetProperty(x => x.PedagogyErrorMessage, errorMessage),
                cancellationToken);
    }

    private static async Task<List<StructuredLearningPedagogyKnowledgeFactDto>> LoadKnowledgeFactsAsync(
        HumanOsDbContext dbContext,
        Guid sectionId,
        CancellationToken cancellationToken)
    {
        return await dbContext.StructuredLearningKnowledgeFacts
            .Where(fact => fact.EngramId == sectionId)
            .Select(fact => new StructuredLearningPedagogyKnowledgeFactDto
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
                    .Select(step => new StructuredLearningPedagogyGraphPathStepDto
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
                    }).ToList()
            })
            .ToListAsync(cancellationToken);
    }

    /// <summary>Generates real illustrations for the Hypothesis step, the
    /// Teaching step (always present), and every DeepDive ExampleCard that
    /// has an IllustrationPrompt — mutates each step's/card's IllustrationId
    /// in place. Same "best-effort, never blocks the rest" behavior as
    /// ExampleCardIllustrationService/TraditionalCoursePipelineService's own
    /// per-card illustration step: a missing IllustrationId just means the
    /// UI shows no image (or falls back to Emoji for DeepDive cards).
    ///
    /// <paramref name="illustrationOverride"/> is only ever non-null for the
    /// ONE section a professor is adding right now via Edit mode's "Add
    /// section" feature (2026-08-11) — mirrors
    /// AddCapabilityGraphNodeFunction's 4-way IllustrationMode choice:
    /// "None" skips illustrations entirely; "UploadDirect" uses the
    /// professor's own image AS-IS for the Teaching step only (same
    /// one-record simplification Graph's own node-addition uses), captioned
    /// via <see cref="_imageDescriptionAgent"/>; "AiGeneratedFromUpload"
    /// keeps the normal AI-designed illustrations but generates them via
    /// image-edit using the professor's image as a reference instead of a
    /// blank generation; a null override (or "AiGenerated") is the default,
    /// unchanged text-to-image behavior for every other (bulk) section.</summary>
    private static string HashString(string value)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(value);
        using var sha = System.Security.Cryptography.SHA256.Create();
        var hash = sha.ComputeHash(bytes);
        return Convert.ToHexString(hash)[..32];
    }

    private async Task GenerateProfessorLectureAndSpeechAsync(
        HumanOsDbContext dbContext,
        StructuredLearningDocument document,
        StructuredLearningDocumentSection section,
        List<StructuredLearningSectionStepDto> steps,
        string languageCode,
        CancellationToken cancellationToken)
    {
        var teachingStep = steps.FirstOrDefault(s => s.StepType == StructuredLearningStepType.Teaching);
        if (teachingStep is null)
        {
            return;
        }

        // 1. Generate generic professor spoken lecture for all students
        HumanOS.Contracts.Studio.StructuredLearningProfessorLectureResult? lecture = null;
        if (_professorLectureAgent.IsConfigured)
        {
            var lectureStopwatch = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                var context = new HumanOS.Contracts.Studio.StructuredLearningProfessorLectureContext
                {
                    CourseId = document.StructuredLearningDocumentId,
                    CourseTitle = document.DocumentName,
                    SectionId = section.StructuredLearningDocumentSectionId,
                    ChapterTitle = section.ChapterTitle,
                    SubchapterTitle = section.SubchapterTitle,
                    Capability = section.Capability,
                    TeachingContent = teachingStep.Content,
                    IllustrationDescription = teachingStep.IllustrationPrompt,
                    DomainOrTopic = document.Category ?? document.TargetSkill,
                    TargetLanguage = languageCode
                };

                var lectureGeneration = await _professorLectureAgent.GenerateLectureAsync(context, cancellationToken);
                lectureStopwatch.Stop();
                lecture = lectureGeneration.Result;
                teachingStep.ProfessorLecture = lecture;

                // Real LLM cost of this call (2026-10-04) — previously
                // discarded entirely (see GenerateLectureAsync's doc
                // comment), meaning the professor lecture's generation cost
                // never showed up in the Pedagogy step total on the admin
                // "Cursos" tab nor the Simulations authoring baseline.
                try
                {
                    await _usageService.RecordAsync(
                        document.StructuredLearningDocumentId, section.StructuredLearningDocumentSectionId, "Pedagogy",
                        "StructuredLearningProfessorLectureAgent", lectureGeneration.TokenUsage.ModelName,
                        lectureGeneration.TokenUsage.InputTokens, lectureGeneration.TokenUsage.OutputTokens,
                        lectureGeneration.TokenUsage.CachedInputTokens, lectureStopwatch.ElapsedMilliseconds, cancellationToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // Usage logging must never break pedagogy generation itself.
                }

                // Also update the teaching step's visible text to the professor's spoken lecture class
                if (!string.IsNullOrWhiteSpace(lecture.SpokenLectureScript))
                {
                    teachingStep.Content = lecture.SpokenLectureScript;
                }
            }
            catch
            {
                // Best-effort: lecture generation failure does not abort the entire section pipeline.
            }
        }

        // 2. Synthesize audio via AzureSpeechService and upload to Data Lake
        if (_speechService.IsConfigured && _illustrationStorage.IsConfigured)
        {
            try
            {
                var speechText = lecture?.SpokenLectureScript ?? teachingStep.Content;
                if (!string.IsNullOrWhiteSpace(speechText))
                {
                    // Detect language from text to ensure an English course always uses an English male voice,
                    // and a Spanish course uses a Spanish male voice, even if document metadata is inconsistent.
                    var padded = $" {speechText.ToLowerInvariant()} ";
                    string[] englishWords = [" the ", " and ", " you ", " is ", " to ", " your ", " with ", " that ", " this "];
                    string[] spanishWords = [" el ", " la ", " de ", " que ", " los ", " las ", " para ", " con ", " es ", " tu "];
                    var isEnglish = englishWords.Count(w => padded.Contains(w)) > spanishWords.Count(w => padded.Contains(w));
                    var isSpanish = spanishWords.Count(w => padded.Contains(w)) > englishWords.Count(w => padded.Contains(w));

                    var voiceName = isEnglish
                        ? "en-US-AndrewNeural"
                        : isSpanish
                        ? "es-MX-CecilioNeural"
                        : (languageCode.StartsWith("es", StringComparison.OrdinalIgnoreCase) ? "es-MX-CecilioNeural" : "en-US-AndrewNeural");

                    var ttsStopwatch = System.Diagnostics.Stopwatch.StartNew();
                    var audioBytes = await _speechService.SynthesizeAsync(speechText, voiceName, cancellationToken);
                    ttsStopwatch.Stop();
                    using var audioStream = new MemoryStream(audioBytes);

                    var storagePath = await _illustrationStorage.UploadLectureAudioAsync(
                        section.TenantId,
                        document.StructuredLearningDocumentId,
                        section.StructuredLearningDocumentSectionId,
                        audioStream,
                        contentType: "audio/mpeg",
                        cancellationToken: cancellationToken);

                    teachingStep.SpeechStoragePath = storagePath;

                    // Real Azure Speech TTS cost of narrating the Learn step's
                    // lecture (2026-10-04) — previously never recorded anywhere,
                    // a genuine gap in both the admin "Cursos" cost tab and the
                    // Simulations authoring baseline. Reuses the SAME
                    // StructuredLearningGenerationUsage table with a sentinel
                    // ModelName ("azure-speech-tts") so ComputeCostForModel/
                    // TokenCostEstimator can price it by CHARACTER count (billed
                    // via AgentPricing:AzureSpeechNeuralPerMillionCharactersUsd)
                    // instead of the per-token LLM rate — InputTokens here holds
                    // the character count, not a token count.
                    try
                    {
                        await _usageService.RecordAsync(
                            document.StructuredLearningDocumentId, section.StructuredLearningDocumentSectionId, "Pedagogy",
                            "AzureSpeechService", TokenCostEstimator.AzureSpeechTtsModelSentinel, speechText.Length, 0, 0,
                            ttsStopwatch.ElapsedMilliseconds, cancellationToken);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        // Usage logging must never break pedagogy generation itself.
                    }
                }
            }
            catch
            {
                // Best-effort: audio synthesis failure does not abort the section pipeline.
            }
        }
    }

    private async Task GenerateIllustrationsAsync(
        HumanOsDbContext dbContext,
        Guid tenantId,
        Guid sectionId,
        List<StructuredLearningSectionStepDto> steps,
        SectionIllustrationOverride? illustrationOverride,
        CancellationToken cancellationToken)
    {
        if (!_illustrationStorage.IsConfigured)
        {
            return;
        }

        var mode = illustrationOverride?.Mode ?? "AiGenerated";
        if (mode == "None")
        {
            return;
        }

        if (mode == "UploadDirect" && illustrationOverride?.UploadedImageBytes is { Length: > 0 } uploadedBytes)
        {
            var teachingStep = steps.FirstOrDefault(s => s.StepType == StructuredLearningStepType.Teaching);
            if (teachingStep is null)
            {
                return;
            }

            string? caption = null;
            if (_imageDescriptionAgent.IsConfigured)
            {
                try
                {
                    var described = await _imageDescriptionAgent.DescribeAsync(
                        uploadedBytes, illustrationOverride.UploadedImageContentType ?? "image/png", teachingStep.Content, cancellationToken);
                    caption = described.Description;
                }
                catch
                {
                    // Best-effort: a missing caption never blocks using the uploaded image.
                }
            }

            try
            {
                using var uploadStream = new MemoryStream(uploadedBytes);
                var storagePath = await _illustrationStorage.UploadIllustrationAsync(
                    tenantId, capabilityId: sectionId, nodeId: sectionId, 1, uploadStream,
                    contentType: illustrationOverride.UploadedImageContentType ?? "image/png", cancellationToken: cancellationToken);

                var illustration = new StructuredLearningIllustration
                {
                    StructuredLearningIllustrationId = Guid.NewGuid(),
                    StructuredLearningDocumentSectionId = sectionId,
                    StoragePath = storagePath,
                    Prompt = "Imagen proporcionada por el profesor.",
                    Caption = caption,
                    Purpose = StructuredLearningIllustrationPurpose.Teaching,
                    ImageModel = "user-upload",
                    Width = 0,
                    Height = 0,
                    CreatedDate = DateTime.UtcNow
                };

                dbContext.StructuredLearningIllustrations.Add(illustration);
                await dbContext.SaveChangesAsync(cancellationToken);
                teachingStep.IllustrationId = illustration.StructuredLearningIllustrationId;
            }
            catch
            {
                // Best-effort: a failed upload never blocks section creation.
            }

            return;
        }

        if (!_imageService.IsConfigured)
        {
            return;
        }

        var useReference = mode == "AiGeneratedFromUpload" && illustrationOverride?.UploadedImageBytes is { Length: > 0 };
        var referenceImageBytes = useReference ? illustrationOverride!.UploadedImageBytes : null;

        var imageIndex = 0;

        foreach (var step in steps)
        {
            if (step.StepType is not (StructuredLearningStepType.Hypothesis or StructuredLearningStepType.Teaching))
            {
                continue;
            }
            if (string.IsNullOrWhiteSpace(step.IllustrationPrompt))
            {
                continue;
            }

            imageIndex++;
            var purpose = step.StepType == StructuredLearningStepType.Hypothesis
                ? StructuredLearningIllustrationPurpose.Hypothesis
                : StructuredLearningIllustrationPurpose.Teaching;

            step.IllustrationId = await GenerateAndPersistIllustrationAsync(
                dbContext, tenantId, sectionId, imageIndex, step.IllustrationPrompt, caption: null, purpose, referenceImageBytes, cancellationToken);
        }

        var deepDiveStep = steps.FirstOrDefault(s => s.StepType == StructuredLearningStepType.DeepDive);
        var cards = deepDiveStep?.ExampleCards;
        if (cards is not { Count: > 0 })
        {
            return;
        }

        foreach (var card in cards)
        {
            if (card.IllustrationPrompts is { Count: > 0 } stepPrompts)
            {
                var ids = new List<Guid>();
                foreach (var stepPrompt in stepPrompts)
                {
                    if (string.IsNullOrWhiteSpace(stepPrompt))
                    {
                        continue;
                    }

                    imageIndex++;
                    var id = await GenerateAndPersistIllustrationAsync(
                        dbContext, tenantId, sectionId, imageIndex, stepPrompt, card.Title, StructuredLearningIllustrationPurpose.DeepDiveCard, referenceImageBytes, cancellationToken);
                    if (id is not null)
                    {
                        ids.Add(id.Value);
                    }
                }

                if (ids.Count > 0)
                {
                    card.IllustrationIds = ids;
                }

                continue;
            }

            if (string.IsNullOrWhiteSpace(card.IllustrationPrompt))
            {
                continue;
            }

            imageIndex++;
            card.IllustrationId = await GenerateAndPersistIllustrationAsync(
                dbContext, tenantId, sectionId, imageIndex, card.IllustrationPrompt, card.Title, StructuredLearningIllustrationPurpose.DeepDiveCard, referenceImageBytes, cancellationToken);
        }
    }

    private async Task<Guid?> GenerateAndPersistIllustrationAsync(
        HumanOsDbContext dbContext,
        Guid tenantId,
        Guid sectionId,
        int imageIndex,
        string illustrationPrompt,
        string? caption,
        StructuredLearningIllustrationPurpose purpose,
        byte[]? referenceImageBytes,
        CancellationToken cancellationToken)
    {
        try
        {
#pragma warning disable OPENAI001 // GeneratedImageQuality.LowQuality is an experimental SDK member (2.9.1) — explicitly opted into for speed.
            var generated = referenceImageBytes is { Length: > 0 }
                ? await _imageService.GenerateFromReferenceAsync(illustrationPrompt, referenceImageBytes, cancellationToken, quality: OpenAI.Images.GeneratedImageQuality.LowQuality)
                : await _imageService.GenerateAsync(illustrationPrompt, logoImageBytes: null, cancellationToken, quality: OpenAI.Images.GeneratedImageQuality.LowQuality);
#pragma warning restore OPENAI001
            using var stream = generated.ImageBytes.ToStream();
            var storagePath = await _illustrationStorage.UploadIllustrationAsync(
                tenantId, capabilityId: sectionId, nodeId: sectionId, imageIndex, stream, cancellationToken: cancellationToken);

            var processVersion = "structured-learning-pedagogy-v1";
            var promptVersion = StructuredLearningSectionDesignerAgent.SourceFaithfulContractVersion;
            var illustration = new StructuredLearningIllustration
            {
                StructuredLearningIllustrationId = Guid.NewGuid(),
                StructuredLearningDocumentSectionId = sectionId,
                StoragePath = storagePath,
                Prompt = illustrationPrompt,
                PromptVersion = promptVersion,
                ProcessVersion = processVersion,
                PromptHash = HashString(illustrationPrompt),
                ConfigSnapshotJson = JsonSerializer.Serialize(new { processVersion, promptVersion, purpose, imageModel = generated.ImageModel }),
                Caption = caption,
                Purpose = purpose,
                ImageModel = generated.ImageModel,
                Width = generated.Width,
                Height = generated.Height,
                CreatedDate = DateTime.UtcNow
            };

            dbContext.StructuredLearningIllustrations.Add(illustration);
            await dbContext.SaveChangesAsync(cancellationToken);

            return illustration.StructuredLearningIllustrationId;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Best-effort — see GenerateIllustrationsAsync's doc comment.
            return null;
        }
    }

    /// <summary>Runs the quality guardrails (structural + content) written
    /// in <see cref="StructuredLearningSectionEvaluator"/> BEFORE this agent
    /// existed. Returns an empty list when everything passes.</summary>
    private static List<string> ValidateGeneratedSteps(
        List<StructuredLearningSectionStepDto> steps,
        StructuredLearningDocument document,
        bool includeDeepDiveStep,
        bool includeHypothesisStep,
        string sectionSourceText,
        string capabilityStatement)
    {
        var failures = new List<string>();

        var structural = StructuredLearningSectionEvaluator.ValidateStructure(
            steps.Select(s => (s.StepType, ContentForStructuralCheck(s))).ToList(),
            includeDeepDiveStep,
            document.IncludeRecallStep,
            document.IncludeProductionStep,
            document.IncludeAssessmentStep,
            includeHypothesisStep);
        if (!structural.Passed)
        {
            failures.AddRange(structural.Failures);
        }

        var deepDiveStep = steps.FirstOrDefault(s => s.StepType == StructuredLearningStepType.DeepDive);
        failures.AddRange(StructuredLearningSectionEvaluator.ValidateDeepDiveCards(
            includeDeepDiveStep,
            deepDiveStep?.ExampleCards?.Select(c => (c.Title, c.Text)).ToList()));

        // Atomicity (2026-08-17, Paso 4) — catches an engram whose
        // Capability statement bundles more than one teachable idea, which
        // the chapter/engram-extraction splitters should have separated
        // already; failing here triggers the same retry-once path as any
        // other structural failure.
        var atomicity = StructuredLearningSectionEvaluator.CheckCapabilityAtomicity(capabilityStatement);
        if (!atomicity.Passed)
        {
            failures.Add(atomicity.Reason ?? "Capability statement is not atomic.");
        }

        return failures;
    }

    /// <summary>DeepDive uses its own type-specific list instead of Content
    /// — build a representative string from it so the shared "no step is
    /// empty" structural check still applies meaningfully. Recall/Assessment
    /// (2026-08-10) intentionally carry NO design-time content at all
    /// (their real questions are generated live) — see
    /// <see cref="StructuredLearningSectionEvaluator.ValidateStructure"/>,
    /// which skips the empty-content check for those two step types.</summary>
    private static string ContentForStructuralCheck(StructuredLearningSectionStepDto step) => step.StepType switch
    {
        StructuredLearningStepType.DeepDive => string.Join(" ", step.ExampleCards?.Select(c => c.Text) ?? []),
        _ => step.Content
    };

    public static string? BuildStepDataJson(StructuredLearningSectionStepDto step) => step.StepType switch
    {
        StructuredLearningStepType.DeepDive when step.ExampleCards is { Count: > 0 } => JsonSerializer.Serialize(step.ExampleCards),
        StructuredLearningStepType.Hypothesis when step.HypothesisQuestions is { Count: > 0 } => JsonSerializer.Serialize(
            step.HypothesisQuestions.Select(q => new
            {
                question = q.Question,
                options = q.Options,
                correctIndex = q.CorrectIndex ?? 0
            })),
        StructuredLearningStepType.Teaching when step.ProfessorLecture is not null || step.IllustrationId is not null || (step.WebSearchCitations is { Count: > 0 }) =>
            JsonSerializer.Serialize(new
            {
                step.IllustrationId,
                step.SpeechStoragePath,
                ProfessorLecture = step.ProfessorLecture,
                WebSearchCitations = step.WebSearchCitations ?? []
            }),
        StructuredLearningStepType.Hypothesis when step.IllustrationId is not null =>
            JsonSerializer.Serialize(new { step.IllustrationId }),
        _ => null
    };
}
