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
    /// <summary>Minimum overlap ratio for a teaching step to be considered
    /// grounded in the section's own wording. This is intentionally low so
    /// normal paraphrase is accepted, but the step must still contain real
    /// section concepts rather than generic filler.</summary>
    private const double MinGroundednessOverlap = 0.08;

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
        bool includeProductionStep,
        bool includeAssessmentStep = true)
    {
        var result = new StructuralCheckResult { Passed = true };

        var expectedOrder = new List<StructuredLearningStepType> { StructuredLearningStepType.Hypothesis, StructuredLearningStepType.Teaching };
        if (includeDeepDiveStep) expectedOrder.Add(StructuredLearningStepType.DeepDive);
        if (includeRecallStep) expectedOrder.Add(StructuredLearningStepType.Recall);
        if (includeProductionStep) expectedOrder.Add(StructuredLearningStepType.Production);
        if (includeAssessmentStep) expectedOrder.Add(StructuredLearningStepType.Assessment);

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
            // check doesn't apply to them. DeepDive is checked separately
            // by ValidateDeepDiveCards (needs the real ExampleCards list,
            // not the flattened string built for this generic check).
            if (step.StepType is StructuredLearningStepType.Recall or StructuredLearningStepType.Assessment or StructuredLearningStepType.DeepDive)
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

    /// <summary>Checks the DeepDive step's real ExampleCards list directly —
    /// the generic ValidateStructure check above only sees a flattened
    /// string built from these cards, which can't distinguish "zero cards"
    /// from "cards with short text", giving a confusing failure message.
    /// Requires at least one card whenever DeepDive is included, since the
    /// designer prompt now explicitly forbids returning zero cards (a
    /// section always has at least one sub-topic worth a card).</summary>
    public static List<string> ValidateDeepDiveCards(bool includeDeepDiveStep, IReadOnlyList<(string Title, string Text)>? exampleCards)
    {
        if (!includeDeepDiveStep)
        {
            return [];
        }

        if (exampleCards is null || exampleCards.Count == 0)
        {
            return ["DeepDive step returned zero ExampleCards — at least one is required."];
        }

        var failures = new List<string>();
        for (var i = 0; i < exampleCards.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(exampleCards[i].Text) || exampleCards[i].Text.Trim().Length < MinStepContentLength)
            {
                failures.Add($"DeepDive ExampleCard #{i + 1} ('{exampleCards[i].Title}') has empty or too-short Text.");
            }
        }

        return failures;
    }

    /// <summary>Result of <see cref="CheckCapabilityAtomicity"/>.</summary>
    public sealed class AtomicityCheckResult
    {
        public bool Passed { get; set; }

        /// <summary>Human-readable reason when Passed is false — shown to
        /// the designer/logged so the engram can be split further.</summary>
        public string? Reason { get; set; }
    }

    /// <summary>Phrases that signal a Capability statement is bundling more
    /// than one distinct teachable idea into one sentence, e.g. "El
    /// voluntario puede acompañar a un participante Y ADEMÁS gestionar
    /// conflictos" — two separate capabilities, should be two engrams.</summary>
    private static readonly string[] CompoundScopeSignals =
    [
        "y además", "además de", "así como", "y también", "y asimismo",
        "as well as", "in addition to", "and also", "along with",
    ];

    /// <summary>Above this length, a one-sentence Capability is almost
    /// always bundling more than one idea rather than staying atomic —
    /// matches the "one Capability = one engram" discipline from
    /// StructuredLearningDocumentSection.Capability's own doc comment.</summary>
    private const int MaxAtomicCapabilityLength = 220;

    /// <summary>
    /// Atomicity check (2026-08-17, Paso 4) — structural/deterministic,
    /// no LLM: a section's engram is atomic enough to teach/learn in one
    /// pass only if its one-sentence <c>Capability</c> statement names
    /// exactly ONE teachable idea. Flags compound-scope connector phrases,
    /// semicolon-joined independent clauses (two full statements crammed
    /// into one Capability), and excessive length as signals the
    /// underlying section should have been split into more than one
    /// engram at chapter/engram-extraction time (see
    /// StructuredLearningSectionEvaluator's own class doc comment; the
    /// splitters in DocumentChapterSplitter/DocumentTypeOutlineParser are
    /// what should have produced a finer-grained section in the first
    /// place — this check exists to catch the cases they miss).
    /// </summary>
    public static AtomicityCheckResult CheckCapabilityAtomicity(string capabilityStatement)
    {
        if (string.IsNullOrWhiteSpace(capabilityStatement))
        {
            return new AtomicityCheckResult { Passed = false, Reason = "Capability statement is empty." };
        }

        var trimmed = capabilityStatement.Trim();
        var lower = trimmed.ToLowerInvariant();

        var matchedSignal = CompoundScopeSignals.FirstOrDefault(signal => lower.Contains(signal, StringComparison.Ordinal));
        if (matchedSignal is not null)
        {
            return new AtomicityCheckResult
            {
                Passed = false,
                Reason = $"Capability contains a compound-scope connector ('{matchedSignal}') — split into separate engrams, one per idea."
            };
        }

        // A semicolon usually joins two independent clauses — each a
        // separate, fully-formed capability statement on its own.
        if (trimmed.Count(c => c == ';') >= 1)
        {
            return new AtomicityCheckResult
            {
                Passed = false,
                Reason = "Capability contains a semicolon-joined independent clause — split into separate engrams, one per idea."
            };
        }

        if (trimmed.Length > MaxAtomicCapabilityLength)
        {
            return new AtomicityCheckResult
            {
                Passed = false,
                Reason = $"Capability statement is {trimmed.Length} characters (max {MaxAtomicCapabilityLength}) — likely bundles more than one teachable idea."
            };
        }

        return new AtomicityCheckResult { Passed = true };
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

    /// <summary>Groundedness: the step's content must stay anchored to the
    /// section's actual wording and ideas, but it may be expressed in a
    /// natural professor voice rather than a near-verbatim quote.</summary>
    public static Task<bool> CheckGroundedInSourceAsync(string stepContent, string sourceSectionText) =>
        RunCheckAsync("grounded_in_source", response =>
        {
            // The rule is intentionally conservative: a teaching paragraph must
            // match multiple concrete ideas from the section itself. Generic
            // word overlap alone is too fuzzy and creates false positives like
            // "volunteer" appearing in unrelated content.
            var sourceAnchorsMatched = CountSourceAnchors(response, sourceSectionText);
            return sourceAnchorsMatched >= 2;
        }, stepContent);

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

    private static int CountSourceAnchors(string text, string reference)
    {
        var anchors = new[]
        {
            "volunteer", "volunteers",
            "companion", "companions",
            "walk beside", "walks beside",
            "group activities", "group activity",
            "one-on-one help",
            "stay engaged", "staying engaged",
            "support", "supporting",
            "participant", "participants",
            "cheering on",
            "alongside everyone else"
        };

        var normalizedReference = reference.ToLowerInvariant();
        var matched = 0;
        foreach (var anchor in anchors)
        {
            if (!normalizedReference.Contains(anchor, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (text.Contains(anchor, StringComparison.OrdinalIgnoreCase))
            {
                matched++;
            }
        }

        return matched;
    }

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
