using HumanOS.Contracts.ProcessDocuments;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace HumanOS.Services.ProcessDocuments;

/// <summary>
/// Generates a second, "enriched" PDF for a process document: the chapter/step index plus the
/// extracted <see cref="ProcessStep.SourceContent"/> text and the correctly-matched
/// <see cref="ProcessStep.StepImages"/>, laid out inline, step by step. This enriched PDF is stored
/// alongside the original source PDF and is what gets linked to the Capability (see
/// ProcessDocumentFunction.SaveProcessAsCapability) so Studio's existing course-generation pipeline
/// (which re-extracts text+images from "a PDF") gets pre-structured, correctly-illustrated content
/// instead of having to re-discover it from the raw original document.
/// </summary>
public static class EnrichedProcessPdfGenerator
{
    static EnrichedProcessPdfGenerator()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public static byte[] Generate(string title, string? summary, ProcessDocumentStructure document)
    {
        var doc = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(36);
                page.DefaultTextStyle(x => x.FontSize(11));

                page.Header().Column(col =>
                {
                    col.Item().Text(title).FontSize(20).Bold();
                    col.Item().PaddingBottom(10).LineHorizontal(1);
                });

                page.Content().Column(column =>
                {
                    // Process summary
                    if (!string.IsNullOrWhiteSpace(summary))
                    {
                        column.Item().PaddingBottom(6).Text("Summary").FontSize(16).Bold();
                        column.Item().PaddingBottom(15).Text(summary);
                    }

                    // Index / table of contents
                    column.Item().PaddingBottom(10).Text("Index").FontSize(16).Bold();
                    foreach (var ch in document.Chapters)
                    {
                        column.Item().Text(ch.Title).Bold();
                        foreach (var st in ch.Steps)
                        {
                            column.Item().PaddingLeft(15).Text($"{st.StepNumber}. {st.StepName}");
                        }
                    }

                    column.Item().PaddingVertical(15).LineHorizontal(1);

                    // Chapter/step content, with extracted text and matched images inline
                    foreach (var ch in document.Chapters)
                    {
                        column.Item().PaddingTop(10).Text(ch.Title).FontSize(16).Bold();
                        if (!string.IsNullOrWhiteSpace(ch.Description))
                        {
                            column.Item().PaddingBottom(5).Text(ch.Description).Italic();
                        }

                        foreach (var st in ch.Steps)
                        {
                            column.Item().PaddingTop(8).Text($"{st.StepNumber}. {st.StepName}").FontSize(13).Bold();

                            if (!string.IsNullOrWhiteSpace(st.Objective))
                            {
                                column.Item().Text(t =>
                                {
                                    t.Span("Objective: ").SemiBold();
                                    t.Span(st.Objective);
                                });
                            }

                            if (!string.IsNullOrWhiteSpace(st.SourceContent))
                            {
                                column.Item().PaddingTop(4).Text(st.SourceContent);
                            }

                            foreach (var img in st.StepImages)
                            {
                                var bytes = TryDecodeDataUrl(img.DataUrl);
                                if (bytes is { Length: > 0 })
                                {
                                    column.Item().PaddingTop(6).MaxHeight(260).Image(bytes).FitArea();
                                }
                            }
                        }
                    }
                });

                page.Footer().AlignCenter().Text(t =>
                {
                    t.CurrentPageNumber();
                    t.Span(" / ");
                    t.TotalPages();
                });
            });
        });

        return doc.GeneratePdf();
    }

    private static byte[]? TryDecodeDataUrl(string? dataUrl)
    {
        if (string.IsNullOrWhiteSpace(dataUrl)) return null;
        var commaIdx = dataUrl.IndexOf(',');
        var base64 = commaIdx >= 0 ? dataUrl[(commaIdx + 1)..] : dataUrl;
        try
        {
            return Convert.FromBase64String(base64);
        }
        catch
        {
            return null;
        }
    }
}
