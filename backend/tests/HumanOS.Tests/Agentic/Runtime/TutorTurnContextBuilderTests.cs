using FluentAssertions;
using HumanOS.Agentic.Runtime;
using HumanOS.Agents.Runtime;
using HumanOS.Agents.Studio;
using Xunit;

namespace HumanOS.Tests.Agentic.Runtime;

/// <summary>
/// Verifies the structural anti-offloading rule (2026-07-14, see
/// /memories/repo/human-os-runtime-design.md): knowledge/tool access must
/// be gated by <see cref="RuntimeStage"/>, not left to the Tutor's own
/// judgment. Recall/Prediction must NEVER allow knowledge access,
/// regardless of future prompt changes.
/// </summary>
public class TutorTurnContextBuilderTests
{
    private static RuntimeSessionState NewState(RuntimeStage stage) => new()
    {
        Session = new RuntimeSession
        {
            RuntimeSessionId = Guid.NewGuid(),
            PersonId = Guid.NewGuid(),
            CapabilityModuleId = Guid.NewGuid(),
            Stage = stage,
            Contract = new RuntimePedagogicalContract
            {
                CapabilityModuleId = Guid.NewGuid(),
                TargetMetric = CapabilityMetric.Recall,
                RecallRequirement = "req",
                LearnerProduction = "prod",
                SuccessCriteria = ["criterion"]
            }
        }
    };

    [Theory]
    [InlineData(RuntimeStage.RecallRequired)]
    [InlineData(RuntimeStage.PredictionRequired)]
    public void RecallAndPrediction_NeverAllowKnowledgeAccess_AndHaveNoTools(RuntimeStage stage)
    {
        var context = TutorTurnContextBuilder.Build(NewState(stage), stage);

        context.Permissions.KnowledgeAccessAllowed.Should().BeFalse();
        context.Permissions.AllowedTools.Should().BeEmpty();
    }

    [Fact]
    public void LearnerProduction_AllowsComputationalTools_ButNotKnowledgeAccess()
    {
        var context = TutorTurnContextBuilder.Build(NewState(RuntimeStage.LearnerProduction), RuntimeStage.LearnerProduction);

        context.Permissions.KnowledgeAccessAllowed.Should().BeFalse();
        context.Permissions.AllowedTools.Should().Contain([TutorTool.Calculator, TutorTool.TableReader]);
    }

    [Fact]
    public void Instruction_AllowsKnowledgeAccess_AndCarriesTheModuleScript()
    {
        var context = TutorTurnContextBuilder.Build(
            NewState(RuntimeStage.Instruction), RuntimeStage.Instruction, moduleScript: "the real script");

        context.Permissions.KnowledgeAccessAllowed.Should().BeTrue();
        context.ModuleScript.Should().Be("the real script");
    }

    [Fact]
    public void ModuleScript_IsNullOutsideInstructionStage_EvenIfProvided()
    {
        var context = TutorTurnContextBuilder.Build(
            NewState(RuntimeStage.Reflection), RuntimeStage.Reflection, moduleScript: "should be dropped");

        context.ModuleScript.Should().BeNull();
    }

    [Fact]
    public void Build_CarriesAccumulatedEvidenceAndFixedContract()
    {
        var state = NewState(RuntimeStage.Assessment);
        var evidence = new StudentEvidence
        {
            RuntimeSessionId = state.Session.RuntimeSessionId,
            CapabilityModuleId = state.Session.CapabilityModuleId,
            Origin = StudentEvidenceOrigin.Recall
        };
        state.Session.Evidence.Add(evidence);

        var context = TutorTurnContextBuilder.Build(state, RuntimeStage.Assessment);

        context.AccumulatedEvidence.Should().ContainSingle().Which.Should().Be(evidence);
        context.Contract.Should().Be(state.Session.Contract);
    }
}
