using HumanOS.Models.Capabilities.Graph;

namespace HumanOS.Agents.Studio;

/// <summary>
/// Deterministic, code-level safety net for <see cref="BlueprintValidatorAgent"/>'s
/// structured output — the same "LLM proposes, code has final say for
/// objectively-checkable facts" pattern already used by
/// MetricVerificationValidator/CompletedModuleValidator in the old
/// pipeline. Runs immediately after the LLM call in
/// <see cref="BlueprintValidatorAgent.ValidateAsync"/>, BEFORE the
/// verdict is persisted.
/// </summary>
/// <remarks>
/// Can only make the verdict STRICTER than what the LLM returned (append
/// Issues/Warnings, downgrade Status) — never looser. Never touches
/// <see cref="NodeExperienceBlueprintStep.Content"/> or any other
/// blueprint content — ExperienceDesigner CREATES, BlueprintValidator only
/// VERIFIES.
/// </remarks>
public static class BlueprintValidationGuard
{
    private static readonly ExperienceStepType[] CanonicalStepOrder =
    [
        ExperienceStepType.Hypothesis,
        ExperienceStepType.Teaching,
        ExperienceStepType.Recall,
        ExperienceStepType.Production,
        ExperienceStepType.Assessment
    ];

    private static readonly string[] PlaceholderMarkers =
    [
        "lorem ipsum", "todo", "[...]", "placeholder"
    ];

    private static readonly string[] UnobservableAssessmentVerbs =
    [
        "entiende", "comprende", "entender", "comprender", "understands", "comprehends"
    ];

    public static void Enforce(
        NodeExperienceBlueprint blueprint,
        int illustrationCount,
        BlueprintValidationResponse response)
    {
        ArgumentNullException.ThrowIfNull(blueprint);
        ArgumentNullException.ThrowIfNull(response);

        var orderedSteps = blueprint.Steps.OrderBy(s => s.SortOrder).ToList();
        var structuralFailure = false;

        // VALIDACIÓN 1 — Memory Paradox complete.
        foreach (var stepType in CanonicalStepOrder)
        {
            if (!orderedSteps.Any(s => s.StepType == stepType))
            {
                structuralFailure = true;
                response.Issues.Add(new BlueprintValidationIssueDto
                {
                    Area = ToArea(stepType),
                    Message = $"Missing step: {stepType}."
                });
            }
        }

        // VALIDACIÓN 2 — order correct (SortOrder must mirror ExperienceStepType's numeric value).
        foreach (var step in orderedSteps)
        {
            if (step.SortOrder != (int)step.StepType)
            {
                structuralFailure = true;
                response.Issues.Add(new BlueprintValidationIssueDto
                {
                    Area = ToArea(step.StepType),
                    Message = $"Step '{step.StepType}' has SortOrder={step.SortOrder}, expected {(int)step.StepType} — order violates the fixed Memory Paradox sequence."
                });
            }
        }

        // VALIDACIÓN 10 — no empty/placeholder content.
        foreach (var step in orderedSteps)
        {
            if (string.IsNullOrWhiteSpace(step.Content))
            {
                structuralFailure = true;
                response.Issues.Add(new BlueprintValidationIssueDto
                {
                    Area = ToArea(step.StepType),
                    Message = $"Step '{step.StepType}' has empty content."
                });
                continue;
            }

            var lowerContent = step.Content.ToLowerInvariant();
            if (PlaceholderMarkers.Any(marker => lowerContent.Contains(marker)))
            {
                structuralFailure = true;
                response.Issues.Add(new BlueprintValidationIssueDto
                {
                    Area = ToArea(step.StepType),
                    Message = $"Step '{step.StepType}' contains placeholder content."
                });
            }
        }

        // VALIDACIÓN 8 — illustration usage (deterministic, since it's a pure counting check).
        if (illustrationCount > 0)
        {
            var reusesAnyIllustration = orderedSteps.Any(s =>
                !string.IsNullOrWhiteSpace(s.ReferencedIllustrationIdsJson) &&
                s.ReferencedIllustrationIdsJson != "[]");

            if (!reusesAnyIllustration)
            {
                response.Warnings.Add(new BlueprintValidationIssueDto
                {
                    Area = BlueprintValidationArea.Illustration,
                    Message = $"{illustrationCount} illustration(s) available for this node but never referenced by any step."
                });
            }
        }

        // VALIDACIÓN 7 (partial, deterministic keyword check) — unobservable verbs in Assessment.
        var assessmentStep = orderedSteps.FirstOrDefault(s => s.StepType == ExperienceStepType.Assessment);
        if (assessmentStep is not null && !string.IsNullOrWhiteSpace(assessmentStep.Content))
        {
            var lowerAssessment = assessmentStep.Content.ToLowerInvariant();
            if (UnobservableAssessmentVerbs.Any(verb => lowerAssessment.Contains(verb)))
            {
                response.Warnings.Add(new BlueprintValidationIssueDto
                {
                    Area = BlueprintValidationArea.Assessment,
                    Message = "Assessment uses an unobservable verb (e.g. 'entiende'/'comprende') instead of an observable behavior."
                });
            }
        }

        // Reconcile the overall Status: a structural failure always forces
        // Rejected, regardless of what the LLM itself concluded.
        if (structuralFailure)
        {
            response.Status = BlueprintValidationStatus.Rejected;
            response.Score = Math.Min(response.Score, 40);
        }
        else if (response.Issues.Count > 0 && response.Status is BlueprintValidationStatus.Approved or BlueprintValidationStatus.ApprovedWithWarnings)
        {
            // The guard (or the LLM itself) found blocking Issues but the
            // LLM's own Status didn't reflect that — never trust an
            // "Approved" verdict alongside a non-empty Issues list.
            response.Status = BlueprintValidationStatus.NeedsRevision;
        }
        else if (response.Issues.Count == 0 && response.Warnings.Count > 0 && response.Status == BlueprintValidationStatus.Approved)
        {
            response.Status = BlueprintValidationStatus.ApprovedWithWarnings;
        }
    }

    private static BlueprintValidationArea ToArea(ExperienceStepType stepType) => stepType switch
    {
        ExperienceStepType.Hypothesis => BlueprintValidationArea.Hypothesis,
        ExperienceStepType.Teaching => BlueprintValidationArea.Teaching,
        ExperienceStepType.Recall => BlueprintValidationArea.Recall,
        ExperienceStepType.Production => BlueprintValidationArea.Production,
        ExperienceStepType.Assessment => BlueprintValidationArea.Assessment,
        _ => BlueprintValidationArea.References
    };
}
