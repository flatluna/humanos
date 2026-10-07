using HumanOS.Agents.Studio;
using HumanOS.Contracts.Studio;
using HumanOS.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace HumanOS.Services;

/// <summary>
/// Reads persisted <see cref="Models.Capabilities.Studio.StructuredLearningGenerationUsage"/>
/// rows and turns them into cost/time-dashboard responses, reusing
/// <see cref="TokenCostEstimator"/> for the USD estimate — mirrors
/// <see cref="CapabilityCostService"/>'s exact design, grouped by
/// <c>StepName</c> ("Extraction"/"KnowledgeGraph"/"Pedagogy") instead of a
/// chapter/node SectionLabel.
/// </summary>
public sealed class StructuredLearningCostService
{
    private readonly HumanOsDbContext _dbContext;
    private readonly IConfiguration _configuration;

    public StructuredLearningCostService(HumanOsDbContext dbContext, IConfiguration configuration)
    {
        _dbContext = dbContext;
        _configuration = configuration;
    }

    /// <summary>Real per-video cost by the model that generated it (2026-10-01,
    /// corrected pricing) — mirrors the frontend's priceForModel: videos are
    /// NOT priced at the flat per-image placeholder rate. The actual video
    /// generation model is "gemini-omni-flash-preview" (Google), billed at
    /// ~$0.10 USD per second of 720p video; every clip here is a fixed
    /// 8-second generation, so $0.10 × 8 = $0.80 per video. Returns 0 only
    /// for "merged" (local ffmpeg, unbilled) or an empty model name.</summary>
    private const decimal GeminiOmniFlashCostPerSecondUsd = 0.10m;
    private const int VideoClipSeconds = 8;
    private const decimal VideoCostPerVideoUsd = GeminiOmniFlashCostPerSecondUsd * VideoClipSeconds;

    private static decimal VideoCostForModel(string? imageModel)
    {
        if (string.IsNullOrEmpty(imageModel)) return 0m;
        if (imageModel.Equals("merged", StringComparison.OrdinalIgnoreCase)) return 0m;
        return VideoCostPerVideoUsd;
    }

    private decimal ReadImageRate()
    {
        var raw = _configuration["AgentPricing:PerImage"];
        return decimal.TryParse(raw, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var value)
            ? value
            : 0.04m;
    }

    public async Task<IReadOnlyList<StructuredLearningCostSummaryResponse>> GetSummariesAsync(
        Guid? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        var docQuery = _dbContext.StructuredLearningDocuments.AsNoTracking();
        if (tenantId != null && tenantId != Guid.Empty)
        {
            docQuery = docQuery.Where(d => d.TenantId == tenantId.Value);
        }

        var documents = await docQuery
            .Select(d => new { d.StructuredLearningDocumentId, d.DocumentName })
            .ToListAsync(cancellationToken);

        var usageRows = await _dbContext.StructuredLearningGenerationUsages
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        // Illustrations have no direct DocumentId FK — join through their
        // section to attribute each generated image/video back to its
        // document. Split by MediaType (2026-08-26) since videos are priced
        // per-model (Veo/Omni), not at the flat per-image placeholder rate.
        var illustrationsByDocument = await _dbContext.StructuredLearningIllustrations
            .AsNoTracking()
            .Join(
                _dbContext.StructuredLearningDocumentSections.AsNoTracking(),
                illustration => illustration.StructuredLearningDocumentSectionId,
                section => section.StructuredLearningDocumentSectionId,
                (illustration, section) => new { section.StructuredLearningDocumentId, illustration.MediaType, illustration.ImageModel })
            .ToListAsync(cancellationToken);

        var imageCountsByDocument = illustrationsByDocument
            .Where(i => i.MediaType != "Video")
            .GroupBy(i => i.StructuredLearningDocumentId)
            .ToDictionary(g => g.Key, g => g.Count());

        var videoCountsByDocument = illustrationsByDocument
            .Where(i => i.MediaType == "Video")
            .GroupBy(i => i.StructuredLearningDocumentId)
            .ToDictionary(g => g.Key, g => g.Count());

        var videoCostByDocument = illustrationsByDocument
            .Where(i => i.MediaType == "Video")
            .GroupBy(i => i.StructuredLearningDocumentId)
            .ToDictionary(g => g.Key, g => g.Sum(i => VideoCostForModel(i.ImageModel)));

        var results = new List<StructuredLearningCostSummaryResponse>(documents.Count);
        foreach (var document in documents)
        {
            var rows = usageRows.Where(u => u.StructuredLearningDocumentId == document.StructuredLearningDocumentId).ToList();
            if (rows.Count == 0)
            {
                continue;
            }

            var tokenUsage = rows
                .Select(u => new AgentTokenUsage
                {
                    AgentName = u.AgentName,
                    ModelName = u.ModelName ?? string.Empty,
                    InputTokens = u.InputTokens,
                    OutputTokens = u.OutputTokens,
                    CachedInputTokens = u.CachedInputTokens,
                })
                .ToList();

            var imagesGeneratedCount = imageCountsByDocument.GetValueOrDefault(document.StructuredLearningDocumentId);
            var videosGeneratedCount = videoCountsByDocument.GetValueOrDefault(document.StructuredLearningDocumentId);
            var videoCostUsd = videoCostByDocument.GetValueOrDefault(document.StructuredLearningDocumentId);
            var estimate = TokenCostEstimator.Estimate(tokenUsage, imagesGeneratedCount, _configuration);

            results.Add(new StructuredLearningCostSummaryResponse
            {
                StructuredLearningDocumentId = document.StructuredLearningDocumentId,
                DocumentName = document.DocumentName,
                InputTokens = rows.Sum(u => (long)u.InputTokens),
                OutputTokens = rows.Sum(u => (long)u.OutputTokens),
                CachedInputTokens = rows.Sum(u => (long)u.CachedInputTokens),
                TotalElapsedMilliseconds = rows.Sum(u => u.ElapsedMilliseconds),
                ImagesGeneratedCount = imagesGeneratedCount,
                ImageCostUsd = estimate.ImageCostUsd,
                VideosGeneratedCount = videosGeneratedCount,
                VideoCostUsd = Math.Round(videoCostUsd, 4),
                EstimatedCostUsd = Math.Round(estimate.TotalCostUsd + videoCostUsd, 4),
                GeneratedDate = rows.Min(u => u.CreatedDate),
            });
        }


        return results.OrderByDescending(r => r.InputTokens + r.OutputTokens).ToList();
    }

    public async Task<StructuredLearningCostDetailResponse?> GetDetailAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        var document = await _dbContext.StructuredLearningDocuments
            .AsNoTracking()
            .Where(d => d.StructuredLearningDocumentId == documentId)
            .Select(d => new { d.StructuredLearningDocumentId, d.DocumentName })
            .SingleOrDefaultAsync(cancellationToken);

        if (document is null) return null;

        var usageRows = await _dbContext.StructuredLearningGenerationUsages
            .AsNoTracking()
            .Where(u => u.StructuredLearningDocumentId == documentId)
            .ToListAsync(cancellationToken);

        // Illustrations have no direct StepName of their own — every one
        // generated for this document belongs to the Pedagogy step (only
        // step that ever calls the image service). Split by MediaType
        // (2026-08-26) since videos are priced per-model, not at the flat
        // per-image placeholder rate.
        var illustrations = await _dbContext.StructuredLearningIllustrations
            .AsNoTracking()
            .Join(
                _dbContext.StructuredLearningDocumentSections.AsNoTracking().Where(s => s.StructuredLearningDocumentId == documentId),
                illustration => illustration.StructuredLearningDocumentSectionId,
                section => section.StructuredLearningDocumentSectionId,
                (illustration, section) => new { illustration.MediaType, illustration.ImageModel })
            .ToListAsync(cancellationToken);

        var imagesGeneratedCount = illustrations.Count(i => i.MediaType != "Video");
        var videosGeneratedCount = illustrations.Count(i => i.MediaType == "Video");
        var videoCostUsd = Math.Round(illustrations.Where(i => i.MediaType == "Video").Sum(i => VideoCostForModel(i.ImageModel)), 4);

        var steps = usageRows
            .GroupBy(u => u.StepName)
            .Select(g =>
            {
                var rows = g.ToList();
                var stepTokenUsage = rows
                    .Select(u => new AgentTokenUsage
                    {
                        AgentName = u.AgentName,
                        ModelName = u.ModelName ?? string.Empty,
                        InputTokens = u.InputTokens,
                        OutputTokens = u.OutputTokens,
                        CachedInputTokens = u.CachedInputTokens,
                    })
                    .ToList();

                var stepImagesGeneratedCount = g.Key == "Pedagogy" ? imagesGeneratedCount : 0;
                var stepEstimate = TokenCostEstimator.Estimate(stepTokenUsage, stepImagesGeneratedCount, _configuration);

                return new StructuredLearningCostStepResponse
                {
                    StepName = g.Key,
                    Agents = string.Join(", ", rows.Select(u => u.AgentName).Distinct()),
                    Models = string.Join(", ", rows.Select(u => u.ModelName).Where(m => !string.IsNullOrWhiteSpace(m)).Distinct()),
                    InputTokens = rows.Sum(u => u.InputTokens),
                    OutputTokens = rows.Sum(u => u.OutputTokens),
                    CachedInputTokens = rows.Sum(u => u.CachedInputTokens),
                    TotalElapsedMilliseconds = rows.Sum(u => u.ElapsedMilliseconds),
                    CallCount = rows.Count,
                    ImagesGeneratedCount = stepImagesGeneratedCount,
                    ImageCostUsd = stepEstimate.ImageCostUsd,
                    EstimatedCostUsd = stepEstimate.TotalCostUsd,
                };
            })
            .OrderByDescending(s => s.InputTokens + s.OutputTokens)
            .ToList();

        if (videosGeneratedCount > 0)
        {
            steps.Add(new StructuredLearningCostStepResponse
            {
                StepName = "Video",
                Agents = "GoogleVeoVideoService/GoogleOmniFlashVideoService",
                Models = string.Join(", ", illustrations.Where(i => i.MediaType == "Video").Select(i => i.ImageModel).Where(m => !string.IsNullOrWhiteSpace(m)).Distinct()),
                ImagesGeneratedCount = 0,
                ImageCostUsd = 0m,
                VideosGeneratedCount = videosGeneratedCount,
                VideoCostUsd = videoCostUsd,
                EstimatedCostUsd = videoCostUsd,
            });
        }

        var tokenUsage = usageRows
            .Select(u => new AgentTokenUsage
            {
                AgentName = u.AgentName,
                ModuleId = u.StepName,
                ModelName = u.ModelName ?? string.Empty,
                InputTokens = u.InputTokens,
                OutputTokens = u.OutputTokens,
                CachedInputTokens = u.CachedInputTokens,
            })
            .ToList();

        var estimate = TokenCostEstimator.Estimate(tokenUsage, imagesGeneratedCount, _configuration);

        return new StructuredLearningCostDetailResponse
        {
            StructuredLearningDocumentId = document.StructuredLearningDocumentId,
            DocumentName = document.DocumentName,
            Steps = steps,
            InputTokens = tokenUsage.Sum(u => (long)u.InputTokens),
            OutputTokens = tokenUsage.Sum(u => (long)u.OutputTokens),
            CachedInputTokens = tokenUsage.Sum(u => (long)u.CachedInputTokens),
            TotalElapsedMilliseconds = usageRows.Sum(u => u.ElapsedMilliseconds),
            ImagesGeneratedCount = imagesGeneratedCount,
            ImageCostUsd = estimate.ImageCostUsd,
            VideosGeneratedCount = videosGeneratedCount,
            VideoCostUsd = videoCostUsd,
            EstimatedCostUsd = Math.Round(estimate.TotalCostUsd + videoCostUsd, 4),
        };
    }

    /// <summary>Per-engram breakdown for one document — everything billed to
    /// each individual section: initial design generation (Extraction/
    /// KnowledgeGraph/Pedagogy) AND every later edit (manual or AI both
    /// trigger real "Edit"/"KnowledgeGraph"/"KnowledgeFacts" LLM calls, see
    /// StructuredLearningReviewService), plus that engram's own images/
    /// videos. Lets a designer answer "what did THIS engram really cost"
    /// for client billing, instead of only a whole-document total.</summary>
    public async Task<IReadOnlyList<StructuredLearningEngramCostResponse>> GetPerEngramAsync(
        Guid documentId, Guid? onlySectionId = null, CancellationToken cancellationToken = default)
    {
        var sectionsQuery = _dbContext.StructuredLearningDocumentSections
            .AsNoTracking()
            .Where(s => s.StructuredLearningDocumentId == documentId);
        if (onlySectionId is Guid sectionId)
        {
            sectionsQuery = sectionsQuery.Where(s => s.StructuredLearningDocumentSectionId == sectionId);
        }
        var sections = await sectionsQuery
            .OrderBy(s => s.SortOrder)
            .Select(s => new { s.StructuredLearningDocumentSectionId, s.ChapterTitle, s.SubchapterTitle })
            .ToListAsync(cancellationToken);

        var usageRows = await _dbContext.StructuredLearningGenerationUsages
            .AsNoTracking()
            .Where(u => u.StructuredLearningDocumentId == documentId && u.StructuredLearningDocumentSectionId != null)
            .ToListAsync(cancellationToken);

        var illustrations = await _dbContext.StructuredLearningIllustrations
            .AsNoTracking()
            .Where(i => i.StructuredLearningDocumentSectionId != null)
            .Join(
                _dbContext.StructuredLearningDocumentSections.AsNoTracking().Where(s => s.StructuredLearningDocumentId == documentId),
                illustration => illustration.StructuredLearningDocumentSectionId,
                section => section.StructuredLearningDocumentSectionId,
                (illustration, section) => new { section.StructuredLearningDocumentSectionId, illustration.MediaType, illustration.ImageModel })
            .ToListAsync(cancellationToken);

        var results = new List<StructuredLearningEngramCostResponse>(sections.Count);
        foreach (var section in sections)
        {
            var rows = usageRows.Where(u => u.StructuredLearningDocumentSectionId == section.StructuredLearningDocumentSectionId).ToList();
            var sectionIllustrations = illustrations.Where(i => i.StructuredLearningDocumentSectionId == section.StructuredLearningDocumentSectionId).ToList();
            var imagesGeneratedCount = sectionIllustrations.Count(i => i.MediaType != "Video");
            var videosGeneratedCount = sectionIllustrations.Count(i => i.MediaType == "Video");
            var videoCostUsd = Math.Round(sectionIllustrations.Where(i => i.MediaType == "Video").Sum(i => VideoCostForModel(i.ImageModel)), 4);

            if (rows.Count == 0 && imagesGeneratedCount == 0 && videosGeneratedCount == 0)
            {
                continue;
            }

            var tokenUsage = rows
                .Select(u => new AgentTokenUsage
                {
                    AgentName = u.AgentName,
                    ModelName = u.ModelName ?? string.Empty,
                    InputTokens = u.InputTokens,
                    OutputTokens = u.OutputTokens,
                    CachedInputTokens = u.CachedInputTokens,
                })
                .ToList();
            var estimate = TokenCostEstimator.Estimate(tokenUsage, imagesGeneratedCount, _configuration);
            var modelBreakdown = TokenCostEstimator.EstimateByModel(tokenUsage, _configuration)
                .Select(m => new StructuredLearningEngramModelCostResponse
                {
                    ModelName = m.ModelName,
                    CallCount = m.CallCount,
                    InputTokens = m.BillableInputTokens + m.CachedInputTokens,
                    CachedInputTokens = m.CachedInputTokens,
                    OutputTokens = m.OutputTokens,
                    InputRatePerMillionTokens = m.InputRatePerMillionTokens,
                    CachedInputRatePerMillionTokens = m.CachedInputRatePerMillionTokens,
                    OutputRatePerMillionTokens = m.OutputRatePerMillionTokens,
                    InputCostUsd = m.InputCostUsd,
                    CachedInputCostUsd = m.CachedInputCostUsd,
                    OutputCostUsd = m.OutputCostUsd,
                    TotalCostUsd = m.TotalCostUsd,
                })
                .ToList();
            var imageRate = ReadImageRate();
            var videoModelBreakdown = sectionIllustrations
                .Where(i => i.MediaType == "Video")
                .GroupBy(i => string.IsNullOrWhiteSpace(i.ImageModel) ? "(unknown model)" : i.ImageModel!)
                .Select(g => new StructuredLearningEngramVideoModelCostResponse
                {
                    ModelName = g.Key,
                    VideoCount = g.Count(),
                    RatePerVideoUsd = VideoCostForModel(g.Key),
                    TotalCostUsd = Math.Round(g.Count() * VideoCostForModel(g.Key), 4),
                })
                .ToList();

            results.Add(new StructuredLearningEngramCostResponse
            {
                StructuredLearningDocumentSectionId = section.StructuredLearningDocumentSectionId,
                ChapterTitle = section.ChapterTitle,
                SubchapterTitle = section.SubchapterTitle,
                Steps = string.Join(", ", rows.Select(u => u.StepName).Distinct()),
                CallCount = rows.Count,
                InputTokens = rows.Sum(u => (long)u.InputTokens),
                OutputTokens = rows.Sum(u => (long)u.OutputTokens),
                CachedInputTokens = rows.Sum(u => (long)u.CachedInputTokens),
                ModelBreakdown = modelBreakdown,
                LlmCostUsd = Math.Round(estimate.TotalCostUsd - estimate.ImageCostUsd, 4),
                ImagesGeneratedCount = imagesGeneratedCount,
                ImageRatePerImageUsd = imageRate,
                ImageCostUsd = estimate.ImageCostUsd,
                VideosGeneratedCount = videosGeneratedCount,
                VideoModelBreakdown = videoModelBreakdown,
                VideoCostUsd = videoCostUsd,
                TotalCostUsd = Math.Round(estimate.TotalCostUsd + videoCostUsd, 4),
            });
        }

        return results;
    }
}
