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
    private readonly IDbContextFactory<HumanOsDbContext> _dbContextFactory;
    private readonly IConfiguration _configuration;

    // Stateless helpers (take their DbContext per-call, not constructor-injected)
    // — constructed directly here rather than via DI, same pattern as
    // TestCuradorGraphArchitectFlow, so this whole service stays Singleton-safe.
    private readonly CapabilityGraphIllustrationStorageService _illustrationStorage;
    private readonly CapabilityGraphPersistenceService _persistenceService = new();
    private readonly NodeExperienceBlueprintPersistenceService _blueprintPersistenceService = new();
    private readonly BlueprintValidationPersistenceService _blueprintValidationPersistenceService = new();

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

    public async Task<PdfCapabilityGraphResult> RunAsync(
        byte[] pdfBytes,
        string fileName,
        Guid capabilityDomainId,
        string capabilityName,
        Guid tenantId,
        Action<string> reportProgress,
        CancellationToken cancellationToken,
        bool enableWebEnrichment = false)
    {
        // ==================== 1. EXTRACT TEXT + PAGE COUNT ====================
        reportProgress("Extrayendo texto del PDF");

        PdfTextExtractor.ExtractionResult extraction;
        using (var stream = new MemoryStream(pdfBytes))
        {
            extraction = PdfTextExtractor.ExtractTextWithPageCount(stream);
        }

        var maxPages = MaxSourcePdfPages;
        if (extraction.PageCount > maxPages)
        {
            throw new PdfTooLargeException(extraction.PageCount, maxPages);
        }

        if (string.IsNullOrWhiteSpace(extraction.Text))
        {
            throw new InvalidOperationException(
                $"'{fileName}' has no extractable text (it may be a scanned/image-only PDF).");
        }

        // ==================== 2. DETECT CHAPTERS (best-effort) ====================
        reportProgress("Detectando capítulos/índice");

        var sourceMaterial = new RawMaterialItem
        {
            Type = RawMaterialType.Pdf,
            Label = fileName,
            Content = extraction.Text
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
        // several calls below are best-effort and may never happen.
        var tokenUsage = new List<AgentTokenUsage>();

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

        var documentChapterOrder = hasRealChapters ? string.Join("\n", chapterOrderLines) : null;
        var graphResult = await _graphArchitect.ExtractGraphAsync(
            capabilityName, mergedCorpus, documentChapterOrder, cancellationToken);

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
            capabilityId = await EnsureCapabilityAsync(setupDbContext, capabilityDomainId, capabilityName, cancellationToken);
        }

        // ==================== 6. ILLUSTRATIONS (best-effort) ====================
        var illustrationRecords = new List<CapabilityGraphPersistenceService.NodeIllustrationRecord>();
        if (_imageService.IsConfigured && _illustrationStorage.IsConfigured)
        {
            reportProgress("Generando ilustraciones");

            foreach (var node in graph.Nodes)
            {
                foreach (var (imageIndex, promptDto) in node.IllustrationPrompts.Select((p, i) => (i + 1, p)))
                {
                    try
                    {
                        var generated = await _imageService.GenerateAsync(promptDto.Prompt, cancellationToken);
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
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        // Best-effort — a failed illustration shouldn't abort the whole run.
                    }
                }
            }
        }

        // ==================== 7. PERSIST THE GRAPH ====================
        reportProgress("Guardando el grafo en la base de datos");

        CapabilityGraph persistedGraph;
        await using (var persistDbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            persistedGraph = await _persistenceService.PersistAsync(
                persistDbContext, capabilityId, graph, illustrationRecords, cancellationToken,
                executiveSummary, keyEntitiesJson);
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

            foreach (var node in reloadedNodes)
            {
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
                    try
                    {
                        await using var indexDbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
                        await _knowledgeIndexService.IndexNodeAsync(indexDbContext, node, allChunks, cancellationToken);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        // Best-effort — see comment above.
                    }
                }
            }
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

        return new PdfCapabilityGraphResult
        {
            CapabilityId = capabilityId,
            CapabilityGraphId = persistedGraph.CapabilityGraphId,
            GraphName = persistedGraph.Name,
            PageCount = extraction.PageCount,
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

    /// <summary>Returns a copy of <paramref name="usage"/> with
    /// <see cref="AgentTokenUsage.ModuleId"/> set — <see cref="AgentTokenUsage"/>
    /// itself only exposes ModuleId as init-only, and the agents that
    /// produce it don't know which chapter/node they were called for.</summary>
    private static AgentTokenUsage WithModuleId(AgentTokenUsage usage, string moduleId) => new()
    {
        AgentName = usage.AgentName,
        ModuleId = moduleId,
        InputTokens = usage.InputTokens,
        OutputTokens = usage.OutputTokens,
        CachedInputTokens = usage.CachedInputTokens
    };

    private static async Task<Guid> EnsureCapabilityAsync(
        HumanOsDbContext dbContext, Guid capabilityDomainId, string capabilityName, CancellationToken cancellationToken)
    {
        var domainExists = await dbContext.CapabilityDomains
            .AnyAsync(d => d.CapabilityDomainId == capabilityDomainId, cancellationToken);

        if (!domainExists)
        {
            throw new InvalidOperationException($"CapabilityDomainId '{capabilityDomainId}' does not exist.");
        }

        var capability = new Capability
        {
            CapabilityId = Guid.NewGuid(),
            CapabilityDomainId = capabilityDomainId,
            Code = $"PDF-{DateTime.UtcNow:yyyyMMddHHmmssfff}",
            Name = capabilityName,
            Description = "Generado automáticamente a partir de un PDF subido por el usuario.",
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

            var designResult = await _experienceDesigner.DesignBlueprintAsync(
                node, availableIllustrations, webFindings, cancellationToken);
            tokenUsage.Add(WithModuleId(designResult.TokenUsage, node.Name));

            NodeExperienceBlueprint persistedBlueprint;
            await using (var blueprintDbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken))
            {
                persistedBlueprint = await _blueprintPersistenceService.PersistAsync(
                    blueprintDbContext, node.CapabilityGraphNodeId, designResult.Blueprint, node.Illustrations.ToList(), cancellationToken);
            }

            if (_blueprintValidator.IsConfigured)
            {
                try
                {
                    var validationResult = await _blueprintValidator.ValidateAsync(
                        node, persistedBlueprint, node.Illustrations.Count, cancellationToken);
                    tokenUsage.Add(WithModuleId(validationResult.TokenUsage, node.Name));

                    await using var validationDbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
                    await _blueprintValidationPersistenceService.PersistAsync(
                        validationDbContext,
                        persistedBlueprint.NodeExperienceBlueprintId,
                        validationResult.Validation,
                        validationResult.TokenUsage,
                        cancellationToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // Validation is a quality gate, not a hard requirement —
                    // the node still has a usable blueprint without it.
                }
            }

            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return false;
        }
    }
}
