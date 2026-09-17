using HumanOS.Contracts.ProcessDocuments;

namespace HumanOS.Services.ProcessDocuments;

public interface IEngramStudioEngramSelector
{
    bool IsConfigured { get; }
    string? DeploymentName { get; }

    Task<RelevantEngramsResponse> SelectRelevantEngramsAsync(
        WorkStepRequirementsAnalysisResult requirements,
        IReadOnlyList<CandidateClassDto> candidateClasses,
        string? organizationId = null,
        string? language = null,
        CancellationToken cancellationToken = default);
}
