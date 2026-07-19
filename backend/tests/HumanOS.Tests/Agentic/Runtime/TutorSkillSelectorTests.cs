using FluentAssertions;
using HumanOS.Agentic.Runtime;
using HumanOS.Agents.Runtime;
using HumanOS.Agents.Studio;
using Xunit;

namespace HumanOS.Tests.Agentic.Runtime;

/// <summary>
/// Paso 5 (2026-07-14) — verifies <see cref="TutorSkillSelector"/> maps
/// RuntimeStage (+ TargetMetric for LearnerProduction) to the correct
/// <see cref="TutorSkill"/>, per /memories/repo/human-os-runtime-design.md.
/// </summary>
public class TutorSkillSelectorTests
{
    [Theory]
    [InlineData(RuntimeStage.RecallRequired, TutorSkill.Recall)]
    [InlineData(RuntimeStage.PredictionRequired, TutorSkill.Prediction)]
    [InlineData(RuntimeStage.Reflection, TutorSkill.Reflection)]
    public void Select_StageDrivenSkills_IgnoreTargetMetric(RuntimeStage stage, TutorSkill expected)
    {
        foreach (var metric in Enum.GetValues<CapabilityMetric>())
        {
            TutorSkillSelector.Select(stage, metric).Should().Be(expected);
        }
    }

    [Theory]
    [InlineData(CapabilityMetric.Application, TutorSkill.Application)]
    [InlineData(CapabilityMetric.Confidence, TutorSkill.Confidence)]
    [InlineData(CapabilityMetric.Independence, TutorSkill.Independence)]
    [InlineData(CapabilityMetric.Recall, TutorSkill.Recall)]
    public void Select_LearnerProduction_DependsOnTargetMetric(CapabilityMetric metric, TutorSkill expected)
    {
        TutorSkillSelector.Select(RuntimeStage.LearnerProduction, metric).Should().Be(expected);
    }

    [Fact]
    public void Select_LearnerProduction_ReturnsNull_ForMetricsWithNoSkillYet()
    {
        TutorSkillSelector.Select(RuntimeStage.LearnerProduction, CapabilityMetric.Knowledge).Should().BeNull();
        TutorSkillSelector.Select(RuntimeStage.LearnerProduction, CapabilityMetric.Retention).Should().BeNull();
        TutorSkillSelector.Select(RuntimeStage.LearnerProduction, CapabilityMetric.Fluency).Should().BeNull();
    }

    [Theory]
    [InlineData(RuntimeStage.ModuleStarted)]
    [InlineData(RuntimeStage.Instruction)]
    [InlineData(RuntimeStage.Assessment)]
    [InlineData(RuntimeStage.Completed)]
    public void Select_StagesWithNoSkill_ReturnNull(RuntimeStage stage)
    {
        TutorSkillSelector.Select(stage, CapabilityMetric.Application).Should().BeNull();
    }
}
