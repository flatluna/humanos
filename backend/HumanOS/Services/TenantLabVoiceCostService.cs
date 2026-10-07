using HumanOS.Contracts.Tenancy;
using HumanOS.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace HumanOS.Services;

/// <summary>Total Realtime-voice agent time/cost across a tenant's own (or global) Labs'
/// attempts (2026-10-01) \u2014 powers the admin "Labs \u2014 Agent cost" tab. LAB_Attempt lives in a
/// separate EF model (HumanOS.SimulationLabs project) but the SAME physical Azure SQL database
/// (see TenantLabCostsService's identical rationale), so it's queried here with a plain ADO.NET
/// SqlCommand. Reuses LAB_Attempt.ATT_DuracionSegundos (already recorded when an attempt
/// completes) rather than adding new instrumentation \u2014 every voice attempt already measures its
/// own wall-clock duration. Same flat per-minute rate as Buddy (gpt-realtime-mini, see
/// TenantBuddyUsageService's doc comment for the real published-rate rationale).</summary>
public sealed class TenantLabVoiceCostService
{
    private const decimal DefaultRealtimePerMinuteUsd = 0.045m;

    private readonly IDbContextFactory<HumanOsDbContext> _dbContextFactory;
    private readonly IConfiguration _configuration;

    public TenantLabVoiceCostService(IDbContextFactory<HumanOsDbContext> dbContextFactory, IConfiguration configuration)
    {
        _dbContextFactory = dbContextFactory;
        _configuration = configuration;
    }

    public async Task<IReadOnlyList<TenantLabVoiceCostResponse>> GetVoiceCostsAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var connection = dbContext.Database.GetDbConnection();
        var shouldClose = connection.State != System.Data.ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText =
                "SELECT l.LAB_IdLab, l.LAB_Nombre, l.LAB_EsGlobal, " +
                "COUNT(a.ATT_IdAttempt), ISNULL(SUM(a.ATT_DuracionSegundos), 0) " +
                "FROM LAB_Lab l " +
                "LEFT JOIN LAB_LabVersion v ON v.LAB_IdLab = l.LAB_IdLab " +
                "LEFT JOIN LAB_Attempt a ON a.LAB_IdVersion = v.LAB_IdVersion " +
                "AND a.ATT_UsaVoz = 1 AND a.ATT_DuracionSegundos IS NOT NULL " +
                "WHERE l.SEG_IdTenant = @tenantId OR l.LAB_EsGlobal = 1 " +
                "GROUP BY l.LAB_IdLab, l.LAB_Nombre, l.LAB_EsGlobal " +
                "ORDER BY ISNULL(SUM(a.ATT_DuracionSegundos), 0) DESC";
            var parameter = command.CreateParameter();
            parameter.ParameterName = "@tenantId";
            parameter.Value = tenantId;
            command.Parameters.Add(parameter);

            var perMinuteRate = ReadDecimal("AgentPricing:RealtimePerMinuteUsd", DefaultRealtimePerMinuteUsd);
            var results = new List<TenantLabVoiceCostResponse>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var totalSeconds = reader.GetInt32(4);
                results.Add(new TenantLabVoiceCostResponse
                {
                    LabId = reader.GetGuid(0),
                    Name = reader.GetString(1),
                    IsGlobal = reader.GetBoolean(2),
                    SessionCount = reader.GetInt32(3),
                    TotalDurationSeconds = totalSeconds,
                    EstimatedCostUsd = Math.Round((decimal)totalSeconds / 60m * perMinuteRate, 4),
                });
            }
            return results;
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private decimal ReadDecimal(string key, decimal fallback)
    {
        var raw = _configuration[key];
        return decimal.TryParse(raw, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var value)
            ? value
            : fallback;
    }
}
