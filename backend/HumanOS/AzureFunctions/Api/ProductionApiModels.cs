using HumanOS.Services;

namespace HumanOS.AzureFunctions.Api;

/// <summary>
/// Request/response DTOs for the Production ("Aplícalo") formative
/// evaluation endpoint (see ProductionEvaluationService.cs). Kept in its
/// own file per this codebase's one-file-per-feature-area DTO convention.
/// </summary>
public sealed class EvaluateProductionRequest
{
    public Guid LearningSessionStepId { get; set; }

    public string StudentSubmission { get; set; } = string.Empty;
}

public sealed class EvaluateProductionResponse
{
    public bool IsCorrect { get; set; }

    public int Score { get; set; }

    public string Feedback { get; set; } = string.Empty;

    public Guid LearningEvidenceId { get; set; }
}

internal static class ProductionApiMappers
{
    public static EvaluateProductionResponse ToDto(ProductionEvaluationService.EvaluationOutcome outcome) => new()
    {
        IsCorrect = outcome.IsCorrect,
        Score = outcome.Score,
        Feedback = outcome.Feedback,
        LearningEvidenceId = outcome.LearningEvidenceId
    };
}
