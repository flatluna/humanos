using HumanOS.Services;

namespace HumanOS.AzureFunctions.Api;

/// <summary>
/// Request/response DTOs for TutorAgentV2's HTTP API (see
/// /memories/repo/agent-framework-native-architecture-mandate.md and
/// TutorService.cs). Kept in their own file, alongside the two Tutor
/// Function classes, per this codebase's convention of one *ApiModels.cs
/// file per feature area (see RuntimeGraphApiModels.cs).
/// </summary>
public sealed class TutorAskRequest
{
    public Guid LearningSessionStepId { get; set; }

    /// <summary>One of "Teaching", "Production", "AssessmentFeedback" —
    /// NOT "Recall" (that mode is exclusively served by
    /// <see cref="TutorSubmitRecallAttemptFunction"/>, which also persists
    /// and applies the attempt-cap/mastery gate).</summary>
    public string Mode { get; set; } = string.Empty;

    public string StudentMessage { get; set; } = string.Empty;

    /// <summary>Required when Mode is "AssessmentFeedback"; ignored otherwise.</summary>
    public string? RawAssessmentFeedback { get; set; }
}

public sealed class TutorTurnDto
{
    public string Message { get; set; } = string.Empty;

    public int? RecallScore { get; set; }

    /// <summary>Illustration(s) the Tutor's Message referenced in text
    /// (same shape as RuntimeStepDto.Illustrations) — the frontend renders
    /// the actual image from StoragePath alongside the Tutor's Message;
    /// the Tutor itself never sees or returns image bytes, only this
    /// metadata resolved server-side from the blueprint step.</summary>
    public List<RuntimeIllustrationDto> Illustrations { get; set; } = [];
}

public sealed class SubmitRecallAttemptRequest
{
    public Guid LearningSessionStepId { get; set; }

    public string StudentResponse { get; set; } = string.Empty;

    /// <summary>The hint/question the Tutor showed right before this
    /// attempt (the previous call's TutorTurnDto.Message) — null for the
    /// student's very first attempt on this step.</summary>
    public string? TutorPromptShown { get; set; }
}

public sealed class RecallAttemptOutcomeDto
{
    public TutorTurnDto TutorTurn { get; set; } = null!;

    public Guid LearningEvidenceId { get; set; }

    /// <summary>Attempts used on the CURRENT item (resets after each
    /// mastered item, and after a regression-to-Teaching cycle).</summary>
    public int AttemptsUsedForItem { get; set; }

    /// <summary>How many of <see cref="ItemsRequired"/> distinct items the
    /// student has mastered so far on this Recall step.</summary>
    public int ItemsMastered { get; set; }

    public int ItemsRequired { get; set; }

    public bool Mastered { get; set; }

    /// <summary>True only when all ItemsRequired items are now mastered
    /// and the step advanced to Production — NextStep is set in that case.</summary>
    public bool Advanced { get; set; }

    /// <summary>True when the student exhausted their attempt budget on
    /// one item and the node regressed back to Teaching — NextStep is set
    /// (the reactivated Teaching step) in that case.</summary>
    public bool RegressedToTeaching { get; set; }

    public RuntimeStepDto? NextStep { get; set; }
}

internal static class TutorApiMappers
{
    public static TutorTurnDto ToDto(Agentic.Runtime.TutorTurnResult result) => new()
    {
        Message = result.Response.Message,
        RecallScore = result.Response.RecallScore,
        Illustrations = result.Illustrations.Select(i => new RuntimeIllustrationDto
        {
            IllustrationId = i.CapabilityGraphNodeIllustrationId,
            StoragePath = i.StoragePath,
            Caption = i.Caption
        }).ToList()
    };

    public static RecallAttemptOutcomeDto ToDto(TutorService.RecallAttemptOutcome outcome) => new()
    {
        TutorTurn = ToDto(outcome.TutorTurn),
        LearningEvidenceId = outcome.LearningEvidenceId,
        AttemptsUsedForItem = outcome.AttemptsUsedForItem,
        ItemsMastered = outcome.ItemsMastered,
        ItemsRequired = outcome.ItemsRequired,
        Mastered = outcome.Mastered,
        Advanced = outcome.Advanced,
        RegressedToTeaching = outcome.RegressedToTeaching,
        NextStep = outcome.NextStep is null ? null : RuntimeGraphApiMappers.ToStepDto(outcome.NextStep)
    };
}
