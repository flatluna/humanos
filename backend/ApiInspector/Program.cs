using System.Text.Json;
using HumanOS.Agents.Studio;
using HumanOS.Storage;
using Microsoft.Extensions.Configuration;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;

const string humanOsDir = @"C:\EducationAI\HumanOS\backend\HumanOS";
const string sourceImagePath = @"C:\EducationAI\HumanOS\cover-test.png";
const string pdfPath = @"C:\EducationAI\HumanOS\test-sample-with-image.pdf";

// Synthesize a one-page test PDF with real embedded text AND a real
// embedded PNG image, so ExtractPagesWithImages() has something to find —
// test-sample.pdf turned out to be text-only (0 embedded images).
using (var pdfDocument = new PdfDocument())
{
    var page = pdfDocument.AddPage();
    using var gfx = XGraphics.FromPdfPage(page);
    var font = new XFont("Arial", 16);
    gfx.DrawString("Capítulo 1: Anatomía del sistema óseo", font, XBrushes.Black, new XPoint(40, 40));
    using var image = XImage.FromFile(sourceImagePath);
    gfx.DrawImage(image, 40, 80, 300, 300 * image.PixelHeight / image.PixelWidth);
    pdfDocument.Save(pdfPath);
}

Console.WriteLine($"Generated test PDF with embedded image: {pdfPath}");
Console.WriteLine();

// local.settings.json isn't a flat IConfiguration-shaped file — Azure
// Functions' own host flattens its "Values" object at startup, so a plain
// AddJsonFile() here would put everything under "Values:X" instead of "X".
// Read it manually and feed "Values" in as in-memory config instead.
var localSettingsJson = File.ReadAllText(Path.Combine(humanOsDir, "local.settings.json"));
using var settingsDoc = JsonDocument.Parse(localSettingsJson);
var values = new Dictionary<string, string?>();
if (settingsDoc.RootElement.TryGetProperty("Values", out var valuesElement))
{
    foreach (var prop in valuesElement.EnumerateObject())
    {
        values[prop.Name] = prop.Value.GetString();
    }
}

var configuration = new ConfigurationBuilder()
    .AddInMemoryCollection(values)
    .AddJsonFile(Path.Combine(humanOsDir, "appsettings.json"), optional: true)
    .Build();

Console.WriteLine("=== PDF extraction + image description test ===");
Console.WriteLine($"PDF: {pdfPath}");

var pdfBytes = File.ReadAllBytes(pdfPath);
List<PdfTextExtractor.PageExtractionResult> pages;
using (var stream = new MemoryStream(pdfBytes))
{
    pages = PdfTextExtractor.ExtractPagesWithImages(stream);
}

Console.WriteLine($"Pages: {pages.Count}");
foreach (var page in pages)
{
    Console.WriteLine($"--- Página {page.PageNumber} ---");
    Console.WriteLine($"Text length: {page.Text.Length}");
    if (page.Text.Length > 0)
    {
        Console.WriteLine($"Text preview: {page.Text[..Math.Min(200, page.Text.Length)]}");
    }
    Console.WriteLine($"Images found (>=100px): {page.Images.Count}");
    foreach (var img in page.Images)
    {
        Console.WriteLine($"  - {img.ContentType}, {img.Bytes.Length} bytes");
    }
}

var agent = new PdfImageDescriptionAgent(configuration);
Console.WriteLine();
Console.WriteLine($"PdfImageDescriptionAgent.IsConfigured: {agent.IsConfigured}");

var firstImagePage = pages.FirstOrDefault(p => p.Images.Count > 0);
if (firstImagePage is null)
{
    Console.WriteLine("No images >=100px were extracted from this PDF — nothing to describe.");
    return;
}

if (!agent.IsConfigured)
{
    Console.WriteLine("Agent not configured (missing AzureOpenAIEndpoint/DeploymentName) — cannot call the vision model.");
    return;
}

var firstImage = firstImagePage.Images[0];
Console.WriteLine();
Console.WriteLine($"=== Calling vision model on página {firstImagePage.PageNumber}'s first image ({firstImage.ContentType}, {firstImage.Bytes.Length} bytes) ===");

var result = await agent.DescribeAsync(firstImage.Bytes, firstImage.ContentType, firstImagePage.Text);

Console.WriteLine();
Console.WriteLine("--- Description returned ---");
Console.WriteLine(result.Description);
Console.WriteLine();
Console.WriteLine($"Tokens: input={result.TokenUsage.InputTokens} output={result.TokenUsage.OutputTokens} cached={result.TokenUsage.CachedInputTokens} model={result.TokenUsage.ModelName}");
