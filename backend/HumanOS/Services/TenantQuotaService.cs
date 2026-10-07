using HumanOS.Data;
using HumanOS.Models.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.Services;

public sealed class QuotaRow
{
    public string Key { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string LabelEn { get; set; } = string.Empty;
    public string LabelEs { get; set; } = string.Empty;
    public string DescriptionEn { get; set; } = string.Empty;
    public string DescriptionEs { get; set; } = string.Empty;
    public string UnitEn { get; set; } = string.Empty;
    public string UnitEs { get; set; } = string.Empty;
    public decimal PlatformDefault { get; set; }
    public decimal EffectiveLimit { get; set; }
    public bool IsOverridden { get; set; }
    public bool IsEnforced { get; set; }

    /// <summary>Worst-case USD/employee/month if this quota's EffectiveLimit
    /// is fully consumed every month — null when this key has no confident
    /// per-unit $ rate to compute from (e.g. "max courses/month", which
    /// doesn't map to a flat per-unit cost). See
    /// <see cref="TenantQuotaService.EstimateMaxMonthlyCostUsd"/>.</summary>
    public decimal? EstimatedMaxMonthlyCostUsd { get; set; }

    /// <summary>Short note on WHERE the per-unit $ rate came from — a fixed
    /// config rate (voice/text) vs. this tenant's own REAL average cost per
    /// course/Engram observed from actual Studio generation runs (see
    /// <see cref="TenantQuotaService.GetTenantQuotasAsync"/>). Null when no
    /// cost estimate applies.</summary>
    public string? CostBasisEn { get; set; }
    public string? CostBasisEs { get; set; }
}

/// <summary>
/// Reads/writes per-tenant quota overrides (<see cref="TenantServiceQuota"/>)
/// against the platform-wide <see cref="QuotaCatalog"/> (2026-10-04) — the
/// single place both the admin "Quotas" page and any enforcement check
/// (e.g. BuddyVoiceSessionFunction) go through to resolve "what's this
/// tenant's actual limit for X right now".
/// </summary>
public sealed class TenantQuotaService
{
    private readonly HumanOsDbContext _dbContext;
    private readonly Microsoft.Extensions.Configuration.IConfiguration _configuration;
    private readonly TenantCourseTotalsService _courseTotalsService;

    public TenantQuotaService(
        HumanOsDbContext dbContext,
        Microsoft.Extensions.Configuration.IConfiguration configuration,
        TenantCourseTotalsService courseTotalsService)
    {
        _dbContext = dbContext;
        _configuration = configuration;
        _courseTotalsService = courseTotalsService;
    }

    /// <summary>Every known quota for this tenant, merged with any configured
    /// override — what the admin "Quotas" page renders.</summary>
    public async Task<List<QuotaRow>> GetTenantQuotasAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var overrides = await _dbContext.TenantServiceQuotas
            .AsNoTracking()
            .Where(q => q.TenantId == tenantId)
            .ToDictionaryAsync(q => q.QuotaKey, q => q.LimitValue, cancellationToken);

        // Real average cost per course/Engram for THIS tenant (2026-10-04) —
        // reuses the exact same totals already shown on the "Cursos" tab
        // (TenantCourseTotalsService), so "Max courses/month"'s $ estimate is
        // grounded in actual recorded Studio generation spend, never a guess.
        // Null average (tenant has 0 courses yet) means no cost can be
        // estimated — shown as "not enough data yet" rather than a fabricated
        // number.
        decimal? avgCostPerCourse = null;
        decimal? avgCostPerEngram = null;
        int sampledCourseCount = 0;
        try
        {
            var totals = await _courseTotalsService.GetTotalsAsync(tenantId, cancellationToken);
            sampledCourseCount = totals.CourseCount;
            if (totals.CourseCount > 0)
            {
                avgCostPerCourse = Math.Round(totals.EstimatedCostUsd / totals.CourseCount, 4);
            }
            if (totals.EngramsCount > 0)
            {
                avgCostPerEngram = Math.Round(totals.EstimatedCostUsd / totals.EngramsCount, 6);
            }
        }
        catch
        {
            // Best-effort — the Quotas page must still render even if course
            // totals aggregation fails for some reason.
        }

        // Platform-wide fallback (2026-10-04) — when THIS tenant has
        // generated zero courses of its own (e.g. it only has access to
        // global catalog courses someone else built), fall back to the real
        // average cost per course/Engram across EVERY tenant on the platform,
        // instead of showing "not enough data". Clearly flagged via
        // usedPlatformWideAverage so the UI can label it as a platform
        // estimate rather than this tenant's own number.
        var usedPlatformWideAverage = false;
        if (avgCostPerCourse is null)
        {
            try
            {
                var platformAvg = await GetPlatformWideAverageCourseCostAsync(cancellationToken);
                if (platformAvg.AvgCostPerCourse is not null)
                {
                    avgCostPerCourse = platformAvg.AvgCostPerCourse;
                    avgCostPerEngram = platformAvg.AvgCostPerEngram;
                    sampledCourseCount = platformAvg.CourseCount;
                    usedPlatformWideAverage = true;
                }
            }
            catch
            {
                // Best-effort — fall through to "not enough data" below.
            }
        }

        // Real average cost per Deep Dive card/image (2026-10-04) — ALWAYS
        // platform-wide (every tenant generates cards through the same
        // image model/pipeline, so this rate is meaningful regardless of
        // whether THIS tenant has its own courses yet). Images only — video
        // illustrations are deliberately excluded, same "no video quota yet"
        // decision as the rest of this feature.
        decimal? avgCostPerCard = null;
        int sampledImageCount = 0;
        try
        {
            (avgCostPerCard, sampledImageCount) = await GetPlatformWideAverageImageCostAsync(cancellationToken);
        }
        catch
        {
            // Best-effort.
        }

        // Worst-case REAL RUNTIME delivery cost for one course (2026-10-04) —
        // distinct from everything above (which prices Studio AUTHORING, not
        // a student actually TAKING the course). Sums, per pedagogy stage
        // (Before you start/Learn/Deep dive/In your role/Recall/Assessment),
        // the MAXIMUM real cost ever observed for that stage across every
        // Engram on the platform (Voice Professor minutes + text tokens +
        // text-to-speech characters, from StructuredLearningEngramRuntimeUsage
        // — real student usage, not a Studio generation estimate), then
        // multiplies by this tenant's "Max Engrams per course" limit to get a
        // worst-case per-course runtime cost.
        decimal? worstCaseRuntimeCostPerCourse = null;
        decimal? textOnlyRuntimeCostPerCourse = null;
        decimal? worstCaseRuntimeCostPerEngramRaw = null;
        int sampledRuntimeRowCount = 0;
        string? runtimeStageBreakdown = null;
        try
        {
            var (worstCasePerEngram, textOnlyPerEngram, rowCount, maxCostByStage) = await GetPlatformWideMaxRuntimeCostPerEngramAsync(cancellationToken);
            if (worstCasePerEngram is not null)
            {
                var engramsPerCourseLimit = overrides.TryGetValue(QuotaCatalog.MaxEngramsPerCourse, out var engramsOverride)
                    ? engramsOverride
                    : QuotaCatalog.Find(QuotaCatalog.MaxEngramsPerCourse)!.PlatformDefault;
                worstCaseRuntimeCostPerCourse = Math.Round(worstCasePerEngram.Value * engramsPerCourseLimit, 2);
                textOnlyRuntimeCostPerCourse = textOnlyPerEngram is not null ? Math.Round(textOnlyPerEngram.Value * engramsPerCourseLimit, 2) : null;
                worstCaseRuntimeCostPerEngramRaw = worstCasePerEngram;
                sampledRuntimeRowCount = rowCount;
                runtimeStageBreakdown = string.Join(", ", maxCostByStage
                    .OrderByDescending(kv => kv.Value)
                    .Select(kv => $"{kv.Key}=${kv.Value}"));
            }
        }
        catch
        {
            // Best-effort.
        }

        return QuotaCatalog.All.Select(def =>
        {
            var effectiveLimit = overrides.TryGetValue(def.Key, out var overrideValue) ? overrideValue : def.PlatformDefault;
            var (cost, basisEn, basisEs) = EstimateMaxMonthlyCostUsd(
                def.Key, effectiveLimit, avgCostPerCourse, avgCostPerEngram, sampledCourseCount, usedPlatformWideAverage,
                avgCostPerCard, sampledImageCount, worstCaseRuntimeCostPerCourse, sampledRuntimeRowCount, runtimeStageBreakdown,
                textOnlyRuntimeCostPerCourse, worstCaseRuntimeCostPerEngramRaw);
            return new QuotaRow
            {
                Key = def.Key,
                Category = def.Category,
                LabelEn = def.LabelEn,
                LabelEs = def.LabelEs,
                DescriptionEn = def.DescriptionEn,
                DescriptionEs = def.DescriptionEs,
                UnitEn = def.UnitEn,
                UnitEs = def.UnitEs,
                PlatformDefault = def.PlatformDefault,
                EffectiveLimit = effectiveLimit,
                IsOverridden = overrides.ContainsKey(def.Key),
                IsEnforced = def.IsEnforced,
                EstimatedMaxMonthlyCostUsd = cost,
                CostBasisEn = basisEn,
                CostBasisEs = basisEs,
            };
        }).ToList();
    }

    /// <summary>Worst-case USD/employee/month if a quota's limit is fully
    /// consumed — computed from the SAME live "AgentPricing" config rates
    /// <see cref="TokenCostEstimator"/> (text) and
    /// <see cref="TenantBuddyUsageService"/> (voice) already use, so this
    /// number never drifts out of sync with what the platform is actually
    /// billed. "Max courses/month" uses this TENANT'S OWN real average
    /// cost-per-course when it has one, or the PLATFORM-WIDE real average
    /// (every tenant's actual generation spend) when it doesn't yet — never
    /// a config-rate guess, since a course's real cost varies far too much by
    /// length/pipeline options for one flat constant to mean anything.
    /// Returns (null, null, null) when truly no data exists anywhere yet.</summary>
    public (decimal? Cost, string? BasisEn, string? BasisEs) EstimateMaxMonthlyCostUsd(
        string quotaKey, decimal effectiveLimit, decimal? avgCostPerCourse, decimal? avgCostPerEngram, int sampledCourseCount,
        bool usedPlatformWideAverage = false, decimal? avgCostPerCard = null, int sampledImageCount = 0,
        decimal? worstCaseRuntimeCostPerCourse = null, int sampledRuntimeRowCount = 0, string? runtimeStageBreakdown = null,
        decimal? textOnlyRuntimeCostPerCourse = null, decimal? worstCaseRuntimeCostPerEngram = null)
    {
        var scopeEn = usedPlatformWideAverage ? "Platform-wide real average (this tenant has no courses of its own yet)" : "This tenant's own real average";
        var scopeEs = usedPlatformWideAverage ? "Promedio real de toda la plataforma (este tenant aún no tiene cursos propios)" : "Promedio real de este tenant";

        switch (quotaKey)
        {
            case QuotaCatalog.BuddyVoiceMinutesPerMonth:
            {
                var perMinuteUsd = ReadDecimal("AgentPricing:RealtimePerMinuteUsd", 0.045m);
                return (Math.Round(effectiveLimit * perMinuteUsd, 4), $"${perMinuteUsd}/minute (configured rate)", $"${perMinuteUsd}/minuto (tarifa configurada)");
            }
            case QuotaCatalog.BuddyTextInputTokensPerMonth:
            {
                var perMillionUsd = ReadDecimal("AgentPricing:Models:gpt-5-mini:InputPerMillionTokens", 0.25m);
                return (Math.Round(effectiveLimit / 1_000_000m * perMillionUsd, 4), $"${perMillionUsd}/1M input tokens (gpt-5-mini rate)", $"${perMillionUsd}/1M tokens entrada (tarifa gpt-5-mini)");
            }
            case QuotaCatalog.BuddyTextOutputTokensPerMonth:
            {
                var perMillionUsd = ReadDecimal("AgentPricing:Models:gpt-5-mini:OutputPerMillionTokens", 2.00m);
                return (Math.Round(effectiveLimit / 1_000_000m * perMillionUsd, 4), $"${perMillionUsd}/1M output tokens (gpt-5-mini rate)", $"${perMillionUsd}/1M tokens salida (tarifa gpt-5-mini)");
            }
            case QuotaCatalog.MaxCoursesPerMonth:
            {
                if (avgCostPerCourse is null)
                {
                    return (null,
                        "Not enough data yet — no tenant on the platform has generated a course.",
                        "Aún no hay datos suficientes — ningún tenant de la plataforma ha generado un curso todavía.");
                }
                return (Math.Round(effectiveLimit * avgCostPerCourse.Value, 2),
                    $"{scopeEn}: ${avgCostPerCourse.Value}/course (from {sampledCourseCount} course(s))" +
                        (avgCostPerEngram is not null ? $", ${avgCostPerEngram.Value}/Engram" : ""),
                    $"{scopeEs}: ${avgCostPerCourse.Value}/curso (de {sampledCourseCount} curso(s))" +
                        (avgCostPerEngram is not null ? $", ${avgCostPerEngram.Value}/Engram" : ""));
            }
            case QuotaCatalog.MaxEngramsPerCourse:
            {
                if (avgCostPerEngram is null)
                {
                    return (null,
                        "Not enough data yet — no tenant on the platform has generated an Engram.",
                        "Aún no hay datos suficientes — ningún tenant de la plataforma ha generado un Engram todavía.");
                }
                // Worst-case for ONE course at the Engram ceiling — not
                // multiplied by MaxCoursesPerMonth, since that's a separate,
                // independent quota (a course could be generated well under
                // this Engram cap, or this cap could apply to every course
                // in the month — shown as "per course", not "per month").
                return (Math.Round(effectiveLimit * avgCostPerEngram.Value, 2),
                    $"{scopeEn}: ${avgCostPerEngram.Value}/Engram (from {sampledCourseCount} course(s))",
                    $"{scopeEs}: ${avgCostPerEngram.Value}/Engram (de {sampledCourseCount} curso(s))");
            }
            case QuotaCatalog.MaxDeepDiveCardsPerEngram:
            {
                if (avgCostPerCard is null)
                {
                    return (null,
                        "Not enough data yet — no tenant on the platform has generated a Deep Dive card image.",
                        "Aún no hay datos suficientes — ningún tenant de la plataforma ha generado una imagen de tarjeta Deep Dive todavía.");
                }
                // Image generation cost ONLY (AgentPricing:PerImage rate) —
                // the card's own text/prompt tokens are already counted
                // inside avgCostPerEngram above; showing that again here
                // would double-count it. Videos are excluded from this rate
                // (MediaType="Video" rows skipped) — no video quota yet.
                return (Math.Round(effectiveLimit * avgCostPerCard.Value, 2),
                    $"Platform-wide real average: ${avgCostPerCard.Value}/image (from {sampledImageCount} generated image(s), excludes video; card text tokens are already counted in the Engram cost above)",
                    $"Promedio real de toda la plataforma: ${avgCostPerCard.Value}/imagen (de {sampledImageCount} imagen(es) generadas, excluye video; los tokens de texto de la tarjeta ya están contados en el costo del Engram de arriba)");
            }
            case QuotaCatalog.MaxCoursesPerPersonPerMonth:
            {
                if (worstCaseRuntimeCostPerCourse is null)
                {
                    return (null,
                        "Not enough data yet — no student has used a course's Learn/Deep Dive/Recall/Assessment steps yet.",
                        "Aún no hay datos suficientes — ningún estudiante ha usado los pasos de Learn/Deep Dive/Recall/Assessment de un curso todavía.");
                }
                var breakdownSuffixEn = runtimeStageBreakdown is not null ? $" [{runtimeStageBreakdown}]" : "";
                var breakdownSuffixEs = runtimeStageBreakdown is not null ? $" [{runtimeStageBreakdown}]" : "";
                var textOnlyNoteEn = textOnlyRuntimeCostPerCourse is not null
                    ? $" — text-only (no Realtime voice): ${textOnlyRuntimeCostPerCourse.Value}/course, {Math.Round(effectiveLimit * textOnlyRuntimeCostPerCourse.Value, 2)} at this limit (same {effectiveLimit} courses cost LESS if voice is never used)"
                    : "";
                var textOnlyNoteEs = textOnlyRuntimeCostPerCourse is not null
                    ? $" — solo texto (sin voz Realtime): ${textOnlyRuntimeCostPerCourse.Value}/curso, {Math.Round(effectiveLimit * textOnlyRuntimeCostPerCourse.Value, 2)} en este límite (los mismos {effectiveLimit} cursos cuestan MENOS si nunca se usa voz)"
                    : "";
                return (Math.Round(effectiveLimit * worstCaseRuntimeCostPerCourse.Value, 2),
                    $"With Realtime voice (worst case, {sampledRuntimeRowCount} real usage row(s)): ${worstCaseRuntimeCostPerCourse.Value}/course{breakdownSuffixEn}{textOnlyNoteEn}",
                    $"Con voz Realtime (peor caso, {sampledRuntimeRowCount} fila(s) real(es)): ${worstCaseRuntimeCostPerCourse.Value}/curso{breakdownSuffixEs}{textOnlyNoteEs}");
            }
            case QuotaCatalog.MaxEngramReviewsPerPersonPerMonth:
            {
                if (worstCaseRuntimeCostPerEngram is null)
                {
                    return (null,
                        "Not enough data yet — no student has used a course's Learn/Deep Dive/Recall/Assessment steps yet.",
                        "Aún no hay datos suficientes — ningún estudiante ha usado los pasos de Learn/Deep Dive/Recall/Assessment de un curso todavía.");
                }
                // PROXY, not a direct measurement — "Reactivar Engrama" doesn't
                // record its own duration/tokens today (see EngramReviewCompletion's
                // doc comment: just a completion marker). Reuses the SAME
                // worst-case-per-stage figure as "Max courses/employee" since
                // this feature re-walks the SAME pedagogy stages for one Engram.
                return (Math.Round(effectiveLimit * worstCaseRuntimeCostPerEngram.Value, 2),
                    $"PROXY (not yet directly measured — 'Reactivar Engrama' doesn't record call duration today): reuses the worst-case-per-stage Engram cost from {sampledRuntimeRowCount} real usage row(s): ${worstCaseRuntimeCostPerEngram.Value}/review",
                    $"PROXY (aún no medido directamente — 'Reactivar Engrama' no registra duración de llamada hoy): reutiliza el costo de peor-caso-por-etapa del Engram, de {sampledRuntimeRowCount} fila(s) real(es): ${worstCaseRuntimeCostPerEngram.Value}/repaso");
            }
            default:
                return (null, null, null);
        }
    }

    /// <summary>Real average cost per course/Engram across EVERY tenant on
    /// the platform (2026-10-04) — the fallback basis for "Max courses" and
    /// "Max Engrams per course" when the CURRENT tenant hasn't generated any
    /// courses of its own yet. Sums actual recorded generation usage
    /// (StructuredLearningGenerationUsage + CapabilityGenerationUsage) across
    /// ALL tenants, converts to USD via the same per-model AgentPricing rates
    /// TokenCostEstimator uses, then divides by the total course/Engram
    /// counts — same math as TenantCourseTotalsService, just unscoped.</summary>
    private async Task<(decimal? AvgCostPerCourse, decimal? AvgCostPerEngram, int CourseCount)> GetPlatformWideAverageCourseCostAsync(
        CancellationToken cancellationToken)
    {
        var structuredUsage = await _dbContext.StructuredLearningGenerationUsages
            .AsNoTracking()
            .GroupBy(u => u.ModelName)
            .Select(g => new { ModelName = g.Key, InputTokens = g.Sum(x => (long)x.InputTokens), OutputTokens = g.Sum(x => (long)x.OutputTokens), CachedInputTokens = g.Sum(x => (long)x.CachedInputTokens) })
            .ToListAsync(cancellationToken);

        var capabilityUsage = await _dbContext.CapabilityGenerationUsages
            .AsNoTracking()
            .GroupBy(u => u.ModelName)
            .Select(g => new { ModelName = g.Key, InputTokens = g.Sum(x => (long)x.InputTokens), OutputTokens = g.Sum(x => (long)x.OutputTokens), CachedInputTokens = g.Sum(x => (long)x.CachedInputTokens) })
            .ToListAsync(cancellationToken);

        decimal totalCost = 0m;
        foreach (var group in structuredUsage.Concat(capabilityUsage))
        {
            totalCost += ComputeCostForModel(group.ModelName, group.InputTokens, group.OutputTokens, group.CachedInputTokens);
        }

        // Image generation cost (2026-10-04) — Deep Dive cards (Structured
        // Learning) and node illustrations (Capability Graph) generate REAL
        // images, billed per-image via "AgentPricing:PerImage", completely
        // separate from the token-based cost above. Without this, a course's
        // real average cost was understated by however many images it
        // generated. Video rows are excluded (MediaType="Video") — no video
        // quota/cost yet, per explicit product decision.
        var perImageRate = ReadDecimal("AgentPricing:PerImage", 0.04m);
        var structuredImageCount = await _dbContext.StructuredLearningIllustrations.AsNoTracking()
            .CountAsync(i => i.MediaType != "Video", cancellationToken);
        var capabilityImageCount = await _dbContext.CapabilityGraphNodeIllustrations.AsNoTracking().CountAsync(cancellationToken);
        totalCost += (structuredImageCount + capabilityImageCount) * perImageRate;

        if (totalCost <= 0m) return (null, null, 0);

        var structuredCourseCount = await _dbContext.StructuredLearningGenerationUsages.AsNoTracking()
            .Select(u => u.StructuredLearningDocumentId).Distinct().CountAsync(cancellationToken);
        var capabilityCourseCount = await _dbContext.CapabilityGenerationUsages.AsNoTracking()
            .Select(u => u.CapabilityId).Distinct().CountAsync(cancellationToken);
        var totalCourseCount = structuredCourseCount + capabilityCourseCount;

        var structuredEngramCount = await _dbContext.StructuredLearningDocumentSections.AsNoTracking().CountAsync(cancellationToken);
        var capabilityEngramCount = await _dbContext.CapabilityGraphNodes.AsNoTracking().CountAsync(cancellationToken);
        var totalEngramCount = structuredEngramCount + capabilityEngramCount;

        decimal? avgCostPerCourse = totalCourseCount > 0 ? Math.Round(totalCost / totalCourseCount, 4) : null;
        decimal? avgCostPerEngram = totalEngramCount > 0 ? Math.Round(totalCost / totalEngramCount, 6) : null;
        return (avgCostPerCourse, avgCostPerEngram, totalCourseCount);
    }

    /// <summary>Real average cost per generated Deep Dive card IMAGE across
    /// EVERY tenant on the platform (2026-10-04) — image generation only
    /// (AgentPricing:PerImage rate), excluding video. Always platform-wide
    /// (never tenant-scoped) since every tenant generates cards through the
    /// exact same image model/pipeline, so one global rate is meaningful
    /// regardless of whether the CURRENT tenant has generated any cards yet.</summary>
    private async Task<(decimal? AvgCostPerImage, int ImageCount)> GetPlatformWideAverageImageCostAsync(CancellationToken cancellationToken)
    {
        var perImageRate = ReadDecimal("AgentPricing:PerImage", 0.04m);
        var structuredImageCount = await _dbContext.StructuredLearningIllustrations.AsNoTracking()
            .CountAsync(i => i.MediaType != "Video", cancellationToken);
        var capabilityImageCount = await _dbContext.CapabilityGraphNodeIllustrations.AsNoTracking().CountAsync(cancellationToken);
        var totalImageCount = structuredImageCount + capabilityImageCount;

        return totalImageCount > 0 ? (perImageRate, totalImageCount) : (null, 0);
    }

    /// <summary>Worst-case REAL runtime cost for ONE Engram, summed across
    /// its 6 pedagogy stages (2026-10-04) — each stage priced at the MAXIMUM
    /// cost ever actually incurred by a real student at that stage, across
    /// every Engram on the platform. Source: <see cref="HumanOS.Models.Capabilities.Studio.StructuredLearningEngramRuntimeUsage"/>
    /// (a REAL per-turn usage ledger — Voice Professor minutes, text Q&amp;A
    /// tokens, text-to-speech characters — distinct from Studio's own
    /// AUTHORING cost used by <see cref="GetPlatformWideAverageCourseCostAsync"/>).
    /// "Worst case per stage, summed" deliberately overshoots any single real
    /// Engram's actual cost (no one Engram hits every stage's own maximum
    /// simultaneously) — intentional for quota/margin planning, which should
    /// be conservative.</summary>
    /// <summary>Worst-case REAL runtime cost for ONE Engram, summed across
    /// its 6 pedagogy stages (2026-10-04) — each stage priced at the MAXIMUM
    /// cost ever actually incurred by a real student at that stage, across
    /// every Engram on the platform. Source: <see cref="HumanOS.Models.Capabilities.Studio.StructuredLearningEngramRuntimeUsage"/>
    /// (a REAL per-turn usage ledger — Voice Professor minutes, text Q&amp;A
    /// tokens, text-to-speech characters — distinct from Studio's own
    /// AUTHORING cost used by <see cref="GetPlatformWideAverageCourseCostAsync"/>).
    /// "Worst case per stage, summed" deliberately overshoots any single real
    /// Engram's actual cost (no one Engram hits every stage's own maximum
    /// simultaneously) — intentional for quota/margin planning, which should
    /// be conservative. ALSO returns the same calculation with Voice rows
    /// excluded entirely (2026-10-04, explicit product question: "if the
    /// employee never uses Realtime voice, doesn't each course cost less and
    /// let them take more courses on the same budget?" — yes, and this
    /// text-only figure proves it with real numbers instead of leaving it as
    /// an unverified assumption).</summary>
    private async Task<(decimal? WorstCaseWithVoicePerEngram, decimal? TextOnlyPerEngram, int RowCount, Dictionary<string, decimal> MaxCostByStage)> GetPlatformWideMaxRuntimeCostPerEngramAsync(CancellationToken cancellationToken)
    {
        var rows = await _dbContext.StructuredLearningEngramRuntimeUsages
            .AsNoTracking()
            .Where(u => u.StructuredLearningDocumentSectionId != null && u.StepType != null)
            .Select(u => new
            {
                SectionId = u.StructuredLearningDocumentSectionId!.Value,
                StepType = u.StepType!,
                u.Channel,
                u.ModelName,
                u.DurationSeconds,
                u.InputTokens,
                u.OutputTokens,
                u.CharacterCount,
            })
            .ToListAsync(cancellationToken);

        if (rows.Count == 0) return (null, null, 0, new Dictionary<string, decimal>());

        var realtimePerMinuteUsd = ReadDecimal("AgentPricing:RealtimePerMinuteUsd", 0.045m);
        var speechPerMillionCharsUsd = ReadDecimal("AgentPricing:AzureSpeechNeuralPerMillionCharactersUsd", 16m);

        decimal RowCost(string channel, string? modelName, int durationSeconds, int inputTokens, int outputTokens, int characterCount) => channel switch
        {
            "Voice" => durationSeconds / 60m * realtimePerMinuteUsd,
            "TextToSpeech" => characterCount / 1_000_000m * speechPerMillionCharsUsd,
            _ => ComputeCostForModel(modelName, inputTokens, outputTokens, 0),
        };

        // Sum real cost per (Engram, Stage) first, THEN take the MAX across
        // Engrams for each stage — never average across stages, since the
        // whole point is a conservative worst-case quota basis.
        var costPerEngramStage = rows
            .GroupBy(r => (r.SectionId, r.StepType))
            .Select(g => new
            {
                g.Key.StepType,
                Cost = g.Sum(r => RowCost(r.Channel, r.ModelName, r.DurationSeconds, r.InputTokens, r.OutputTokens, r.CharacterCount)),
            });

        var maxCostByStage = costPerEngramStage
            .GroupBy(x => x.StepType)
            .ToDictionary(g => g.Key, g => Math.Round(g.Max(x => x.Cost), 6));

        var worstCasePerEngram = Math.Round(maxCostByStage.Values.Sum(), 4);

        // Same calculation, Voice rows excluded entirely (an employee who
        // never opens the Realtime voice Professor and only uses the
        // course's text chat / TextToSpeech read-aloud).
        var textOnlyRows = rows.Where(r => r.Channel != "Voice").ToList();
        decimal? textOnlyPerEngram = null;
        if (textOnlyRows.Count > 0)
        {
            var textOnlyMaxByStage = textOnlyRows
                .GroupBy(r => (r.SectionId, r.StepType))
                .Select(g => new { g.Key.StepType, Cost = g.Sum(r => RowCost(r.Channel, r.ModelName, r.DurationSeconds, r.InputTokens, r.OutputTokens, r.CharacterCount)) })
                .GroupBy(x => x.StepType)
                .ToDictionary(g => g.Key, g => g.Max(x => x.Cost));
            textOnlyPerEngram = Math.Round(textOnlyMaxByStage.Values.Sum(), 4);
        }

        return (worstCasePerEngram, textOnlyPerEngram, rows.Count, maxCostByStage);
    }

    /// <summary>Converts raw token counts for ONE model into USD using the
    /// same "AgentPricing:Models:{model}" / "AgentPricing:Default*" config
    /// rates <see cref="TokenCostEstimator"/> reads — duplicated here (rather
    /// than calling TokenCostEstimator directly) because that type works off
    /// an <c>AgentTokenUsage</c> list shape built for the PDF pipeline, not a
    /// raw grouped-by-model tuple.</summary>
    private decimal ComputeCostForModel(string? modelName, long inputTokens, long outputTokens, long cachedInputTokens)
    {
        // Azure Speech TTS rows (2026-10-04, sentinel ModelName written by
        // StructuredLearningPedagogyService's Professor Lecture audio
        // synthesis) — InputTokens holds a CHARACTER count here, billed per
        // million characters, never the per-token LLM rate below. See
        // TokenCostEstimator.AzureSpeechTtsModelSentinel.
        if (modelName == TokenCostEstimator.AzureSpeechTtsModelSentinel)
        {
            return inputTokens / 1_000_000m * ReadDecimal("AgentPricing:AzureSpeechNeuralPerMillionCharactersUsd", 16m);
        }

        var modelKey = string.IsNullOrWhiteSpace(modelName) ? null : $"AgentPricing:Models:{modelName}";
        var inputRate = modelKey is null ? ReadDecimal("AgentPricing:DefaultInputPerMillionTokens", 1.25m) : ReadDecimal($"{modelKey}:InputPerMillionTokens", ReadDecimal("AgentPricing:DefaultInputPerMillionTokens", 1.25m));
        var cachedRate = modelKey is null ? ReadDecimal("AgentPricing:DefaultCachedInputPerMillionTokens", 0.625m) : ReadDecimal($"{modelKey}:CachedInputPerMillionTokens", ReadDecimal("AgentPricing:DefaultCachedInputPerMillionTokens", 0.625m));
        var outputRate = modelKey is null ? ReadDecimal("AgentPricing:DefaultOutputPerMillionTokens", 10.00m) : ReadDecimal($"{modelKey}:OutputPerMillionTokens", ReadDecimal("AgentPricing:DefaultOutputPerMillionTokens", 10.00m));

        var billableInput = Math.Max(0, inputTokens - cachedInputTokens);
        return billableInput / 1_000_000m * inputRate
            + cachedInputTokens / 1_000_000m * cachedRate
            + outputTokens / 1_000_000m * outputRate;
    }

    private decimal ReadDecimal(string key, decimal fallback)
    {
        var raw = _configuration[key];
        return decimal.TryParse(raw, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var value)
            ? value
            : fallback;
    }

    /// <summary>Real, already-configured per-unit $ rates to price a "Buddy
    /// at max quota" business scenario (2026-10-04, Simulations wizard Step
    /// 3) — the EXACT same rates/keys <see cref="EstimateMaxMonthlyCostUsd"/>
    /// already uses for BuddyVoiceMinutesPerMonth/BuddyTextInputTokensPerMonth/
    /// BuddyTextOutputTokensPerMonth (see the admin "Quotas" tab's Buddy
    /// category), just returned standalone (no DB query — these are flat
    /// config-driven rates, not usage averages) so the wizard can multiply
    /// them by admin-entered employee/quota-limit quantities instead of
    /// re-deriving anything.</summary>
    public sealed class BuddyCostBaseline
    {
        public decimal VoicePerMinuteUsd { get; set; }
        public decimal TextInputPerMillionTokensUsd { get; set; }
        public decimal TextOutputPerMillionTokensUsd { get; set; }
        /// <summary>Platform default monthly limits (<see cref="QuotaCatalog"/>) — shown as the wizard's starting point, editable.</summary>
        public decimal DefaultVoiceMinutesPerMonth { get; set; }
        public decimal DefaultTextInputTokensPerMonth { get; set; }
        public decimal DefaultTextOutputTokensPerMonth { get; set; }
    }

    /// <summary>Synchronous — pure config reads, no DB query (see <see cref="BuddyCostBaseline"/>'s doc comment). Powers GET /simulations/buddy-cost-baseline.</summary>
    public BuddyCostBaseline GetBuddyCostBaseline()
    {
        return new BuddyCostBaseline
        {
            VoicePerMinuteUsd = ReadDecimal("AgentPricing:RealtimePerMinuteUsd", 0.045m),
            TextInputPerMillionTokensUsd = ReadDecimal("AgentPricing:Models:gpt-5-mini:InputPerMillionTokens", 0.25m),
            TextOutputPerMillionTokensUsd = ReadDecimal("AgentPricing:Models:gpt-5-mini:OutputPerMillionTokens", 2.00m),
            DefaultVoiceMinutesPerMonth = QuotaCatalog.Find(QuotaCatalog.BuddyVoiceMinutesPerMonth)!.PlatformDefault,
            DefaultTextInputTokensPerMonth = QuotaCatalog.Find(QuotaCatalog.BuddyTextInputTokensPerMonth)!.PlatformDefault,
            DefaultTextOutputTokensPerMonth = QuotaCatalog.Find(QuotaCatalog.BuddyTextOutputTokensPerMonth)!.PlatformDefault,
        };
    }

    /// <summary>Real, already-computed platform-wide cost for the "AI
    /// Agents" / "Grow With Us" per-course chat agent (2026-10-04,
    /// Simulations wizard Step 5) — a STUDENT asking free-form text/voice
    /// questions anytime during the day, distinct from the structured
    /// pedagogy flow (Step 2) and from Buddy (Step 3). Every number here is
    /// a straight read of <see cref="Models.Capabilities.Studio.StructuredLearningEngramRuntimeUsage"/>
    /// rows with StepType="AiAgentChat" (the tracking just added 2026-10-04 —
    /// see StructuredLearningChatService/PostAiAgentVoiceUsageFunction) —
    /// zero new calculation. Null averages mean no real usage has been
    /// recorded yet (shown as "not enough data" rather than a fabricated
    /// number) — this is a BRAND NEW tracked feature, so early on these will
    /// likely be null until real students chat with it.</summary>
    public sealed class AiAgentCostBaseline
    {
        /// <summary>Average real cost per text chat turn — $/message (platform-wide average across every recorded "AiAgentChat" Text row, or Buddy's real average as a proxy — see <see cref="UsedBuddyProxy"/>). Shown as a REFERENCE figure only; the scenario calculation itself uses the per-million-token rates below × admin-entered tokens/conversation (same pattern as Buddy's Step 3), since no single "tokens per conversation" assumption is itself a measured fact.</summary>
        public decimal? CostPerTextMessage { get; set; }
        /// <summary>Text input — $/1M tokens (gpt-5-mini real configured rate, same as Buddy).</summary>
        public decimal TextInputPerMillionTokensUsd { get; set; }
        /// <summary>Text output — $/1M tokens (gpt-5-mini real configured rate, same as Buddy).</summary>
        public decimal TextOutputPerMillionTokensUsd { get; set; }
        /// <summary>Realtime voice — $/minute. Same flat config rate as Buddy (see <see cref="BuddyCostBaseline.VoicePerMinuteUsd"/>), not an average (voice is billed by the second regardless of content).</summary>
        public decimal VoicePerMinuteUsd { get; set; }
        public int SampledTextMessageCount { get; set; }
        /// <summary>True when <see cref="CostPerTextMessage"/> came from Buddy's real text-chat token average (<see cref="Models.People.BuddyChatUsage"/>) instead of AI Agents' own usage — a PROXY used only while this brand-new feature has zero recorded turns of its own. Same model (gpt-5-mini), same conversational Q&amp;A shape, so it's a reasonable stand-in — never invented from thin air.</summary>
        public bool UsedBuddyProxy { get; set; }
    }

    /// <summary>Builds <see cref="AiAgentCostBaseline"/> straight from
    /// StructuredLearningEngramRuntimeUsage rows tagged StepType="AiAgentChat",
    /// reusing the SAME <see cref="ComputeCostForModel"/> rate table every
    /// other estimate in this file uses — no new formula. Falls back to
    /// Buddy's own real per-message average (<see cref="Models.People.BuddyChatUsage"/>)
    /// when AI Agents chat has zero usage of its own yet — same proxy
    /// pattern as "Reactivate Engram" reusing Step 2's rates. Powers GET
    /// /simulations/ai-agent-cost-baseline.</summary>
    public async Task<AiAgentCostBaseline> GetAiAgentCostBaselineAsync(CancellationToken cancellationToken)
    {
        var textRows = await _dbContext.StructuredLearningEngramRuntimeUsages
            .AsNoTracking()
            .Where(u => u.StepType == "AiAgentChat" && u.Channel == "Text")
            .Select(u => new { u.ModelName, u.InputTokens, u.OutputTokens })
            .ToListAsync(cancellationToken);

        decimal? costPerTextMessage = null;
        var usedBuddyProxy = false;
        var sampledCount = textRows.Count;
        if (textRows.Count > 0)
        {
            var totalCost = textRows.Sum(r => ComputeCostForModel(r.ModelName, r.InputTokens, r.OutputTokens, 0));
            costPerTextMessage = Math.Round(totalCost / textRows.Count, 6);
        }
        else
        {
            // No AI Agents chat usage recorded yet (brand-new feature) —
            // fall back to Buddy's own real per-turn average (same model,
            // same conversational Q&A shape) rather than showing "no data".
            var buddyRows = await _dbContext.BuddyChatUsages
                .AsNoTracking()
                .Select(u => new { u.InputTokens, u.OutputTokens })
                .ToListAsync(cancellationToken);
            if (buddyRows.Count > 0)
            {
                var buddyModelName = ReadConfigString("AzureOpenAIDeploymentName") ?? "gpt-5-mini";
                var totalCost = buddyRows.Sum(r => ComputeCostForModel(buddyModelName, r.InputTokens, r.OutputTokens, 0));
                costPerTextMessage = Math.Round(totalCost / buddyRows.Count, 6);
                usedBuddyProxy = true;
                sampledCount = buddyRows.Count;
            }
        }

        return new AiAgentCostBaseline
        {
            CostPerTextMessage = costPerTextMessage,
            TextInputPerMillionTokensUsd = ReadDecimal("AgentPricing:Models:gpt-5-mini:InputPerMillionTokens", 0.25m),
            TextOutputPerMillionTokensUsd = ReadDecimal("AgentPricing:Models:gpt-5-mini:OutputPerMillionTokens", 2.00m),
            VoicePerMinuteUsd = ReadDecimal("AgentPricing:RealtimePerMinuteUsd", 0.045m),
            SampledTextMessageCount = sampledCount,
            UsedBuddyProxy = usedBuddyProxy,
        };
    }

    private string? ReadConfigString(string key) => _configuration[key];


    /// <summary>Real, already-computed platform-wide unit costs to price a
    /// "DESIGN a course in HumanOS Studio" business scenario (2026-10-04,
    /// Simulations wizard Step 1) — the Studio AUTHORING pipeline (PDF
    /// section extraction, KnowledgeGraph entity/connection structuring,
    /// KnowledgeFacts generation, Pedagogy lesson writing + Learn-stage
    /// Professor Lecture narration (TTS) + Deep Dive images), completely
    /// distinct from <see cref="CourseCostBaseline"/> (a STUDENT actually
    /// taking/running the finished course). Every number here is a straight
    /// read of <see cref="Models.Capabilities.Studio.StructuredLearningGenerationUsage"/>
    /// (StepName="Extraction"/"KnowledgeGraph"/"KnowledgeFacts"/"Pedagogy",
    /// the exact same rows <see cref="StructuredLearningCostService"/>
    /// already prices on the per-course cost detail page) — zero new
    /// calculation.</summary>
    public sealed class CourseAuthoringCostBaseline
    {
        /// <summary>PDF section extraction/chunking (embedding calls) — $/Engram (platform-wide average).</summary>
        public decimal? CostPerEngramExtraction { get; set; }
        /// <summary>Knowledge graph structuring — entity + connection extraction — $/Engram (platform-wide average).</summary>
        public decimal? CostPerEngramKnowledgeGraph { get; set; }
        /// <summary>Knowledge facts generation — $/Engram (platform-wide average).</summary>
        public decimal? CostPerEngramKnowledgeFacts { get; set; }
        /// <summary>Pedagogy step — $/Engram (platform-wide average): lesson writing (Hypothesis/Teaching/DeepDive/Recall/Production/Assessment design) PLUS the Learn-stage Professor Lecture (LLM script + Azure Speech TTS narration audio). Deep Dive image generation is priced separately via <see cref="CostPerImage"/>, never double-counted here.</summary>
        public decimal? CostPerEngramPedagogy { get; set; }
        /// <summary>Deep Dive card image generation — $/image (platform-wide, excludes video). Same figure as <see cref="CourseCostBaseline.CostPerImage"/>.</summary>
        public decimal? CostPerImage { get; set; }
        public int SampledEngramCount { get; set; }
        public int SampledImageCount { get; set; }
    }

    /// <summary>Builds <see cref="CourseAuthoringCostBaseline"/> straight from
    /// <see cref="Models.Capabilities.Studio.StructuredLearningGenerationUsage"/>
    /// rows, grouped by StepName then ModelName, reusing the SAME <see cref="ComputeCostForModel"/>
    /// rate table every other estimate in this file uses — no new formula.
    /// Powers GET /simulations/course-authoring-cost-baseline.</summary>
    public async Task<CourseAuthoringCostBaseline> GetCourseAuthoringCostBaselineAsync(CancellationToken cancellationToken)
    {
        var usageByStepAndModel = await _dbContext.StructuredLearningGenerationUsages
            .AsNoTracking()
            .GroupBy(u => new { u.StepName, u.ModelName })
            .Select(g => new
            {
                g.Key.StepName,
                g.Key.ModelName,
                InputTokens = g.Sum(x => (long)x.InputTokens),
                OutputTokens = g.Sum(x => (long)x.OutputTokens),
                CachedInputTokens = g.Sum(x => (long)x.CachedInputTokens),
            })
            .ToListAsync(cancellationToken);

        var costByStep = usageByStepAndModel
            .GroupBy(x => x.StepName)
            .ToDictionary(
                g => g.Key,
                g => g.Sum(x => ComputeCostForModel(x.ModelName, x.InputTokens, x.OutputTokens, x.CachedInputTokens)));

        var engramCount = await _dbContext.StructuredLearningDocumentSections.AsNoTracking().CountAsync(cancellationToken);
        var (avgCostPerImage, sampledImageCount) = await GetPlatformWideAverageImageCostAsync(cancellationToken);

        decimal? PerEngram(string stepName) =>
            engramCount > 0 && costByStep.TryGetValue(stepName, out var cost) ? Math.Round(cost / engramCount, 6) : null;

        return new CourseAuthoringCostBaseline
        {
            CostPerEngramExtraction = PerEngram("Extraction"),
            CostPerEngramKnowledgeGraph = PerEngram("KnowledgeGraph"),
            CostPerEngramKnowledgeFacts = PerEngram("KnowledgeFacts"),
            CostPerEngramPedagogy = PerEngram("Pedagogy"),
            CostPerImage = avgCostPerImage,
            SampledEngramCount = engramCount,
            SampledImageCount = sampledImageCount,
        };
    }

    public sealed class CourseCostBaseline
    {
        /// <summary>Teaching stage (="Learn"), WITH Realtime voice — $/Engram.</summary>
        public decimal? CostPerEngramWithVoice { get; set; }
        /// <summary>Teaching stage (="Learn"), text-only (no Realtime voice) — $/Engram.</summary>
        public decimal? CostPerEngramTextOnly { get; set; }
        /// <summary>Recall stage, max real cost observed — $/Engram/recall round.</summary>
        public decimal? CostPerRecall { get; set; }
        /// <summary>Assessment stage, max real cost observed — $/Engram/assessment round.</summary>
        public decimal? CostPerAssessment { get; set; }
        /// <summary>Production stage ("In your role" / "En tu rol"), max real cost observed — $/Engram/run.</summary>
        public decimal? CostPerInYourRoleRun { get; set; }
        /// <summary>Deep Dive card image generation — $/image (platform-wide, excludes video).</summary>
        public decimal? CostPerImage { get; set; }
        public int SampledRuntimeRowCount { get; set; }
        public int SampledImageCount { get; set; }
    }

    /// <summary>Builds <see cref="CourseCostBaseline"/> straight from the SAME
    /// private helpers <see cref="GetTenantQuotasAsync"/> already calls — no
    /// new query, no new formula, just a different-shaped read of numbers
    /// that already exist. Powers GET /simulations/course-cost-baseline.</summary>
    public async Task<CourseCostBaseline> GetCourseCostBaselineAsync(CancellationToken cancellationToken)
    {
        var (_, _, rowCount, maxCostByStage) = await GetPlatformWideMaxRuntimeCostPerEngramAsync(cancellationToken);
        var (avgCostPerImage, sampledImageCount) = await GetPlatformWideAverageImageCostAsync(cancellationToken);

        decimal? textOnlyTeaching = null;
        {
            // Teaching stage cost with Voice rows excluded — mirrors the
            // "text-only" branch inside GetPlatformWideMaxRuntimeCostPerEngramAsync,
            // isolated to just the Teaching stage (that method only returns a
            // course-wide text-only total, not broken out per stage).
            var rows = await _dbContext.StructuredLearningEngramRuntimeUsages
                .AsNoTracking()
                .Where(u => u.StructuredLearningDocumentSectionId != null && u.StepType == "Teaching" && u.Channel != "Voice")
                .Select(u => new { SectionId = u.StructuredLearningDocumentSectionId!.Value, u.Channel, u.ModelName, u.DurationSeconds, u.InputTokens, u.OutputTokens, u.CharacterCount })
                .ToListAsync(cancellationToken);
            if (rows.Count > 0)
            {
                var speechPerMillionCharsUsd = ReadDecimal("AgentPricing:AzureSpeechNeuralPerMillionCharactersUsd", 16m);
                decimal RowCost(string channel, string? modelName, int durationSeconds, int inputTokens, int outputTokens, int characterCount) => channel switch
                {
                    "TextToSpeech" => characterCount / 1_000_000m * speechPerMillionCharsUsd,
                    _ => ComputeCostForModel(modelName, inputTokens, outputTokens, 0),
                };
                textOnlyTeaching = Math.Round(rows
                    .GroupBy(r => r.SectionId)
                    .Select(g => g.Sum(r => RowCost(r.Channel, r.ModelName, r.DurationSeconds, r.InputTokens, r.OutputTokens, r.CharacterCount)))
                    .Max(), 6);
            }
        }

        return new CourseCostBaseline
        {
            CostPerEngramWithVoice = maxCostByStage.TryGetValue("Teaching", out var teachingWithVoice) ? teachingWithVoice : null,
            CostPerEngramTextOnly = textOnlyTeaching,
            CostPerRecall = maxCostByStage.TryGetValue("Recall", out var recall) ? recall : null,
            CostPerAssessment = maxCostByStage.TryGetValue("Assessment", out var assessment) ? assessment : null,
            CostPerInYourRoleRun = maxCostByStage.TryGetValue("Production", out var production) ? production : null,
            CostPerImage = avgCostPerImage,
            SampledRuntimeRowCount = rowCount,
            SampledImageCount = sampledImageCount,
        };
    }

    /// <summary>Resolves just the effective limit for one quota key — used by
    /// enforcement checks, which don't need the whole catalog/labels.</summary>
    public async Task<decimal> GetEffectiveLimitAsync(Guid tenantId, string quotaKey, CancellationToken cancellationToken)
    {
        var definition = QuotaCatalog.Find(quotaKey)
            ?? throw new ArgumentException($"Unknown quota key '{quotaKey}'.", nameof(quotaKey));

        var overrideValue = await _dbContext.TenantServiceQuotas
            .AsNoTracking()
            .Where(q => q.TenantId == tenantId && q.QuotaKey == quotaKey)
            .Select(q => (decimal?)q.LimitValue)
            .FirstOrDefaultAsync(cancellationToken);

        return overrideValue ?? definition.PlatformDefault;
    }

    /// <summary>Upserts (or, with a null value, clears back to the platform
    /// default) this tenant's override for one quota key.</summary>
    public async Task<QuotaRow> SetQuotaAsync(Guid tenantId, string quotaKey, decimal? limitValue, CancellationToken cancellationToken)
    {
        if (QuotaCatalog.Find(quotaKey) is null)
        {
            throw new ArgumentException($"Unknown quota key '{quotaKey}'.", nameof(quotaKey));
        }

        var existing = await _dbContext.TenantServiceQuotas
            .FirstOrDefaultAsync(q => q.TenantId == tenantId && q.QuotaKey == quotaKey, cancellationToken);

        if (limitValue is null)
        {
            if (existing is not null)
            {
                _dbContext.TenantServiceQuotas.Remove(existing);
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
        }
        else if (existing is not null)
        {
            existing.LimitValue = limitValue.Value;
            existing.UpdatedDate = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        else
        {
            _dbContext.TenantServiceQuotas.Add(new TenantServiceQuota
            {
                TenantId = tenantId,
                QuotaKey = quotaKey,
                LimitValue = limitValue.Value,
            });
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        // Recompute through the shared path (keeps the "max courses" real-
        // average-cost logic in exactly one place) and return just this key.
        var refreshed = await GetTenantQuotasAsync(tenantId, cancellationToken);
        return refreshed.First(q => q.Key == quotaKey);
    }

    /// <summary>True when this employee has already used up their tenant's
    /// configured monthly Buddy voice-minutes quota (calendar month,
    /// UTC) — the actual enforcement check <see cref="HumanOS.AzureFunctions.Api.BuddyVoiceSessionFunction"/>
    /// calls before minting a new Realtime session. Text chat is never
    /// subject to this — only the Realtime voice channel.</summary>
    public async Task<(bool Exceeded, int UsedSeconds, int LimitSeconds)> HasExceededBuddyVoiceQuotaAsync(
        Guid tenantId, Guid personId, CancellationToken cancellationToken)
    {
        var limitMinutes = await GetEffectiveLimitAsync(tenantId, QuotaCatalog.BuddyVoiceMinutesPerMonth, cancellationToken);
        var limitSeconds = (int)(limitMinutes * 60m);

        var now = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var usedSeconds = await _dbContext.BuddySessionUsages
            .AsNoTracking()
            .Where(u => u.PersonId == personId && u.CreatedDate >= monthStart)
            .SumAsync(u => (int?)u.DurationSeconds, cancellationToken) ?? 0;

        return (usedSeconds >= limitSeconds, usedSeconds, limitSeconds);
    }
}
