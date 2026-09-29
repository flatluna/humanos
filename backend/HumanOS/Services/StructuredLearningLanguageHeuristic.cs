using System.Text.Json;

namespace HumanOS.Services;

/// <summary>
/// Resolves a Structured Learning document's language for catalog/filter
/// purposes. LanguageCode in Document.ConfigSnapshotJson has been found to be
/// mistagged on some documents (authored/copied without updating the config),
/// so it is only used as a fallback when the actual sample text (executive
/// summary/name) doesn't have enough signal to decide reliably. Whenever the
/// sample text gives a clear, non-tied word-frequency signal, that content-based
/// result wins over the stored tag — the real content is more trustworthy than
/// a config value nobody re-checks after copy/paste.
/// </summary>
public static class StructuredLearningLanguageHeuristic
{
    public static string Resolve(string? configSnapshotJson, string? sampleText)
    {
        string? configuredCode = null;
        if (!string.IsNullOrWhiteSpace(configSnapshotJson))
        {
            try
            {
                var snapshot = JsonSerializer.Deserialize<JsonElement>(configSnapshotJson);
                foreach (var prop in snapshot.EnumerateObject())
                {
                    if (string.Equals(prop.Name, "LanguageCode", StringComparison.OrdinalIgnoreCase) &&
                        prop.Value.ValueKind == JsonValueKind.String)
                    {
                        var code = prop.Value.GetString();
                        if (!string.IsNullOrWhiteSpace(code))
                        {
                            configuredCode = code.Trim().ToLowerInvariant().StartsWith("es") ? "es" : "en";
                        }
                    }
                }
            }
            catch (JsonException) { }
        }

        if (!string.IsNullOrWhiteSpace(sampleText))
        {
            var padded = $" {sampleText.ToLowerInvariant()} ";
            string[] englishWords = [" the ", " and ", " you ", " is ", " to ", " your ", " with ", " that ", " this "];
            string[] spanishWords = [" el ", " la ", " de ", " que ", " los ", " las ", " para ", " con ", " es ", " tu "];
            var englishHits = englishWords.Count(w => padded.Contains(w));
            var spanishHits = spanishWords.Count(w => padded.Contains(w));
            if (englishHits != spanishHits)
            {
                return englishHits > spanishHits ? "en" : "es";
            }
        }

        return configuredCode ?? "es";
    }
}
