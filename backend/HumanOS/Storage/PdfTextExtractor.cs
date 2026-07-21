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
}
