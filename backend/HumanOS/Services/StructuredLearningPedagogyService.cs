using System.Text.Json;
using HumanOS.Agents.Studio;
using HumanOS.Data;
using HumanOS.Models.Capabilities.Studio;
using HumanOS.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

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

    public StructuredLearningPedagogyService(
        StructuredLearningSectionDesignerAgent designer,
        IDbContextFactory<HumanOsDbContext> dbContextFactory,
        IConfiguration configuration,
        GraphIllustrationImageService imageService,
        HumanOS.Agents.Studio.PdfImageDescriptionAgent imageDescriptionAgent)
    {
        _designer = designer;
        _dbContextFactory = dbContextFactory;
        _configuration = configuration;
        _imageService = imageService;
        _imageDescriptionAgent = imageDescriptionAgent;
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
    public async Task<StructuredLearningPedagogyResult> RunAsync(
        Guid documentId,
        string languageCode,
        Action<string, int, int, IReadOnlyList<string>> reportProgress,
        CancellationToken cancellationToken,
        bool forceRegenerateAll = false,
        byte[]? uploadedImageBytes = null,
        string? uploadedImageContentType = null,
        string? referenceImageUsageInstruction = null,
        SectionIllustrationOverride? illustrationOverride = null)
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

            pendingSections = await sectionsQuery
                .OrderBy(s => s.SortOrder)
                .ToListAsync(cancellationToken);
        }

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
            pedagogicalAudience = document.PedagogicalAudience,
            includeRecallStep = document.IncludeRecallStep,
            includeProductionStep = document.IncludeProductionStep,
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

        reportProgress($"Generando pedagogía para {totalSections} secciones pendientes", 0, totalSections, []);

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

                var (succeeded, inputTokens, outputTokens) = await ProcessSectionAsync(document, section, languageCode, perSectionOverride, cancellationToken);
                lock (progressLock)
                {
                    activeTitles.Remove(section.SubchapterTitle);
                    if (succeeded) completedCount++; else failedCount++;
                    totalInputTokens += inputTokens;
                    totalOutputTokens += outputTokens;
                    reportProgress(
                        $"{(succeeded ? "Completada" : "Falló")}: {section.SubchapterTitle}",
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

        var sectionOverride = illustrationOverride?.SectionId == section.StructuredLearningDocumentSectionId ? illustrationOverride : null;
        var illustrationGuidance = sectionOverride?.Mode == "AiGeneratedFromUpload" ? sectionOverride.ReferenceImageUsageInstruction : null;

        try
        {
            var design = await _designer.DesignAsync(
                section.ChapterTitle,
                section.SubchapterTitle,
                section.Text,
                languageCode,
                document.PedagogicalAudience,
                includeDeepDiveStep,
                document.MaxDeepDiveCardsPerNode,
                document.IncludeRecallStep,
                document.IncludeProductionStep,
                cancellationToken,
                illustrationGuidance);

            var steps = design.Response.Steps;
            var failures = ValidateGeneratedSteps(steps, document, includeDeepDiveStep, section.Text);

            if (failures.Count > 0)
            {
                // Retry once — occasionally the model drops/reorders a step
                // (e.g. skips DeepDive) despite the explicit instructions;
                // a single retry resolves the vast majority of these (same
                // cheap-retry pattern as StructuredLearningRecallEngine's
                // own null-score retry).
                var retryDesign = await _designer.DesignAsync(
                    section.ChapterTitle,
                    section.SubchapterTitle,
                    section.Text,
                    languageCode,
                    document.PedagogicalAudience,
                    includeDeepDiveStep,
                    document.MaxDeepDiveCardsPerNode,
                    document.IncludeRecallStep,
                    document.IncludeProductionStep,
                    cancellationToken,
                    illustrationGuidance);

                var retrySteps = retryDesign.Response.Steps;
                var retryFailures = ValidateGeneratedSteps(retrySteps, document, includeDeepDiveStep, section.Text);

                design = new StructuredLearningSectionDesignerAgent.DesignResult
                {
                    Response = retryDesign.Response,
                    TokenUsage = new AgentTokenUsage
                    {
                        AgentName = design.TokenUsage.AgentName,
                        ModelName = design.TokenUsage.ModelName,
                        InputTokens = design.TokenUsage.InputTokens + retryDesign.TokenUsage.InputTokens,
                        OutputTokens = design.TokenUsage.OutputTokens + retryDesign.TokenUsage.OutputTokens,
                        CachedInputTokens = design.TokenUsage.CachedInputTokens + retryDesign.TokenUsage.CachedInputTokens
                    }
                };
                steps = retrySteps;
                failures = retryFailures;
            }

            if (failures.Count > 0)
            {
                await MarkFailedAsync(dbContext, section.StructuredLearningDocumentSectionId, string.Join(" | ", failures), cancellationToken);
                return (false, design.TokenUsage.InputTokens, design.TokenUsage.OutputTokens);
            }

            await GenerateIllustrationsAsync(dbContext, section.TenantId, section.StructuredLearningDocumentSectionId, steps, sectionOverride, cancellationToken);

            // A "retry"/regenerate on a section that already has old steps
            // persisted (e.g. it succeeded once under an earlier prompt
            // version) must overwrite, not accumulate duplicates.
            await dbContext.StructuredLearningSectionSteps
                .Where(s => s.StructuredLearningDocumentSectionId == section.StructuredLearningDocumentSectionId)
                .ExecuteDeleteAsync(cancellationToken);

            var processVersion = "structured-learning-pedagogy-v1";
            var promptVersion = StructuredLearningSectionDesignerAgent.SourceFaithfulContractVersion;
            var modelName = design.TokenUsage.ModelName;
            var configSnapshot = JsonSerializer.Serialize(new
            {
                processVersion,
                promptVersion,
                includeDeepDiveStep,
                maxDeepDiveCards = document.MaxDeepDiveCardsPerNode,
                includeRecallStep = document.IncludeRecallStep,
                includeProductionStep = document.IncludeProductionStep,
                languageCode,
                pedagogicalAudience = document.PedagogicalAudience,
                illustrationGuidanceProvided = !string.IsNullOrWhiteSpace(illustrationGuidance)
            });

            var statements = design.Response.Statements ?? new List<StructuredLearningStatement>();

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

            return (true, design.TokenUsage.InputTokens, design.TokenUsage.OutputTokens);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await MarkFailedAsync(dbContext, section.StructuredLearningDocumentSectionId, ex.Message, cancellationToken);
            return (false, 0, 0);
        }
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
            var generated = referenceImageBytes is { Length: > 0 }
                ? await _imageService.GenerateFromReferenceAsync(illustrationPrompt, referenceImageBytes, cancellationToken)
                : await _imageService.GenerateAsync(illustrationPrompt, logoImageBytes: null, cancellationToken);
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
        string sectionSourceText)
    {
        var failures = new List<string>();

        var structural = StructuredLearningSectionEvaluator.ValidateStructure(
            steps.Select(s => (s.StepType, ContentForStructuralCheck(s))).ToList(),
            includeDeepDiveStep,
            document.IncludeRecallStep,
            document.IncludeProductionStep);
        if (!structural.Passed)
        {
            failures.AddRange(structural.Failures);
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
        StructuredLearningStepType.Hypothesis or StructuredLearningStepType.Teaching when step.IllustrationId is not null =>
            JsonSerializer.Serialize(new { step.IllustrationId }),
        _ => null
    };
}
