using System.Text;
using Docnet.Core;
using Docnet.Core.Models;
using SkiaSharp;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Core;

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

    /// <summary>A single vector-drawn path (line/curve/fill) with both
    /// dimensions at least this many PDF points is "substantial" enough to
    /// plausibly be part of a chart/diagram rather than a table rule,
    /// underline, or bullet — used to filter out hairlines before
    /// computing the UNION bounding box in <see cref="HasSignificantVectorDrawing"/>.</summary>
    private const double MinSubstantialPathDimensionPoints = 10;

    /// <summary>The UNION of every substantial path's bounding box (see
    /// <see cref="MinSubstantialPathDimensionPoints"/>) must be at least
    /// this many PDF points wide AND tall to be treated as a real
    /// chart/diagram worth rasterizing the whole page for — e.g. a pie
    /// chart is drawn as many individual small wedge paths whose
    /// INDIVIDUAL bounding boxes are modest, but whose combined footprint
    /// is clearly a real figure, not random scattered table graphics.</summary>
    private const double MinSignificantVectorDrawingUnionDimensionPoints = 80;

    /// <summary>Scale factor used when rasterizing a whole page as a
    /// fallback (see <see cref="ExtractPagesWithImages"/>'s doc comment on
    /// vector-only figures) — 2x a standard 72-DPI PDF page point grid
    /// gives ~144 DPI, sharp enough for a vision model to read chart labels
    /// without producing an unreasonably large PNG.</summary>
    private const double PageRasterizationScale = 2.0;

    /// <summary>Fraction of the vector-drawn chart's own area that an
    /// existing raster image must overlap for the whole-page rasterization
    /// fallback to be skipped (see <see cref="IsAreaAlreadyCoveredByRasterImages"/>).</summary>
    private const double VectorDrawingCoverageThreshold = 0.5;

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
    ///
    /// A FOURTH case this also now handles (2026-11-04 "Figure 1.1 pie
    /// chart never extracted" fix): many PDFs (especially Word/PowerPoint
    /// exports) draw charts and diagrams — pie charts, bar charts, flow
    /// diagrams — as native PDF VECTOR graphics (paths/lines/fills) rather
    /// than as an embedded raster image. <see cref="UglyToad.PdfPig"/>'s
    /// <c>page.GetImages()</c> only ever returns raster XObjects, so a
    /// page whose only "figure" is vector-drawn previously contributed
    /// NOTHING for that figure — not a description, not an image, nothing
    /// — even though the page's real text (e.g. a "Figure 1.1: ..."
    /// caption) clearly referenced one. This happens EVEN ON a page that
    /// also has a separate, perfectly-extracted raster image elsewhere
    /// (e.g. "Figure 1.2" on the same page as the vector-drawn "Figure
    /// 1.1" pie chart) — so this check runs regardless of whether the page
    /// already produced raster images above. When the page has at least
    /// one group of vector paths whose UNION bounding box is large enough
    /// to be a real chart (see <see cref="HasSignificantVectorDrawing"/>,
    /// which filters out table borders/underlines/rules first), the whole
    /// page is additionally rasterized (via Docnet.Core/PDFium — a real,
    /// if native, PDF renderer) into one more PNG for that page, so the
    /// caller's vision-model description pass (see
    /// <see cref="HumanOS.Services.PdfContentExtractionService"/>) still
    /// has something to actually describe for the vector-only figure.
    /// </summary>
    public static List<PageExtractionResult> ExtractPagesWithImages(Stream pdfContent)
    {
        // Buffered up front (rather than read directly off pdfContent) so
        // the SAME bytes can also be handed to Docnet.Core for the vector-
        // chart rasterization fallback below, which needs a byte[] (not a
        // stream) and its own independent native-library document handle.
        byte[] pdfBytes;
        using (var buffer = new MemoryStream())
        {
            pdfContent.CopyTo(buffer);
            pdfBytes = buffer.ToArray();
        }

        var pages = new List<PageExtractionResult>();

        using (var document = PdfDocument.Open(new MemoryStream(pdfBytes)))
        {
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

                var vectorCoverageBounds = candidateImages.Select(image => image.Bounds).ToList();
                if (HasSignificantVectorDrawing(page, out var vectorDrawingBounds)
                    && !IsAreaAlreadyCoveredByRasterImages(vectorDrawingBounds, vectorCoverageBounds))
                {
                    var rasterized = TryRasterizePage(pdfBytes, page.Number - 1);
                    if (rasterized is not null)
                    {
                        pageResult.Images.Add(rasterized);
                    }
                }

                pages.Add(pageResult);
            }
        }

        return pages;
    }

    /// <summary>True when the page's vector-drawn paths (lines/curves/
    /// fills) add up to something plausibly chart-sized. A single pie
    /// chart "wedge" or bar-chart "bar" is itself only a modest-sized path
    /// — e.g. one slice of a pie might be a ~90x70pt path — so checking any
    /// ONE path's bounding box in isolation would miss real charts.
    /// Instead: filter out hairline paths (table rules/underlines — see
    /// <see cref="MinSubstantialPathDimensionPoints"/>), then take the
    /// UNION bounding box of everything that's left; if that union is
    /// itself large enough (see
    /// <see cref="MinSignificantVectorDrawingUnionDimensionPoints"/>), the
    /// page almost certainly contains a real vector-drawn chart/diagram —
    /// see <see cref="ExtractPagesWithImages"/>'s doc comment.</summary>
    private static bool HasSignificantVectorDrawing(UglyToad.PdfPig.Content.Page page, out PdfRectangle bounds)
    {
        double? left = null, bottom = null, right = null, top = null;

        foreach (var path in page.ExperimentalAccess.Paths)
        {
            PdfRectangle b;
            try
            {
                if (path.GetBoundingRectangle() is not { } boundingBox)
                {
                    continue;
                }
                b = boundingBox;
            }
            catch
            {
                continue;
            }

            if (b.Width < MinSubstantialPathDimensionPoints || b.Height < MinSubstantialPathDimensionPoints)
            {
                continue;
            }

            left = left is null ? b.Left : Math.Min(left.Value, b.Left);
            bottom = bottom is null ? b.Bottom : Math.Min(bottom.Value, b.Bottom);
            right = right is null ? b.Right : Math.Max(right.Value, b.Right);
            top = top is null ? b.Top : Math.Max(top.Value, b.Top);
        }

        if (left is null)
        {
            bounds = default;
            return false;
        }

        var unionWidth = right!.Value - left.Value;
        var unionHeight = top!.Value - bottom!.Value;

        bounds = new PdfRectangle(left.Value, bottom.Value, right.Value, top.Value);

        return unionWidth >= MinSignificantVectorDrawingUnionDimensionPoints
            && unionHeight >= MinSignificantVectorDrawingUnionDimensionPoints;
    }

    /// <summary>True when the already-extracted raster image(s) on this
    /// page, COMBINED, substantially cover the vector-drawn chart's own
    /// bounding box (2026-11-05 "spurious combined-page rasterization"
    /// fix) — in that case the figure's real content is already available
    /// as proper per-image rasters (each will get its own vision-model
    /// description), so rasterizing the WHOLE page again on top of them
    /// would only add a redundant "everything stacked together" extra
    /// image/chapter. Sums each image's own overlap with the vector
    /// bounds (rather than checking one image at a time) because the
    /// common real case is TWO side-by-side images under one shared
    /// decorative border/frame — neither image alone reaches the 60%
    /// threshold, but together they legitimately cover the figure. Treats
    /// raster images as non-overlapping with each other (true in
    /// practice), so summing their individual overlaps approximates the
    /// union without double-counting.</summary>
    private static bool IsAreaAlreadyCoveredByRasterImages(PdfRectangle vectorDrawingBounds, List<PdfRectangle> imageBounds)
    {
        var vectorArea = vectorDrawingBounds.Width * vectorDrawingBounds.Height;
        if (vectorArea <= 0)
        {
            return false;
        }

        double totalOverlapArea = 0;

        foreach (var imageBound in imageBounds)
        {
            var left = Math.Max(vectorDrawingBounds.Left, imageBound.Left);
            var right = Math.Min(vectorDrawingBounds.Right, imageBound.Right);
            var bottom = Math.Max(vectorDrawingBounds.Bottom, imageBound.Bottom);
            var top = Math.Min(vectorDrawingBounds.Top, imageBound.Top);

            var overlapWidth = right - left;
            var overlapHeight = top - bottom;
            if (overlapWidth <= 0 || overlapHeight <= 0)
            {
                continue;
            }

            totalOverlapArea += overlapWidth * overlapHeight;
        }

        return totalOverlapArea / vectorArea >= VectorDrawingCoverageThreshold;
    }

    /// <summary>Renders one (zero-based) page of the PDF to a PNG using
    /// Docnet.Core/PDFium. Returns null (rather than throwing) on any
    /// failure — this is strictly a best-effort fallback for vector-only
    /// figures, so a native-library hiccup must never fail the whole
    /// extraction run.</summary>
    private static ExtractedPageImage? TryRasterizePage(byte[] pdfBytes, int pageIndex)
    {
        try
        {
            using var docReader = DocLib.Instance.GetDocReader(pdfBytes, new PageDimensions(PageRasterizationScale));
            using var pageReader = docReader.GetPageReader(pageIndex);

            var width = pageReader.GetPageWidth();
            var height = pageReader.GetPageHeight();
            var bgra = pageReader.GetImage();

            if (width <= 0 || height <= 0 || bgra.Length < width * height * 4)
            {
                return null;
            }

            using var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul));
            System.Runtime.InteropServices.Marshal.Copy(bgra, 0, bitmap.GetPixels(), bgra.Length);

            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);

            return new ExtractedPageImage { Bytes = data.ToArray(), ContentType = "image/png" };
        }
        catch
        {
            return null;
        }
    }

    private static bool IsJpeg(byte[] bytes) => bytes.Length > 2 && bytes[0] == 0xFF && bytes[1] == 0xD8;
}
