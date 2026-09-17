using System.Diagnostics;
using System.Text;
using HumanOS.Contracts.ProcessDocuments;
using Microsoft.Extensions.Logging;

namespace HumanOS.Services.ProcessDocuments;

/// <summary>
/// Generates the standard employee-facing Step Learning Assignment Markdown and structured metadata
/// based on trusted results from Steps 2, 4, 5, and 6.
/// </summary>
public sealed class StepLearningAssignmentGenerator : IStepLearningAssignmentGenerator
{
    private readonly IEngramStudioCatalogService _catalogService;
    private readonly ILogger<StepLearningAssignmentGenerator>? _logger;

    public StepLearningAssignmentGenerator(
        IEngramStudioCatalogService catalogService,
        ILogger<StepLearningAssignmentGenerator>? logger = null)
    {
        _catalogService = catalogService;
        _logger = logger;
    }

    public async Task<StepLearningAssignmentResponse> GenerateAssignmentAsync(
        WorkStepRequirementsAnalysisResult step2Result,
        RelevantEngramsResponse step4Result,
        LearningContentCoverageResponse step5Result,
        LearningContentGapDraftResponse step6Result,
        string? organizationId = null,
        CancellationToken cancellationToken = default)
    {
        if (step2Result is null) throw new ArgumentNullException(nameof(step2Result));
        if (step4Result is null) throw new ArgumentNullException(nameof(step4Result));
        if (step5Result is null) throw new ArgumentNullException(nameof(step5Result));
        if (step6Result is null) throw new ArgumentNullException(nameof(step6Result));

        var stopwatch = Stopwatch.StartNew();

        var warnings = new List<string>();
        if (step2Result.Warnings != null) warnings.AddRange(step2Result.Warnings);
        if (step4Result.Warnings != null) warnings.AddRange(step4Result.Warnings);
        if (step5Result.Warnings != null) warnings.AddRange(step5Result.Warnings);
        if (step6Result.Warnings != null) warnings.AddRange(step6Result.Warnings);

        var assumptions = new List<string>();
        if (step2Result.Assumptions != null) assumptions.AddRange(step2Result.Assumptions);
        if (step4Result.Assumptions != null) assumptions.AddRange(step4Result.Assumptions);
        if (step5Result.Assumptions != null) assumptions.AddRange(step5Result.Assumptions);
        if (step6Result.Assumptions != null) assumptions.AddRange(step6Result.Assumptions);

        // 1. Validate catalog integrity for Step 4 Classes and Engrams
        var availableClasses = await _catalogService.GetAvailableClassesAsync(organizationId, null, cancellationToken)
            ?? Array.Empty<EngramStudioClassSummary>();
        var classMap = availableClasses.ToDictionary(c => c.ClassId, StringComparer.OrdinalIgnoreCase);

        // Check if draft IDs are leaked as approved catalog content
        var draftRefs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var ed in step6Result.EngramDrafts) draftRefs.Add(ed.DraftReference);
        foreach (var cd in step6Result.ClassDrafts) draftRefs.Add(cd.DraftReference);

        var verifiedClasses = new List<SelectedClassDto>();
        var seenClassIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenEngramIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var sc in step4Result.SelectedClasses)
        {
            if (string.IsNullOrWhiteSpace(sc.ClassId)) continue;

            // Reject if ID is a draft reference
            if (draftRefs.Contains(sc.ClassId))
            {
                warnings.Add($"Catalog-integrity warning: Draft Class reference '{sc.ClassId}' was excluded from approved learning content.");
                continue;
            }

            // Reject if not in real catalog or duplicate
            if (!classMap.TryGetValue(sc.ClassId, out var catalogClass))
            {
                warnings.Add($"Catalog-integrity warning: Class '{sc.ClassId}' is no longer available or published in the catalog. Excluded from approved content.");
                continue;
            }

            if (!seenClassIds.Add(sc.ClassId))
            {
                continue;
            }

            // Filter Engrams belonging to this class
            var validEngrams = new List<SelectedEngramDto>();
            foreach (var se in sc.SelectedEngrams)
            {
                if (string.IsNullOrWhiteSpace(se.EngramId)) continue;
                if (draftRefs.Contains(se.EngramId))
                {
                    warnings.Add($"Catalog-integrity warning: Draft Engram reference '{se.EngramId}' was excluded from approved learning content.");
                    continue;
                }

                if (seenEngramIds.Add(se.EngramId))
                {
                    validEngrams.Add(se);
                }
            }

            // Exclude empty classes
            if (validEngrams.Count > 0)
            {
                verifiedClasses.Add(new SelectedClassDto
                {
                    LearningPathId = catalogClass.LearningPathId,
                    LearningPathName = catalogClass.LearningPathName,
                    ClassId = catalogClass.ClassId,
                    ClassName = catalogClass.ClassName,
                    ClassDescription = catalogClass.ClassDescription,
                    SelectedEngrams = validEngrams
                });
            }
        }

        // 2. Determine Assignment Availability Status
        // Rules:
        // - "ready": Step 5 status is covered, all critical requirements covered, no draft needed.
        // - "ready_with_content_gaps": Essential work supported, one or more supporting requirements remain partially covered, draft content exists, can still proceed.
        // - "blocked_by_content_gap": A critical requirement is not covered, required draft remains in needs_expert_review.
        var coverageStatus = step5Result.OverallCoverageStatus;
        var hasCriticalGap = step5Result.ContentGaps.Any(g => g.Criticality == "critical");
        var hasGaps = step5Result.ContentGaps.Count > 0 || step6Result.DraftCreated;

        string assignmentStatus;
        bool blockingContentGap;

        if (coverageStatus == "covered" && !hasGaps && verifiedClasses.Count > 0)
        {
            assignmentStatus = "ready";
            blockingContentGap = false;
        }
        else if (hasCriticalGap || coverageStatus == "content_gap" || verifiedClasses.Count == 0)
        {
            assignmentStatus = "blocked_by_content_gap";
            blockingContentGap = true;
        }
        else
        {
            assignmentStatus = "ready_with_content_gaps";
            blockingContentGap = false;
        }

        // 3. Collect Draft References
        var contentGapDraftReferences = new List<string>();
        foreach (var ed in step6Result.EngramDrafts)
        {
            if (!string.IsNullOrWhiteSpace(ed.DraftReference))
                contentGapDraftReferences.Add(ed.DraftReference);
        }
        foreach (var cd in step6Result.ClassDrafts)
        {
            if (!string.IsNullOrWhiteSpace(cd.DraftReference))
                contentGapDraftReferences.Add(cd.DraftReference);
        }

        // 4. Render Simple, Standard Markdown
        var markdown = RenderMarkdown(
            step2Result,
            verifiedClasses,
            step5Result,
            step6Result,
            assignmentStatus,
            blockingContentGap);

        var learningPathIds = verifiedClasses.Select(c => c.LearningPathId).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var classIds = verifiedClasses.Select(c => c.ClassId).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var engramIds = seenEngramIds.ToList();

        stopwatch.Stop();

        var response = new StepLearningAssignmentResponse
        {
            DocumentId = step2Result.DocumentId,
            StepId = step2Result.StepId,
            StepNumber = step2Result.StepNumber,
            WorkStep = step2Result.WorkStep,
            AssignmentStatus = assignmentStatus,
            AssignmentMarkdown = markdown,
            LearningPathIds = learningPathIds,
            ClassIds = classIds,
            EngramIds = engramIds,
            CoverageStatus = coverageStatus,
            ContentGapDraftReferences = contentGapDraftReferences.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            BlockingContentGap = blockingContentGap,
            Assumptions = assumptions.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            Warnings = warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            SourcePages = step2Result.SourcePages != null ? [.. step2Result.SourcePages] : []
        };

        var obs = new StepLearningAssignmentObservability
        {
            DocumentId = step2Result.DocumentId,
            StepId = step2Result.StepId,
            OrganizationId = organizationId ?? "global",
            AssignmentStatus = assignmentStatus,
            CoverageStatus = coverageStatus,
            LearningPathCount = learningPathIds.Count,
            ClassCount = classIds.Count,
            EngramCount = engramIds.Count,
            ContentGapCount = step5Result.ContentGaps.Count,
            BlockingContentGap = blockingContentGap,
            Duration = stopwatch.Elapsed,
            Success = true,
            WarningsCount = response.Warnings.Count
        };

        _logger?.LogInformation(
            "Generated Step Learning Assignment for step {StepId}: Status={Status}, LPs={LPs}, Classes={Classes}, Engrams={Engrams}, BlockingGap={Blocking} in {DurationMs}ms",
            obs.StepId,
            obs.AssignmentStatus,
            obs.LearningPathCount,
            obs.ClassCount,
            obs.EngramCount,
            obs.BlockingContentGap,
            obs.Duration.TotalMilliseconds);

        return response;
    }

    private static string RenderMarkdown(
        WorkStepRequirementsAnalysisResult step2,
        List<SelectedClassDto> selectedClasses,
        LearningContentCoverageResponse step5,
        LearningContentGapDraftResponse step6,
        string assignmentStatus,
        bool blockingContentGap)
    {
        var sb = new StringBuilder();

        // 1. Header & Work Step
        var stepNumStr = !string.IsNullOrWhiteSpace(step2.StepNumber) ? step2.StepNumber : "1";
        sb.AppendLine($"# Step {stepNumStr} Learning Assignment");
        sb.AppendLine();
        sb.AppendLine("## Work Step");
        sb.AppendLine();
        sb.AppendLine($"**{step2.WorkStep}**");
        sb.AppendLine();

        if (blockingContentGap)
        {
            sb.AppendLine("> **Note:** Learning content pending expert review.");
            sb.AppendLine();
        }

        // 2. Employee Must Be Able To
        sb.AppendLine("## Employee Must Be Able To");
        sb.AppendLine();
        var abilities = (step2.EmployeeMustBeAbleTo ?? []).Distinct().ToList();
        if (abilities.Count > 0)
        {
            foreach (var ability in abilities)
            {
                sb.AppendLine($"- {ability}");
            }
        }
        else
        {
            sb.AppendLine($"- Perform {step2.WorkStep} according to standard process.");
        }
        sb.AppendLine();

        // 3. Required Classes and Engrams
        sb.AppendLine("## Required Classes and Engrams");
        sb.AppendLine();

        if (selectedClasses.Count == 0)
        {
            sb.AppendLine("*No approved catalog classes currently available for this step.*");
            sb.AppendLine();
        }
        else
        {
            for (var i = 0; i < selectedClasses.Count; i++)
            {
                var sc = selectedClasses[i];
                sb.AppendLine($"### Class {i + 1}: {sc.ClassName}");
                sb.AppendLine();
                sb.AppendLine($"**Learning Path:** {sc.LearningPathName}");
                sb.AppendLine();
                sb.AppendLine("**Relevant Engrams:**");
                sb.AppendLine();

                var engramNum = 1;
                foreach (var se in sc.SelectedEngrams)
                {
                    sb.AppendLine($"{engramNum++}. {se.EngramName}");
                }
                sb.AppendLine();
            }
        }

        // 4. How We Know the Employee Did It
        sb.AppendLine("## How We Know the Employee Did It");
        sb.AppendLine();
        sb.AppendLine("The employee provides or produces:");
        sb.AppendLine();
        var evidence = (step2.CompletionEvidence ?? []).Distinct().ToList();
        if (evidence.Count > 0)
        {
            foreach (var ev in evidence)
            {
                sb.AppendLine($"- {ev}");
            }
        }
        else
        {
            sb.AppendLine("- Documented completion evidence or verified work artifact.");
        }
        sb.AppendLine();

        // 5. How the Employee Performed
        sb.AppendLine("## How the Employee Performed");
        sb.AppendLine();
        sb.AppendLine("Select one:");
        sb.AppendLine();
        sb.AppendLine("- [ ] Completed independently");
        sb.AppendLine("- [ ] Completed with limited guidance");
        sb.AppendLine("- [ ] Completed with significant assistance");
        sb.AppendLine("- [ ] Not completed");
        sb.AppendLine();
        sb.AppendLine("### Reviewer Notes");
        sb.AppendLine();
        sb.AppendLine("**What the employee did well:**");
        sb.AppendLine();
        sb.AppendLine("[Write here]");
        sb.AppendLine();
        sb.AppendLine("**What the employee missed:**");
        sb.AppendLine();
        sb.AppendLine("[Write here]");
        sb.AppendLine();
        sb.AppendLine("**What assistance was required:**");
        sb.AppendLine();
        sb.AppendLine("[Write here]");
        sb.AppendLine();
        sb.AppendLine("**What should improve next time:**");
        sb.AppendLine();
        sb.AppendLine("[Write here]");
        sb.AppendLine();

        // 6. Engram Application
        sb.AppendLine("## Engram Application");
        sb.AppendLine();

        if (selectedClasses.Count == 0)
        {
            sb.AppendLine("*Pending catalog content.*");
            sb.AppendLine();
        }
        else
        {
            for (var i = 0; i < selectedClasses.Count; i++)
            {
                var sc = selectedClasses[i];
                sb.AppendLine($"### Class {i + 1}: {sc.ClassName}");
                sb.AppendLine();
                foreach (var se in sc.SelectedEngrams)
                {
                    sb.AppendLine($"- **{se.EngramName}:** Demonstrated / Partially Demonstrated / Not Demonstrated");
                }
                sb.AppendLine();
            }
        }

        // 7. Learning Content Gaps (only when gaps exist)
        if (step5.ContentGaps.Count > 0 || step6.DraftCreated)
        {
            sb.AppendLine("## Learning Content Gaps");
            sb.AppendLine();

            foreach (var gap in step5.ContentGaps)
            {
                var contentReq = gap.RecommendedGapType == "new_engram_in_existing_class"
                    ? "New Engram in an existing Class"
                    : "New Class";

                var impact = gap.Criticality == "critical"
                    ? "Critical gap. Complete preparation is blocked until the content is approved."
                    : "Supporting gap. The assignment may proceed.";

                sb.AppendLine($"- **Missing capability:** {gap.Requirement}");
                sb.AppendLine($"- **Content required:** {contentReq}");
                sb.AppendLine("- **Status:** Needs Expert Review");
                sb.AppendLine($"- **Impact:** {impact}");
                sb.AppendLine();
            }
        }

        // 8. Final Result
        sb.AppendLine("## Final Result");
        sb.AppendLine();
        sb.AppendLine("Select one:");
        sb.AppendLine();
        sb.AppendLine("- [ ] Ready to perform this step independently");
        sb.AppendLine("- [ ] Ready to perform this step with guidance");
        sb.AppendLine("- [ ] Needs additional preparation");
        sb.AppendLine("- [ ] Needs to repeat the step");
        sb.AppendLine();

        // 9. Recommended Next Action
        sb.AppendLine("## Recommended Next Action");
        sb.AppendLine();
        sb.AppendLine("- **If ready:** Move to the next work step.");
        sb.AppendLine("- **If partially ready:** Reinforce the relevant Engrams from the assigned Classes.");
        sb.AppendLine("- **If not ready:** Review the relevant Class content and repeat the work step with guidance.");

        return sb.ToString().TrimEnd();
    }
}
