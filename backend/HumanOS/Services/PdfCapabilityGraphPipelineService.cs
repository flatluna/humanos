using HumanOS.Agents.Studio;
using HumanOS.Data;
using HumanOS.Models.Capabilities;
using HumanOS.Models.Capabilities.Graph;
using HumanOS.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace HumanOS.Services;

/// <summary>
/// The V2 "PDF → CapabilityGraph" pipeline: uploads a real PDF and turns it
/// into a fully persisted CapabilityGraph (nodes + edges + illustrations)
/// plus a per-node Memory Paradox blueprint (Hypothesis/Teaching/Recall/
/// Production/Assessment), reusing every existing Studio agent exactly as
/// they already work — see /memories/repo/humanstudio-multiagent-vision.md
/// and the design discussion that produced this pipeline.
///
/// Pipeline (see <see cref="RunAsync"/> for the authoritative step order):
///   1. Extract text + page count (PdfPig) — reject if over the
///      configured page limit.
///   2. Detect chapters (TocExtractionAgent, best-effort) and slice the
///      text into one chapter per <see cref="DocumentChapterSplitter"/>
///      (the SAME algorithm V1's CuradorExecutor uses).
///   3. Curate EACH chapter separately (CuradorAgent, one call per
///      chapter) — this keeps grounding scoped and lets each chunk's Tag
///      be annotated with its chapter's order/title.
///   4. MERGE all chapters' curated chunks into ONE CuratedCorpus.
///   5. Design the graph with a SINGLE GraphArchitectAgent call over the
///      merged corpus (never once per chapter) — this is what lets the
///      LLM create cross-chapter Requires/BuildsOn edges. The document's
///      chapter order is passed along as an optional signal, never a
///      rigid per-chapter node structure (graph stays flat/plain — see
///      the "Opción B" design decision).
///   6. Generate + upload illustrations (best-effort).
///   7. Persist the graph (CapabilityGraphPersistenceService).
///   8. Per node: OPTIONAL Grounding-with-Bing-Search lookup (only for
///      nodes GraphArchitectAgent flagged NeedsCurrentInfo=true — evolving
///      topics, never timeless ones like arithmetic/algebra), then
///      design + persist + validate its Memory Paradox blueprint
///      (ExperienceDesignerAgent + BlueprintValidatorAgent, exactly the
///      same as TestCuradorGraphArchitectFlow's PASO 6/7/11), then
///      best-effort index the node's own content into embedded RAG chunks
///      (NodeKnowledgeIndexService — see
///      /memories/repo/tutor-document-wide-context-gap.md) so the Tutor
///      can later answer cross-node factual questions at runtime.
///
/// Deliberately Singleton-safe (only depends on Singleton agents +
/// IDbContextFactory + IConfiguration) so <see cref="PdfCapabilityGraphOrchestrator"/>
/// (which must be Singleton to keep in-memory runs alive across HTTP
/// calls) can hold a direct reference, same pattern as
/// HumanOS.Agentic.Studio.CapabilityCreationOrchestrator (V1).
/// </summary>
public sealed class PdfCapabilityGraphPipelineService
{
    private const int DefaultMaxPages = 100;

    private readonly CuradorAgent _curador;
    private readonly TocExtractionAgent _tocExtraction;
    private readonly GraphArchitectAgent _graphArchitect;
    private readonly DocumentContextAgent _documentContext;
    private readonly GraphIllustrationImageService _imageService;
    private readonly ExperienceDesignerAgent _experienceDesigner;
    private readonly BlueprintValidatorAgent _blueprintValidator;
    private readonly WebGroundingService _webGrounding;
    private readonly NodeKnowledgeIndexService _knowledgeIndexService;
    private readonly IdeaToDocumentAgent _ideaToDocument;
    private readonly PdfImageDescriptionAgent _pdfImageDescription;
    private readonly IDbContextFactory<HumanOsDbContext> _dbContextFactory;
    private readonly IConfiguration _configuration;

    // Stateless helpers (take their DbContext per-call, not constructor-injected)
    // — constructed directly here rather than via DI, same pattern as
    // TestCuradorGraphArchitectFlow, so this whole service stays Singleton-safe.
    private readonly CapabilityGraphIllustrationStorageService _illustrationStorage;
    private readonly CapabilityGraphPersistenceService _persistenceService = new();
    private readonly NodeExperienceBlueprintPersistenceService _blueprintPersistenceService = new();
    private readonly BlueprintValidationPersistenceService _blueprintValidationPersistenceService = new();
    private readonly CapabilityGenerationUsagePersistenceService _usagePersistenceService = new();

    public PdfCapabilityGraphPipelineService(
        CuradorAgent curador,
        TocExtractionAgent tocExtraction,
        GraphArchitectAgent graphArchitect,
        DocumentContextAgent documentContext,
        GraphIllustrationImageService imageService,
        ExperienceDesignerAgent experienceDesigner,
        BlueprintValidatorAgent blueprintValidator,
        WebGroundingService webGrounding,
        NodeKnowledgeIndexService knowledgeIndexService,
        IdeaToDocumentAgent ideaToDocument,
        PdfImageDescriptionAgent pdfImageDescription,
        IDbContextFactory<HumanOsDbContext> dbContextFactory,
        IConfiguration configuration)
    {
        _curador = curador;
        _tocExtraction = tocExtraction;
        _graphArchitect = graphArchitect;
        _documentContext = documentContext;
        _imageService = imageService;
        _experienceDesigner = experienceDesigner;
        _blueprintValidator = blueprintValidator;
        _webGrounding = webGrounding;
        _knowledgeIndexService = knowledgeIndexService;
        _ideaToDocument = ideaToDocument;
        _pdfImageDescription = pdfImageDescription;
        _dbContextFactory = dbContextFactory;
        _configuration = configuration;
        _illustrationStorage = new CapabilityGraphIllustrationStorageService(configuration);
    }

    public bool IsConfigured => _curador.IsConfigured && _graphArchitect.IsConfigured;

    /// <summary>Configurable via the 'MaxCapabilitySourcePdfPages' app
    /// setting (appsettings.json) — defaults to 100 pages when unset or
    /// invalid.</summary>
    public int MaxSourcePdfPages
    {
        get
        {
            var raw = _configuration["MaxCapabilitySourcePdfPages"];
            return int.TryParse(raw, out var value) && value > 0 ? value : DefaultMaxPages;
        }
    }

    public sealed class PdfTooLargeException(int pageCount, int maxPages) : Exception(
        $"The PDF has {pageCount} pages, which exceeds the configured maximum of {maxPages} pages.")
    {
        public int PageCount { get; } = pageCount;

        public int MaxPages { get; } = maxPages;
    }

    private const int DefaultMaxImagesToDescribe = 60;
    private const int DefaultMaxImagesPerPage = 8;

    /// <summary>Configurable via the 'MaxCapabilityPdfImagesToDescribe' app
    /// setting — caps how many embedded page images get sent to
    /// <see cref="PdfImageDescriptionAgent"/> per run, so a PDF with an
    /// unusually large number of embedded images can't blow up run time/
    /// cost. Defaults to 60 when unset or invalid.</summary>
    public int MaxPdfImagesToDescribe
    {
        get
        {
            var raw = _configuration["MaxCapabilityPdfImagesToDescribe"];
            return int.TryParse(raw, out var value) && value > 0 ? value : DefaultMaxImagesToDescribe;
        }
    }

    /// <summary>Configurable via the 'MaxCapabilityPdfImagesPerPage' app
    /// setting — caps how many DISTINCT images get described PER PAGE, so
    /// one image-heavy page (e.g. a page whose scan was split into many
    /// small raster tiles/insets by whatever tool produced the PDF) cannot
    /// consume the entire <see cref="MaxPdfImagesToDescribe"/> budget and
    /// starve every later page of any description (2026-07-23 — a real
    /// PDF hit página 1 alone having 50+ distinct embedded images, using
    /// up the whole global budget before page 2 was even reached).
    /// Defaults to 8 when unset or invalid.</summary>
    public int MaxPdfImagesPerPage
    {
        get
        {
            var raw = _configuration["MaxCapabilityPdfImagesPerPage"];
            return int.TryParse(raw, out var value) && value > 0 ? value : DefaultMaxImagesPerPage;
        }
    }

    public async Task<PdfCapabilityGraphResult> RunAsync(
        byte[] pdfBytes,
        string fileName,
        Guid capabilityDomainId,
        string capabilityName,
        Guid tenantId,
        Action<string> reportProgress,
        CancellationToken cancellationToken,
        bool enableWebEnrichment = false,
        Guid? subjectId = null,
        Guid? programId = null,
        int? programSequenceNumber = null,
        string? capabilityObjectives = null,
        string? capabilityRequirements = null)
    {
        // ==================== 1. EXTRACT TEXT + IMAGES, PER PAGE ====================
        reportProgress("Extrayendo texto del PDF");

        List<PdfTextExtractor.PageExtractionResult> pages;
        using (var stream = new MemoryStream(pdfBytes))
        {
            pages = PdfTextExtractor.ExtractPagesWithImages(stream);
        }

        var pageCount = pages.Count;
        var maxPages = MaxSourcePdfPages;
        if (pageCount > maxPages)
        {
            throw new PdfTooLargeException(pageCount, maxPages);
        }

        // ==================== 1b. DESCRIBE EMBEDDED IMAGES (best-effort) ====================
        // Real AI vision description (via PdfImageDescriptionAgent, the
        // same main LLM deployment used everywhere else) of every embedded
        // image above a minimum size — NOT just OCR/text extraction — so
        // scanned/image-only pages and diagrams/photos alongside real text
        // still reach Curador/GraphArchitect with genuine content. See
        // /memories/repo/pdf-to-capability-graph-v2-pipeline.md.
        //
        // PdfPig's page.GetImages() yields one entry PER PLACEMENT in the
        // content stream, not one per unique embedded resource — a single
        // picture drawn more than once on a page (or reused across pages,
        // e.g. a repeated background/logo) shows up as several identical
        // ExtractedPageImage byte arrays. We hash each image's bytes and
        // only ever call the vision model once per unique hash; every
        // occurrence (including duplicates) still gets the resulting
        // description folded into ITS OWN page's text, so per-page context
        // for the CapabilityGraph pipeline is preserved even when we skip
        // the redundant AI call.
        //
        // A PER-PAGE cap (MaxPdfImagesPerPage) is also enforced, on top of
        // the global MaxPdfImagesToDescribe budget: some PDFs have a single
        // page whose scan/graphic is split into dozens of small distinct
        // raster tiles by whatever tool produced the file — without a
        // per-page cap that one page alone can burn through the entire
        // global budget and leave every later page (which may hold the
        // actual pedagogically important images) with zero descriptions.
        var imageTokenUsage = new List<AgentTokenUsage>();
        var describedImageCount = 0;

        if (_pdfImageDescription.IsConfigured)
        {
            var maxImages = MaxPdfImagesToDescribe;
            var maxImagesPerPage = MaxPdfImagesPerPage;
            var descriptionsByHash = new Dictionary<string, string>();
            var totalUniqueImages = pages
                .SelectMany(p => p.Images)
                .Select(image => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(image.Bytes)))
                .Distinct()
                .Count();
            var progressTotal = Math.Min(maxImages, totalUniqueImages);

            foreach (var page in pages)
            {
                var describedOnThisPage = 0;

                foreach (var image in page.Images)
                {
                    var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(image.Bytes));

                    if (descriptionsByHash.TryGetValue(hash, out var cachedDescription))
                    {
                        page.Text = AppendImageDescription(page.Text, page.PageNumber, cachedDescription);
                        continue;
                    }

                    if (describedImageCount >= maxImages || describedOnThisPage >= maxImagesPerPage)
                    {
                        continue;
                    }

                    try
                    {
                        reportProgress($"Describiendo imagen {describedImageCount + 1}/{progressTotal} (página {page.PageNumber})");

                        var description = await _pdfImageDescription.DescribeAsync(
                            image.Bytes, image.ContentType, page.Text, cancellationToken);

                        descriptionsByHash[hash] = description.Description;
                        page.Text = AppendImageDescription(page.Text, page.PageNumber, description.Description);

                        imageTokenUsage.Add(WithModuleId(description.TokenUsage, $"Página {page.PageNumber}"));
                        describedImageCount++;
                        describedOnThisPage++;
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        // Best-effort — one bad/unsupported image should never
                        // fail the whole run.
                    }
                }
            }
        }

        var combinedText = string.Join("\n\n", pages.Select(p => p.Text).Where(t => !string.IsNullOrWhiteSpace(t)));

        if (string.IsNullOrWhiteSpace(combinedText))
        {
            throw new InvalidOperationException(
                $"'{fileName}' has no extractable text or describable image content.");
        }

        var result = await RunFromDocumentTextAsync(
            combinedText,
            pageCount,
            fileName,
            RawMaterialType.Pdf,
            "un PDF subido por el usuario",
            capabilityDomainId,
            capabilityName,
            tenantId,
            reportProgress,
            cancellationToken,
            enableWebEnrichment,
            subjectId,
            seedTokenUsage: imageTokenUsage.Count > 0 ? imageTokenUsage : null,
            programId,
            programSequenceNumber,
            capabilityObjectives,
            capabilityRequirements);

        result.DescribedImageCount = describedImageCount;
        return result;
    }

    /// <summary>
    /// The "Texto/idea" entry point (2026-07-21): the user provides only a
    /// short description of what the capability should teach (e.g.
    /// "Capacidad para que un niño aprenda a sumar y restar") instead of a
    /// PDF. Unlike <see cref="CuradorAgent"/>/<see cref="GraphArchitectAgent"/>
    /// — which are deliberately grounded and forbidden from inventing
    /// content not present in the user's own material — there IS no
    /// user-supplied material here, so <see cref="IdeaToDocumentAgent"/>
    /// is explicitly allowed to use its own general knowledge to WRITE a
    /// textbook-style source document covering the topic. That generated
    /// text then flows through the exact same grounded
    /// Curador → GraphArchitect pipeline as a real PDF would (via
    /// <see cref="RunFromDocumentTextAsync"/>), so every downstream rule
    /// (traceability to a chunk, no invented nodes, etc.) still holds
    /// against THIS text, same as it would against extracted PDF text.
    /// </summary>
    public async Task<PdfCapabilityGraphResult> RunFromDescriptionAsync(
        string description,
        string capabilityName,
        Guid capabilityDomainId,
        Guid tenantId,
        Action<string> reportProgress,
        CancellationToken cancellationToken,
        bool enableWebEnrichment = false,
        Guid? subjectId = null,
        Guid? programId = null,
        int? programSequenceNumber = null,
        string? capabilityObjectives = null,
        string? capabilityRequirements = null)
    {
        if (!_ideaToDocument.IsConfigured)
        {
            throw new InvalidOperationException(
                "The IdeaToDocument agent is not configured. Set the 'AzureOpenAIEndpoint' and " +
                "'AzureOpenAIDeploymentName' application settings.");
        }

        reportProgress("Expandiendo la idea en material de curso");
        var expansion = await _ideaToDocument.ExpandAsync(capabilityName, description, cancellationToken);

        return await RunFromDocumentTextAsync(
            expansion.DocumentText,
            0,
            capabilityName,
            RawMaterialType.UserNote,
            "una descripción/idea proporcionada por el usuario",
            capabilityDomainId,
            capabilityName,
            tenantId,
            reportProgress,
            cancellationToken,
            enableWebEnrichment,
            subjectId,
            seedTokenUsage: [expansion.TokenUsage],
            programId,
            programSequenceNumber,
            capabilityObjectives,
            capabilityRequirements);
    }

    /// <summary>Shared continuation of both <see cref="RunAsync"/> (real
    /// PDF text) and <see cref="RunFromDescriptionAsync"/> (LLM-expanded
    /// idea text) — everything from chapter detection onward is identical
    /// regardless of where <paramref name="documentText"/> came from.
    /// <paramref name="pageCount"/> is 0 for the description path (no
    /// real pages exist).</summary>
    private async Task<PdfCapabilityGraphResult> RunFromDocumentTextAsync(
        string documentText,
        int pageCount,
        string sourceLabel,
        RawMaterialType sourceType,
        string capabilityGenerationSourceDescription,
        Guid capabilityDomainId,
        string capabilityName,
        Guid tenantId,
        Action<string> reportProgress,
        CancellationToken cancellationToken,
        bool enableWebEnrichment,
        Guid? subjectId,
        List<AgentTokenUsage>? seedTokenUsage,
        Guid? programId = null,
        int? programSequenceNumber = null,
        string? capabilityObjectives = null,
        string? capabilityRequirements = null)
    {
        // ==================== 2. DETECT CHAPTERS (best-effort) ====================
        reportProgress("Detectando capítulos/índice");

        var sourceMaterial = new RawMaterialItem
        {
            Type = sourceType,
            Label = sourceLabel,
            Content = documentText
        };

        List<RawMaterialItem> chapterMaterials;
        if (_tocExtraction.IsConfigured)
        {
            try
            {
                chapterMaterials = await DocumentChapterSplitter.SplitIntoChapterMaterialsAsync(
                    sourceMaterial, _tocExtraction, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // TocExtraction is best-effort — fall back to the whole
                // document as a single chapter rather than failing the run.
                chapterMaterials = [sourceMaterial];
            }
        }
        else
        {
            chapterMaterials = [sourceMaterial];
        }

        // Token usage across the WHOLE run (2026-07-20 — cost-per-capability
        // observability, see PdfCapabilityGraphResult.TokenUsage doc comment).
        // Collected as we go rather than reconstructed after the fact, since
        // several calls below are best-effort and may never happen. Seeded
        // with the IdeaToDocument expansion call's usage when this run came
        // from a description rather than a real PDF.
        var tokenUsage = seedTokenUsage is null ? new List<AgentTokenUsage>() : new List<AgentTokenUsage>(seedTokenUsage);

        // ==================== 3+4. CURATE PER CHAPTER, THEN MERGE ====================
        var allChunks = new List<CuratedChunk>();
        var summaryParts = new List<string>();
        var chapterOrderLines = new List<string>();
        var hasRealChapters = chapterMaterials.Count > 1;

        for (var i = 0; i < chapterMaterials.Count; i++)
        {
            var chapterNumber = i + 1;
            var chapterLabel = chapterMaterials[i].Label;

            if (hasRealChapters)
            {
                chapterOrderLines.Add($"{chapterNumber}. {chapterLabel}");
            }

            var chapterBatch = new List<RawMaterialItem> { chapterMaterials[i] };

            reportProgress($"Curando capítulo {chapterNumber} de {chapterMaterials.Count}");
            var curationResult = await _curador.CurateAsync(chapterBatch, cancellationToken);
            tokenUsage.Add(WithModuleId(curationResult.TokenUsage, $"Cap.{chapterNumber} {chapterLabel}"));

            foreach (var chunk in curationResult.Corpus.Chunks)
            {
                allChunks.Add(new CuratedChunk
                {
                    // Chapter order/title baked into the Tag itself so it
                    // travels naturally into GraphArchitectAgent's
                    // "[{Tag}] {Content}" prompt line — no agent-prompt
                    // change needed for this part of the ordering signal.
                    Tag = hasRealChapters ? $"Cap.{chapterNumber} {chapterLabel} — {chunk.Tag}" : chunk.Tag,
                    Content = chunk.Content
                });
            }

            if (!string.IsNullOrWhiteSpace(curationResult.Corpus.Summary))
            {
                summaryParts.Add(curationResult.Corpus.Summary);
            }
        }

        var mergedCorpus = new CuratedCorpus
        {
            Summary = string.Join("\n\n", summaryParts),
            Chunks = allChunks
        };

        // ==================== 5. DESIGN THE GRAPH (single call) ====================
        reportProgress("Diseñando el grafo de aprendizaje");

        // Extra grounding context (2026-07-23) when this Capability is
        // being created straight into an existing Program's sequence: the
        // Program's own objectives/requirements plus whatever the designer
        // set specifically for THIS Capability's role in that Program (its
        // intended sequence position, its own objectives/requirements).
        // Best-effort — a failure to read the Program must never abort
        // Capability creation.
        string? programContext = null;
        if (programId is Guid contextProgramId)
        {
            try
            {
                await using var programContextDbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
                var program = await programContextDbContext.Programs
                    .AsNoTracking()
                    .SingleOrDefaultAsync(p => p.ProgramId == contextProgramId, cancellationToken);

                if (program is not null)
                {
                    var lines = new List<string>
                    {
                        "This Capability is being created directly into an existing Program's sequence:",
                        $"Program: {program.Name}"
                    };
                    if (!string.IsNullOrWhiteSpace(program.Description)) lines.Add($"Program description: {program.Description}");
                    if (!string.IsNullOrWhiteSpace(program.Objectives)) lines.Add($"Program-wide objectives: {program.Objectives}");
                    if (!string.IsNullOrWhiteSpace(program.Requirements)) lines.Add($"Program-wide requirements: {program.Requirements}");
                    if (programSequenceNumber is int seq) lines.Add($"This Capability's intended sequence position within the Program: #{seq}");
                    if (!string.IsNullOrWhiteSpace(capabilityObjectives)) lines.Add($"This Capability's own objectives within the Program: {capabilityObjectives}");
                    if (!string.IsNullOrWhiteSpace(capabilityRequirements)) lines.Add($"This Capability's own prerequisites within the Program: {capabilityRequirements}");

                    programContext = string.Join("\n", lines);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Best-effort — see comment above.
            }
        }

        var documentChapterOrder = hasRealChapters ? string.Join("\n", chapterOrderLines) : null;
        var graphResult = await _graphArchitect.ExtractGraphAsync(
            capabilityName, mergedCorpus, documentChapterOrder, programContext, cancellationToken);

        var graph = graphResult.Graph;
        tokenUsage.Add(WithModuleId(graphResult.TokenUsage, capabilityName));

        // Document-wide executive summary + key entities (2026-07-20 — see
        // Agents/Studio/DocumentContextAgent.cs and
        // /memories/repo/tutor-document-wide-context-gap.md). Best-effort,
        // runs once per capability alongside GraphArchitectAgent — a failure
        // here must never abort capability creation, same discipline as
        // illustrations/web-grounding below.
        string? executiveSummary = null;
        string? keyEntitiesJson = null;
        if (_documentContext.IsConfigured)
        {
            reportProgress("Extrayendo resumen ejecutivo y entidades clave");
            try
            {
                var documentContextResult = await _documentContext.ExtractAsync(capabilityName, mergedCorpus, cancellationToken);
                executiveSummary = documentContextResult.Context.ExecutiveSummary;
                keyEntitiesJson = System.Text.Json.JsonSerializer.Serialize(documentContextResult.Context.Entities);
                tokenUsage.Add(WithModuleId(documentContextResult.TokenUsage, capabilityName));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Best-effort — see comment above.
            }
        }

        // Per-node Grounding-with-Bing-Search targeting (2026-07-19,
        // replaces the old per-CHAPTER approach): GraphArchitectAgent
        // already flagged which nodes genuinely benefit from a real-time
        // web lookup (NeedsCurrentInfo) — carry that decision by NodeId so
        // it survives the DB round-trip below (persisted CapabilityGraphNode
        // entities don't have this field; only the in-memory GraphNodeDto
        // does).
        var nodesNeedingCurrentInfo = new HashSet<Guid>(
            graph.Nodes.Where(n => n.NeedsCurrentInfo).Select(n => n.NodeId));

        // Real Capability row this graph belongs to — created up front so
        // illustration storage paths (PASO 6) and persistence (PASO 7) both
        // use the same CapabilityId.
        Guid capabilityId;
        await using (var setupDbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            capabilityId = await EnsureCapabilityAsync(
                setupDbContext, capabilityDomainId, capabilityName, subjectId, capabilityGenerationSourceDescription, cancellationToken);

            // Top-down flow (2026-07-23): Programs are created empty first;
            // Capabilities are connected to them afterward. When the wizard
            // asked to attach this new Capability to an existing Program,
            // do it right here — same DbContext/scope as the Capability's
            // own insert, so both rows are visible together immediately.
            // programSequenceNumber/capabilityObjectives/capabilityRequirements
            // carry the designer's own explicit choices from the wizard
            // (see NewCapabilityPage.tsx's sequence-slot picker) through to
            // the ProgramCapability row.
            if (programId is Guid requestedProgramId)
            {
                await ProgramService.AttachCapabilityToEndAsync(
                    setupDbContext,
                    requestedProgramId,
                    capabilityId,
                    cancellationToken,
                    programSequenceNumber,
                    capabilityObjectives,
                    capabilityRequirements);
            }
        }

        // ==================== 6. ILLUSTRATIONS (best-effort) ====================
        // Debug logging (2026-07-21): the host process has been observed to
        // die silently (no exception ever printed to the console, no
        // zombie holding the port — a hard, immediate process exit) while
        // in this exact loop, on two separate runs. Since a normal .NET
        // exception here IS caught below, whatever kills the process must
        // be something that bypasses regular try/catch entirely (native
        // crash, OOM, stack overflow, etc.) — logging BEFORE each call and
        // flushing immediately to a plain file (independent of the
        // console/Functions logging pipeline, which may not flush before a
        // hard process death) is the only way to see which exact
        // node/image/prompt was in flight when it died.
        LogPipelineEvent($"=== Illustration stage starting — {graph.Nodes.Count} nodes ===");

        var illustrationRecords = new List<CapabilityGraphPersistenceService.NodeIllustrationRecord>();
        if (_imageService.IsConfigured && _illustrationStorage.IsConfigured)
        {
            reportProgress("Generando ilustraciones");

            foreach (var node in graph.Nodes)
            {
                foreach (var (imageIndex, promptDto) in node.IllustrationPrompts.Select((p, i) => (i + 1, p)))
                {
                    LogPipelineEvent(
                        $"START node={node.NodeId} image={imageIndex} purpose={promptDto.Purpose} " +
                        $"promptLen={promptDto.Prompt.Length}");
                    try
                    {
                        var generated = await _imageService.GenerateAsync(promptDto.Prompt, cancellationToken);
                        LogPipelineEvent(
                            $"GENERATED node={node.NodeId} image={imageIndex} bytes={generated.ImageBytes.ToArray().Length} model={generated.ImageModel}");

                        using var imageStream = generated.ImageBytes.ToStream();
                        var storagePath = await _illustrationStorage.UploadIllustrationAsync(
                            tenantId, capabilityId, node.NodeId, imageIndex, imageStream);

                        illustrationRecords.Add(new CapabilityGraphPersistenceService.NodeIllustrationRecord
                        {
                            NodeId = node.NodeId,
                            StoragePath = storagePath,
                            Prompt = promptDto.Prompt,
                            ImageModel = generated.ImageModel,
                            Width = generated.Width,
                            Height = generated.Height,
                            Purpose = promptDto.Purpose
                        });
                        LogPipelineEvent($"UPLOADED node={node.NodeId} image={imageIndex} path={storagePath}");
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        LogPipelineEvent(
                            $"FAILED node={node.NodeId} image={imageIndex} " +
                            $"exType={ex.GetType().FullName} msg={ex.Message}\n{ex}");
                        // Best-effort — a failed illustration shouldn't abort the whole run.
                    }
                }
            }
        }

        LogPipelineEvent("=== Illustration stage finished ===");

        // ==================== 6b. COURSE COVER IMAGE (best-effort) ====================
        // One extra image representing the WHOLE capability (distinct from
        // per-node illustrations above), shown on the student's course card
        // (2026-07-21). Prompt is built from the capability name + the
        // document-wide executive summary (falls back to the graph's own
        // Description when the summary agent isn't configured) so the
        // image reflects the actual subject matter, not a generic stock look.
        string? coverImageStoragePath = null;
        if (_imageService.IsConfigured && _illustrationStorage.IsConfigured)
        {
            reportProgress("Generando imagen de portada del curso");
            try
            {
                var coverPrompt =
                    $"A clean, modern, professional editorial illustration representing a course titled " +
                    $"'{capabilityName}'. {executiveSummary ?? graph.Description}. " +
                    "Flat design, no text, no letters, no words, wide banner composition, soft color palette.";

                var generatedCover = await _imageService.GenerateAsync(coverPrompt, cancellationToken);
                using var coverStream = generatedCover.ImageBytes.ToStream();
                coverImageStoragePath = await _illustrationStorage.UploadCoverImageAsync(
                    tenantId, capabilityId, coverStream, cancellationToken: cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Best-effort — a failed cover image shouldn't abort the whole run.
            }
        }

        // ==================== 7. PERSIST THE GRAPH ====================
        reportProgress("Guardando el grafo en la base de datos");

        CapabilityGraph persistedGraph;
        await using (var persistDbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            persistedGraph = await _persistenceService.PersistAsync(
                persistDbContext, capabilityId, graph, illustrationRecords, cancellationToken,
                executiveSummary, keyEntitiesJson, coverImageStoragePath);
        }

        // Cost dashboard (2026-07-23): persist every LLM call's token usage
        // gathered so far (chapters' Curador calls, GraphArchitect,
        // DocumentContext) so it survives past this run's in-memory
        // lifetime. Best-effort — must never abort an otherwise-successful
        // Capability creation. The per-node ExperienceDesigner/
        // BlueprintValidator calls happen AFTER this point and are persisted
        // in a second best-effort pass at the very end of this method (see
        // final return block) so nothing is lost even if this pipeline run
        // takes a long time to finish the blueprint stage.
        var usagePersistedThroughIndex = tokenUsage.Count;
        try
        {
            await using var usageDbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            await _usagePersistenceService.PersistAsync(
                usageDbContext, capabilityId, persistedGraph.CapabilityGraphId, tokenUsage, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Best-effort — see comment above.
        }

        // ==================== 8. PER-NODE MEMORY PARADOX BLUEPRINT ====================
        var nodesWithBlueprintCount = 0;
        if (_experienceDesigner.IsConfigured)
        {
            List<CapabilityGraphNode> reloadedNodes;
            await using (var nodesDbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken))
            {
                reloadedNodes = await nodesDbContext.CapabilityGraphNodes
                    .Include(n => n.Illustrations)
                    .Where(n => n.CapabilityGraphId == persistedGraph.CapabilityGraphId)
                    .OrderBy(n => n.SortOrder)
                    .ToListAsync(cancellationToken);
            }

            LogPipelineEvent($"=== Blueprint stage starting — {reloadedNodes.Count} nodes ===");

            foreach (var node in reloadedNodes)
            {
                LogPipelineEvent($"NODE-START node={node.CapabilityGraphNodeId} name={node.Name}");

                // Per-node web enrichment: only for nodes GraphArchitectAgent
                // flagged as NeedsCurrentInfo=true (evolving topics), never
                // for timeless ones (math/algebra/stable definitions) — see
                // GraphArchitectAgent's NeedsCurrentInfo decision rule.
                // Best-effort, same as every other web-enrichment call in
                // this pipeline: never fails the whole run.
                string? webFindings = null;
                if (enableWebEnrichment && _webGrounding.IsConfigured
                    && nodesNeedingCurrentInfo.Contains(node.CapabilityGraphNodeId))
                {
                    reportProgress($"Buscando información actualizada (Bing) para: {node.Name}");
                    try
                    {
                        var webResult = await _webGrounding.SearchAsync(node.Name, capabilityName, cancellationToken);
                        if (!string.IsNullOrWhiteSpace(webResult.Text))
                        {
                            webFindings = webResult.Text;
                        }
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        // Best-effort optional supplement — never fail the
                        // whole capability-creation run over it.
                    }
                }

                reportProgress($"Diseñando experiencia de aprendizaje: {node.Name}");
                if (await TryDesignPersistValidateBlueprintAsync(node, webFindings, tokenUsage, cancellationToken))
                {
                    nodesWithBlueprintCount++;
                }

                // Best-effort RAG indexing (2026-07-20) — a failure here
                // must never abort the rest of capability creation, same
                // discipline as illustrations/web-grounding above.
                if (_knowledgeIndexService.IsConfigured)
                {
                    LogPipelineEvent($"RAG-INDEX-START node={node.CapabilityGraphNodeId}");
                    try
                    {
                        await using var indexDbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
                        await _knowledgeIndexService.IndexNodeAsync(indexDbContext, node, allChunks, cancellationToken);
                        LogPipelineEvent($"RAG-INDEX-OK node={node.CapabilityGraphNodeId}");
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        LogPipelineEvent($"RAG-INDEX-FAILED node={node.CapabilityGraphNodeId} exType={ex.GetType().FullName} msg={ex.Message}");
                        // Best-effort — see comment above.
                    }
                }

                LogPipelineEvent($"NODE-DONE node={node.CapabilityGraphNodeId}");
            }

            LogPipelineEvent("=== Blueprint stage finished ===");
        }

        // ==================== 9. DETERMINISTIC COVERAGE CHECK (no LLM call) ====================
        // NOTE (2026-07-20 bug fix): node.References echoes back only the
        // SHORT original chunk tag, never the "Cap.N Label — " prefix this
        // service bakes into allChunks[*].Tag for multi-chapter documents
        // (see the loop above). An exact-match comparison here therefore
        // reported almost EVERY chunk as "unreferenced" for any multi-
        // chapter PDF, which was never actually true — use the same
        // suffix/contains-aware match as NodeKnowledgeIndexService's
        // SourceMaterial indexing so this check reflects real coverage.
        var referencedTags = graph.Nodes.SelectMany(n => n.References).ToList();
        var unreferencedTags = allChunks
            .Select(c => c.Tag)
            .Where(tag => !string.IsNullOrWhiteSpace(tag)
                && !referencedTags.Any(refTag => NodeKnowledgeIndexService.ChunkTagMatchesReference(tag, refTag)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Cost dashboard (2026-07-23), continued: persist the remaining
        // per-node ExperienceDesigner/BlueprintValidator usage rows added to
        // `tokenUsage` since the earlier persistence pass (right after graph
        // persistence, above) — only the DELTA, to avoid duplicate rows.
        // Best-effort, same discipline as every other step here.
        if (tokenUsage.Count > usagePersistedThroughIndex)
        {
            try
            {
                await using var finalUsageDbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
                await _usagePersistenceService.PersistAsync(
                    finalUsageDbContext,
                    capabilityId,
                    persistedGraph.CapabilityGraphId,
                    tokenUsage.Skip(usagePersistedThroughIndex).ToList(),
                    cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Best-effort — see comment above.
            }
        }

        return new PdfCapabilityGraphResult
        {
            CapabilityId = capabilityId,
            CapabilityGraphId = persistedGraph.CapabilityGraphId,
            ProgramId = programId,
            GraphName = persistedGraph.Name,
            PageCount = pageCount,
            ChapterCount = chapterMaterials.Count,
            NodeCount = persistedGraph.Nodes.Count,
            EdgeCount = persistedGraph.Edges.Count,
            NodesWithBlueprintCount = nodesWithBlueprintCount,
            UnreferencedChunkTags = unreferencedTags,
            TokenUsage = tokenUsage,
            IllustrationsGeneratedCount = illustrationRecords.Count,
            EstimatedCost = TokenCostEstimator.Estimate(tokenUsage, illustrationRecords.Count, _configuration)
        };
    }

    /// <summary>Debug-only (2026-07-21, see illustration-stage crash
    /// investigation comment above): appends one timestamped line to a
    /// plain text file next to the running assembly, flushing immediately.
    /// Deliberately NOT using ILogger/console — those buffers may never
    /// flush if the process dies as abruptly as observed. Swallows its own
    /// I/O errors so logging itself can never be the thing that breaks the
    /// pipeline.</summary>
    private static readonly object PipelineLogLock = new();

    private static void LogPipelineEvent(string message)
    {
        try
        {
            var line = $"[{DateTime.UtcNow:O}] {message}{Environment.NewLine}";
            lock (PipelineLogLock)
            {
                File.AppendAllText("illustration-debug.log", line);
            }
        }
        catch
        {
            // Never let debug logging itself take down the pipeline.
        }
    }

    /// <summary>Returns a copy of <paramref name="usage"/> with
    /// <see cref="AgentTokenUsage.ModuleId"/> set — <see cref="AgentTokenUsage"/>
    /// itself only exposes ModuleId as init-only, and the agents that
    /// produce it don't know which chapter/node they were called for.</summary>
    private static AgentTokenUsage WithModuleId(AgentTokenUsage usage, string moduleId) => new()
    {
        AgentName = usage.AgentName,
        ModuleId = moduleId,
        ModelName = usage.ModelName,
        InputTokens = usage.InputTokens,
        OutputTokens = usage.OutputTokens,
        CachedInputTokens = usage.CachedInputTokens
    };

    /// <summary>Folds an embedded image's (possibly cached/reused) AI
    /// description into a page's extracted text, so every page that
    /// contains the image — even a duplicate placement of one already
    /// described elsewhere — keeps its full context for Curador/
    /// GraphArchitect.</summary>
    private static string AppendImageDescription(string pageText, int pageNumber, string description) =>
        string.IsNullOrWhiteSpace(pageText)
            ? $"[Descripción de imagen — página {pageNumber}]\n{description}"
            : $"{pageText}\n\n[Descripción de imagen — página {pageNumber}]\n{description}";

    private static async Task<Guid> EnsureCapabilityAsync(
        HumanOsDbContext dbContext,
        Guid capabilityDomainId,
        string capabilityName,
        Guid? subjectId,
        string generationSourceDescription,
        CancellationToken cancellationToken)
    {
        var domainExists = await dbContext.CapabilityDomains
            .AnyAsync(d => d.CapabilityDomainId == capabilityDomainId, cancellationToken);

        if (!domainExists)
        {
            throw new InvalidOperationException($"CapabilityDomainId '{capabilityDomainId}' does not exist.");
        }

        if (subjectId is Guid nonNullSubjectId)
        {
            var subjectExists = await dbContext.Subjects
                .AnyAsync(s => s.SubjectId == nonNullSubjectId, cancellationToken);

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
            Code = $"PDF-{DateTime.UtcNow:yyyyMMddHHmmssfff}",
            Name = capabilityName,
            Description = $"Generado automáticamente a partir de {generationSourceDescription}.",
            IsActive = true,
            CreatedDate = DateTime.UtcNow,
            UpdatedDate = DateTime.UtcNow
        };

        dbContext.Capabilities.Add(capability);
        await dbContext.SaveChangesAsync(cancellationToken);

        return capability.CapabilityId;
    }

    /// <summary>Designs, persists and validates a single node's blueprint.
    /// Swallows its own exceptions (returns false) so one bad node never
    /// aborts blueprint generation for the rest of the graph. Appends its
    /// own agent calls' token usage (ExperienceDesigner + BlueprintValidator,
    /// ModuleId = node name) to <paramref name="tokenUsage"/>.</summary>
    private async Task<bool> TryDesignPersistValidateBlueprintAsync(
        CapabilityGraphNode node, string? webFindings, List<AgentTokenUsage> tokenUsage, CancellationToken cancellationToken)
    {
        try
        {
            var availableIllustrations = node.Illustrations
                .Select((illustration, i) => new AvailableIllustrationDto
                {
                    Index = i + 1,
                    Prompt = illustration.Prompt ?? string.Empty,
                    Caption = illustration.Caption,
                    Purpose = illustration.Purpose
                })
                .ToList();

            LogPipelineEvent($"DESIGN-START node={node.CapabilityGraphNodeId}");
            var designResult = await _experienceDesigner.DesignBlueprintAsync(
                node, availableIllustrations, webFindings, cancellationToken);
            tokenUsage.Add(WithModuleId(designResult.TokenUsage, node.Name));
            LogPipelineEvent($"DESIGN-OK node={node.CapabilityGraphNodeId}");

            NodeExperienceBlueprint persistedBlueprint;
            LogPipelineEvent($"SQL-SAVE-START node={node.CapabilityGraphNodeId} what=blueprint");
            await using (var blueprintDbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken))
            {
                persistedBlueprint = await _blueprintPersistenceService.PersistAsync(
                    blueprintDbContext, node.CapabilityGraphNodeId, designResult.Blueprint, node.Illustrations.ToList(), cancellationToken);
            }
            LogPipelineEvent($"SQL-SAVE-OK node={node.CapabilityGraphNodeId} what=blueprint");

            if (_blueprintValidator.IsConfigured)
            {
                try
                {
                    LogPipelineEvent($"VALIDATE-START node={node.CapabilityGraphNodeId}");
                    var validationResult = await _blueprintValidator.ValidateAsync(
                        node, persistedBlueprint, node.Illustrations.Count, cancellationToken);
                    tokenUsage.Add(WithModuleId(validationResult.TokenUsage, node.Name));
                    LogPipelineEvent($"VALIDATE-OK node={node.CapabilityGraphNodeId}");

                    LogPipelineEvent($"SQL-SAVE-START node={node.CapabilityGraphNodeId} what=validation");
                    await using var validationDbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
                    await _blueprintValidationPersistenceService.PersistAsync(
                        validationDbContext,
                        persistedBlueprint.NodeExperienceBlueprintId,
                        validationResult.Validation,
                        validationResult.TokenUsage,
                        cancellationToken);
                    LogPipelineEvent($"SQL-SAVE-OK node={node.CapabilityGraphNodeId} what=validation");
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    LogPipelineEvent($"VALIDATE-FAILED node={node.CapabilityGraphNodeId} exType={ex.GetType().FullName} msg={ex.Message}");
                    // Validation is a quality gate, not a hard requirement —
                    // the node still has a usable blueprint without it.
                }
            }

            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogPipelineEvent($"BLUEPRINT-FAILED node={node.CapabilityGraphNodeId} exType={ex.GetType().FullName} msg={ex.Message}\n{ex}");
            return false;
        }
    }
}
