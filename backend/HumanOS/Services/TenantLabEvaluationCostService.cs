using HumanOS.Agents.Studio;
using HumanOS.Contracts.Tenancy;
using HumanOS.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace HumanOS.Services;

/// <summary>Total AttemptEvaluationAgent (prompt in/out) cost across a tenant's own (or global)
/// Labs' attempt evaluations (2026-10-01) \u2014 powers the admin "Labs \u2014 Evaluation cost" tab.
/// LAB_AttemptEvaluation lives in a separate EF model (HumanOS.SimulationLabs project) but the
/// SAME physical Azure SQL database (see TenantLabCostsService's identical rationale), so it's
/// queried here with a plain ADO.NET SqlCommand. Reuses <see cref="TokenCostEstimator"/>'s
/// per-model rate scheme so this is directly comparable to every other LLM-cost dashboard in
/// the app.</summary>
public sealed class TenantLabEvaluationCostService
{
    private readonly IDbContextFactory<HumanOsDbContext> _dbContextFactory;
    private readonly IConfiguration _configuration;

    public TenantLabEvaluationCostService(IDbContextFactory<HumanOsDbContext> dbContextFactory, IConfiguration configuration)
    {
        _dbContextFactory = dbContextFactory;
        _configuration = configuration;
    }

    public async Task<IReadOnlyList<TenantLabEvaluationCostResponse>> GetEvaluationCostsAsync(Guid tenantId, CancellationToken cancellationToken)
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
                "SELECT l.LAB_IdLab, l.LAB_Nombre, l.LAB_EsGlobal, e.EVL_IdEvaluation, e.EVL_GeneratedModel, " +
                "ISNULL(e.EVL_InputTokens, 0), ISNULL(e.EVL_OutputTokens, 0), ISNULL(e.EVL_CachedInputTokens, 0) " +
                "FROM LAB_Lab l " +
                "LEFT JOIN LAB_LabVersion v ON v.LAB_IdLab = l.LAB_IdLab " +
                "LEFT JOIN LAB_Attempt a ON a.LAB_IdVersion = v.LAB_IdVersion " +
                "LEFT JOIN LAB_AttemptEvaluation e ON e.ATT_IdAttempt = a.ATT_IdAttempt " +
                "AND (e.EVL_InputTokens IS NOT NULL OR e.EVL_OutputTokens IS NOT NULL) " +
                "WHERE l.SEG_IdTenant = @tenantId OR l.LAB_EsGlobal = 1";
            var parameter = command.CreateParameter();
            parameter.ParameterName = "@tenantId";
            parameter.Value = tenantId;
            command.Parameters.Add(parameter);

            var rowsByLab = new Dictionary<Guid, (string Name, bool IsGlobal, List<AgentTokenUsage> Usage)>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var labId = reader.GetGuid(0);
                if (!rowsByLab.TryGetValue(labId, out var entry))
                {
                    entry = (reader.GetString(1), reader.GetBoolean(2), []);
                    rowsByLab[labId] = entry;
                }

                if (!reader.IsDBNull(3))
                {
                    entry.Usage.Add(new AgentTokenUsage
                    {
                        AgentName = string.Empty,
                        ModelName = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                        InputTokens = reader.GetInt32(5),
                        OutputTokens = reader.GetInt32(6),
                        CachedInputTokens = reader.GetInt32(7),
                    });
                }
            }

            var results = new List<TenantLabEvaluationCostResponse>();
            foreach (var (labId, entry) in rowsByLab)
            {
                var estimate = TokenCostEstimator.Estimate(entry.Usage, 0, _configuration);
                results.Add(new TenantLabEvaluationCostResponse
                {
                    LabId = labId,
                    Name = entry.Name,
                    IsGlobal = entry.IsGlobal,
                    EvaluationCount = entry.Usage.Count,
                    InputTokens = entry.Usage.Sum(u => (long)u.InputTokens),
                    OutputTokens = entry.Usage.Sum(u => (long)u.OutputTokens),
                    CachedInputTokens = entry.Usage.Sum(u => (long)u.CachedInputTokens),
                    EstimatedCostUsd = estimate.TotalCostUsd,
                });
            }
            return results.OrderByDescending(r => r.EstimatedCostUsd).ToList();
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }
}
