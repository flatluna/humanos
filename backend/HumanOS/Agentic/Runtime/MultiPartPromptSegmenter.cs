using System.Text.RegularExpressions;

namespace HumanOS.Agentic.Runtime;

/// <summary>
/// Splits a stored chapter prompt (e.g. <c>PredictionPrompt</c>/
/// <c>RecallPrompt</c>) into individual sub-questions when it was authored
/// as a multi-part numbered questionnaire (fixed 2026-07-17 — real
/// production bug found live: a chapter's <c>PredictionPrompt</c> had 7
/// numbered sub-questions, and the Tutor's "never omit/summarize the
/// source content" rule made it read ALL 7 aloud in one breath instead of
/// a real back-and-forth — explicit user feedback: "le estamos leyendo
/// todo de un jalón... esto es una locura"). Splitting here lets the
/// Runtime present ONE sub-question per turn — a genuine turn-based
/// micro-dialogue — reusing the EXISTING Runtime/TTS infrastructure, no
/// new realtime voice API needed (per explicit user decision 2026-07-17).
/// </summary>
/// <remarks>
/// Deliberately conservative: if the text does NOT look like a numbered
/// list (fewer than 2 matches), it's returned as a single-element list —
/// well-formed, single-question prompts (the INTENDED Studio authoring
/// shape per <c>InstructorAgent</c>'s own rule: "ONE strong, concrete
/// anticipation question") behave IDENTICALLY to before this fix, zero
/// regression risk.
/// </remarks>
internal static class MultiPartPromptSegmenter
{
    private static readonly Regex NumberedItemPattern = new(@"(?m)^\s*\d+[\)\.]\s+", RegexOptions.Compiled);

    public static IReadOnlyList<string> Split(string prompt)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            return [prompt];
        }

        var matches = NumberedItemPattern.Matches(prompt);

        if (matches.Count < 2)
        {
            return [prompt];
        }

        var segments = new List<string>(matches.Count);
        for (var i = 0; i < matches.Count; i++)
        {
            var start = matches[i].Index;
            var end = i + 1 < matches.Count ? matches[i + 1].Index : prompt.Length;
            segments.Add(prompt[start..end].Trim());
        }

        return segments;
    }
}
