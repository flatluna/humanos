using HumanOS.Services;

namespace HumanOS.AzureFunctions.Api;

/// <summary>
/// Adaptive Assessment (2026-07-18) — request/response DTOs for the new
/// dynamic, one-question-at-a-time Assessment stage, driven by
/// <see cref="AdaptiveAssessmentEngine"/>. Kept in its own file, matching
/// this codebase's one-file-per-feature-area DTO convention (see
/// RuntimeGraphApiModels.cs / TutorApiModels.cs). Completely additive —
/// the OLD single-free-text-then-holistic-evaluate flow
/// (EvaluateRuntimeAssessmentRequest / RuntimeAssessmentResultDto in
/// RuntimeGraphApiModels.cs) is left untouched.
/// </summary>
public sealed class StartAssessmentRoundRequest
{
    public Guid LearningSessionNodeId { get; set; }
}

public sealed class GetActiveAssessmentRoundQuery
{
    public Guid LearningSessionNodeId { get; set; }
}

public sealed class SubmitAssessmentAnswerRequest
{
    public Guid AssessmentQuestionId { get; set; }
    public string StudentAnswer { get; set; } = string.Empty;
}

public sealed class AssessmentQuestionDto
{
    public Guid AssessmentQuestionId { get; set; }
    public int QuestionIndex { get; set; }
    public string QuestionType { get; set; } = string.Empty;
    public string QuestionText { get; set; } = string.Empty;
}

/// <summary>Current state of a round — used both by start-round and the resume ("active") endpoint.</summary>
public sealed class AssessmentRoundStateDto
{
    public Guid AssessmentRoundId { get; set; }
    public int RoundNumber { get; set; }
    public int TotalQuestions { get; set; } = 5;
    public string Status { get; set; } = string.Empty;

    /// <summary>Null while InProgress.</summary>
    public int? FinalScore { get; set; }

    /// <summary>Null once the round is Passed/Failed (nothing left to answer).</summary>
    public AssessmentQuestionDto? CurrentQuestion { get; set; }
}

public sealed class AssessmentAnswerGradeDto
{
    public string Correctness { get; set; } = string.Empty;
    public int Score { get; set; }
    public string Feedback { get; set; } = string.Empty;
}

public sealed class SubmitAssessmentAnswerResponse
{
    public AssessmentAnswerGradeDto Grade { get; set; } = null!;
    public bool RoundComplete { get; set; }

    /// <summary>Set only when RoundComplete.</summary>
    public bool? Passed { get; set; }

    /// <summary>Set only when RoundComplete.</summary>
    public int? FinalScore { get; set; }

    /// <summary>Set when !RoundComplete (next question this round), or when RoundComplete &amp;&amp; !Passed (Q1 of the auto-started new round).</summary>
    public AssessmentQuestionDto? NextQuestion { get; set; }

    /// <summary>Set only when a new round was auto-started (this round just closed as Failed).</summary>
    public int? NewRoundNumber { get; set; }
    public Guid? NewAssessmentRoundId { get; set; }
}

/// <summary>
/// Maps AdaptiveAssessmentEngine's own result types onto the HTTP DTOs
/// above — same "one small static mapper class" convention as
/// RuntimeGraphApiMappers.
/// </summary>
internal static class AdaptiveAssessmentApiMappers
{
    public static AssessmentQuestionDto? ToQuestionDto(AdaptiveAssessmentEngine.QuestionInfo? question) =>
        question is null ? null : new AssessmentQuestionDto
        {
            AssessmentQuestionId = question.AssessmentQuestionId,
            QuestionIndex = question.QuestionIndex,
            QuestionType = question.QuestionType.ToString(),
            QuestionText = question.QuestionText
        };

    public static AssessmentRoundStateDto ToRoundStateDto(AdaptiveAssessmentEngine.RoundState round) => new()
    {
        AssessmentRoundId = round.AssessmentRoundId,
        RoundNumber = round.RoundNumber,
        Status = round.Status.ToString(),
        FinalScore = round.FinalScore,
        CurrentQuestion = ToQuestionDto(round.CurrentQuestion)
    };

    public static SubmitAssessmentAnswerResponse ToSubmitAnswerResponse(AdaptiveAssessmentEngine.SubmitAnswerResult result) => new()
    {
        Grade = new AssessmentAnswerGradeDto
        {
            Correctness = result.Grade.Correctness.ToString(),
            Score = result.Grade.Score,
            Feedback = result.Grade.Feedback
        },
        RoundComplete = result.RoundComplete,
        Passed = result.Passed,
        FinalScore = result.FinalScore,
        NextQuestion = ToQuestionDto(result.NextQuestion),
        NewRoundNumber = result.NewRoundNumber,
        NewAssessmentRoundId = result.NewAssessmentRoundId
    };
}
