using System.Collections.Concurrent;
using System.Text.Json;
using HumanOS.Agents.Studio;
using HumanOS.Data;
using HumanOS.Models.Capabilities;
using HumanOS.Models.Capabilities.Traditional;
using HumanOS.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace HumanOS.Services;

/// <summary>
/// The "Traditional Course" pipeline (2026-08-09): the second, simpler
/// Studio course-creation method — linear Chapters -> Lessons (subchapters)
/// made of plain reading Cards (no Recall/Production/per-card graded
/// interaction), ending each chapter with ONE deterministic multiple-choice
/// exam. Deliberately reuses every existing Studio building block the
/// Graph/"Memory Paradox" pipeline (<see cref="PdfCapabilityGraphPipelineService"/>)
/// already uses for extraction, chapter/subchapter detection, grounding,
/// web enrichment and illustrations — only the CONTENT-DESIGN and
/// PERSISTENCE steps are new/simpler (see <see cref="LessonCardAgent"/>,
/// <see cref="ChapterExamAgent"/>).
///
/// Pipeline (see <see cref="RunAsync"/>):
///   1. Extract text (PdfContentExtractionService for a PDF, or
///      IdeaToDocumentAgent for the "idea/text" entry point).
///   2. Detect chapters + lessons via the SAME 3-tier
///      <see cref="Agents.Studio.DocumentChapterSplitter"/> used by the
///      Graph pipeline — "Chapter=Level, Subsection=Node" maps almost
///      exactly onto "Chapter/Lesson" here.
///   3. PER CHAPTER (parallel, bounded by <see cref="MaxConcurrentChapters"/>):
///      curate each lesson (CuradorAgent), optionally enrich with live web
///      search (WebGroundingService), design its reading cards
///      (LessonCardAgent), generate card illustrations (best-effort), then
///      design ONE multiple-choice final exam for the whole chapter
///      (ChapterExamAgent) grounded in all of that chapter's curated
///      content.
///   4. Generate a course cover image (best-effort).
///   5. Persist everything in one Capability (LearningMode=TraditionalChapters).
///
/// Deliberately Singleton-safe (only depends on Singleton agents +
/// IDbContextFactory + IConfiguration), same discipline as
/// <see cref="PdfCapabilityGraphPipelineService"/>, so
/// <see cref="TraditionalCoursePipelineOrchestrator"/> can hold a direct
/// reference.
/// </summary>
public sealed class TraditionalCoursePipelineService
{
    private const int DefaultMaxPages = 100;
    private const int DefaultMaxConcurrentChapters = 3;
    private const int DefaultQuestionsPerChapterExam = 5;
    private const int DefaultMaxImagesToDescribe = 60;
    private const int DefaultMaxImagesPerPage = 8;

    private readonly CuradorAgent _curador;
    private readonly TocExtractionAgent _tocExtraction;
    private readonly LessonCardAgent _lessonCard;
    private readonly ChapterExamAgent _chapterExam;
    private readonly DocumentContextAgent _documentContext;
    private readonly GraphIllustrationImageService _imageService;
    private readonly WebGroundingService _webGrounding;
    private readonly IdeaToDocumentAgent _ideaToDocument;
    private readonly PdfImageDescriptionAgent _pdfImageDescription;
    private readonly PdfContentExtractionService _pdfContentExtraction;
    private readonly IDbContextFactory<HumanOsDbContext> _dbContextFactory;
    private readonly IConfiguration _configuration;
    private readonly CapabilityGraphIllustrationStorageService _illustrationStorage;

    public TraditionalCoursePipelineService(
        CuradorAgent curador,
        TocExtractionAgent tocExtraction,
        LessonCardAgent lessonCard,
        ChapterExamAgent chapterExam,
        DocumentContextAgent documentContext,
        GraphIllustrationImageService imageService,
        WebGroundingService webGrounding,
        IdeaToDocumentAgent ideaToDocument,
        PdfImageDescriptionAgent pdfImageDescription,
        PdfContentExtractionService pdfContentExtraction,
        IDbContextFactory<HumanOsDbContext> dbContextFactory,
        IConfiguration configuration)
    {
        _curador = curador;
        _tocExtraction = tocExtraction;
        _lessonCard = lessonCard;
        _chapterExam = chapterExam;
        _documentContext = documentContext;
        _imageService = imageService;
        _webGrounding = webGrounding;
        _ideaToDocument = ideaToDocument;
        _pdfImageDescription = pdfImageDescription;
        _pdfContentExtraction = pdfContentExtraction;
        _dbContextFactory = dbContextFactory;
        _configuration = configuration;
        _illustrationStorage = new CapabilityGraphIllustrationStorageService(configuration);
    }

    public bool IsConfigured => _curador.IsConfigured && _lessonCard.IsConfigured && _chapterExam.IsConfigured;

    public int MaxSourcePdfPages
    {
        get
        {
            var raw = _configuration["MaxCapabilitySourcePdfPages"];
            return int.TryParse(raw, out var value) && value > 0 ? value : DefaultMaxPages;
        }
    }

    public int MaxConcurrentChapters
    {
        get
        {
            var raw = _configuration["MaxConcurrentTraditionalChapters"];
            return int.TryParse(raw, out var value) && value > 0 ? value : DefaultMaxConcurrentChapters;
        }
    }

    public sealed class PdfTooLargeException(int pageCount, int maxPages) : Exception(
        $"The PDF has {pageCount} pages, which exceeds the configured maximum of {maxPages} pages.")
    {
        public int PageCount { get; } = pageCount;

        public int MaxPages { get; } = maxPages;
    }

    // ==================== In-memory build DTOs (pre-persistence) ====================

    private sealed class BuiltIllustration
    {
        public string StoragePath { get; set; } = string.Empty;

        public string Prompt { get; set; } = string.Empty;

        public string? Caption { get; set; }

        public string ImageModel { get; set; } = string.Empty;

        public int Width { get; set; }

        public int Height { get; set; }

        public TraditionalIllustrationPurpose Purpose { get; set; }
    }

    private sealed class BuiltCard
    {
        public string Title { get; set; } = string.Empty;

        public string Text { get; set; } = string.Empty;

        public int SortOrder { get; set; }

        public BuiltIllustration? Illustration { get; set; }
    }

    private sealed class BuiltLesson
    {
        public string Title { get; set; } = string.Empty;

        public int SortOrder { get; set; }

        public List<BuiltCard> Cards { get; set; } = [];
    }

    private sealed class BuiltExamQuestion
    {
        public string QuestionText { get; set; } = string.Empty;

        public List<string> Options { get; set; } = [];

        public int CorrectOptionIndex { get; set; }

        public int SortOrder { get; set; }

        public BuiltIllustration? Illustration { get; set; }
    }

    private sealed class BuiltChapter
    {
        public string Title { get; set; } = string.Empty;

        public int SortOrder { get; set; }

        public List<BuiltLesson> Lessons { get; set; } = [];

        public List<BuiltExamQuestion> ExamQuestions { get; set; } = [];
    }

    /// <summary>Entry point for an uploaded PDF source document.</summary>
    public async Task<TraditionalCourseResult> RunAsync(
        byte[] pdfBytes,
        string fileName,
        Guid capabilityDomainId,
        string capabilityName,
        Guid tenantId,
        Action<string> reportProgress,
        CancellationToken cancellationToken,
        bool enableWebEnrichment = false,
        Guid? subjectId = null,
        string? capabilityDescription = null,
        string languageCode = "es",
        string? pedagogicalAudience = null,
        int questionsPerChapterExam = DefaultQuestionsPerChapterExam,
        bool strictSyllabusMode = false,
        List<CapabilityOutlineChapterDto>? approvedOutline = null,
        byte[]? brandLogoBytes = null,
        byte[]? uploadedReferenceImageBytes = null,
        string? referenceImageUsageInstruction = null,
        Guid? ownerPersonId = null,
        Guid? ownerTenantId = null,
        DateTime? consentAcceptedDate = null,
        Guid? consentAcceptedByPersonId = null,
        string? consentAcceptedByName = null)
    {
        ReportProgress(reportProgress, languageCode, "Extracting text from the document", "Extrayendo texto del documento");

        var maxImagesToDescribe = _configuration.GetValue<int?>("MaxCapabilityPdfImagesToDescribe") ?? DefaultMaxImagesToDescribe;
        var maxImagesPerPage = _configuration.GetValue<int?>("MaxCapabilityPdfImagesPerPage") ?? DefaultMaxImagesPerPage;

        var extraction = await _pdfContentExtraction.ExtractAsync(
            pdfBytes, _pdfImageDescription, maxImagesToDescribe, maxImagesPerPage, reportProgress, cancellationToken);

        if (extraction.PageCount > MaxSourcePdfPages)
        {
            throw new PdfTooLargeException(extraction.PageCount, MaxSourcePdfPages);
        }

        return await RunFromDocumentTextAsync(
            extraction.CombinedText, $"el PDF '{fileName}'", extraction.PageCount, capabilityDomainId, capabilityName, tenantId,
            reportProgress, cancellationToken, enableWebEnrichment, subjectId, capabilityDescription, languageCode, pedagogicalAudience,
            questionsPerChapterExam, strictSyllabusMode, approvedOutline, brandLogoBytes, uploadedReferenceImageBytes,
            referenceImageUsageInstruction, ownerPersonId, ownerTenantId, consentAcceptedDate, consentAcceptedByPersonId,
            consentAcceptedByName);
    }

    /// <summary>Entry point for the "Texto/idea" source (no PDF).</summary>
    public async Task<TraditionalCourseResult> RunFromDescriptionAsync(
        string description,
        string capabilityName,
        Guid capabilityDomainId,
        Guid tenantId,
        Action<string> reportProgress,
        CancellationToken cancellationToken,
        bool enableWebEnrichment = false,
        Guid? subjectId = null,
        string languageCode = "es",
        string? pedagogicalAudience = null,
        int questionsPerChapterExam = DefaultQuestionsPerChapterExam,
        bool strictSyllabusMode = false,
        List<CapabilityOutlineChapterDto>? approvedOutline = null,
        string? preExpandedDocumentText = null,
        byte[]? brandLogoBytes = null,
        byte[]? uploadedReferenceImageBytes = null,
        string? referenceImageUsageInstruction = null,
        Guid? ownerPersonId = null,
        Guid? ownerTenantId = null,
        DateTime? consentAcceptedDate = null,
        Guid? consentAcceptedByPersonId = null,
        string? consentAcceptedByName = null)
    {
        string documentText;
        if (!string.IsNullOrWhiteSpace(preExpandedDocumentText))
        {
            documentText = preExpandedDocumentText;
        }
        else
        {
            ReportProgress(reportProgress, languageCode, "Expanding the idea into a complete document", "Expandiendo la idea en un documento completo");
            var expansion = await _ideaToDocument.ExpandAsync(capabilityName, description, languageCode, strictSyllabusMode, cancellationToken);
            documentText = expansion.DocumentText;
        }

        return await RunFromDocumentTextAsync(
            documentText, "la descripción proporcionada", 0, capabilityDomainId, capabilityName, tenantId,
            reportProgress, cancellationToken, enableWebEnrichment, subjectId, description, languageCode, pedagogicalAudience,
            questionsPerChapterExam, strictSyllabusMode, approvedOutline, brandLogoBytes, uploadedReferenceImageBytes,
            referenceImageUsageInstruction, ownerPersonId, ownerTenantId, consentAcceptedDate, consentAcceptedByPersonId,
            consentAcceptedByName);
    }

    private async Task<TraditionalCourseResult> RunFromDocumentTextAsync(
        string documentText,
        string sourceDescription,
        int pageCount,
        Guid capabilityDomainId,
        string capabilityName,
        Guid tenantId,
        Action<string> reportProgress,
        CancellationToken cancellationToken,
        bool enableWebEnrichment,
        Guid? subjectId,
        string? capabilityDescription,
        string languageCode,
        string? pedagogicalAudience,
        int questionsPerChapterExam,
        bool strictSyllabusMode,
        List<CapabilityOutlineChapterDto>? approvedOutline,
        byte[]? brandLogoBytes,
        byte[]? uploadedReferenceImageBytes,
        string? referenceImageUsageInstruction,
        Guid? ownerPersonId,
        Guid? ownerTenantId,
        DateTime? consentAcceptedDate,
        Guid? consentAcceptedByPersonId,
        string? consentAcceptedByName)
    {
        if (string.IsNullOrWhiteSpace(documentText))
        {
            throw new InvalidOperationException("No extractable text was found for this course's source material.");
        }

        var normalizedQuestionsPerChapterExam = Math.Clamp(questionsPerChapterExam, 1, 20);
        var normalizedLanguageCode = string.IsNullOrWhiteSpace(languageCode) ? "es" : languageCode.Trim().ToLowerInvariant();

        // ==================== 1. DETECT CHAPTERS + LESSONS ====================
        ReportProgress(reportProgress, normalizedLanguageCode, "Detecting chapters and lessons", "Detectando capítulos y subcapítulos");

        var source = new RawMaterialItem { Type = RawMaterialType.Pdf, Label = capabilityName, Content = documentText };

        List<RawMaterialItem> lessonItems = approvedOutline is { Count: > 0 }
            ? DocumentChapterSplitter.BuildMaterialsFromApprovedOutline(source, approvedOutline)
            : await DocumentChapterSplitter.SplitIntoChapterMaterialsAsync(source, _tocExtraction, strictSyllabusMode, cancellationToken);

        // Group the flat lesson list into chapters, preserving first-seen
        // order — LevelTitle is the parent chapter when this item is a
        // finer-grained subsection/lesson; falls back to the item's own
        // ChapterTitle when it has no further subsection split (that whole
        // item IS both the chapter and its only lesson).
        var chapterOrder = new List<string>();
        var chapterLessons = new Dictionary<string, List<RawMaterialItem>>();
        foreach (var item in lessonItems)
        {
            var chapterKey = item.LevelTitle ?? item.ChapterTitle ?? capabilityName;
            if (!chapterLessons.TryGetValue(chapterKey, out var list))
            {
                list = [];
                chapterLessons[chapterKey] = list;
                chapterOrder.Add(chapterKey);
            }

            list.Add(item);
        }

        var tokenUsage = new ConcurrentBag<AgentTokenUsage>();
        var illustrationsGenerated = 0;
        // One curated-text chunk per chapter, tagged by its title — the ONLY
        // input the course-wide executive summary (step 2.5 below) is grounded in.
        var curatedChapterChunks = new ConcurrentBag<CuratedChunk>();

        // ==================== 2. PER-CHAPTER CONTENT DESIGN (parallel) ====================
        ReportProgress(reportProgress, normalizedLanguageCode, "Designing lessons and chapter exams", "Diseñando lecciones y exámenes por capítulo");

        var builtChapters = new BuiltChapter[chapterOrder.Count];
        using var semaphore = new SemaphoreSlim(MaxConcurrentChapters);
        // Boxed in an array (not a plain int) so it can be shared and safely
        // mutated via Interlocked from multiple parallel chapter tasks without
        // an async method needing a `ref int` parameter (not allowed — CS1988).
        var illustrationCounter = new int[1];

        var chapterTasks = chapterOrder.Select(async (chapterKey, chapterIndex) =>
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                var lessons = chapterLessons[chapterKey];
                var builtChapter = new BuiltChapter { Title = chapterKey, SortOrder = chapterIndex + 1 };
                var chapterContentParts = new List<string>();

                for (var lessonIndex = 0; lessonIndex < lessons.Count; lessonIndex++)
                {
                    var lessonItem = lessons[lessonIndex];
                    var lessonTitle = lessonItem.ChapterTitle ?? lessonItem.Label;

                    var curation = await _curador.CurateAsync([lessonItem], cancellationToken);
                    tokenUsage.Add(WithModuleId(curation.TokenUsage, lessonTitle));

                    var curatedText = string.Join(
                        "\n\n",
                        curation.Corpus.Chunks.Select(c => $"[{c.Tag}] {c.Content}"));
                    chapterContentParts.Add(curatedText);

                    string? webFindings = null;
                    if (enableWebEnrichment && _webGrounding.IsConfigured)
                    {
                        try
                        {
                            var grounding = await _webGrounding.SearchAsync(lessonTitle, capabilityName, cancellationToken);
                            webFindings = grounding.Text;
                        }
                        catch (Exception ex) when (ex is not OperationCanceledException)
                        {
                            // Best-effort — web enrichment never blocks course creation.
                        }
                    }

                    var cardsResult = await _lessonCard.DesignAsync(
                        chapterIndex + 1,
                        chapterKey,
                        lessonIndex + 1,
                        lessonTitle,
                        curatedText,
                        webFindings,
                        normalizedLanguageCode,
                        cancellationToken);
                    tokenUsage.Add(WithModuleId(cardsResult.TokenUsage, lessonTitle));

                    var builtLesson = new BuiltLesson { Title = lessonTitle, SortOrder = lessonIndex + 1 };

                    for (var cardIndex = 0; cardIndex < cardsResult.Response.Cards.Count; cardIndex++)
                    {
                        var card = cardsResult.Response.Cards[cardIndex];
                        var builtCard = new BuiltCard { Title = card.Title, Text = card.Text, SortOrder = cardIndex + 1 };

                        if (!string.IsNullOrWhiteSpace(card.IllustrationPrompt) && _imageService.IsConfigured && _illustrationStorage.IsConfigured)
                        {
                            try
                            {
                                var generated = uploadedReferenceImageBytes is { Length: > 0 }
                                    ? await _imageService.GenerateFromReferenceAsync(
                                        BuildReferenceImagePrompt(card.IllustrationPrompt, referenceImageUsageInstruction),
                                        uploadedReferenceImageBytes,
                                        cancellationToken)
                                    : await _imageService.GenerateAsync(card.IllustrationPrompt, brandLogoBytes, cancellationToken);
                                builtCard.Illustration = new BuiltIllustration
                                {
                                    Prompt = card.IllustrationPrompt,
                                    Caption = card.Title,
                                    ImageModel = generated.ImageModel,
                                    Width = generated.Width,
                                    Height = generated.Height,
                                    Purpose = TraditionalIllustrationPurpose.Card,
                                    // StoragePath is resolved after CapabilityId exists — stash bytes via closure below.
                                };
                                builtCard.Illustration.StoragePath = await UploadPendingAsync(generated.ImageBytes, tenantId, illustrationCounter, cancellationToken);
                                Interlocked.Increment(ref illustrationsGenerated);
                            }
                            catch (Exception ex) when (ex is not OperationCanceledException)
                            {
                                // Best-effort — a failed card illustration never blocks course creation.
                            }
                        }

                        builtLesson.Cards.Add(builtCard);
                    }

                    builtChapter.Lessons.Add(builtLesson);
                }

                var chapterCuratedText = string.Join("\n\n---\n\n", chapterContentParts);
                curatedChapterChunks.Add(new CuratedChunk { Tag = chapterKey, Content = chapterCuratedText });
                var examResult = await _chapterExam.DesignAsync(chapterKey, chapterCuratedText, normalizedQuestionsPerChapterExam, normalizedLanguageCode, cancellationToken);
                tokenUsage.Add(WithModuleId(examResult.TokenUsage, chapterKey));

                for (var questionIndex = 0; questionIndex < examResult.Response.Questions.Count; questionIndex++)
                {
                    var question = examResult.Response.Questions[questionIndex];
                    var builtQuestion = new BuiltExamQuestion
                    {
                        QuestionText = question.QuestionText,
                        Options = question.Options,
                        CorrectOptionIndex = question.CorrectOptionIndex,
                        SortOrder = questionIndex + 1
                    };

                    if (!string.IsNullOrWhiteSpace(question.IllustrationPrompt) && _imageService.IsConfigured && _illustrationStorage.IsConfigured)
                    {
                        try
                        {
                            var generated = uploadedReferenceImageBytes is { Length: > 0 }
                                ? await _imageService.GenerateFromReferenceAsync(
                                    BuildReferenceImagePrompt(question.IllustrationPrompt, referenceImageUsageInstruction),
                                    uploadedReferenceImageBytes,
                                    cancellationToken)
                                : await _imageService.GenerateAsync(question.IllustrationPrompt, brandLogoBytes, cancellationToken);
                            builtQuestion.Illustration = new BuiltIllustration
                            {
                                Prompt = question.IllustrationPrompt,
                                ImageModel = generated.ImageModel,
                                Width = generated.Width,
                                Height = generated.Height,
                                Purpose = TraditionalIllustrationPurpose.ExamQuestion,
                                StoragePath = await UploadPendingAsync(generated.ImageBytes, tenantId, illustrationCounter, cancellationToken)
                            };
                            Interlocked.Increment(ref illustrationsGenerated);
                        }
                        catch (Exception ex) when (ex is not OperationCanceledException)
                        {
                            // Best-effort.
                        }
                    }

                    builtChapter.ExamQuestions.Add(builtQuestion);
                }

                builtChapters[chapterIndex] = builtChapter;
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(chapterTasks);

        // ==================== 2.5 COURSE-WIDE EXECUTIVE SUMMARY (best-effort) ====================
        // ONE call over every chapter's own curated content — never a
        // per-lesson gloss — mirrors DocumentContextAgent's exact role in the
        // Structured Learning pipeline (see StructuredLearningExtractionService).
        string? executiveSummary = null;
        if (_documentContext.IsConfigured)
        {
            ReportProgress(reportProgress, normalizedLanguageCode, "Writing the course executive summary", "Redactando el resumen ejecutivo del curso");
            try
            {
                var courseCorpus = new CuratedCorpus { Summary = string.Empty, Chunks = [.. curatedChapterChunks] };
                var contextResult = await _documentContext.ExtractAsync(capabilityName, courseCorpus, normalizedLanguageCode, cancellationToken);
                executiveSummary = contextResult.Context.ExecutiveSummary;
                tokenUsage.Add(contextResult.TokenUsage);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Best-effort — never blocks course creation.
            }
        }

        // ==================== 3. CREATE THE CAPABILITY ROW ====================
        ReportProgress(reportProgress, normalizedLanguageCode, "Creating the course", "Creando el curso");

        Guid capabilityId;
        await using (var setupDbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            capabilityId = await EnsureCapabilityAsync(
                setupDbContext, capabilityDomainId, capabilityName, subjectId, sourceDescription, capabilityDescription,
                normalizedLanguageCode, pedagogicalAudience, ownerPersonId, ownerTenantId, consentAcceptedDate,
                consentAcceptedByPersonId, consentAcceptedByName, executiveSummary, cancellationToken);
        }

        // ==================== 4. COURSE COVER IMAGE (best-effort) ====================
        string? coverImageStoragePath = null;
        if (_imageService.IsConfigured && _illustrationStorage.IsConfigured)
        {
            ReportProgress(reportProgress, normalizedLanguageCode, "Generating the course cover image", "Generando imagen de portada del curso");
            try
            {
                var coverPrompt =
                    $"A clean, modern, professional editorial illustration representing a course titled " +
                    $"'{capabilityName}'. Flat design, no text, no letters, no words, wide banner composition, soft color palette.";

                var generatedCover = uploadedReferenceImageBytes is { Length: > 0 }
                    ? await _imageService.GenerateFromReferenceAsync(
                        BuildReferenceImagePrompt(coverPrompt, referenceImageUsageInstruction),
                        uploadedReferenceImageBytes,
                        cancellationToken)
                    : await _imageService.GenerateAsync(coverPrompt, brandLogoBytes, cancellationToken);
                using var coverStream = generatedCover.ImageBytes.ToStream();
                coverImageStoragePath = await _illustrationStorage.UploadCoverImageAsync(tenantId, capabilityId, coverStream, cancellationToken: cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Best-effort.
            }
        }

        // ==================== 5. PERSIST EVERYTHING ====================
        ReportProgress(reportProgress, normalizedLanguageCode, "Saving the course to the database", "Guardando el curso en la base de datos");

        int lessonCount = 0, cardCount = 0, examQuestionCount = 0;
        await using (var persistDbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            foreach (var builtChapter in builtChapters)
            {
                var chapterEntity = new CapabilityChapter
                {
                    CapabilityChapterId = Guid.NewGuid(),
                    CapabilityId = capabilityId,
                    Title = builtChapter.Title,
                    SortOrder = builtChapter.SortOrder,
                    CreatedDate = DateTime.UtcNow
                };

                foreach (var builtLesson in builtChapter.Lessons)
                {
                    lessonCount++;
                    var lessonEntity = new CapabilityLesson
                    {
                        CapabilityLessonId = Guid.NewGuid(),
                        CapabilityChapterId = chapterEntity.CapabilityChapterId,
                        Title = builtLesson.Title,
                        SortOrder = builtLesson.SortOrder,
                        CreatedDate = DateTime.UtcNow
                    };

                    foreach (var builtCard in builtLesson.Cards)
                    {
                        cardCount++;
                        Guid? illustrationId = null;
                        if (builtCard.Illustration is { } illustration)
                        {
                            var illustrationEntity = ToIllustrationEntity(illustration, capabilityId);
                            persistDbContext.CapabilityTraditionalIllustrations.Add(illustrationEntity);
                            illustrationId = illustrationEntity.CapabilityTraditionalIllustrationId;
                        }

                        lessonEntity.Cards.Add(new CapabilityLessonCard
                        {
                            CapabilityLessonCardId = Guid.NewGuid(),
                            CapabilityLessonId = lessonEntity.CapabilityLessonId,
                            Title = builtCard.Title,
                            Text = builtCard.Text,
                            SortOrder = builtCard.SortOrder,
                            CapabilityTraditionalIllustrationId = illustrationId,
                            CreatedDate = DateTime.UtcNow
                        });
                    }

                    chapterEntity.Lessons.Add(lessonEntity);
                }

                foreach (var builtQuestion in builtChapter.ExamQuestions)
                {
                    examQuestionCount++;
                    Guid? illustrationId = null;
                    if (builtQuestion.Illustration is { } illustration)
                    {
                        var illustrationEntity = ToIllustrationEntity(illustration, capabilityId);
                        persistDbContext.CapabilityTraditionalIllustrations.Add(illustrationEntity);
                        illustrationId = illustrationEntity.CapabilityTraditionalIllustrationId;
                    }

                    chapterEntity.ExamQuestions.Add(new CapabilityChapterExamQuestion
                    {
                        CapabilityChapterExamQuestionId = Guid.NewGuid(),
                        CapabilityChapterId = chapterEntity.CapabilityChapterId,
                        QuestionText = builtQuestion.QuestionText,
                        OptionsJson = JsonSerializer.Serialize(builtQuestion.Options),
                        CorrectOptionIndex = builtQuestion.CorrectOptionIndex,
                        SortOrder = builtQuestion.SortOrder,
                        CapabilityTraditionalIllustrationId = illustrationId,
                        CreatedDate = DateTime.UtcNow
                    });
                }

                persistDbContext.CapabilityChapters.Add(chapterEntity);
            }

            if (coverImageStoragePath is not null)
            {
                persistDbContext.CapabilityTraditionalIllustrations.Add(new CapabilityTraditionalIllustration
                {
                    CapabilityTraditionalIllustrationId = Guid.NewGuid(),
                    CapabilityId = capabilityId,
                    StoragePath = coverImageStoragePath,
                    Prompt = "Course cover",
                    Purpose = TraditionalIllustrationPurpose.Cover,
                    ImageModel = "gpt-image",
                    Width = 1024,
                    Height = 1024,
                    CreatedDate = DateTime.UtcNow
                });
            }

            await persistDbContext.SaveChangesAsync(cancellationToken);
        }

        return new TraditionalCourseResult
        {
            CapabilityId = capabilityId,
            CourseName = capabilityName,
            ExecutiveSummary = executiveSummary,
            PageCount = pageCount,
            ChapterCount = builtChapters.Length,
            LessonCount = lessonCount,
            CardCount = cardCount,
            ExamQuestionCount = examQuestionCount,
            IllustrationsGeneratedCount = illustrationsGenerated,
            TokenUsage = [.. tokenUsage]
        };
    }

    private static string BuildReferenceImagePrompt(string prompt, string? referenceImageUsageInstruction)
    {
        var guidance = string.IsNullOrWhiteSpace(referenceImageUsageInstruction)
            ? "Use the uploaded reference image as visual guidance for the composition, subject, style, and overall mood of this illustration."
            : referenceImageUsageInstruction.Trim();

        return $"{prompt}\n\nReference image guidance: {guidance} Use the uploaded image as an actual visual reference, not just a vague style suggestion. Adapt it to fit this scene while keeping the image content faithful to the reference when appropriate.";
    }

    private async Task<string> UploadPendingAsync(BinaryData imageBytes, Guid tenantId, int[] illustrationCounter, CancellationToken cancellationToken)
    {
        // Illustrations are generated/uploaded BEFORE the Capability row
        // exists (content design runs in parallel per chapter, ahead of
        // Capability creation) — uploaded under a temporary per-run folder
        // keyed by a fresh GUID, then referenced as-is; the blob path
        // itself is opaque (IDs only) so this is safe and permanent.
        var pseudoNodeId = Guid.NewGuid();
        var index = Interlocked.Increment(ref illustrationCounter[0]);
        using var stream = imageBytes.ToStream();
        return await _illustrationStorage.UploadIllustrationAsync(tenantId, pseudoNodeId, pseudoNodeId, index, stream, cancellationToken: cancellationToken);
    }

    private static CapabilityTraditionalIllustration ToIllustrationEntity(BuiltIllustration illustration, Guid capabilityId) => new()
    {
        CapabilityTraditionalIllustrationId = Guid.NewGuid(),
        CapabilityId = capabilityId,
        StoragePath = illustration.StoragePath,
        Prompt = illustration.Prompt,
        Caption = illustration.Caption,
        ImageModel = illustration.ImageModel,
        Width = illustration.Width,
        Height = illustration.Height,
        Purpose = illustration.Purpose,
        CreatedDate = DateTime.UtcNow
    };

    private static void ReportProgress(Action<string> reportProgress, string? languageCode, string englishMessage, string spanishMessage)
    {
        reportProgress(string.Equals(languageCode?.Trim(), "en", StringComparison.OrdinalIgnoreCase)
            ? englishMessage
            : spanishMessage);
    }

    private static AgentTokenUsage WithModuleId(AgentTokenUsage usage, string moduleId) => new()
    {
        AgentName = usage.AgentName,
        ModuleId = moduleId,
        ModelName = usage.ModelName,
        InputTokens = usage.InputTokens,
        OutputTokens = usage.OutputTokens,
        CachedInputTokens = usage.CachedInputTokens
    };

    /// <summary>Generates and persists ExecutiveSummary for an EXISTING
    /// Traditional Course created before this feature existed (2026-08-21) —
    /// grounded ONLY in that course's own already-persisted chapter/lesson
    /// card text (never invented), one CuratedChunk per chapter. Throws if
    /// the capability doesn't exist, isn't TraditionalChapters, or the
    /// DocumentContextAgent isn't configured.</summary>
    public async Task<string> BackfillExecutiveSummaryAsync(Guid capabilityId, CancellationToken cancellationToken = default)
    {
        if (!_documentContext.IsConfigured)
        {
            throw new InvalidOperationException(
                "The DocumentContext agent is not configured. Set the 'AzureOpenAIEndpoint' and 'AzureOpenAIDeploymentName' application settings.");
        }

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var capability = await dbContext.Capabilities.FirstOrDefaultAsync(
            c => c.CapabilityId == capabilityId && c.LearningMode == CourseLearningMode.TraditionalChapters, cancellationToken)
            ?? throw new InvalidOperationException($"No traditional-mode course found with CapabilityId '{capabilityId}'.");

        var chapters = await dbContext.CapabilityChapters
            .Include(c => c.Lessons).ThenInclude(l => l.Cards)
            .Where(c => c.CapabilityId == capabilityId)
            .OrderBy(c => c.SortOrder)
            .ToListAsync(cancellationToken);

        var chunks = chapters.Select(chapter => new CuratedChunk
        {
            Tag = chapter.Title,
            Content = string.Join(
                "\n\n",
                chapter.Lessons.OrderBy(l => l.SortOrder).SelectMany(l => l.Cards.OrderBy(c => c.SortOrder))
                    .Select(card => $"{card.Title}\n{card.Text}"))
        }).ToList();

        var corpus = new CuratedCorpus { Summary = string.Empty, Chunks = chunks };
        var contextResult = await _documentContext.ExtractAsync(capability.Name, corpus, capability.ContentLanguageCode, cancellationToken);

        capability.ExecutiveSummary = contextResult.Context.ExecutiveSummary;
        capability.UpdatedDate = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        return contextResult.Context.ExecutiveSummary;
    }

    private static async Task<Guid> EnsureCapabilityAsync(
        HumanOsDbContext dbContext,
        Guid capabilityDomainId,
        string capabilityName,
        Guid? subjectId,
        string generationSourceDescription,
        string? userProvidedDescription,
        string languageCode,
        string? pedagogicalAudience,
        Guid? ownerPersonId,
        Guid? ownerTenantId,
        DateTime? consentAcceptedDate,
        Guid? consentAcceptedByPersonId,
        string? consentAcceptedByName,
        string? executiveSummary,
        CancellationToken cancellationToken)
    {
        var domainExists = await dbContext.CapabilityDomains.AnyAsync(d => d.CapabilityDomainId == capabilityDomainId, cancellationToken);
        if (!domainExists)
        {
            throw new InvalidOperationException($"CapabilityDomainId '{capabilityDomainId}' does not exist.");
        }

        if (subjectId is Guid nonNullSubjectId)
        {
            var subjectExists = await dbContext.Subjects.AnyAsync(s => s.SubjectId == nonNullSubjectId, cancellationToken);
            if (!subjectExists)
            {
                throw new InvalidOperationException($"SubjectId '{nonNullSubjectId}' does not exist.");
            }
        }

        var capability = new Capability
        {
            CapabilityId = Guid.NewGuid(),
            CapabilityDomainId = capabilityDomainId,
            SubjectId = subjectId,
            LearningMode = CourseLearningMode.TraditionalChapters,
            Code = $"TRAD-{DateTime.UtcNow:yyyyMMddHHmmssfff}",
            Name = capabilityName,
            Description = !string.IsNullOrWhiteSpace(userProvidedDescription)
                ? userProvidedDescription
                : $"Generado automáticamente a partir de {generationSourceDescription}.",
            ExecutiveSummary = executiveSummary,
            IsActive = true,
            ContentLanguageCode = languageCode,
            PedagogicalAudience = string.IsNullOrWhiteSpace(pedagogicalAudience) ? null : pedagogicalAudience.Trim(),
            OwnerPersonId = ownerPersonId,
            OwnerTenantId = ownerTenantId,
            ConsentAcceptedDate = consentAcceptedDate,
            ConsentAcceptedByPersonId = consentAcceptedByPersonId,
            ConsentAcceptedByName = string.IsNullOrWhiteSpace(consentAcceptedByName) ? null : consentAcceptedByName.Trim(),
            CreatedDate = DateTime.UtcNow,
            UpdatedDate = DateTime.UtcNow
        };

        dbContext.Capabilities.Add(capability);
        await dbContext.SaveChangesAsync(cancellationToken);
        return capability.CapabilityId;
    }
}
