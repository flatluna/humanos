using HumanOS.Contracts.Tenancy;
using HumanOS.Data;
using HumanOS.Models.Learning;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.Services;

/// <summary>Computes the read-only aggregate dashboard stats for one
/// tenant's detail page (2026-10-01) — see <see cref="TenantDashboardStatsResponse"/>
/// doc comment for exact scope. The LAB_Lab table lives in a separate EF
/// model (HumanOS.SimulationLabs project) but the SAME physical Azure SQL
/// database (HumanOSDev), so it's queried here with a plain ADO.NET
/// SqlCommand against the shared connection rather than referencing that
/// other project.</summary>
public sealed class TenantDashboardStatsService
{
    private readonly IDbContextFactory<HumanOsDbContext> _dbContextFactory;

    public TenantDashboardStatsService(IDbContextFactory<HumanOsDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task<TenantDashboardStatsResponse> GetStatsAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var tenantPersonIds = dbContext.People
            .AsNoTracking()
            .Where(person => person.TenantId == tenantId)
            .Select(person => person.PersonId);

        var response = new TenantDashboardStatsResponse
        {
            PeopleCount = await tenantPersonIds.CountAsync(cancellationToken),
        };

        var ownedCapabilities = await dbContext.Capabilities
            .Where(c => c.OwnerTenantId == tenantId)
            .GroupBy(c => c.ContentLanguageCode)
            .Select(g => new { Language = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);
        var ownedCapabilityCount = ownedCapabilities.Sum(g => g.Count);
        var ownedSldCount = await dbContext.StructuredLearningDocuments.CountAsync(d => d.TenantId == tenantId, cancellationToken);

        var globalCapabilityCount = await dbContext.Capabilities.CountAsync(c => c.OwnerTenantId == null, cancellationToken);
        var globalSldCount = await dbContext.StructuredLearningDocuments.CountAsync(d => d.IsGlobal, cancellationToken);

        response.Courses.TotalOwnedByTenant = ownedCapabilityCount + ownedSldCount;
        response.Courses.TotalGlobal = globalCapabilityCount + globalSldCount;
        response.Courses.ByLanguage = ownedCapabilities.ToDictionary(g => g.Language, g => g.Count);

        var structuredLearningSessions = dbContext.StructuredLearningEngramSessions
            .AsNoTracking()
            .Where(session => session.PersonId.HasValue && tenantPersonIds.Contains(session.PersonId.Value));
        var consumedSectionIds =
            (from step in dbContext.StructuredLearningEngramSessionSteps.AsNoTracking()
             join session in dbContext.StructuredLearningEngramSessions.AsNoTracking()
                 on step.StructuredLearningEngramSessionId equals session.StructuredLearningEngramSessionId
             where session.PersonId.HasValue
                 && tenantPersonIds.Contains(session.PersonId.Value)
                 && step.StructuredLearningDocumentSectionId.HasValue
             select step.StructuredLearningDocumentSectionId!.Value)
            .Union(structuredLearningSessions
                .Where(session => session.StructuredLearningDocumentSectionId.HasValue)
                .Select(session => session.StructuredLearningDocumentSectionId!.Value))
            .Union(dbContext.StructuredLearningRecallAttempts
                .AsNoTracking()
                .Where(attempt => attempt.PersonId.HasValue && tenantPersonIds.Contains(attempt.PersonId.Value))
                .Select(attempt => attempt.StructuredLearningDocumentSectionId))
            .Union(dbContext.StructuredLearningAssessmentRounds
                .AsNoTracking()
                .Where(round => round.PersonId.HasValue && tenantPersonIds.Contains(round.PersonId.Value))
                .Select(round => round.StructuredLearningDocumentSectionId))
            .Union(dbContext.StructuredLearningEngramRuntimeUsages
                .AsNoTracking()
                .Where(usage => usage.PersonId.HasValue
                    && tenantPersonIds.Contains(usage.PersonId.Value)
                    && usage.StructuredLearningDocumentSectionId.HasValue)
                .Select(usage => usage.StructuredLearningDocumentSectionId!.Value));

        var structuredLearningCourseIds = structuredLearningSessions
            .Where(session => session.StructuredLearningDocumentId.HasValue)
            .Select(session => session.StructuredLearningDocumentId!.Value)
            .Union(dbContext.StructuredLearningDocumentSections
                .AsNoTracking()
                .Where(section => consumedSectionIds.Contains(section.StructuredLearningDocumentSectionId))
                .Select(section => section.StructuredLearningDocumentId));

        var consumedCapabilityIds = dbContext.LearningSessions
            .AsNoTracking()
            .Where(session => tenantPersonIds.Contains(session.PersonId) && session.StartedDate.HasValue)
            .Select(session => session.CapabilityId)
            .Union(dbContext.PersonCapabilities
                .AsNoTracking()
                .Where(personCapability => tenantPersonIds.Contains(personCapability.PersonId)
                    && personCapability.StartedDate.HasValue)
                .Select(personCapability => personCapability.CapabilityId));

        response.Courses.TotalConsumedByTenant =
            await structuredLearningCourseIds.Distinct().CountAsync(cancellationToken)
            + await consumedCapabilityIds.Distinct().CountAsync(cancellationToken);

        response.Media.ImagesCount = await dbContext.Set<Models.Capabilities.Graph.CapabilityGraphNodeIllustration>()
            .Join(dbContext.CapabilityGraphNodes, i => i.CapabilityGraphNodeId, n => n.CapabilityGraphNodeId, (i, n) => n)
            .Join(dbContext.CapabilityGraphs, n => n.CapabilityGraphId, g => g.CapabilityGraphId, (n, g) => g)
            .Join(dbContext.Capabilities, g => g.CapabilityId, c => c.CapabilityId, (g, c) => c)
            .CountAsync(c => c.OwnerTenantId == tenantId, cancellationToken)
          + await dbContext.StructuredLearningIllustrations
                .Join(dbContext.StructuredLearningDocumentSections, i => i.StructuredLearningDocumentSectionId, s => s.StructuredLearningDocumentSectionId, (i, s) => new { i.MediaType, s.StructuredLearningDocumentId })
                .Join(dbContext.StructuredLearningDocuments, x => x.StructuredLearningDocumentId, d => d.StructuredLearningDocumentId, (x, d) => new { x.MediaType, d.TenantId })
                .CountAsync(x => x.TenantId == tenantId && x.MediaType != "Video", cancellationToken);

        var videoFromStructuredLearning = await dbContext.StructuredLearningIllustrations
            .Join(dbContext.StructuredLearningDocumentSections, i => i.StructuredLearningDocumentSectionId, s => s.StructuredLearningDocumentSectionId, (i, s) => new { i.MediaType, s.StructuredLearningDocumentId })
            .Join(dbContext.StructuredLearningDocuments, x => x.StructuredLearningDocumentId, d => d.StructuredLearningDocumentId, (x, d) => new { x.MediaType, d.TenantId })
            .CountAsync(x => x.TenantId == tenantId && x.MediaType == "Video", cancellationToken);
        var videoFromFilmScenes = await dbContext.FilmScenes
            .Join(dbContext.FilmProductions, s => s.FilmProductionId, p => p.FilmProductionId, (s, p) => new { s.FinalVideoStoragePath, p.TenantId })
            .CountAsync(x => x.TenantId == tenantId && x.FinalVideoStoragePath != null, cancellationToken);
        var videoFromVideoProductions = await dbContext.VideoProductionClips
            .Join(dbContext.VideoProductions, c => c.VideoProductionId, p => p.VideoProductionId, (c, p) => new { c.StoragePath, p.TenantId })
            .CountAsync(x => x.TenantId == tenantId && x.StoragePath != null, cancellationToken);
        response.Media.VideosCount = videoFromStructuredLearning + videoFromFilmScenes + videoFromVideoProductions;

        var (labsOwned, labsGlobal) = await GetLabCountsAsync(dbContext, tenantId, cancellationToken);
        response.Labs.OwnedByTenant = labsOwned;
        response.Labs.Global = labsGlobal;

        return response;
    }

    private static async Task<(int Owned, int Global)> GetLabCountsAsync(
        HumanOsDbContext dbContext, Guid tenantId, CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        var shouldClose = connection.State != System.Data.ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT " +
                "SUM(CASE WHEN SEG_IdTenant = @tenantId AND LAB_EsGlobal = 0 THEN 1 ELSE 0 END) AS Owned, " +
                "SUM(CASE WHEN LAB_EsGlobal = 1 THEN 1 ELSE 0 END) AS Global " +
                "FROM LAB_Lab";
            var parameter = command.CreateParameter();
            parameter.ParameterName = "@tenantId";
            parameter.Value = tenantId;
            command.Parameters.Add(parameter);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                var owned = reader.IsDBNull(0) ? 0 : reader.GetInt32(0);
                var global = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);
                return (owned, global);
            }
            return (0, 0);
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
