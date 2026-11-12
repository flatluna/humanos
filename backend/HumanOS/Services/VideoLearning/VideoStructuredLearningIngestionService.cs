using HumanOS.Agents.Studio;
using HumanOS.Data;
using HumanOS.Models.Capabilities.Studio;
using HumanOS.Storage;
using Microsoft.Data.SqlTypes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SharpToken;

namespace HumanOS.Services.VideoLearning;

/// <summary>
/// Creates a full <see cref="StructuredLearningDocument"/> directly from
/// already-synchronized video Engrams (chapter/subchapter/text/frame image),
/// bypassing PDF upload and TOC/LLM extraction entirely — see
/// EngramsVideos.FrameSync (frame/transcript/OCR sync pipeline) for how the
/// input <see cref="VideoEngramInput"/> list is produced upstream.
///
/// This intentionally does NOT reimplement
/// <see cref="StructuredLearningExtractionService"/>'s PDF pipeline (image
/// description, document classification, knowledge-graph generation, etc.)
/// — once sections exist here, the existing
/// entities/connections/engram-card generation endpoints run against them
/// exactly as they do for a PDF-sourced document, since they only depend on
/// StructuredLearningDocumentSection rows already being persisted.
///
/// The only unavoidable AI cost here is the embedding call per section
/// (<see cref="StructuredLearningDocumentSection.Embedding"/> is a
/// non-nullable SqlVector&lt;float&gt; required for semantic search) — every
/// other PDF-pipeline cost (TOC discovery, image description/generation,
/// document classification) is skipped because the video pipeline already
/// knows the exact structure and already has a real source image per Engram.
/// </summary>
public sealed class VideoStructuredLearningIngestionService
{
    private readonly CapabilityEmbeddingService _embeddingService;
    private readonly CapabilityGraphIllustrationStorageService _illustrationStorage;
    private readonly IDbContextFactory<HumanOsDbContext> _dbContextFactory;
    private readonly StructuredLearningExtractionService _extractionService;
    private readonly GptEncoding _tokenEncoding = GptEncoding.GetEncoding("cl100k_base");

    public VideoStructuredLearningIngestionService(
        CapabilityEmbeddingService embeddingService,
        IDbContextFactory<HumanOsDbContext> dbContextFactory,
        StructuredLearningExtractionService extractionService,
        IConfiguration configuration)
    {
        _embeddingService = embeddingService;
        _illustrationStorage = new CapabilityGraphIllustrationStorageService(configuration);
        _dbContextFactory = dbContextFactory;
        _extractionService = extractionService;
    }

    public async Task<VideoIngestionResult> CreateFromVideoAsync(
        VideoIngestionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Engrams is null || request.Engrams.Count == 0)
        {
            throw new ArgumentException("At least one Engram is required.", nameof(request));
        }

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var documentId = Guid.NewGuid();
        var document = new StructuredLearningDocument
        {
            StructuredLearningDocumentId = documentId,
            TenantId = request.TenantId,
            CreatedByOid = request.CreatedByOid,
            CreatedByEmail = request.CreatedByEmail,
            IsGlobal = request.IsGlobal,
            SessionId = request.SessionId ?? Guid.NewGuid(),
            DocumentName = request.DocumentName,
            SourceCapabilityCode = "video",
            IncludeKnowledgeGraphEntities = request.IncludeKnowledgeGraphEntities,
            IncludeKnowledgeGraphConnections = request.IncludeKnowledgeGraphConnections,
            // The video's structure/type is already known (it came from the
            // video pipeline, not a classified document) — no LLM/heuristic
            // classification call needed, unlike the PDF pipeline.
            DetectedDocumentType = "Video",
            DocumentTypeConfirmedByUser = true,
            ConfirmedDocumentType = "Video",
            ExecutiveSummary = string.IsNullOrWhiteSpace(request.ExecutiveSummary) ? null : request.ExecutiveSummary,
            SourceVideoId = string.IsNullOrWhiteSpace(request.SourceVideoId) ? null : request.SourceVideoId,
            SkillType = string.IsNullOrWhiteSpace(request.SkillType) ? null : request.SkillType,
            AuthorName = string.IsNullOrWhiteSpace(request.AuthorName) ? null : request.AuthorName,
            AuthorCourseName = string.IsNullOrWhiteSpace(request.AuthorCourseName) ? null : request.AuthorCourseName,
            PresentedDate = request.PresentedDate,
            PresentedEntityType = string.IsNullOrWhiteSpace(request.PresentedEntityType) ? null : request.PresentedEntityType,
            PresentedEntityName = string.IsNullOrWhiteSpace(request.PresentedEntityName) ? null : request.PresentedEntityName,
            CourseLanguageCode = string.IsNullOrWhiteSpace(request.CourseLanguageCode) ? null : request.CourseLanguageCode,
            CreatedDate = DateTime.UtcNow,
        };

        dbContext.StructuredLearningDocuments.Add(document);
        await dbContext.SaveChangesAsync(cancellationToken);

        var result = new VideoIngestionResult { StructuredLearningDocumentId = documentId };
        var pageCounter = 0;

        foreach (var engram in request.Engrams)
        {
            pageCounter++;
            var text = engram.Text ?? string.Empty;
            var embedding = await _embeddingService.GenerateEmbeddingAsync(text, cancellationToken);

            var section = new StructuredLearningDocumentSection
            {
                StructuredLearningDocumentSectionId = Guid.NewGuid(),
                StructuredLearningDocumentId = documentId,
                TenantId = request.TenantId,
                SortOrder = pageCounter,
                ChapterTitle = engram.ChapterTitle,
                SubchapterTitle = engram.SubchapterTitle,
                Text = text,
                TotalCharacters = text.Length,
                TotalTokens = _tokenEncoding.Encode(text).Count,
                PageFrom = pageCounter,
                PageTo = pageCounter,
                VideoStartSeconds = engram.StartSeconds,
                VideoEndSeconds = engram.EndSeconds,
                IsInformationalOnly = engram.IsInformationalOnly,
                Embedding = new SqlVector<float>(embedding),
                CreatedDate = DateTime.UtcNow,
            };

            dbContext.StructuredLearningDocumentSections.Add(section);
            await dbContext.SaveChangesAsync(cancellationToken);

            var sectionResult = new VideoIngestionSectionResult
            {
                StructuredLearningDocumentSectionId = section.StructuredLearningDocumentSectionId,
                ChapterTitle = engram.ChapterTitle,
                SubchapterTitle = engram.SubchapterTitle,
            };

            if (_illustrationStorage.IsConfigured && engram.Frames.Count > 0)
            {
                // Blob path is {tenantId}/{capabilityId}/{nodeId}/image-{NN}.png
                // (CapabilityGraphIllustrationStorageService) — deterministic on
                // (capabilityId, nodeId, index) alone, NOT on the illustration row's
                // own Id. Since every "Teaching" illustration generated later for
                // this same section (GenerateIllustrationsAsync's UploadDirect/
                // EnhanceVideoFrame single-image branch) uses the SAME
                // (capabilityId=sectionId, nodeId=sectionId) pair with a hardcoded
                // index of 1, a raw 1/2/3... index here would silently let any
                // later pedagogy regeneration overwrite (destroy) this real video
                // frame's own blob (2026-11-06 bug: this is exactly what happened —
                // the real frame was clobbered by a subsequent AI-generated
                // illustration sharing index 1). Offset into a dedicated range
                // (same pattern as the 200+ step-image range above) so real frames
                // NEVER collide with any other illustration purpose's index.
                const int realFrameIndexOffset = 500;
                var imageIndex = 0;
                foreach (var frame in engram.Frames.OrderBy(f => f.TimestampSeconds))
                {
                    if (string.IsNullOrWhiteSpace(frame.ImageBase64))
                    {
                        continue;
                    }

                    imageIndex++;
                    var imageBytes = Convert.FromBase64String(frame.ImageBase64);
                    using var imageStream = new MemoryStream(imageBytes);

                    // capabilityId/nodeId are both the section's own Id here —
                    // same convention StructuredLearningExtractionService uses
                    // for its own illustrations (only opaque GUIDs are needed
                    // to build the blob path, see CapabilityGraphIllustrationStorageService).
                    var storagePath = await _illustrationStorage.UploadIllustrationAsync(
                        request.TenantId,
                        section.StructuredLearningDocumentSectionId,
                        section.StructuredLearningDocumentSectionId,
                        realFrameIndexOffset + imageIndex,
                        imageStream,
                        frame.ImageContentType,
                        cancellationToken);

                    var illustration = new StructuredLearningIllustration
                    {
                        StructuredLearningIllustrationId = Guid.NewGuid(),
                        StructuredLearningDocumentSectionId = section.StructuredLearningDocumentSectionId,
                        StoragePath = storagePath,
                        Prompt = $"Real video frame extracted by EngramsVideos.FrameSync @ {frame.TimestampSeconds:0.##}s",
                        // Caption carries this frame's own synced transcript
                        // slice — reused verbatim later by the video
                        // pedagogy orchestrator to build the Teaching step's
                        // per-scene text, never invented.
                        Caption = frame.SceneText,
                        VideoTimestampSeconds = frame.TimestampSeconds,
                        Purpose = StructuredLearningIllustrationPurpose.SourceDocument,
                        ImageModel = "video-frame",
                        CreatedDate = DateTime.UtcNow,
                    };

                    dbContext.StructuredLearningIllustrations.Add(illustration);
                    await dbContext.SaveChangesAsync(cancellationToken);

                    // First real frame stays the legacy single-image
                    // reference for any older consumer that only reads
                    // StructuredLearningIllustrationId.
                    sectionResult.StructuredLearningIllustrationId ??= illustration.StructuredLearningIllustrationId;
                }
            }

            result.Sections.Add(sectionResult);
        }

        // Knowledge Graph (entities/connections) is never generated inline
        // above — unlike the PDF pipeline, this service only persists
        // sections/embeddings/illustrations. Without this call
        // KnowledgeGraphSection would silently render nothing (zero
        // entities) for every video-sourced document, since entity/edge
        // extraction is otherwise only ever triggered from
        // StructuredLearningExtractionService's own Step 2 pipeline.
        if (request.IncludeKnowledgeGraphEntities)
        {
            try
            {
                await _extractionService.BackfillKnowledgeGraphAsync(documentId, _ => { }, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Best-effort, same rationale as the PDF pipeline: a Knowledge
                // Graph failure must never discard the already-persisted
                // sections/illustrations.
                Console.WriteLine($"[VideoIngestion] Knowledge Graph generation failed for document {documentId}: {ex.Message}");
            }
        }

        return result;
    }
}
