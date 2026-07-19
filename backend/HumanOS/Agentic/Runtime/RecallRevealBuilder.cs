namespace HumanOS.Agentic.Runtime;

/// <summary>
/// Builds a short, deterministic "here's what you missed" reveal message
/// (fixed 2026-07-17 — explicit user request: "llegar a un punto donde el
/// agente simplemente le da la respuesta correcta cuando ya llevamos un x
/// numero de iteraciones"). Deliberately NOT an LLM call — reuses the
/// EXACT stored source content verbatim, so there is zero risk of the
/// model inventing or distorting the "correct answer" at the one moment
/// it's finally being revealed after a learner has genuinely struggled
/// across the full retry budget.
/// </summary>
internal static class RecallRevealBuilder
{
    public static string Build(string sourceContent) =>
        $"""
        Antes de seguir, repasemos juntos lo esencial que no lograste recordar por completo — está bien, ya lo intentaste varias veces:

        {sourceContent}

        Ahora sigamos adelante.
        """;
}
