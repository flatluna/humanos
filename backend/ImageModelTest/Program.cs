// Throwaway diagnostic: compare gpt-image-2 vs gpt-image-1.5 on Spanish
// text-rendering quality (word cloud with accented words) — NOT part of
// the real pipeline, just a quick visual check before deciding whether to
// invest in an HTML/structured-content architecture change.
using System.Text.Json;
using Azure.AI.OpenAI;
using OpenAI.Images;

var settingsPath = Path.Combine("..", "HumanOS", "local.settings.json");
var settingsJson = File.ReadAllText(settingsPath);
using var doc = JsonDocument.Parse(settingsJson);
var values = doc.RootElement.GetProperty("Values");
var endpoint = values.GetProperty("AzureOpenAIEndpoint").GetString()!;
var apiKey = values.GetProperty("AzureOpenAIApiKey").GetString()!;

var client = new AzureOpenAIClient(new Uri(endpoint), new System.ClientModel.ApiKeyCredential(apiKey));

const string prompt = """
    Los mismos comentarios ahora con etiquetas 'Positivo', 'Neutral', 'Negativo' y barras
    que muestran la puntuación de sentimiento.
    """;

#pragma warning disable OPENAI001
var options = new ImageGenerationOptions
{
    Size = GeneratedImageSize.W1024xH1024,
    Quality = GeneratedImageQuality.LowQuality,
};
#pragma warning restore OPENAI001

foreach (var deployment in new[] { "gpt-image-2", "gpt-image-1.5" })
{
    Console.WriteLine($"Generating with {deployment}...");
    var imageClient = client.GetImageClient(deployment);
    var response = await imageClient.GenerateImageAsync(prompt, options);
    var outPath = Path.Combine(Path.GetTempPath(), $"wordcloud-{deployment}.png");
    await File.WriteAllBytesAsync(outPath, response.Value.ImageBytes.ToArray());
    Console.WriteLine($"Saved: {outPath}");
}
