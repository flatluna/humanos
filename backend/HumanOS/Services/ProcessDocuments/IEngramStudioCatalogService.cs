using HumanOS.Contracts.ProcessDocuments;

namespace HumanOS.Services.ProcessDocuments;

public interface IEngramStudioCatalogService
{
    Task<IReadOnlyList<EngramStudioClassSummary>> GetAvailableClassesAsync(
        string? organizationId = null,
        string? language = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<EngramStudioEngramSummary>> GetEngramsByClassIdsAsync(
        IEnumerable<string> classIds,
        string? organizationId = null,
        string? language = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<EngramStudioLearningPathSummary>> GetAvailableLearningPathsAsync(
        string? organizationId = null,
        CancellationToken cancellationToken = default);
}
