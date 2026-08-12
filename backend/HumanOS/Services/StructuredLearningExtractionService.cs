using HumanOS.Agents.Studio;
using HumanOS.Data;
using HumanOS.Models.Capabilities.Studio;
using HumanOS.Storage;
using Microsoft.Data.SqlTypes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SharpToken;

namespace HumanOS.Services;

/// <summary>
/// Step 2 of the new TOC-driven "Structured Learning Document" Studio
/// pipeline: takes a designer-APPROVED outline (from the SAME wizard flow
/// as the Graph/Traditional pipelines — Material type -&gt; Upload content
/// -&gt; Review -&gt; Learning path design) and turns it into fully-extracted
/// <see cref="StructuredLearningDocumentSection"/> rows — real verbatim
/// text, character/token counts, and a semantic embedding vector.
///
/// Deliberately NO LLM judgment call in this step (only an embedding call,
/// which is not generative) — never invents/paraphrases content. The
/// Entity/Connection ontology tables exist in the schema from Step 1 but
/// are intentionally left EMPTY here; they are populated by a future,
/// separate LLM-based step (Step "2.5"/ontology), not this one.
///
/// New, additive component — does not modify the existing Graph
/// (<see cref="PdfCapabilityGraphPipelineService"/>) or Traditional
/// (<see cref="TraditionalCoursePipelineService"/>) pipelines; only reuses
/// their already-proven building blocks (PdfContentExtractionService,
/// DocumentChapterSplitter, CapabilityEmbeddingService).
/// </summary>
public sealed class StructuredLearningExtractionService
{
    private const int DefaultMaxPages = 100;
    private const int DefaultMaxImagesToDescribe = 60;
    private const int DefaultMaxImagesPerPage = 8;

    /// <summary>How many leading characters of a section's own Content are
    /// used as a search anchor to locate its page range — see
    /// <see cref="ResolvePageRange"/>.</summary>
    private const int PageAnchorLength = 60;

    private readonly IdeaToDocumentAgent _ideaToDocument;
    private readonly PdfImageDescriptionAgent _pdfImageDescription;
    private readonly PdfContentExtractionService _pdfContentExtraction;
    private readonly CapabilityEmbeddingService _embeddingService;
    private readonly IDbContextFactory<HumanOsDbContext> _dbContextFactory;
    private readonly IConfiguration _configuration;
    private readonly GptEncoding _tokenEncoding = GptEncoding.GetEncoding("cl100k_base");

    public StructuredLearningExtractionService(
        IdeaToDocumentAgent ideaToDocument,
        PdfImageDescriptionAgent pdfImageDescription,
        PdfContentExtractionService pdfContentExtraction,
        CapabilityEmbeddingService embeddingService,
        IDbContextFactory<HumanOsDbContext> dbContextFactory,
        IConfiguration configuration)
    {
        _ideaToDocument = ideaToDocument;
        _pdfImageDescription = pdfImageDescription;
        _pdfContentExtraction = pdfContentExtraction;
        _embeddingService = embeddingService;
        _dbContextFactory = dbContextFactory;
        _configuration = configuration;
    }

    /// <summary>The embedding call is the only hard requirement — the
    /// rest of this step is deterministic, no-LLM extraction.</summary>
    public bool IsConfigured => _embeddingService.IsConfigured;

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

    /// <summary>Entry point for an uploaded PDF source document.</summary>
    public async Task<StructuredLearningExtractionResult> RunFromPdfAsync(
        byte[] pdfBytes,
        string fileName,
        Guid tenantId,
        Guid sessionId,
        List<CapabilityOutlineChapterDto> approvedOutline,
        Action<string> reportProgress,
        CancellationToken cancellationToken)
    {
        reportProgress("Extrayendo texto del documento");

        var maxImagesToDescribe = _configuration.GetValue<int?>("MaxCapabilityPdfImagesToDescribe") ?? DefaultMaxImagesToDescribe;
        var maxImagesPerPage = _configuration.GetValue<int?>("MaxCapabilityPdfImagesPerPage") ?? DefaultMaxImagesPerPage;

        var extraction = await _pdfContentExtraction.ExtractAsync(
            pdfBytes, _pdfImageDescription, maxImagesToDescribe, maxImagesPerPage, reportProgress, cancellationToken);

        if (extraction.PageCount > MaxSourcePdfPages)
        {
            throw new PdfTooLargeException(extraction.PageCount, MaxSourcePdfPages);
        }

        // Separate, plain (no image descriptions folded in) re-extraction
        // used ONLY for best-effort page-range lookup — see
        // ResolvePageRange's doc comment for why this is safe even though
        // it's a different string than extraction.CombinedText.
        List<PdfTextExtractor.PageExtractionResult> plainPages;
        using (var stream = new MemoryStream(pdfBytes))
        {
            plainPages = PdfTextExtractor.ExtractPagesWithImages(stream);
        }

        return await RunFromDocumentTextAsync(
            extraction.CombinedText, fileName, plainPages, tenantId, sessionId, approvedOutline, reportProgress, cancellationToken);
    }

    /// <summary>Entry point for the "Texto/idea" source (no PDF) — no page
    /// numbers exist for this source, so every section's PageFrom/PageTo
    /// stays null.</summary>
    public async Task<StructuredLearningExtractionResult> RunFromDescriptionAsync(
        string description,
        string documentName,
        Guid tenantId,
        Guid sessionId,
        List<CapabilityOutlineChapterDto> approvedOutline,
        string? preExpandedDocumentText,
        string languageCode,
        bool strictSyllabusMode,
        Action<string> reportProgress,
        CancellationToken cancellationToken)
    {
        string documentText;
        if (!string.IsNullOrWhiteSpace(preExpandedDocumentText))
        {
            documentText = preExpandedDocumentText;
        }
        else
        {
            reportProgress("Expandiendo la idea en un documento completo");
            var expansion = await _ideaToDocument.ExpandAsync(documentName, description, languageCode, strictSyllabusMode, cancellationToken);
            documentText = expansion.DocumentText;
        }

        return await RunFromDocumentTextAsync(
            documentText, documentName, null, tenantId, sessionId, approvedOutline, reportProgress, cancellationToken);
    }

    private async Task<StructuredLearningExtractionResult> RunFromDocumentTextAsync(
        string documentText,
        string documentName,
        List<PdfTextExtractor.PageExtractionResult>? plainPages,
        Guid tenantId,
        Guid sessionId,
        List<CapabilityOutlineChapterDto> approvedOutline,
        Action<string> reportProgress,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(documentText))
        {
            throw new InvalidOperationException("No extractable text was found for this document.");
        }

        if (approvedOutline is not { Count: > 0 })
        {
            throw new InvalidOperationException("An approved outline (Learning path design) is required for Step 2.");
        }

        reportProgress("Localizando secciones aprobadas");

        var source = new RawMaterialItem { Type = RawMaterialType.Pdf, Label = documentName, Content = documentText };
        var sectionItems = DocumentChapterSplitter.BuildMaterialsFromApprovedOutline(source, approvedOutline);

        var pageOffsets = plainPages is null ? null : BuildPageOffsets(plainPages);
        var plainJoinedText = plainPages is null ? null : string.Join("\n\n", plainPages.Select(p => p.Text).Where(t => !string.IsNullOrWhiteSpace(t)));

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var document = new StructuredLearningDocument
        {
            StructuredLearningDocumentId = Guid.NewGuid(),
            TenantId = tenantId,
            SessionId = sessionId,
            DocumentName = documentName,
            CreatedDate = DateTime.UtcNow
        };
        dbContext.StructuredLearningDocuments.Add(document);
        await dbContext.SaveChangesAsync(cancellationToken);

        var totalCharacters = 0;
        var totalTokens = 0;
        var pageSearchFrom = 0;

        for (var i = 0; i < sectionItems.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var item = sectionItems[i];
            reportProgress($"Extrayendo sección {i + 1}/{sectionItems.Count}: {item.ChapterTitle}");

            var characterCount = item.Content.Length;
            var tokenCount = _tokenEncoding.Encode(item.Content).Count;
            var embedding = await _embeddingService.GenerateEmbeddingAsync(item.Content, cancellationToken);

            (int? pageFrom, int? pageTo) = (null, null);
            if (pageOffsets is not null && plainJoinedText is not null)
            {
                (pageFrom, pageTo, pageSearchFrom) = ResolvePageRange(plainJoinedText, pageOffsets, item.Content, pageSearchFrom);
            }

            var section = new StructuredLearningDocumentSection
            {
                StructuredLearningDocumentSectionId = Guid.NewGuid(),
                StructuredLearningDocumentId = document.StructuredLearningDocumentId,
                TenantId = tenantId,
                SortOrder = i,
                ChapterTitle = item.LevelTitle ?? item.ChapterTitle ?? documentName,
                SubchapterTitle = item.ChapterTitle ?? string.Empty,
                Text = item.Content,
                TotalCharacters = characterCount,
                TotalTokens = tokenCount,
                Embedding = new SqlVector<float>(embedding),
                PageFrom = pageFrom,
                PageTo = pageTo,
                CreatedDate = DateTime.UtcNow
            };
            dbContext.StructuredLearningDocumentSections.Add(section);
            // Persisted immediately per section (not batched at the end) so
            // an interrupted run never loses already-paid-for embedding calls.
            await dbContext.SaveChangesAsync(cancellationToken);

            totalCharacters += characterCount;
            totalTokens += tokenCount;
        }

        return new StructuredLearningExtractionResult
        {
            StructuredLearningDocumentId = document.StructuredLearningDocumentId,
            DocumentName = documentName,
            SectionCount = sectionItems.Count,
            TotalCharacters = totalCharacters,
            TotalTokens = totalTokens
        };
    }

    /// <summary>Offset (in the plain, no-image-description page join) where
    /// each page's text begins, paired with that page's 1-based number.</summary>
    private static List<(int Offset, int PageNumber)> BuildPageOffsets(List<PdfTextExtractor.PageExtractionResult> pages)
    {
        var offsets = new List<(int Offset, int PageNumber)>();
        var cursor = 0;
        foreach (var page in pages)
        {
            if (string.IsNullOrWhiteSpace(page.Text))
            {
                continue;
            }

            offsets.Add((cursor, page.PageNumber));
            cursor += page.Text.Length + 2; // "\n\n" separator
        }

        return offsets;
    }

    /// <summary>
    /// Best-effort page-range lookup: searches for a short anchor (this
    /// section's own leading characters) inside a PLAIN re-join of the
    /// PDF's pages (no AI image descriptions folded in — those are only
    /// ever APPENDED after a page's real text, never inserted before it,
    /// so a section's own leading text is unaffected and still appears
    /// verbatim in this plain text). Never blocks extraction — returns
    /// (null, null) if the anchor can't be located.
    /// </summary>
    private static (int? PageFrom, int? PageTo, int NextSearchFrom) ResolvePageRange(
        string plainJoinedText, List<(int Offset, int PageNumber)> pageOffsets, string sectionContent, int searchFrom)
    {
        var anchor = sectionContent.TrimStart();
        anchor = anchor[..Math.Min(PageAnchorLength, anchor.Length)];
        if (string.IsNullOrWhiteSpace(anchor))
        {
            return (null, null, searchFrom);
        }

        var matchStart = plainJoinedText.IndexOf(anchor, Math.Min(searchFrom, plainJoinedText.Length), StringComparison.Ordinal);
        if (matchStart < 0)
        {
            return (null, null, searchFrom);
        }

        var matchEnd = Math.Min(matchStart + sectionContent.Length, plainJoinedText.Length - 1);

        var pageFrom = PageAt(pageOffsets, matchStart);
        var pageTo = PageAt(pageOffsets, matchEnd);
        return (pageFrom, pageTo, matchStart);
    }

    private static int? PageAt(List<(int Offset, int PageNumber)> pageOffsets, int offset)
    {
        var page = pageOffsets.LastOrDefault(p => p.Offset <= offset);
        return page.PageNumber == 0 ? pageOffsets.FirstOrDefault().PageNumber : page.PageNumber;
    }
}
