using HumanOS.Contracts.Tenancy;
using HumanOS.Data;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.Services;

/// <summary>Per-lab name + AI generation cost for a tenant's Labs tab
/// (2026-10-01) — LAB_Lab lives in a separate EF model
/// (HumanOS.SimulationLabs project) but the SAME physical Azure SQL
/// database, so it's queried here with a plain ADO.NET SqlCommand against
/// the shared connection, same pattern as
/// <see cref="TenantDashboardStatsService.GetLabCountsAsync"/>.</summary>
public sealed class TenantLabCostsService
{
    private readonly IDbContextFactory<HumanOsDbContext> _dbContextFactory;

    public TenantLabCostsService(IDbContextFactory<HumanOsDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task<IReadOnlyList<TenantLabCostResponse>> GetLabCostsAsync(Guid tenantId, CancellationToken cancellationToken)
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
                "SELECT LAB_IdLab, LAB_Nombre, LAB_EsGlobal, ISNULL(GEN_EstimatedCostUsd, 0) " +
                "FROM LAB_Lab " +
                "WHERE SEG_IdTenant = @tenantId OR LAB_EsGlobal = 1 " +
                "ORDER BY ISNULL(GEN_EstimatedCostUsd, 0) DESC";
            var parameter = command.CreateParameter();
            parameter.ParameterName = "@tenantId";
            parameter.Value = tenantId;
            command.Parameters.Add(parameter);

            var results = new List<TenantLabCostResponse>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                results.Add(new TenantLabCostResponse
                {
                    LabId = reader.GetGuid(0),
                    Name = reader.GetString(1),
                    IsGlobal = reader.GetBoolean(2),
                    EstimatedCostUsd = reader.GetDecimal(3),
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
}
