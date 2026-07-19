using HumanOS.Agentic.Runtime;
using HumanOS.Agents.Runtime;
using HumanOS.Data;
using HumanOS.Models.Capabilities.Graph;
using HumanOS.Models.Learning;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.Services;

/// <summary>
/// Formative (non-scoring) grading for the Production ("Aplícalo") step —
/// see ProductionEvaluatorAgent/ProductionEvaluationGate. Deliberately
/// separate from TutorService (which only handles on-demand Tutor
/// guidance turns, never grades) and from AssessmentEvaluator/
/// AdaptiveAssessmentEngine (which grade the Assessment step and DO write
/// LearningAssessmentResult) — this service's grade NEVER touches
/// LearningAssessmentResult and NEVER affects node mastery/unlocking.
///
/// Does NOT advance the step, on either a correct or incorrect verdict —
/// advancing (via InstructorRuntimeOrchestrator.AdvanceToNextStepAsync,
/// the existing /instructor-runtime/steps/advance endpoint) is a separate,
/// explicit action the frontend only takes after showing the student a
/// correct verdict.
/// </summary>
public sealed class ProductionEvaluationService
{
    private readonly ProductionEvaluatorAgent _agent;
    private readonly InstructorRuntimeOrchestrator _orchestrator;

    public ProductionEvaluationService(ProductionEvaluatorAgent agent, InstructorRuntimeOrchestrator orchestrator)
    {
        _agent = agent;
        _orchestrator = orchestrator;
    }

    public bool IsConfigured => _agent.IsConfigured;

    public sealed class EvaluationOutcome
    {
        public bool IsCorrect { get; set; }
        public int Score { get; set; }
        public string Feedback { get; set; } = string.Empty;
        public Guid LearningEvidenceId { get; set; }
    }

    /// <param name="learningSessionStepId">Must be a Production-type LearningSessionStep.</param>
    /// <param name="studentSubmission">What the student just submitted for grading.</param>
    public async Task<EvaluationOutcome> EvaluateAsync(
        HumanOsDbContext dbContext,
        Guid learningSessionStepId,
        string studentSubmission,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        var step = await dbContext.LearningSessionSteps
            .Include(s => s.LearningSessionNode)
            .FirstOrDefaultAsync(s => s.LearningSessionStepId == learningSessionStepId, cancellationToken);

        if (step is null)
        {
            throw new InvalidOperationException($"LearningSessionStep {learningSessionStepId} not found.");
        }

        if (step.StepType != ExperienceStepType.Production)
        {
            throw new InvalidOperationException(
                $"LearningSessionStep {learningSessionStepId} is not a Production step (StepType={step.StepType}).");
        }

        if (step.LearningSessionNode is null)
        {
            throw new InvalidOperationException(
                $"LearningSessionStep {learningSessionStepId} has no parent LearningSessionNode — data inconsistency.");
        }

        var nodeExperienceBlueprintId = step.LearningSessionNode.NodeExperienceBlueprintId;

        var blueprintStep = await dbContext.NodeExperienceBlueprintSteps
            .AsNoTracking()
            .FirstOrDefaultAsync(
                s => s.NodeExperienceBlueprintId == nodeExperienceBlueprintId && s.StepType == ExperienceStepType.Production,
                cancellationToken)
            ?? throw new InvalidOperationException(
                $"Blueprint {nodeExperienceBlueprintId} has no Production content — data inconsistency.");

        var evaluation = await _agent.EvaluateAsync(blueprintStep.Content, studentSubmission, cancellationToken);
        var isCorrect = ProductionEvaluationGate.IsCorrect(evaluation.Score);

        var evidenceId = await _orchestrator.SubmitResponseAsync(
            dbContext,
            learningSessionStepId,
            studentSubmission,
            cancellationToken,
            tutorScore: evaluation.Score);

        return new EvaluationOutcome
        {
            IsCorrect = isCorrect,
            Score = evaluation.Score,
            Feedback = evaluation.Feedback,
            LearningEvidenceId = evidenceId
        };
    }
}
