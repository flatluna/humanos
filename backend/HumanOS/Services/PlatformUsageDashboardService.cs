using HumanOS.Agents.Studio;
using HumanOS.Contracts.Dashboard;
using HumanOS.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace HumanOS.Services;

/// <summary>Platform-wide (ALL tenants combined, never scoped to one
/// tenant) usage totals bucketed by calendar month (2026-10-06) — powers
/// the "Usage dashboard" (Power-BI-style) page with a single month-range
/// filter driving 6 panels: courses created, courses taken, Buddy, in-course
/// agents, Labs, and Brain review. Each method reuses the SAME usage tables
/// and cost formulas as the existing per-tenant dashboards
/// (<see cref="TenantMonthlyCostService"/>, <see cref="TenantBuddyUsageService"/>,
/// <see cref="StructuredLearningEngramStatsService"/>, <see cref="TenantLabVoiceCostService"/>,
/// <see cref="TenantLabEvaluationCostService"/>) just grouped by month instead
/// of by tenant, so the numbers always reconcile with the tenant-level
/// pages.</summary>
public sealed class PlatformUsageDashboardService
{
    private const decimal DefaultRealtimePerMinuteUsd = 0.045m;
    private const decimal VideoCostPerVideoUsd = 0.80m;

    private readonly IDbContextFactory<HumanOsDbContext> _dbContextFactory;
    private readonly IConfiguration _configuration;

    public PlatformUsageDashboardService(IDbContextFactory<HumanOsDbContext> dbContextFactory, IConfiguration configuration)
    {
        _dbContextFactory = dbContextFactory;
        _configuration = configuration;
    }

    private static string MonthKey(DateTime date) => date.ToString("yyyy-MM");
    private static string MonthKey(DateTimeOffset date) => date.ToString("yyyy-MM");

    private static bool InRange(string yearMonth, string? from, string? to) =>
        (string.IsNullOrEmpty(from) || string.Compare(yearMonth, from, StringComparison.Ordinal) >= 0) &&
        (string.IsNullOrEmpty(to) || string.Compare(yearMonth, to, StringComparison.Ordinal) <= 0);

    /// <summary>Panel 1 — "Creación de cursos (global)": every Capability +
    /// Structured Learning Document ever generated, ALL tenants, bucketed by
    /// the month it was created.</summary>
    public async Task<List<PlatformCoursesGeneratedMonthlyResponse>> GetCoursesGeneratedMonthlyAsync(string? from, string? to, CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var byMonth = new Dictionary<string, PlatformCoursesGeneratedMonthlyResponse>();

        PlatformCoursesGeneratedMonthlyResponse GetBucket(string yearMonth)
        {
            if (!byMonth.TryGetValue(yearMonth, out var bucket))
            {
                bucket = new PlatformCoursesGeneratedMonthlyResponse { YearMonth = yearMonth };
                byMonth[yearMonth] = bucket;
            }
            return bucket;
        }

        var capabilityCreatedDates = await dbContext.Capabilities.AsNoTracking().Select(c => c.CreatedDate).ToListAsync(cancellationToken);
        var documentCreatedDates = await dbContext.StructuredLearningDocuments.AsNoTracking().Select(d => d.CreatedDate).ToListAsync(cancellationToken);
        foreach (var date in capabilityCreatedDates.Concat(documentCreatedDates))
        {
            var key = MonthKey(date);
            if (!InRange(key, from, to)) continue;
            GetBucket(key).CoursesCreatedCount++;
        }

        var capUsageRows = await dbContext.Set<Models.Capabilities.Graph.CapabilityGenerationUsage>()
            .AsNoTracking()
            .Select(u => new { u.CreatedDate, u.InputTokens, u.OutputTokens, u.CachedInputTokens, u.ModelName })
            .ToListAsync(cancellationToken);
        var sldUsageRows = await dbContext.StructuredLearningGenerationUsages
            .AsNoTracking()
            .Select(u => new { u.CreatedDate, u.InputTokens, u.OutputTokens, u.CachedInputTokens, u.ModelName })
            .ToListAsync(cancellationToken);

        foreach (var row in capUsageRows.Select(r => (r.CreatedDate, r.InputTokens, r.OutputTokens, r.CachedInputTokens, r.ModelName))
                     .Concat(sldUsageRows.Select(r => (r.CreatedDate, r.InputTokens, r.OutputTokens, r.CachedInputTokens, r.ModelName))))
        {
            var key = MonthKey(row.CreatedDate);
            if (!InRange(key, from, to)) continue;
            var bucket = GetBucket(key);
            bucket.InputTokens += row.InputTokens;
            bucket.OutputTokens += row.OutputTokens;
            var usage = new List<AgentTokenUsage> { new() { AgentName = string.Empty, ModelName = row.ModelName ?? string.Empty, InputTokens = row.InputTokens, OutputTokens = row.OutputTokens, CachedInputTokens = row.CachedInputTokens } };
            bucket.EstimatedCostUsd += TokenCostEstimator.Estimate(usage, 0, _configuration).TotalCostUsd;
        }

        var imageRate = ReadDecimal("AgentPricing:PerImage", 0.04m);

        var capImageDates = await dbContext.Set<Models.Capabilities.Graph.CapabilityGraphNodeIllustration>()
            .AsNoTracking()
            .Select(i => i.CreatedDate)
            .ToListAsync(cancellationToken);
        foreach (var createdDate in capImageDates)
        {
            var key = MonthKey(createdDate);
            if (!InRange(key, from, to)) continue;
            GetBucket(key).ImagesGeneratedCount++;
            GetBucket(key).EstimatedCostUsd += imageRate;
        }

        var sldIllustrations = await dbContext.StructuredLearningIllustrations
            .AsNoTracking()
            .Select(i => new { i.CreatedDate, i.MediaType, i.ImageModel })
            .ToListAsync(cancellationToken);
        foreach (var illustration in sldIllustrations)
        {
            var key = MonthKey(illustration.CreatedDate);
            if (!InRange(key, from, to)) continue;
            var bucket = GetBucket(key);
            if (illustration.MediaType == "Video")
            {
                bucket.VideosGeneratedCount++;
                var isMerged = string.Equals(illustration.ImageModel, "merged", StringComparison.OrdinalIgnoreCase);
                bucket.EstimatedCostUsd += isMerged ? 0m : VideoCostPerVideoUsd;
            }
            else
            {
                bucket.ImagesGeneratedCount++;
                bucket.EstimatedCostUsd += imageRate;
            }
        }

        foreach (var bucket in byMonth.Values)
        {
            bucket.EstimatedCostUsd = Math.Round(bucket.EstimatedCostUsd, 4);
        }

        return byMonth.Values.OrderBy(b => b.YearMonth).ToList();
    }

    /// <summary>Panel 2 — "Toma de cursos (global)": every real student
    /// course-taking interaction (sessions + Q&A-with-Professor usage), ALL
    /// tenants, bucketed by month.</summary>
    public async Task<List<PlatformCoursesTakenMonthlyResponse>> GetCoursesTakenMonthlyAsync(string? from, string? to, CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var byMonth = new Dictionary<string, PlatformCoursesTakenMonthlyResponse>();

        PlatformCoursesTakenMonthlyResponse GetBucket(string yearMonth)
        {
            if (!byMonth.TryGetValue(yearMonth, out var bucket))
            {
                bucket = new PlatformCoursesTakenMonthlyResponse { YearMonth = yearMonth };
                byMonth[yearMonth] = bucket;
            }
            return bucket;
        }

        var sessionStartedDates = await dbContext.StructuredLearningEngramSessions.AsNoTracking().Select(s => s.StartedDate).ToListAsync(cancellationToken);
        foreach (var date in sessionStartedDates)
        {
            var key = MonthKey(date);
            if (!InRange(key, from, to)) continue;
            GetBucket(key).SessionsCount++;
        }

        var usageRows = await dbContext.StructuredLearningEngramRuntimeUsages
            .AsNoTracking()
            .Select(u => new { u.CreatedDate, u.Channel, u.ModelName, u.InputTokens, u.OutputTokens, u.CharacterCount, u.DurationSeconds })
            .ToListAsync(cancellationToken);

        var ttsRate = ReadDecimal("AgentPricing:AzureSpeechNeuralPerMillionCharactersUsd", 16m);
        foreach (var row in usageRows)
        {
            var key = MonthKey(row.CreatedDate);
            if (!InRange(key, from, to)) continue;
            var bucket = GetBucket(key);
            bucket.InputTokens += row.InputTokens;
            bucket.OutputTokens += row.OutputTokens;
            if (row.Channel == "Voice" && row.DurationSeconds > 0)
            {
                bucket.VoiceSessionCount++;
                bucket.VoiceDurationSeconds += row.DurationSeconds;
            }
            if (row.Channel == "TextToSpeech")
            {
                bucket.TextToSpeechCharacterCount += row.CharacterCount;
                bucket.EstimatedCostUsd += Math.Round((decimal)row.CharacterCount / 1_000_000m * ttsRate, 6);
            }
            else
            {
                var usage = new List<AgentTokenUsage> { new() { AgentName = string.Empty, ModelName = row.ModelName ?? string.Empty, InputTokens = row.InputTokens, OutputTokens = row.OutputTokens } };
                bucket.EstimatedCostUsd += TokenCostEstimator.Estimate(usage, 0, _configuration).TotalCostUsd;
            }
        }

        foreach (var bucket in byMonth.Values)
        {
            bucket.EstimatedCostUsd = Math.Round(bucket.EstimatedCostUsd, 4);
        }

        return byMonth.Values.OrderBy(b => b.YearMonth).ToList();
    }

    /// <summary>Panel 3 — "Uso de Buddy": every Workplace Companion realtime
    /// voice session, ALL tenants, bucketed by month.</summary>
    public async Task<List<PlatformBuddyUsageMonthlyResponse>> GetBuddyUsageMonthlyAsync(string? from, string? to, CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var rows = await dbContext.BuddySessionUsages.AsNoTracking().Select(u => new { u.CreatedDate, u.DurationSeconds }).ToListAsync(cancellationToken);

        var perMinuteRate = ReadDecimal("AgentPricing:RealtimePerMinuteUsd", DefaultRealtimePerMinuteUsd);
        var byMonth = new Dictionary<string, PlatformBuddyUsageMonthlyResponse>();
        foreach (var row in rows)
        {
            var key = MonthKey(row.CreatedDate);
            if (!InRange(key, from, to)) continue;
            if (!byMonth.TryGetValue(key, out var bucket))
            {
                bucket = new PlatformBuddyUsageMonthlyResponse { YearMonth = key };
                byMonth[key] = bucket;
            }
            bucket.SessionCount++;
            bucket.TotalDurationSeconds += row.DurationSeconds;
        }

        foreach (var bucket in byMonth.Values)
        {
            bucket.EstimatedCostUsd = Math.Round((decimal)bucket.TotalDurationSeconds / 60m * perMinuteRate, 4);
        }

        return byMonth.Values.OrderBy(b => b.YearMonth).ToList();
    }

    /// <summary>Panel 4 — "Uso de Agentes (cursos)": the same in-course
    /// Q&amp;A-with-Professor usage rows as <see cref="GetCoursesTakenMonthlyAsync"/>,
    /// but grouped by (month, step, model, channel) so spend can be
    /// attributed to which pedagogy step (Teaching, DeepDive, Production
    /// a.k.a. "In your role", Recall, Assessment, AiAgentChat, etc.) and
    /// which agent/model answered — e.g. Production's own token/cost line
    /// is the "In your role" (workplace-application) spend.</summary>
    public async Task<List<PlatformAgentUsageMonthlyResponse>> GetAgentUsageMonthlyAsync(string? from, string? to, CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var rows = await dbContext.StructuredLearningEngramRuntimeUsages
            .AsNoTracking()
            .Where(u => u.Channel != "TextToSpeech")
            .Select(u => new { u.CreatedDate, u.Channel, u.ModelName, u.StepType, u.InputTokens, u.OutputTokens })
            .ToListAsync(cancellationToken);

        var grouped = rows
            .Select(r => new { YearMonth = MonthKey(r.CreatedDate), r.Channel, StepType = string.IsNullOrWhiteSpace(r.StepType) ? "(unassigned)" : r.StepType!, ModelName = string.IsNullOrWhiteSpace(r.ModelName) ? "(unknown model)" : r.ModelName!, r.InputTokens, r.OutputTokens })
            .Where(r => InRange(r.YearMonth, from, to))
            .GroupBy(r => new { r.YearMonth, r.StepType, r.Channel, r.ModelName })
            .Select(g =>
            {
                var usage = g.Select(r => new AgentTokenUsage { AgentName = string.Empty, ModelName = g.Key.ModelName, InputTokens = r.InputTokens, OutputTokens = r.OutputTokens }).ToList();
                return new PlatformAgentUsageMonthlyResponse
                {
                    YearMonth = g.Key.YearMonth,
                    StepType = g.Key.StepType,
                    ModelName = g.Key.ModelName,
                    Channel = g.Key.Channel,
                    CallCount = g.Count(),
                    InputTokens = g.Sum(r => (long)r.InputTokens),
                    OutputTokens = g.Sum(r => (long)r.OutputTokens),
                    EstimatedCostUsd = TokenCostEstimator.Estimate(usage, 0, _configuration).TotalCostUsd,
                };
            })
            .OrderBy(r => r.YearMonth).ThenByDescending(r => r.EstimatedCostUsd)
            .ToList();

        return grouped;
    }

    /// <summary>Panel 5 — "Uso de Labs": every Lab attempt (realtime voice
    /// duration) and AttemptEvaluationAgent token cost, ALL tenants,
    /// bucketed by month. LAB_Attempt/LAB_AttemptEvaluation live in the
    /// separate HumanOS.SimulationLabs EF model but the SAME physical Azure
    /// SQL database, so queried here with a plain ADO.NET SqlCommand (same
    /// rationale as <see cref="TenantLabVoiceCostService"/>).</summary>
    public async Task<List<PlatformLabsUsageMonthlyResponse>> GetLabsUsageMonthlyAsync(string? from, string? to, CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var connection = dbContext.Database.GetDbConnection();
        var shouldClose = connection.State != System.Data.ConnectionState.Open;
        if (shouldClose) await connection.OpenAsync(cancellationToken);

        var byMonth = new Dictionary<string, PlatformLabsUsageMonthlyResponse>();

        PlatformLabsUsageMonthlyResponse GetBucket(string yearMonth)
        {
            if (!byMonth.TryGetValue(yearMonth, out var bucket))
            {
                bucket = new PlatformLabsUsageMonthlyResponse { YearMonth = yearMonth };
                byMonth[yearMonth] = bucket;
            }
            return bucket;
        }

        try
        {
            await using (var command = connection.CreateCommand())
            {
                command.CommandText =
                    "SELECT FORMAT(ATT_FechaInicio, 'yyyy-MM'), COUNT(*), " +
                    "SUM(CASE WHEN ATT_Modalidad = 'VOICE' AND ATT_DuracionSegundos IS NOT NULL THEN 1 ELSE 0 END), " +
                    "ISNULL(SUM(CASE WHEN ATT_Modalidad = 'VOICE' THEN ATT_DuracionSegundos ELSE 0 END), 0) " +
                    "FROM LAB_Attempt WHERE ATT_FechaInicio IS NOT NULL " +
                    "GROUP BY FORMAT(ATT_FechaInicio, 'yyyy-MM')";
                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                var perMinuteRate = ReadDecimal("AgentPricing:RealtimePerMinuteUsd", DefaultRealtimePerMinuteUsd);
                while (await reader.ReadAsync(cancellationToken))
                {
                    var yearMonth = reader.GetString(0);
                    if (!InRange(yearMonth, from, to)) continue;
                    var bucket = GetBucket(yearMonth);
                    bucket.AttemptCount = reader.GetInt32(1);
                    bucket.VoiceSessionCount = reader.GetInt32(2);
                    bucket.VoiceDurationSeconds = reader.GetInt32(3);
                    bucket.EstimatedCostUsd += Math.Round((decimal)bucket.VoiceDurationSeconds / 60m * perMinuteRate, 4);
                }
            }

            await using (var command = connection.CreateCommand())
            {
                command.CommandText =
                    "SELECT FORMAT(FechaCreacion, 'yyyy-MM'), EVL_GeneratedModel, " +
                    "ISNULL(EVL_InputTokens, 0), ISNULL(EVL_OutputTokens, 0), ISNULL(EVL_CachedInputTokens, 0) " +
                    "FROM LAB_AttemptEvaluation WHERE EVL_InputTokens IS NOT NULL OR EVL_OutputTokens IS NOT NULL";
                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                var byMonthUsage = new Dictionary<string, List<AgentTokenUsage>>();
                while (await reader.ReadAsync(cancellationToken))
                {
                    var yearMonth = reader.GetString(0);
                    if (!InRange(yearMonth, from, to)) continue;
                    if (!byMonthUsage.TryGetValue(yearMonth, out var list))
                    {
                        list = [];
                        byMonthUsage[yearMonth] = list;
                    }
                    list.Add(new AgentTokenUsage
                    {
                        AgentName = string.Empty,
                        ModelName = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                        InputTokens = reader.GetInt32(2),
                        OutputTokens = reader.GetInt32(3),
                        CachedInputTokens = reader.GetInt32(4),
                    });
                }

                foreach (var (yearMonth, usage) in byMonthUsage)
                {
                    var bucket = GetBucket(yearMonth);
                    bucket.EvaluationCount = usage.Count;
                    bucket.EvaluationInputTokens = usage.Sum(u => (long)u.InputTokens);
                    bucket.EvaluationOutputTokens = usage.Sum(u => (long)u.OutputTokens);
                    bucket.EstimatedCostUsd += TokenCostEstimator.Estimate(usage, 0, _configuration).TotalCostUsd;
                }
            }
        }
        finally
        {
            if (shouldClose) await connection.CloseAsync();
        }

        foreach (var bucket in byMonth.Values)
        {
            bucket.EstimatedCostUsd = Math.Round(bucket.EstimatedCostUsd, 4);
        }

        return byMonth.Values.OrderBy(b => b.YearMonth).ToList();
    }

    /// <summary>Panel 6 — "Brain / revisión de engramas": legacy "Reactivar
    /// Engrama" spaced-repetition voice sessions PLUS Structured Learning
    /// engrams opened from the Brain dashboard (Source = "BrainReview"),
    /// ALL tenants, bucketed by month.</summary>
    public async Task<List<PlatformBrainUsageMonthlyResponse>> GetBrainUsageMonthlyAsync(string? from, string? to, CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var rows = await dbContext.EngramReviewSessionUsages.AsNoTracking().Select(u => new { u.CreatedDate, u.DurationSeconds }).ToListAsync(cancellationToken);

        var perMinuteRate = ReadDecimal("AgentPricing:RealtimePerMinuteUsd", DefaultRealtimePerMinuteUsd);
        var byMonth = new Dictionary<string, PlatformBrainUsageMonthlyResponse>();

        PlatformBrainUsageMonthlyResponse GetBucket(string yearMonth)
        {
            if (!byMonth.TryGetValue(yearMonth, out var bucket))
            {
                bucket = new PlatformBrainUsageMonthlyResponse { YearMonth = yearMonth };
                byMonth[yearMonth] = bucket;
            }
            return bucket;
        }

        foreach (var row in rows)
        {
            var key = MonthKey(row.CreatedDate);
            if (!InRange(key, from, to)) continue;
            var bucket = GetBucket(key);
            bucket.SessionCount++;
            bucket.TotalDurationSeconds += row.DurationSeconds;
            bucket.LegacyReviewCostUsd = Math.Round((decimal)bucket.TotalDurationSeconds / 60m * perMinuteRate, 4);
            bucket.EstimatedCostUsd = bucket.LegacyReviewCostUsd;
        }

        // Structured Learning engrams opened from the Brain dashboard
        // (Source = "BrainReview") — same runtime session/usage mechanism
        // as taking the course, tagged by entry point (2026-10-06).
        var brainSessionIds = await dbContext.StructuredLearningEngramSessions
            .AsNoTracking()
            .Where(s => s.Source == "BrainReview")
            .Select(s => new { s.StructuredLearningEngramSessionId, s.StartedDate })
            .ToListAsync(cancellationToken);

        foreach (var session in brainSessionIds)
        {
            var key = MonthKey(session.StartedDate);
            if (!InRange(key, from, to)) continue;
            GetBucket(key).BrainStructuredLearningSessionCount++;
        }

        if (brainSessionIds.Count > 0)
        {
            var sessionIdSet = brainSessionIds.Select(s => s.StructuredLearningEngramSessionId).ToList();
            var usageRows = await dbContext.StructuredLearningEngramRuntimeUsages
                .AsNoTracking()
                .Where(u => u.StructuredLearningEngramSessionId != null && sessionIdSet.Contains(u.StructuredLearningEngramSessionId.Value))
                .Select(u => new { u.CreatedDate, u.Channel, u.ModelName, u.StepType, u.InputTokens, u.OutputTokens, u.DurationSeconds, u.CharacterCount })
                .ToListAsync(cancellationToken);

            var ttsRate = ReadDecimal("AgentPricing:AzureSpeechNeuralPerMillionCharactersUsd", 16m);

            // Text-to-speech is billed per CHARACTER, never per token (its
            // InputTokens/OutputTokens are always 0) — TokenCostEstimator
            // only prices token-based channels, so TTS rows need their own
            // formula or they'd silently cost $0.0000 (2026-10-06 fix).
            decimal RowCostUsd(string channel, int inputTokens, int outputTokens, string? modelName, int characterCount) =>
                channel == "TextToSpeech"
                    ? Math.Round((decimal)characterCount / 1_000_000m * ttsRate, 6)
                    : TokenCostEstimator.Estimate([new AgentTokenUsage { AgentName = string.Empty, ModelName = modelName ?? string.Empty, InputTokens = inputTokens, OutputTokens = outputTokens }], 0, _configuration).TotalCostUsd;

            foreach (var row in usageRows)
            {
                var key = MonthKey(row.CreatedDate);
                if (!InRange(key, from, to)) continue;
                var bucket = GetBucket(key);
                bucket.BrainStructuredLearningInputTokens += row.InputTokens;
                bucket.BrainStructuredLearningOutputTokens += row.OutputTokens;
                if (row.Channel == "Voice") bucket.BrainStructuredLearningVoiceDurationSeconds += row.DurationSeconds;

                bucket.EstimatedCostUsd += RowCostUsd(row.Channel, row.InputTokens, row.OutputTokens, row.ModelName, row.CharacterCount);
            }

            // Brain-ONLY step/model/channel breakdown (2026-10-06) — never
            // the same rows as Panel 4's "Uso de Agentes (cursos)" table,
            // since these are pre-filtered to BrainReview sessions only.
            var breakdownByMonth = usageRows
                .Select(r => new { YearMonth = MonthKey(r.CreatedDate), r.Channel, StepType = string.IsNullOrWhiteSpace(r.StepType) ? "(unassigned)" : r.StepType!, ModelName = string.IsNullOrWhiteSpace(r.ModelName) ? "(unknown model)" : r.ModelName!, r.InputTokens, r.OutputTokens, r.CharacterCount })
                .Where(r => InRange(r.YearMonth, from, to))
                .GroupBy(r => new { r.YearMonth, r.StepType, r.Channel, r.ModelName })
                .Select(g => new PlatformAgentUsageMonthlyResponse
                {
                    YearMonth = g.Key.YearMonth,
                    StepType = g.Key.StepType,
                    ModelName = g.Key.ModelName,
                    Channel = g.Key.Channel,
                    CallCount = g.Count(),
                    InputTokens = g.Sum(r => (long)r.InputTokens),
                    OutputTokens = g.Sum(r => (long)r.OutputTokens),
                    EstimatedCostUsd = g.Sum(r => RowCostUsd(g.Key.Channel, r.InputTokens, r.OutputTokens, g.Key.ModelName, r.CharacterCount)),
                })
                .ToList();

            foreach (var row in breakdownByMonth)
            {
                GetBucket(row.YearMonth).StepBreakdown.Add(row);
            }
        }

        foreach (var bucket in byMonth.Values)
        {
            bucket.EstimatedCostUsd = Math.Round(bucket.EstimatedCostUsd + (decimal)bucket.TotalDurationSeconds / 60m * perMinuteRate, 4);
        }

        return byMonth.Values.OrderBy(b => b.YearMonth).ToList();
    }

    private decimal ReadDecimal(string key, decimal fallback)
    {
        var raw = _configuration[key];
        return decimal.TryParse(raw, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var value)
            ? value
            : fallback;
    }
}
