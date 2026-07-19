using HumanOS.Agents.Runtime;
using HumanOS.Agents.Studio;
using HumanOS.Data;
using HumanOS.Models.Capabilities.Graph;
using HumanOS.Models.Learning;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.Services;

/// <summary>
/// Runtime Paso 3 — the first REAL evaluator of the Instructor Runtime.
/// Answers exactly one question: "¿Esta persona realmente dominó este
/// nodo?" — nothing about graph progression, unlocking, mastery across
/// nodes, recommendations, tutoring, voice, or realtime (all later Runtime
/// Pasos).
///
/// Principle: Evidence != Assessment. LearningEvidence is what the student
/// DID; LearningAssessmentResult is the pedagogical INTERPRETATION of that
/// evidence against the Blueprint's own Assessment criteria.
/// </summary>
public sealed class AssessmentEvaluator
{
    private readonly AssessmentEvaluatorAgent _agent;

    public AssessmentEvaluator(AssessmentEvaluatorAgent agent)
    {
        _agent = agent;
    }

    /// <summary>Result of evaluating one node's Assessment, plus the token usage of the call that produced it.</summary>
    public sealed class Result
    {
        public LearningAssessmentResult AssessmentResult { get; set; } = null!;

        public AgentTokenUsage TokenUsage { get; set; } = null!;
    }

    /// <summary>
    /// Evaluates the Assessment evidence already submitted for a node and
    /// persists a LearningAssessmentResult row (Score, Passed, Feedback).
    /// </summary>
    /// <param name="dbContext">DbContext to read/write with (caller owns its lifetime).</param>
    /// <param name="learningSessionNodeId">The node whose Assessment step is ready to be evaluated.</param>
    public async Task<Result> EvaluateAssessmentAsync(
        HumanOsDbContext dbContext,
        Guid learningSessionNodeId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        // 1. Obtener Assessment Step (+ el resto de steps, para contexto).
        var sessionNode = await dbContext.LearningSessionNodes
            .Include(n => n.Steps).ThenInclude(s => s.Evidence)
            .FirstOrDefaultAsync(n => n.LearningSessionNodeId == learningSessionNodeId, cancellationToken);

        if (sessionNode is null)
        {
            throw new InvalidOperationException($"LearningSessionNode {learningSessionNodeId} not found.");
        }

        var assessmentStep = sessionNode.Steps.SingleOrDefault(s => s.StepType == ExperienceStepType.Assessment);
        if (assessmentStep is null)
        {
            throw new InvalidOperationException(
                $"LearningSessionNode {learningSessionNodeId} has no Assessment step yet.");
        }

        var assessmentEvidenceText = assessmentStep.Evidence
            .OrderByDescending(e => e.CreatedDate)
            .Select(e => e.StudentResponse)
            .FirstOrDefault();

        if (string.IsNullOrWhiteSpace(assessmentEvidenceText))
        {
            throw new InvalidOperationException(
                $"LearningSessionNode {learningSessionNodeId}'s Assessment step has no LearningEvidence yet — cannot evaluate.");
        }

        // 2. Obtener Blueprint + 3. Obtener Assessment Criteria (el Content del step Assessment del Blueprint).
        var blueprintSteps = await dbContext.NodeExperienceBlueprintSteps
            .AsNoTracking()
            .Where(s => s.NodeExperienceBlueprintId == sessionNode.NodeExperienceBlueprintId)
            .ToListAsync(cancellationToken);

        var assessmentCriteria = blueprintSteps
            .FirstOrDefault(s => s.StepType == ExperienceStepType.Assessment)
            ?.Content;

        if (string.IsNullOrWhiteSpace(assessmentCriteria))
        {
            throw new InvalidOperationException(
                $"Blueprint {sessionNode.NodeExperienceBlueprintId} has no Assessment content/criteria — data inconsistency.");
        }

        // 4. Obtener LearningEvidence de los pasos previos (contexto, no se evalúan directamente).
        var priorStepEvidence = new Dictionary<string, string>();
        foreach (var stepType in new[] { ExperienceStepType.Hypothesis, ExperienceStepType.Recall, ExperienceStepType.Production })
        {
            var step = sessionNode.Steps.FirstOrDefault(s => s.StepType == stepType);
            var latestEvidence = step?.Evidence.OrderByDescending(e => e.CreatedDate).FirstOrDefault();
            if (latestEvidence is not null)
            {
                priorStepEvidence[stepType.ToString()] = latestEvidence.StudentResponse;
            }
        }

        // 5. Evaluar evidencia (LLM).
        var evaluationResult = await _agent.EvaluateAsync(
            assessmentCriteria,
            assessmentEvidenceText,
            priorStepEvidence,
            cancellationToken);

        // 6. Generar resultado — Passed se calcula de forma determinista en código
        // (Score >= 70), nunca se confía en un booleano propuesto por el LLM.
        var score = Math.Clamp(evaluationResult.Evaluation.Score, 0, 100);
        var passed = score >= 70;

        var assessmentResult = new LearningAssessmentResult
        {
            LearningSessionNodeId = learningSessionNodeId,
            Score = score,
            Passed = passed,
            Feedback = evaluationResult.Evaluation.Feedback
        };

        dbContext.LearningAssessmentResults.Add(assessmentResult);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new Result
        {
            AssessmentResult = assessmentResult,
            TokenUsage = evaluationResult.TokenUsage
        };
    }
}
