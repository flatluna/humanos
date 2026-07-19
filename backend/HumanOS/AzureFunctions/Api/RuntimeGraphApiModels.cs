using HumanOS.Services;

namespace HumanOS.AzureFunctions.Api;

/// <summary>
/// Runtime V1, Paso 4 (2026-07-17) — request/response DTOs for the HTTP API
/// that exposes the graph-based Runtime (InstructorRuntimeOrchestrator +
/// AssessmentEvaluator + SessionRecoveryEngine) to HumanOS UI. Named
/// "RuntimeGraph..." (not just "Runtime...") to avoid clashing with the
/// OLDER, still-active Interactive Learning Runtime DTOs in
/// <see cref="RuntimeSessionModels"/> (CapabilityModule/TutorAgent-based —
/// a completely separate system).
///
/// Per the Paso 4 spec's core principle: the UI never treats
/// LearningSessionId/LearningSessionNodeId/LearningSessionStepId as its own
/// source of truth — <see cref="SessionRecoveryEngine"/> is. These IDs are
/// only ever handed back to the UI as opaque values to pass to the NEXT
/// call in the same turn (e.g. right after StartSession), never persisted
/// client-side across page loads.
/// </summary>
public sealed class StartRuntimeGraphSessionRequest
{
    public Guid PersonId { get; set; }
    public Guid CapabilityId { get; set; }
    public Guid CapabilityGraphNodeId { get; set; }
}

public sealed class SubmitRuntimeStepResponseRequest
{
    public Guid LearningSessionStepId { get; set; }
    public string Response { get; set; } = string.Empty;
}

public sealed class AdvanceRuntimeStepRequest
{
    public Guid LearningSessionNodeId { get; set; }
}

public sealed class EvaluateRuntimeAssessmentRequest
{
    public Guid LearningSessionNodeId { get; set; }
}

public sealed class CompleteRuntimeNodeRequest
{
    public Guid LearningSessionNodeId { get; set; }
}

public sealed class RuntimeIllustrationDto
{
    public Guid IllustrationId { get; set; }
    public string StoragePath { get; set; } = string.Empty;
    public string? Caption { get; set; }
}

/// <summary>The step content the UI actually renders — used both by StartSession's/GetActiveSession's "CurrentStep" and Advance's "new CurrentStep".</summary>
public sealed class RuntimeStepDto
{
    public Guid LearningSessionStepId { get; set; }
    public string StepType { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public List<RuntimeIllustrationDto> Illustrations { get; set; } = [];
}

/// <summary>Output of StartSession / GetActiveSession — per spec: LearningSessionId, LearningSessionNodeId, CurrentStep, CurrentStepType.</summary>
public sealed class RuntimeSessionInfo
{
    public Guid LearningSessionId { get; set; }
    public Guid LearningSessionNodeId { get; set; }
    public Guid CapabilityGraphNodeId { get; set; }
    public string CurrentStepType { get; set; } = string.Empty;
    public RuntimeStepDto CurrentStep { get; set; } = null!;
}

public sealed class RuntimeSessionRef
{
    public Guid LearningSessionId { get; set; }
}

public sealed class RuntimeNodeRef
{
    public Guid LearningSessionNodeId { get; set; }
    public Guid CapabilityGraphNodeId { get; set; }
}

public sealed class RuntimeStepRef
{
    public Guid LearningSessionStepId { get; set; }
    public string StepType { get; set; } = string.Empty;
}

/// <summary>Output of GetCurrentStep — per spec's exact shape: {session, node, step, content, illustrations}.</summary>
public sealed class RuntimeCurrentStepResponse
{
    public RuntimeSessionRef Session { get; set; } = null!;
    public RuntimeNodeRef Node { get; set; } = null!;
    public RuntimeStepRef Step { get; set; } = null!;
    public string Content { get; set; } = string.Empty;
    public List<RuntimeIllustrationDto> Illustrations { get; set; } = [];
}

/// <summary>Output of EvaluateAssessment — per spec's exact shape: {score, passed, feedback}.</summary>
public sealed class RuntimeAssessmentResultDto
{
    public int Score { get; set; }
    public bool Passed { get; set; }
    public string Feedback { get; set; } = string.Empty;
}

/// <summary>One past student response, for the read-only step review UI (clicking a completed step in the stepper).</summary>
public sealed class EvidenceEntryDto
{
    public string StudentResponse { get; set; } = string.Empty;
    public string? TutorPrompt { get; set; }
    public int? TutorScore { get; set; }
    public DateTime CreatedDate { get; set; }
}

/// <summary>Output of GetStepReview — a read-only "what did I see/answer" recap of any step the student already started.</summary>
public sealed class StepReviewDto
{
    public string StepType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime? StartedDate { get; set; }
    public DateTime? CompletedDate { get; set; }
    public string Content { get; set; } = string.Empty;
    public List<RuntimeIllustrationDto> Illustrations { get; set; } = [];
    public List<EvidenceEntryDto> Evidence { get; set; } = [];
}

/// <summary>One past completed attempt at a node, for the node-summary UI.</summary>
public sealed class NodeAttemptSummaryDto
{
    public Guid LearningSessionNodeId { get; set; }
    public DateTime? StartedDate { get; set; }
    public DateTime? CompletedDate { get; set; }
    public int? FinalScore { get; set; }
    public bool Passed { get; set; }
}

/// <summary>
/// Output of GetNodeSummary — read-only recap shown when opening a node
/// that is already Mastered on the map, instead of silently starting a new
/// attempt from scratch. See InstructorRuntimeOrchestrator.GetNodeSummaryAsync.
/// </summary>
public sealed class NodeSummaryDto
{
    public Guid CapabilityGraphNodeId { get; set; }

    /// <summary>Which attempt (row in <see cref="PastAttempts"/>) the top-level <see cref="Steps"/> belongs to — always the most recent one.</summary>
    public Guid LearningSessionNodeId { get; set; }
    public int AttemptCount { get; set; }
    public DateTime? FirstCompletedDate { get; set; }
    public DateTime? LastCompletedDate { get; set; }
    public int? FinalScore { get; set; }
    public List<StepReviewDto> Steps { get; set; } = [];
    public List<NodeAttemptSummaryDto> PastAttempts { get; set; } = [];
}

/// <summary>
/// Maps the Runtime services' own result types (which the services own and
/// are already tested by the Paso 2/3/3.5 E2E harness) onto the HTTP DTOs
/// above. Kept as one small static class rather than duplicating shape
/// logic across all 7 Functions.
/// </summary>
internal static class RuntimeGraphApiMappers
{
    private static List<RuntimeIllustrationDto> MapIllustrations(List<InstructorRuntimeOrchestrator.IllustrationRef> illustrations) =>
        illustrations.Select(i => new RuntimeIllustrationDto
        {
            IllustrationId = i.CapabilityGraphNodeIllustrationId,
            StoragePath = i.StoragePath,
            Caption = i.Caption
        }).ToList();

    public static RuntimeStepDto ToStepDto(InstructorRuntimeOrchestrator.CurrentStepResult step) => new()
    {
        LearningSessionStepId = step.LearningSessionStepId,
        StepType = step.StepType.ToString(),
        Content = step.StepContent,
        Illustrations = MapIllustrations(step.Illustrations)
    };

    public static RuntimeSessionInfo ToSessionInfo(Guid learningSessionId, Guid learningSessionNodeId, Guid capabilityGraphNodeId, InstructorRuntimeOrchestrator.CurrentStepResult step) => new()
    {
        LearningSessionId = learningSessionId,
        LearningSessionNodeId = learningSessionNodeId,
        CapabilityGraphNodeId = capabilityGraphNodeId,
        CurrentStepType = step.StepType.ToString(),
        CurrentStep = ToStepDto(step)
    };

    public static RuntimeSessionInfo ToSessionInfo(SessionRecoveryEngine.ResumeSessionResult resumed) =>
        ToSessionInfo(resumed.LearningSessionId, resumed.LearningSessionNodeId, resumed.CapabilityGraphNodeId, resumed.CurrentStep);

    public static StepReviewDto ToStepReviewDto(InstructorRuntimeOrchestrator.StepReviewResult review) => new()
    {
        StepType = review.StepType.ToString(),
        Status = review.Status.ToString(),
        StartedDate = review.StartedDate,
        CompletedDate = review.CompletedDate,
        Content = review.StepContent,
        Illustrations = MapIllustrations(review.Illustrations),
        Evidence = review.Evidence.Select(e => new EvidenceEntryDto
        {
            StudentResponse = e.StudentResponse,
            TutorPrompt = e.TutorPrompt,
            TutorScore = e.TutorScore,
            CreatedDate = e.CreatedDate
        }).ToList()
    };

    public static NodeSummaryDto ToNodeSummaryDto(InstructorRuntimeOrchestrator.NodeSummaryResult summary) => new()
    {
        CapabilityGraphNodeId = summary.CapabilityGraphNodeId,
        LearningSessionNodeId = summary.LearningSessionNodeId,
        AttemptCount = summary.AttemptCount,
        FirstCompletedDate = summary.FirstCompletedDate,
        LastCompletedDate = summary.LastCompletedDate,
        FinalScore = summary.FinalScore,
        Steps = summary.Steps.Select(ToStepReviewDto).ToList(),
        PastAttempts = summary.PastAttempts.Select(a => new NodeAttemptSummaryDto
        {
            LearningSessionNodeId = a.LearningSessionNodeId,
            StartedDate = a.StartedDate,
            CompletedDate = a.CompletedDate,
            FinalScore = a.FinalScore,
            Passed = a.Passed
        }).ToList()
    };

    public static RuntimeCurrentStepResponse ToCurrentStepResponse(SessionRecoveryEngine.ResumeSessionResult resumed) => new()
    {
        Session = new RuntimeSessionRef { LearningSessionId = resumed.LearningSessionId },
        Node = new RuntimeNodeRef
        {
            LearningSessionNodeId = resumed.LearningSessionNodeId,
            CapabilityGraphNodeId = resumed.CapabilityGraphNodeId
        },
        Step = new RuntimeStepRef
        {
            LearningSessionStepId = resumed.CurrentStep.LearningSessionStepId,
            StepType = resumed.CurrentStep.StepType.ToString()
        },
        Content = resumed.CurrentStep.StepContent,
        Illustrations = MapIllustrations(resumed.CurrentStep.Illustrations)
    };
}
