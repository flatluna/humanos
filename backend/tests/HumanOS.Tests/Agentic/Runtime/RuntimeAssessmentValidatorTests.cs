using FluentAssertions;
using HumanOS.Agentic.Runtime;
using HumanOS.Agents.Runtime;
using HumanOS.Agents.Studio;
using Xunit;

namespace HumanOS.Tests.Agentic.Runtime;

/// <summary>
/// Paso 6 (2026-07-14) — verifies <see cref="RuntimeAssessmentValidator"/>,
/// the Runtime's deterministic safety net mirroring Studio's proven
/// <c>MetricVerificationValidator</c>, per
/// /memories/repo/human-os-runtime-design.md.
/// </summary>
public class RuntimeAssessmentValidatorTests
{
    private static RuntimePedagogicalContract Contract(
        CapabilityMetric targetMetric = CapabilityMetric.Application,
        params string[] criteria) => new()
    {
        CapabilityModuleId = Guid.NewGuid(),
        TargetMetric = targetMetric,
        RecallRequirement = "req",
        LearnerProduction = "prod",
        SuccessCriteria = criteria.Length == 0 ? ["Criterion A", "Criterion B"] : criteria
    };

    private static RuntimeAssessmentResult ValidVerifiedResult(CapabilityMetric targetMetric = CapabilityMetric.Application) => new()
    {
        TargetMetric = targetMetric,
        Status = MetricVerificationStatus.Verified,
        Explanation = "Meets all criteria.",
        SuccessCriteriaResults =
        [
            new SuccessCriterionAssessment { Criterion = "Criterion A", IsSatisfied = true, Evidence = "evidence A" },
            new SuccessCriterionAssessment { Criterion = "Criterion B", IsSatisfied = true, Evidence = "evidence B" }
        ]
    };

    [Fact]
    public void Validate_AcceptsWellFormedVerifiedResult()
    {
        var act = () => RuntimeAssessmentValidator.Validate(Contract(), ValidVerifiedResult());

        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_Throws_WhenTargetMetricChanged()
    {
        var result = ValidVerifiedResult(CapabilityMetric.Confidence);

        var act = () => RuntimeAssessmentValidator.Validate(Contract(CapabilityMetric.Application), result);

        act.Should().Throw<InvalidOperationException>().WithMessage("*TargetMetric*");
    }

    [Fact]
    public void Validate_Throws_WhenSuccessCriteriaCountMismatch()
    {
        var result = ValidVerifiedResult();
        result.SuccessCriteriaResults.RemoveAt(0);

        var act = () => RuntimeAssessmentValidator.Validate(Contract(), result);

        act.Should().Throw<InvalidOperationException>().WithMessage("*evaluated*");
    }

    [Fact]
    public void Validate_Throws_WhenAnyCriterionEvidenceIsBlank()
    {
        var result = ValidVerifiedResult();
        result.SuccessCriteriaResults[0].Evidence = "  ";

        var act = () => RuntimeAssessmentValidator.Validate(Contract(), result);

        act.Should().Throw<InvalidOperationException>().WithMessage("*evidence*");
    }

    [Fact]
    public void Validate_Throws_WhenVerifiedWithoutExplanation()
    {
        var result = ValidVerifiedResult();
        result.Explanation = "";

        var act = () => RuntimeAssessmentValidator.Validate(Contract(), result);

        act.Should().Throw<InvalidOperationException>().WithMessage("*explanation*");
    }

    [Fact]
    public void Validate_Throws_WhenVerifiedButACriterionFailed()
    {
        var result = ValidVerifiedResult();
        result.SuccessCriteriaResults[1].IsSatisfied = false;

        var act = () => RuntimeAssessmentValidator.Validate(Contract(), result);

        act.Should().Throw<InvalidOperationException>().WithMessage("*not satisfied*");
    }

    [Fact]
    public void Validate_AllowsNotVerified_EvenWithAFailedCriterion_AndNoExplanation()
    {
        var result = ValidVerifiedResult();
        result.Status = MetricVerificationStatus.NotVerified;
        result.Explanation = "";
        result.SuccessCriteriaResults[1].IsSatisfied = false;

        var act = () => RuntimeAssessmentValidator.Validate(Contract(), result);

        act.Should().NotThrow();
    }
}
