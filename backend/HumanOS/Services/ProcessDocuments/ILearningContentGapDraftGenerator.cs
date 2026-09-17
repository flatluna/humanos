using HumanOS.Contracts.ProcessDocuments;

namespace HumanOS.Services.ProcessDocuments;

public interface ILearningContentGapDraftGenerator
{
    bool IsConfigured { get; }
    string? DeploymentName { get; }

    Task<LearningContentGapDraftResponse> GenerateContentGapDraftAsync(
        LearningContentCoverageResponse step5Result,
        WorkStepRequirementsAnalysisResult? step2Requirements = null,
        string? organizationId = null,
        string? language = null,
        CancellationToken cancellationToken = default);
}
