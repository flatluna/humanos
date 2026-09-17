using HumanOS.Contracts.ProcessDocuments;

namespace HumanOS.Services.ProcessDocuments;

public interface ILearningContentCoverageAnalyzer
{
    bool IsConfigured { get; }
    string? DeploymentName { get; }

    Task<LearningContentCoverageResponse> AnalyzeCoverageAndGapsAsync(
        RelevantEngramsResponse step4Result,
        WorkStepRequirementsAnalysisResult? step2Requirements = null,
        string? organizationId = null,
        CancellationToken cancellationToken = default);
}
