namespace HumanOS.Agentic.Studio;

/// <summary>
/// Splits long text into paragraph-aligned chunks (~3000 chars, roughly
/// 700-1000 tokens) suitable for embedding + RAG retrieval. Simple greedy
/// paragraph accumulation — no external tokenizer dependency.
/// </summary>
internal static class TextChunker
{
    private const int MaxChunkChars = 3000;

    public static List<string> Chunk(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        var paragraphs = text
            .Split("\n\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var chunks = new List<string>();
        var current = new System.Text.StringBuilder();

        foreach (var paragraph in paragraphs)
        {
            if (current.Length > 0 && current.Length + paragraph.Length + 2 > MaxChunkChars)
            {
                chunks.Add(current.ToString());
                current.Clear();
            }

            if (current.Length > 0)
            {
                current.Append("\n\n");
            }

            current.Append(paragraph);

            if (current.Length > MaxChunkChars)
            {
                chunks.Add(current.ToString());
                current.Clear();
            }
        }

        if (current.Length > 0)
        {
            chunks.Add(current.ToString());
        }

        return chunks;
    }
}
