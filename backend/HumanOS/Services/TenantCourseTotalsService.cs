using HumanOS.Contracts.Tenancy;

namespace HumanOS.Services;

/// <summary>Aggregates cost/time/media/engram totals across EVERY course
/// this tenant has created (2026-10-01) — one list fetch
/// (<see cref="TenantCoursesService"/>) then, per course, reuses the SAME
/// per-course cost-detail services already powering the single-course
/// detail page (<see cref="StructuredLearningCostService.GetDetailAsync"/>
/// / <see cref="CapabilityCostService.GetDetailAsync"/>) and the
/// engrams/chapters/facts summary (<see cref="StructuredLearningCourseSummaryService"/>),
/// summing everything client-side rather than re-querying usage rows
/// directly — keeps the two totals (single-course vs tenant-wide) always
/// consistent since they're built from the exact same numbers.</summary>
public sealed class TenantCourseTotalsService
{
    private readonly TenantCoursesService _coursesService;
    private readonly StructuredLearningCostService _structuredLearningCostService;
    private readonly CapabilityCostService _capabilityCostService;
    private readonly StructuredLearningCourseSummaryService _courseSummaryService;

    public TenantCourseTotalsService(
        TenantCoursesService coursesService,
        StructuredLearningCostService structuredLearningCostService,
        CapabilityCostService capabilityCostService,
        StructuredLearningCourseSummaryService courseSummaryService)
    {
        _coursesService = coursesService;
        _structuredLearningCostService = structuredLearningCostService;
        _capabilityCostService = capabilityCostService;
        _courseSummaryService = courseSummaryService;
    }

    public async Task<TenantCourseTotalsResponse> GetTotalsAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var courses = await _coursesService.GetTenantCoursesAsync(tenantId, cancellationToken);

        var response = new TenantCourseTotalsResponse { CourseCount = courses.Count };
        var stepsByKey = new Dictionary<(string StepName, string Models), TenantCourseTotalsStepResponse>();

        void AccumulateStep(string stepName, string models, long inputTokens, long outputTokens, long elapsedMs, int images, int videos, decimal costUsd)
        {
            var key = (stepName, models);
            if (!stepsByKey.TryGetValue(key, out var step))
            {
                step = new TenantCourseTotalsStepResponse { StepName = stepName, Models = models };
                stepsByKey[key] = step;
            }
            step.InputTokens += inputTokens;
            step.OutputTokens += outputTokens;
            step.TotalElapsedMilliseconds += elapsedMs;
            step.ImagesGeneratedCount += images;
            step.VideosGeneratedCount += videos;
            step.EstimatedCostUsd += costUsd;
        }

        foreach (var course in courses)
        {
            if (course.CourseType == "StructuredLearning")
            {
                var detail = await _structuredLearningCostService.GetDetailAsync(course.CourseId, cancellationToken);
                if (detail is null) continue;

                response.InputTokens += detail.InputTokens;
                response.OutputTokens += detail.OutputTokens;
                response.TotalElapsedMilliseconds += detail.TotalElapsedMilliseconds;
                response.ImagesGeneratedCount += detail.ImagesGeneratedCount;
                response.VideosGeneratedCount += detail.VideosGeneratedCount;
                response.EstimatedCostUsd += detail.EstimatedCostUsd;

                foreach (var step in detail.Steps)
                {
                    AccumulateStep(step.StepName, step.Models, step.InputTokens, step.OutputTokens, step.TotalElapsedMilliseconds, step.ImagesGeneratedCount, step.VideosGeneratedCount, step.EstimatedCostUsd);
                }

                var summary = await _courseSummaryService.GetSummaryAsync(course.CourseId, cancellationToken);
                if (summary is not null)
                {
                    response.EngramsCount += summary.EngramsCount;
                    response.ChaptersCount += summary.ChaptersCount;
                    response.FactsCount += summary.FactsCount;
                }
            }
            else if (course.CourseType == "Capability")
            {
                var detail = await _capabilityCostService.GetDetailAsync(course.CourseId, cancellationToken);
                if (detail is null) continue;

                response.InputTokens += detail.InputTokens;
                response.OutputTokens += detail.OutputTokens;
                response.ImagesGeneratedCount += detail.ImagesGeneratedCount;
                response.EstimatedCostUsd += detail.EstimatedCostUsd;

                foreach (var section in detail.Sections)
                {
                    AccumulateStep(section.SectionLabel, section.Models, section.InputTokens, section.OutputTokens, 0, 0, 0, section.EstimatedCostUsd);
                }
            }
        }

        response.EstimatedCostUsd = Math.Round(response.EstimatedCostUsd, 4);
        response.Steps = stepsByKey.Values
            .OrderByDescending(s => s.InputTokens + s.OutputTokens)
            .ToList();
        foreach (var step in response.Steps)
        {
            step.EstimatedCostUsd = Math.Round(step.EstimatedCostUsd, 4);
        }

        return response;
    }
}
