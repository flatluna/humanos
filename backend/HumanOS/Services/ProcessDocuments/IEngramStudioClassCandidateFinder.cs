using HumanOS.Contracts.ProcessDocuments;

namespace HumanOS.Services.ProcessDocuments;

public interface IEngramStudioClassCandidateFinder
{
    bool IsConfigured { get; }
    string? DeploymentName { get; }

    Task<CandidateClassSearchResponse> FindCandidateClassesAsync(
        WorkStepRequirementsAnalysisResult requirements,
        string? organizationId = null,
        string? language = null,
        int maxResults = 5,
        CancellationToken cancellationToken = default);
}
