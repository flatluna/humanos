using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace HumanOS.Tests.Services;

public static class TestPdfGenerator
{
    public static byte[] CreateSampleProcessPdf(string title, string chapter, string step, string tableText = "")
    {
        var builder = new PdfDocumentBuilder();
        var page = builder.AddPage(PageSize.A4);
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        var boldFont = builder.AddStandard14Font(Standard14Font.HelveticaBold);

        var y = 750.0;
        // Document Title
        page.AddText(title, 18, new PdfPoint(50, y), boldFont);
        y -= 40;

        // Chapter
        page.AddText(chapter, 14, new PdfPoint(50, y), boldFont);
        y -= 30;

        // Step
        page.AddText(step, 12, new PdfPoint(50, y), boldFont);
        y -= 25;

        // Objective
        page.AddText("Objetivo: Garantizar la correcta recepcion de la orden de compra.", 10, new PdfPoint(50, y), font);
        y -= 20;

        // Required Activities
        page.AddText("Actividades requeridas: Validar datos en el ERP, cotejar con cotizacion", 10, new PdfPoint(50, y), font);
        y -= 20;

        // Required Skills
        page.AddText("Habilidades requeridas: Manejo de SAP, Atencion al detalle", 10, new PdfPoint(50, y), font);
        y -= 20;

        // Deliverables
        page.AddText("Entregables: Reporte de recepcion firmado, Orden aprobada", 10, new PdfPoint(50, y), font);
        y -= 20;

        // Completion condition
        page.AddText("Condicion de finalizacion: Orden registrada con estatus APROBADA", 10, new PdfPoint(50, y), font);
        y -= 20;

        // Checklists
        page.AddText("[ ] Documento fiscal adjunto", 10, new PdfPoint(50, y), font);
        y -= 20;
        page.AddText("[x] Firma autorizada", 10, new PdfPoint(50, y), font);
        y -= 30;

        // Table if provided
        if (!string.IsNullOrWhiteSpace(tableText))
        {
            var lines = tableText.Split('\n');
            foreach (var l in lines)
            {
                page.AddText(l.Trim(), 10, new PdfPoint(50, y), font);
                y -= 18;
            }
        }

        return builder.Build();
    }

    public static byte[] CreateNarrativePdf(string title, string narrative)
    {
        var builder = new PdfDocumentBuilder();
        var page = builder.AddPage(PageSize.A4);
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        var boldFont = builder.AddStandard14Font(Standard14Font.HelveticaBold);

        var y = 750.0;
        page.AddText(title, 16, new PdfPoint(50, y), boldFont);
        y -= 40;

        var lines = narrative.Split('\n');
        foreach (var l in lines)
        {
            page.AddText(l.Trim(), 10, new PdfPoint(50, y), font);
            y -= 20;
        }

        return builder.Build();
    }
}
