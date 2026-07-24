using System.Text;
using UglyToad.PdfPig;

namespace HumanOS.Storage;

/// <summary>
/// Extracts raw text from a PDF Job Description so it can be handed to
/// <see cref="HumanOS.Agents.JobDescriptionExtractionAgent"/>. Uses
/// UglyToad.PdfPig — a real, pure-.NET PDF parser (no native
/// dependencies) — rather than any placeholder/mock text.
///
/// TODO: Add DOCX text extraction (e.g. via DocumentFormat.OpenXml) once
/// DOCX Job Description uploads need real extraction too — today this
/// only handles the PDF path.
/// </summary>
public static class PdfTextExtractor
{
    public static string ExtractText(Stream pdfContent)
    {
        using var document = PdfDocument.Open(pdfContent);
        var builder = new StringBuilder();

        foreach (var page in document.GetPages())
        {
            builder.AppendLine(page.Text);
        }

        return builder.ToString();
    }

    /// <summary>Result of extracting a PDF's full text together with its
    /// page count, so callers can enforce a maximum-size policy (e.g. the
    /// PDF-to-CapabilityGraph pipeline's configurable page limit) without a
    /// second pass over the document.</summary>
    public sealed class ExtractionResult
    {
        public string Text { get; set; } = string.Empty;

        public int PageCount { get; set; }
    }

    public static ExtractionResult ExtractTextWithPageCount(Stream pdfContent)
    {
        using var document = PdfDocument.Open(pdfContent);
        var builder = new StringBuilder();
        var pageCount = 0;

        foreach (var page in document.GetPages())
        {
            builder.AppendLine(page.Text);
            pageCount++;
        }

        return new ExtractionResult { Text = builder.ToString(), PageCount = pageCount };
    }

    /// <summary>An image embedded in a PDF page, extracted in whatever byte
    /// form it can actually be read in (see <see cref="ExtractPagesWithImages"/>).</summary>
    public sealed class ExtractedPageImage
    {
        public byte[] Bytes { get; set; } = [];

        /// <summary>MIME type of <see cref="Bytes"/> — "image/png" or
        /// "image/jpeg", suitable for sending directly to a vision-capable
        /// chat model.</summary>
        public string ContentType { get; set; } = "image/png";
    }

    /// <summary>One page's extracted text plus any embedded images worth
    /// describing (see <see cref="ExtractPagesWithImages"/>).</summary>
    public sealed class PageExtractionResult
    {
        /// <summary>1-based page number, matching PdfPig's own numbering.</summary>
        public int PageNumber { get; set; }

        public string Text { get; set; } = string.Empty;

        public List<ExtractedPageImage> Images { get; set; } = [];
    }

    /// <summary>Below this pixel size (in either dimension) an embedded
    /// image is almost certainly a decorative icon/bullet/rule rather than
    /// real pedagogical content — not worth a vision-model call.</summary>
    private const int MinImageDimensionPixels = 100;

    /// <summary>An image whose ON-PAGE placement covers at least this
    /// fraction of the page's width AND height, on a page that also has
    /// OTHER images, is treated as a full-bleed decorative background/
    /// template layer (e.g. a slide deck's gradient/blur design behind the
    /// real content) rather than real content — see
    /// <see cref="ExtractPagesWithImages"/>.</summary>
    private const double FullBleedCoverageThreshold = 0.9;

    /// <summary>
    /// Extracts BOTH the real text AND every embedded image (above a
    /// minimum size, to skip decorative icons/bullets/rules) for each page.
    /// This is what lets a scanned/image-only page — where PdfPig's own
    /// <c>page.Text</c> is empty because the "page" is really one big
    /// embedded photo — still contribute real content: callers can send
    /// each <see cref="PageExtractionResult.Images"/> entry to a
    /// vision-capable model to get a text description, then fold that back
    /// into the page's material alongside its (possibly empty) real text.
    ///
    /// <see cref="UglyToad.PdfPig.Content.IPdfImage.TryGetPng"/> is tried
    /// first (handles most raster encodings); when it fails (it does not
    /// support converting JPEG-encoded images to PNG) the image's raw
    /// encoded bytes are used directly as a JPEG file IF they actually
    /// start with a JPEG SOI marker (0xFFD8) — anything else (e.g. CCITT
    /// fax, JPEG2000) is skipped rather than sent to a vision model as
    /// garbage bytes.
    ///
    /// Slide-deck-style PDFs (PowerPoint/Canva exports) commonly rasterize
    /// a design template's gradient/blur background into one or more
    /// full-bleed image XObjects PER PAGE — e.g. a single "page" can embed
    /// 50+ distinct decorative layers that together just recreate a soft
    /// color background, none of which carry any pedagogical content. When
    /// a page has more than one embedded image AND one of them covers
    /// nearly the entire page (see <see cref="FullBleedCoverageThreshold"/>),
    /// that full-bleed one is skipped as a decorative background rather
    /// than sent to the vision model — this is what keeps one design-heavy
    /// slide from burning through the whole per-page/per-run image budget
    /// before real content images are even reached (2026-07-23). A page
    /// whose ONLY image is full-bleed is NOT skipped — that's exactly the
    /// original "PDF that's really just one big scanned photo per page"
    /// case this feature exists for.
    /// </summary>
    public static List<PageExtractionResult> ExtractPagesWithImages(Stream pdfContent)
    {
        using var document = PdfDocument.Open(pdfContent);
        var pages = new List<PageExtractionResult>();

        foreach (var page in document.GetPages())
        {
            var pageResult = new PageExtractionResult
            {
                PageNumber = page.Number,
                Text = page.Text
            };

            var candidateImages = page.GetImages()
                .Where(image => image.WidthInSamples >= MinImageDimensionPixels && image.HeightInSamples >= MinImageDimensionPixels)
                .ToList();

            foreach (var image in candidateImages)
            {
                var isFullBleed = page.Width > 0 && page.Height > 0
                    && image.Bounds.Width >= page.Width * FullBleedCoverageThreshold
                    && image.Bounds.Height >= page.Height * FullBleedCoverageThreshold;

                if (isFullBleed && candidateImages.Count > 1)
                {
                    // Likely a decorative background/template layer sitting
                    // behind other, more meaningful images on this page —
                    // not worth a vision-model call.
                    continue;
                }

                if (image.TryGetPng(out var pngBytes) && pngBytes is { Length: > 0 })
                {
                    pageResult.Images.Add(new ExtractedPageImage { Bytes = pngBytes, ContentType = "image/png" });
                    continue;
                }

                byte[] raw;
                try
                {
                    raw = image.RawBytes.ToArray();
                }
                catch
                {
                    continue;
                }

                if (IsJpeg(raw))
                {
                    pageResult.Images.Add(new ExtractedPageImage { Bytes = raw, ContentType = "image/jpeg" });
                }
            }

            pages.Add(pageResult);
        }

        return pages;
    }

    private static bool IsJpeg(byte[] bytes) => bytes.Length > 2 && bytes[0] == 0xFF && bytes[1] == 0xD8;
}
