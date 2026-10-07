namespace HumanOS.Services;

/// <summary>
/// One known, configurable platform quota/limit — the Azure/Microsoft-style
/// "service limits" catalog entry (2026-10-04). Registering a new quota here
/// is the ONLY step needed for it to show up in the admin "Quotas" page and
/// be resolvable via <see cref="TenantQuotaService"/> — no migration, no new
/// table, no new endpoint. Enforcement of any given key (actually blocking
/// something when exceeded) is wired separately, per service, as each one is
/// implemented — a catalog entry existing does not by itself guarantee
/// enforcement is live yet (see each entry's <see cref="IsEnforced"/>).
/// </summary>
public sealed record QuotaDefinition(
    string Key,
    string Category,
    string LabelEn,
    string LabelEs,
    string DescriptionEn,
    string DescriptionEs,
    string UnitEn,
    string UnitEs,
    decimal PlatformDefault,
    bool IsEnforced);

public static class QuotaCatalog
{
    public const string BuddyVoiceMinutesPerMonth = "BuddyVoiceMinutesPerMonth";
    public const string BuddyTextInputTokensPerMonth = "BuddyTextInputTokensPerMonth";
    public const string BuddyTextOutputTokensPerMonth = "BuddyTextOutputTokensPerMonth";
    public const string MaxCoursesPerMonth = "MaxCoursesPerMonth";
    public const string MaxEngramsPerCourse = "MaxEngramsPerCourse";
    public const string MaxDeepDiveCardsPerEngram = "MaxDeepDiveCardsPerEngram";
    public const string MaxCoursesPerPersonPerMonth = "MaxCoursesPerPersonPerMonth";
    public const string MaxEngramReviewsPerPersonPerMonth = "MaxEngramReviewsPerPersonPerMonth";
    public const string MaxVoiceInteractionsPerMonth = "MaxVoiceInteractionsPerMonth";

    /// <summary>All known quotas, in display order. Add a new
    /// <see cref="QuotaDefinition"/> here to add a new configurable limit —
    /// everything else (admin UI listing, defaulting, max-cost estimate) picks
    /// it up automatically. See <see cref="TenantQuotaService.EstimateMaxMonthlyCostUsd"/>
    /// for how each key's worst-case $ exposure is computed from the SAME
    /// AgentPricing config rates TokenCostEstimator/TenantBuddyUsageService use.
    /// UnitEn/UnitEs each bake in their OWN correct period/scope (month, per
    /// course, per Engram, etc.) — never assume every quota is monthly.</summary>
    public static readonly IReadOnlyList<QuotaDefinition> All =
    [
        new(
            BuddyVoiceMinutesPerMonth,
            "Buddy",
            "Buddy — voice minutes / employee / month",
            "Buddy — minutos de voz / empleado / mes",
            "Caps each employee's Realtime voice-companion usage per calendar month.",
            "Limita el uso de voz de Buddy por empleado en el mes calendario.",
            "minutes / month",
            "minutos / mes",
            15m,
            true),
        new(
            BuddyTextInputTokensPerMonth,
            "Buddy",
            "Buddy — text input tokens / employee / month",
            "Buddy — tokens de entrada de texto / empleado / mes",
            "Caps each employee's Buddy TEXT chat input tokens (prompt + tool schemas + conversation history) per calendar month.",
            "Limita los tokens de ENTRADA del chat de texto de Buddy por empleado en el mes calendario (prompt + esquemas de tools + historial).",
            "tokens / month",
            "tokens / mes",
            300000m,
            false),
        new(
            BuddyTextOutputTokensPerMonth,
            "Buddy",
            "Buddy — text output tokens / employee / month",
            "Buddy — tokens de salida de texto / empleado / mes",
            "Caps each employee's Buddy TEXT chat output (model response) tokens per calendar month.",
            "Limita los tokens de SALIDA (respuesta del modelo) del chat de texto de Buddy por empleado en el mes calendario.",
            "tokens / month",
            "tokens / mes",
            120000m,
            false),
        new(
            MaxCoursesPerMonth,
            "Courses",
            "Max courses generated / month",
            "Máximo de cursos generados / mes",
            "Caps how many new courses (Studio pipeline runs) a tenant can generate per calendar month.",
            "Limita cuántos cursos nuevos (ejecuciones del pipeline de Studio) puede generar un tenant por mes calendario.",
            "courses / month",
            "cursos / mes",
            20m,
            false),
        new(
            MaxEngramsPerCourse,
            "Courses",
            "Max Engrams per course",
            "Máximo de Engrams por curso",
            "Caps how many Engrams (sections/nodes) a single course can be split into during Studio generation — the main driver of a course's real generation cost.",
            "Limita cuántos Engrams (secciones/nodos) puede tener un solo curso al generarse en Studio — el principal factor del costo real de generación.",
            "Engrams / course",
            "Engrams / curso",
            40m,
            false),
        new(
            MaxDeepDiveCardsPerEngram,
            "Courses",
            "Max Deep Dive example cards per Engram",
            "Máximo de tarjetas de Deep Dive por Engram",
            "Caps how many optional 'Deep Dive' example cards Studio generates per Engram. Cost is already folded into each Engram's own generation cost, not billed separately — videos are deliberately NOT included in any quota yet (far more expensive; will get their own dedicated quota later).",
            "Limita cuántas tarjetas opcionales de 'Deep Dive' genera Studio por Engram. Su costo ya está incluido en el costo de generación del propio Engram, no se factura aparte — los videos deliberadamente NO están en ninguna cuota todavía (son mucho más caros; tendrán su propia cuota dedicada después).",
            "cards / Engram",
            "tarjetas / Engram",
            6m,
            false),
        new(
            MaxCoursesPerPersonPerMonth,
            "Courses",
            "Max courses taken per employee / month",
            "Máximo de cursos tomados por empleado / mes",
            "Caps how many courses one employee can be actively taking per calendar month. Cost estimate is the REAL worst-case runtime delivery cost of ONE course (every pedagogy stage — Before you start, Learn, Deep dive, In your role, Recall, Assessment — maxed out using Voice Professor, from actual recorded student usage) × this limit.",
            "Limita cuántos cursos puede estar tomando activamente un empleado por mes calendario. El estimado de costo es el peor caso REAL de entregar en runtime UN curso (cada etapa pedagógica — Antes de empezar, Aprender, Deep dive, En tu rol, Recall, Assessment — al máximo usando el Profesor de Voz, calculado del uso real de estudiantes) × este límite.",
            "courses / employee / month",
            "cursos / empleado / mes",
            3m,
            false),
        new(
            MaxEngramReviewsPerPersonPerMonth,
            "Courses",
            "Max Engram reviews ('Reactivar Engrama') per employee / month",
            "Máximo de repasos de Engram ('Reactivar Engrama') por empleado / mes",
            "Caps how many times an employee can use Brain's full-lesson voice/text review ('Reactivar Engrama') per calendar month. This feature walks through the ENTIRE Engram again (every pedagogy stage), so cost is estimated using the SAME worst-case-per-stage figure as 'Max courses per employee' — a PROXY, not yet a real measurement, since this specific feature doesn't record call duration/tokens today (unlike Voice Professor/Buddy).",
            "Limita cuántas veces un empleado puede usar el repaso completo de voz/texto de Brain ('Reactivar Engrama') por mes calendario. Esta función recorre TODO el Engram de nuevo (cada etapa pedagógica), así que el costo se estima con la MISMA cifra de peor-caso-por-etapa que 'Max cursos por empleado' — es un PROXY, no una medición real todavía, ya que esta función específica no registra duración de llamada/tokens hoy (a diferencia de Voice Professor/Buddy).",
            "reviews / employee / month",
            "repasos / empleado / mes",
            20m,
            false),
        new(
            MaxVoiceInteractionsPerMonth,
            "Voice",
            "Max total voice interactions / month",
            "Máximo total de interacciones de voz / mes",
            "Caps the tenant-wide total number of voice sessions (Buddy + Voice Professor + tecBot combined) per calendar month, regardless of per-employee minutes.",
            "Limita el total de sesiones de voz de todo el tenant (Buddy + Profesor de Voz + tecBot combinados) por mes calendario, sin importar los minutos por empleado.",
            "sessions / month",
            "sesiones / mes",
            2000m,
            false),
    ];

    public static QuotaDefinition? Find(string key) => All.FirstOrDefault(q => q.Key == key);
}
