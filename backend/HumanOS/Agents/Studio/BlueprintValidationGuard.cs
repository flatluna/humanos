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

    /// <summary>Used when the blueprint has an optional Deep Dive step —
    /// see <see cref="HumanOS.Services.NodeExperienceBlueprintPersistenceService"/>'s
    /// identically-named array for the full rationale.</summary>
    private static readonly ExperienceStepType[] FormalEducationStepOrder =
    [
        ExperienceStepType.Hypothesis,
        ExperienceStepType.Teaching,
        ExperienceStepType.DeepDive,
        ExperienceStepType.Recall,
        ExperienceStepType.Production,
        ExperienceStepType.Assessment
    ];

    /// <summary>Used when the blueprint omits the optional Production step
    /// (2026-08-04, see <see cref="HumanOS.Models.Capabilities.Capability.IncludeProductionStep"/>)
    /// and has no Deep Dive step either.</summary>
    private static readonly ExperienceStepType[] NoProductionStepOrder =
    [
        ExperienceStepType.Hypothesis,
        ExperienceStepType.Teaching,
        ExperienceStepType.Recall,
        ExperienceStepType.Assessment
    ];

    /// <summary>Used when the blueprint omits the optional Production step
    /// but DOES have a Deep Dive step.</summary>
    private static readonly ExperienceStepType[] FormalEducationNoProductionStepOrder =
    [
        ExperienceStepType.Hypothesis,
        ExperienceStepType.Teaching,
        ExperienceStepType.DeepDive,
        ExperienceStepType.Recall,
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

        var hasExamplesStep = blueprint.Steps.Any(s => s.StepType == ExperienceStepType.DeepDive);
        var hasProductionStep = blueprint.Steps.Any(s => s.StepType == ExperienceStepType.Production);
        var stepOrder = (hasExamplesStep, hasProductionStep) switch
        {
            (true, true) => FormalEducationStepOrder,
            (true, false) => FormalEducationNoProductionStepOrder,
            (false, true) => CanonicalStepOrder,
            (false, false) => NoProductionStepOrder
        };

        var orderedSteps = blueprint.Steps.OrderBy(s => s.SortOrder).ToList();
        var structuralFailure = false;

        // VALIDACIÓN 1 — Memory Paradox complete.
        foreach (var stepType in stepOrder)
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

        // VALIDACIÓN 2 — order correct (SortOrder must mirror this blueprint's
        // canonical position, i.e. its index in stepOrder — NOT the enum's
        // raw numeric value, since the optional Deep Dive step's enum value
        // (5) intentionally sits after Assessment(4) to avoid renumbering
        // any other blueprint's persisted steps; its real teaching position
        // is index 2 (between Teaching and Recall) within stepOrder).
        foreach (var step in orderedSteps)
        {
            var expectedSortOrder = Array.IndexOf(stepOrder, step.StepType);
            if (expectedSortOrder < 0 || step.SortOrder != expectedSortOrder)
            {
                structuralFailure = true;
                response.Issues.Add(new BlueprintValidationIssueDto
                {
                    Area = ToArea(step.StepType),
                    Message = $"Step '{step.StepType}' has SortOrder={step.SortOrder}, expected {expectedSortOrder} — order violates the fixed Memory Paradox sequence."
                });
            }
        }

        // VALIDACIÓN 11 (Deep Dive step, non-blocking) — must have 3+ example cards.
        if (hasExamplesStep)
        {
            var examplesStep = orderedSteps.FirstOrDefault(s => s.StepType == ExperienceStepType.DeepDive);
            var cardCount = 0;
            if (!string.IsNullOrWhiteSpace(examplesStep?.ExampleCardsJson))
            {
                try
                {
                    cardCount = System.Text.Json.JsonSerializer.Deserialize<List<ExampleCard>>(examplesStep.ExampleCardsJson)?.Count ?? 0;
                }
                catch (System.Text.Json.JsonException)
                {
                    cardCount = 0;
                }
            }

            if (cardCount < 3)
            {
                response.Warnings.Add(new BlueprintValidationIssueDto
                {
                    Area = BlueprintValidationArea.Examples,
                    Message = $"Deep Dive step has only {cardCount} example card(s); expected 3 or more."
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
        ExperienceStepType.DeepDive => BlueprintValidationArea.Examples,
        _ => BlueprintValidationArea.References
    };
}
