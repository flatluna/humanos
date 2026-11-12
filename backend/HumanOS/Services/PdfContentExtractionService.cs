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

        /// <summary>Each page's [StartOffset, EndOffset) range within
        /// <see cref="CombinedText"/> — lets a caller that locates an offset
        /// in CombinedText (e.g. an outline node's StartMarker position)
        /// figure out which original PDF page it came from, in order to look
        /// up that page's real images in <see cref="PageImageDataUrls"/>.</summary>
        public List<(int PageNumber, int StartOffset, int EndOffset)> PageOffsets { get; set; } = [];

        /// <summary>Real embedded images (as data: URLs), by original PDF
        /// page number — the same budget-capped, de-duplicated selection
        /// used for AI description (see pass 1 in <see cref="ExtractAsync"/>),
        /// so a direct-PDF-upload course (unlike the HumanOS Enterprise
        /// "saved process" flow, which has its own separate image-attach
        /// pipeline) can still show the designer real thumbnails per
        /// section instead of only the AI-written text description.</summary>
        public Dictionary<int, List<string>> PageImageDataUrls { get; set; } = [];
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
                PageOffsets = cached.PageOffsets,
                PageImageDataUrls = cached.PageImageDataUrls,
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
        var pageImageDataUrls = new Dictionary<int, List<string>>();

        if (imageDescriptionAgent.IsConfigured)
        {
            // Pass 1 (cheap, CPU-only, sequential): walk pages/images in the
            // exact original order to decide — respecting the global
            // (maxImagesToDescribe) and per-page (maxImagesPerPage) caps and
            // hash-based de-duplication — which image instances actually get
            // a real AI description ("primary") vs. reuse an already-chosen
            // hash's result ("duplicate") vs. get skipped. This reproduces
            // the exact same selection the old fully-sequential version made,
            // just split out so the actual network calls (pass 2) can run
            // concurrently instead of one-at-a-time.
            var instances = new List<(PdfTextExtractor.PageExtractionResult Page, PdfTextExtractor.ExtractedPageImage Image, string Hash)>();
            var chosenHashes = new HashSet<string>();
            var perPageCount = new Dictionary<PdfTextExtractor.PageExtractionResult, int>();
            var globalCount = 0;

            foreach (var page in pages)
            {
                perPageCount[page] = 0;

                foreach (var image in page.Images)
                {
                    var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(image.Bytes));
                    instances.Add((page, image, hash));

                    if (chosenHashes.Contains(hash))
                    {
                        continue;
                    }

                    if (globalCount >= maxImagesToDescribe || perPageCount[page] >= maxImagesPerPage)
                    {
                        continue;
                    }

                    chosenHashes.Add(hash);
                    globalCount++;
                    perPageCount[page]++;
                }
            }

            // First occurrence (in order) of each chosen hash — this is the
            // one instance that actually gets described; every later
            // instance sharing its hash just reuses the result in pass 3.
            var primaryByHash = new Dictionary<string, (PdfTextExtractor.PageExtractionResult Page, PdfTextExtractor.ExtractedPageImage Image)>();
            foreach (var instance in instances)
            {
                if (chosenHashes.Contains(instance.Hash) && !primaryByHash.ContainsKey(instance.Hash))
                {
                    primaryByHash[instance.Hash] = (instance.Page, instance.Image);
                }
            }

            // Pass 2 (the actual network calls, bounded concurrency): describe
            // every "primary" image, at most 5 calls in flight at once — a
            // 100-page PDF with dozens of images used to describe them fully
            // sequentially (one call after another), which alone could take
            // 9+ minutes and exceed the function host's timeout. Capping at 5
            // (not unbounded) avoids hammering the Azure OpenAI deployment's
            // rate limit while still cutting wall-clock time roughly 5x.
            const int MaxConcurrentImageDescriptions = 5;
            using var semaphore = new SemaphoreSlim(MaxConcurrentImageDescriptions);
            var descriptionsByHash = new Dictionary<string, PdfImageDescriptionAgent.ImageDescriptionResult>();
            var descriptionsLock = new object();
            var progressTotal = primaryByHash.Count;
            var completedCount = 0;

            var describeTasks = primaryByHash.Select(async kvp =>
            {
                var (hash, (page, image)) = (kvp.Key, kvp.Value);
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    var progressIndex = Interlocked.Increment(ref completedCount);
                    reportProgress?.Invoke($"Describiendo imagen {progressIndex}/{progressTotal} (página {page.PageNumber})");

                    var description = await imageDescriptionAgent.DescribeAsync(
                        image.Bytes, image.ContentType, page.Text, cancellationToken);

                    lock (descriptionsLock)
                    {
                        descriptionsByHash[hash] = description;
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // Best-effort — one bad/unsupported image should never fail the whole run.
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(describeTasks);

            // Pass 3 (cheap, sequential, deterministic order): fold each
            // described image's result into its page's text, visiting every
            // instance (primary or duplicate) in the original reading order
            // so the combined text's layout is identical to before. Token
            // usage is only recorded once per hash — on its first (primary)
            // occurrence — matching the old sequential version's behavior.
            var recordedTokenUsageHashes = new HashSet<string>();
            foreach (var (page, image, hash) in instances)
            {
                // Real image thumbnail (as a data: URL), for every instance
                // within the same budget/de-dup selection used for AI
                // description — lets a direct-PDF-upload course show actual
                // page images per detected section, not just AI-written text.
                if (chosenHashes.Contains(hash))
                {
                    var contentType = string.IsNullOrWhiteSpace(image.ContentType) ? "image/png" : image.ContentType;
                    var dataUrl = $"data:{contentType};base64,{Convert.ToBase64String(image.Bytes)}";
                    if (!pageImageDataUrls.TryGetValue(page.PageNumber, out var urls))
                    {
                        urls = [];
                        pageImageDataUrls[page.PageNumber] = urls;
                    }
                    urls.Add(dataUrl);
                }

                if (descriptionsByHash.TryGetValue(hash, out var description))
                {
                    page.Text = AppendImageDescription(page.Text, page.PageNumber, description.Description);

                    if (recordedTokenUsageHashes.Add(hash))
                    {
                        tokenUsage.Add(new AgentTokenUsage
                        {
                            AgentName = description.TokenUsage.AgentName,
                            ModuleId = $"Página {page.PageNumber}",
                            ModelName = description.TokenUsage.ModelName,
                            InputTokens = description.TokenUsage.InputTokens,
                            OutputTokens = description.TokenUsage.OutputTokens,
                            CachedInputTokens = description.TokenUsage.CachedInputTokens
                        });
                    }
                }
            }

            describedImageCount = descriptionsByHash.Count;
        }

        var nonBlankPages = pages.Where(p => !string.IsNullOrWhiteSpace(p.Text)).ToList();
        var pageOffsets = new List<(int PageNumber, int StartOffset, int EndOffset)>();
        var offsetCursor = 0;
        foreach (var page in nonBlankPages)
        {
            var start = offsetCursor;
            var end = start + page.Text.Length;
            pageOffsets.Add((page.PageNumber, start, end));
            offsetCursor = end + "\n\n".Length;
        }
        var combinedText = string.Join("\n\n", nonBlankPages.Select(p => p.Text));

        var result = new ExtractionResult
        {
            CombinedText = combinedText,
            PageCount = pages.Count,
            DescribedImageCount = describedImageCount,
            PageOffsets = pageOffsets,
            PageImageDataUrls = pageImageDataUrls,
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
