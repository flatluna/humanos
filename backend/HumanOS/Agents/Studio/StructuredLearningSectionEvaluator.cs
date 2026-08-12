using System.Text.RegularExpressions;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using HumanOS.Models.Capabilities.Studio;

namespace HumanOS.Agents.Studio;

/// <summary>
/// Quality guardrails for Step 3 ("Diseñador de Sección" agent, not yet
/// built) — written FIRST, as an executable specification, per the design
/// decision to evaluate BEFORE finishing the agent's prompt. Two kinds of
/// check, deliberately kept separate:
///
///   1. STRUCTURAL checks (plain code, no framework) — same style as the
///      existing <see cref="BlueprintValidator"/>: is the right SET of
///      steps present, in the right order, none empty.
///   2. CONTENT-QUALITY checks (real Microsoft.Extensions.AI.Evaluation
///      framework — <see cref="LocalEvaluator"/>/<see cref="FunctionEvaluator"/>/
///      <see cref="EvalItem"/>) — groundedness (never invent facts outside
///      the section's own source text) and no-duplication (Recall must not
///      just repeat Teaching's wording — the exact bug class already hit
///      once in InstructorAgent/ExperienceDesignerAgent's history).
///
/// These checks work standalone (constructed directly from plain content
/// strings via <see cref="EvalItem"/>) — they do NOT require a real
/// running agent, so they can be written and unit-tested before Step 3's
/// agent exists, then reused unchanged once it does.
/// </summary>
public static class StructuredLearningSectionEvaluator
{
    /// <summary>Minimum fraction of the generated content's own significant
    /// words that must also appear in the section's source text — a cheap,
    /// no-LLM proxy for "didn't invent facts outside the source".</summary>
    private const double MinGroundednessOverlap = 0.3;

    /// <summary>Maximum allowed word-overlap (Jaccard similarity) between
    /// two steps' content — catches Recall/Assessment literally repeating
    /// Teaching's own wording instead of testing retrieval.</summary>
    private const double MaxCrossStepSimilarity = 0.6;

    private const int MinStepContentLength = 20;

    // ==================== 1. Structural checks (plain code) ====================

    public sealed class StructuralCheckResult
    {
        public bool Passed { get; set; }

        public List<string> Failures { get; set; } = [];
    }

    /// <summary>Verifies the exact expected step SET and ORDER for a
    /// section, given the document's pedagogical configuration — mirrors
    /// <see cref="BlueprintValidator"/>'s deterministic, no-LLM style.</summary>
    public static StructuralCheckResult ValidateStructure(
        IReadOnlyList<(StructuredLearningStepType StepType, string Content)> generatedSteps,
        bool includeDeepDiveStep,
        bool includeRecallStep,
        bool includeProductionStep)
    {
        var result = new StructuralCheckResult { Passed = true };

        var expectedOrder = new List<StructuredLearningStepType> { StructuredLearningStepType.Hypothesis, StructuredLearningStepType.Teaching };
        if (includeDeepDiveStep) expectedOrder.Add(StructuredLearningStepType.DeepDive);
        if (includeRecallStep) expectedOrder.Add(StructuredLearningStepType.Recall);
        if (includeProductionStep) expectedOrder.Add(StructuredLearningStepType.Production);
        expectedOrder.Add(StructuredLearningStepType.Assessment);

        var actualOrder = generatedSteps.Select(s => s.StepType).ToList();
        if (!actualOrder.SequenceEqual(expectedOrder))
        {
            result.Passed = false;
            result.Failures.Add(
                $"Expected step order [{string.Join(", ", expectedOrder)}] but got [{string.Join(", ", actualOrder)}].");
        }

        foreach (var step in generatedSteps)
        {
            // Recall/Assessment (2026-08-10) intentionally carry NO
            // design-time content — their real questions are generated
            // live, one at a time — so the "content must not be empty"
            // check doesn't apply to them.
            if (step.StepType is StructuredLearningStepType.Recall or StructuredLearningStepType.Assessment)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(step.Content) || step.Content.Trim().Length < MinStepContentLength)
            {
                result.Passed = false;
                result.Failures.Add($"Step {step.StepType} content is empty or too short.");
            }
        }

        return result;
    }

    // ==================== 2. Content-quality checks (real Eval framework) ====================

    /// <summary>Runs a single custom <see cref="FunctionEvaluator"/> check
    /// against one piece of generated content, using the real
    /// <see cref="LocalEvaluator"/>/<see cref="EvalItem"/> types — no
    /// agent/conversation required, the content is wrapped directly.</summary>
    private static async Task<bool> RunCheckAsync(string checkName, Func<string, bool> check, string content)
    {
        var evaluator = new LocalEvaluator(FunctionEvaluator.Create(checkName, check));
        var item = new EvalItem(
        [
            new ChatMessage(ChatRole.User, "Generated pedagogical step content."),
            new ChatMessage(ChatRole.Assistant, content)
        ]);

        var results = await evaluator.EvaluateAsync([item]);
        return results.Passed == results.Total;
    }

    /// <summary>Groundedness: the step's content must substantially reuse
    /// words that actually appear in the section's own source text.</summary>
    public static Task<bool> CheckGroundedInSourceAsync(string stepContent, string sourceSectionText) =>
        RunCheckAsync("grounded_in_source", response => WordOverlap(response, sourceSectionText) >= MinGroundednessOverlap, stepContent);

    public static bool CheckHypothesisContractVersion(string hypothesisContent)
    {
        var prefix = "ContractVersion: " + StructuredLearningSectionDesignerAgent.SourceFaithfulContractVersion;
        return !string.IsNullOrWhiteSpace(hypothesisContent)
            && hypothesisContent.TrimStart().StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Teaches as a professor directly addressing the student, without narrating
    /// about the source document or drifting into generic reassurance.
    /// </summary>
    public static Task<bool> CheckTeachingStyleAsync(string stepContent, string sourceSectionText) =>
        RunCheckAsync(
            "teaching_style",
            response =>
            {
                var lower = response.ToLowerInvariant();
                var sourceLower = sourceSectionText.ToLowerInvariant();

                var bannedPatterns = new[]
                {
                    "the section explains",
                    "the text states",
                    "according to the document",
                    "the guide is described",
                    "the policy explains",
                    "this card covers",
                    "this section describes"
                };

                var hasMetaNarration = bannedPatterns.Any(pattern => lower.Contains(pattern));
                var hasDirectAddress = lower.Contains("you") || lower.Contains("everyone") || lower.Contains("students") || lower.Contains("let's");
                var isGroundedEnough = WordOverlap(response, sourceLower) >= MinGroundednessOverlap;

                return !hasMetaNarration && hasDirectAddress && isGroundedEnough;
            },
            stepContent);

    /// <summary>No-duplication: this step's content must not just repeat
    /// another step's wording (e.g. Recall repeating Teaching verbatim).</summary>
    public static Task<bool> CheckNotDuplicateOfAsync(string stepContent, string otherStepContent) =>
        RunCheckAsync("not_duplicate_of_other_step", response => JaccardSimilarity(response, otherStepContent) < MaxCrossStepSimilarity, stepContent);

    // ==================== Shared text-similarity helpers ====================

    private static readonly Regex WordPattern = new(@"[a-záéíóúñ]{4,}", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static List<string> Tokenize(string text) =>
        WordPattern.Matches(text.ToLowerInvariant()).Select(m => m.Value).ToList();

    /// <summary>Fraction of <paramref name="text"/>'s own significant words
    /// that also appear in <paramref name="reference"/>.</summary>
    private static double WordOverlap(string text, string reference)
    {
        var textWords = Tokenize(text);
        if (textWords.Count == 0) return 0;

        var referenceWords = new HashSet<string>(Tokenize(reference));
        var matched = textWords.Count(w => referenceWords.Contains(w));
        return (double)matched / textWords.Count;
    }

    private static double JaccardSimilarity(string a, string b)
    {
        var setA = new HashSet<string>(Tokenize(a));
        var setB = new HashSet<string>(Tokenize(b));
        if (setA.Count == 0 && setB.Count == 0) return 0;

        var intersection = setA.Intersect(setB).Count();
        var union = setA.Union(setB).Count();
        return union == 0 ? 0 : (double)intersection / union;
    }
}
