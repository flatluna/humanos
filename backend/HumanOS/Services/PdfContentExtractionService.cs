using HumanOS.Agents.Studio;
using HumanOS.Storage;
using Microsoft.Extensions.Caching.Memory;

namespace HumanOS.Services;

/// <summary>
/// Shared "extract PDF text + describe embedded images, fold the
/// descriptions into each page's text" logic (2026-08-05) — extracted out
/// of <see cref="PdfCapabilityGraphPipelineService.RunAsync"/> so the cheap
/// "Paso 5: Capability Design" outline preview (<see cref="AzureFunctions.Api.PreviewCapabilityOutlineFunction"/>)
/// can reuse the EXACT same text+image extraction instead of calling
/// <see cref="PdfTextExtractor.ExtractText"/> alone. Without this, a
/// scanned/image-heavy PDF (little or no real extractable text) reached
/// the preview's chapter/index detection with almost nothing to work
/// with, even though the real generation run already described its images
/// just fine — the preview's detected outline must be based on the SAME
/// content the real run will actually use.
///
/// ALSO caches its result (in-memory, keyed by a SHA256 hash of the PDF's
/// own bytes) so that when the designer approves Paso 5's preview and then
/// starts the real run on the SAME uploaded file, the real run does NOT
/// pay to describe every embedded image a second time — it reuses exactly
/// the combined text (real text + image descriptions) the preview already
/// produced. A cache miss (e.g. a different process instance in a scaled-
/// out deployment, or the cache entry expired) just falls back to
/// re-extracting from scratch — never a correctness issue, purely a cost/
/// latency optimization.
/// </summary>
public sealed class PdfContentExtractionService(IMemoryCache cache)
{
    /// <summary>How long a computed extraction stays cached — long enough
    /// to cover the typical "designer reviews the Paso 5 preview, then
    /// clicks start" gap, short enough to not hold PDF-sized text in
    /// memory indefinitely.</summary>
    private static readonly MemoryCacheEntryOptions CacheEntryOptions = new() { SlidingExpiration = TimeSpan.FromMinutes(30) };

    public sealed class ExtractionResult
    {
        public string CombinedText { get; set; } = string.Empty;

        public int PageCount { get; set; }

        public int DescribedImageCount { get; set; }

        public List<AgentTokenUsage> TokenUsage { get; set; } = [];
    }

    /// <summary>Extracts every page's text, describes embedded images
    /// (best-effort, budget-capped, de-duplicated by content hash — see
    /// <see cref="PdfTextExtractor.ExtractPagesWithImages"/>'s doc comment)
    /// and folds each description into its own page's text, then joins
    /// every non-blank page into one combined document string. Returns a
    /// cached result (with an EMPTY <see cref="ExtractionResult.TokenUsage"/>,
    /// since no new AI call was made) when the exact same PDF bytes were
    /// already extracted recently — see this class's own doc comment.</summary>
    public async Task<ExtractionResult> ExtractAsync(
        byte[] pdfBytes,
        PdfImageDescriptionAgent imageDescriptionAgent,
        int maxImagesToDescribe,
        int maxImagesPerPage,
        Action<string>? reportProgress = null,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = "PdfContentExtraction:" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(pdfBytes));
        if (cache.TryGetValue(cacheKey, out ExtractionResult? cached) && cached is not null)
        {
            reportProgress?.Invoke("Reutilizando texto e imágenes ya descritos en Paso 5 (Diseño de la Capability)");
            return new ExtractionResult
            {
                CombinedText = cached.CombinedText,
                PageCount = cached.PageCount,
                DescribedImageCount = cached.DescribedImageCount,
                TokenUsage = []
            };
        }

        List<PdfTextExtractor.PageExtractionResult> pages;
        using (var stream = new MemoryStream(pdfBytes))
        {
            pages = PdfTextExtractor.ExtractPagesWithImages(stream);
        }

        var tokenUsage = new List<AgentTokenUsage>();
        var describedImageCount = 0;

        if (imageDescriptionAgent.IsConfigured)
        {
            var descriptionsByHash = new Dictionary<string, string>();
            var totalUniqueImages = pages
                .SelectMany(p => p.Images)
                .Select(image => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(image.Bytes)))
                .Distinct()
                .Count();
            var progressTotal = Math.Min(maxImagesToDescribe, totalUniqueImages);

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

                    if (describedImageCount >= maxImagesToDescribe || describedOnThisPage >= maxImagesPerPage)
                    {
                        continue;
                    }

                    try
                    {
                        reportProgress?.Invoke($"Describiendo imagen {describedImageCount + 1}/{progressTotal} (página {page.PageNumber})");

                        var description = await imageDescriptionAgent.DescribeAsync(
                            image.Bytes, image.ContentType, page.Text, cancellationToken);

                        descriptionsByHash[hash] = description.Description;
                        page.Text = AppendImageDescription(page.Text, page.PageNumber, description.Description);

                        tokenUsage.Add(new AgentTokenUsage
                        {
                            AgentName = description.TokenUsage.AgentName,
                            ModuleId = $"Página {page.PageNumber}",
                            ModelName = description.TokenUsage.ModelName,
                            InputTokens = description.TokenUsage.InputTokens,
                            OutputTokens = description.TokenUsage.OutputTokens,
                            CachedInputTokens = description.TokenUsage.CachedInputTokens
                        });
                        describedImageCount++;
                        describedOnThisPage++;
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        // Best-effort — one bad/unsupported image should never fail the whole run.
                    }
                }
            }
        }

        var combinedText = string.Join("\n\n", pages.Select(p => p.Text).Where(t => !string.IsNullOrWhiteSpace(t)));

        var result = new ExtractionResult
        {
            CombinedText = combinedText,
            PageCount = pages.Count,
            DescribedImageCount = describedImageCount,
            TokenUsage = tokenUsage
        };
        cache.Set(cacheKey, result, CacheEntryOptions);
        return result;
    }

    private static string AppendImageDescription(string pageText, int pageNumber, string description) =>
        string.IsNullOrWhiteSpace(pageText)
            ? $"[Descripción de imagen — página {pageNumber}]\n{description}"
            : $"{pageText}\n\n[Descripción de imagen — página {pageNumber}]\n{description}";
}
