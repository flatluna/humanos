using HumanOS.Contracts.ProcessDocuments;

namespace HumanOS.Services.ProcessDocuments;

public interface IStepLearningAssignmentGenerator
{
    Task<StepLearningAssignmentResponse> GenerateAssignmentAsync(
        WorkStepRequirementsAnalysisResult step2Result,
        RelevantEngramsResponse step4Result,
        LearningContentCoverageResponse step5Result,
        LearningContentGapDraftResponse step6Result,
        string? organizationId = null,
        CancellationToken cancellationToken = default);
}
