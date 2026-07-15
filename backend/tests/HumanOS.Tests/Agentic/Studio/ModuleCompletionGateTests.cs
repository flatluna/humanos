using FluentAssertions;
using HumanOS.Agentic.Studio;
using HumanOS.Agents.Studio;
using Xunit;

namespace HumanOS.Tests.Agentic.Studio;

/// <summary>
/// Paso 5 (2026-07-14) tests for <see cref="ModuleCompletionGate"/> —
/// threshold relaxed from 100% to <see cref="ModuleCompletionGate.MinVerifiedRatio"/>
/// (85%) in Paso 7 (2026-07-14, see HUMAN-OS-STUDIO.md §16). Exercises the
/// real predicates wired into the workflow's conditional edges. Exercises
/// the actual production logic (enabled via <c>[InternalsVisibleTo]</c> in
/// HumanOS.csproj), not a reimplementation in test code.
/// </summary>
public class ModuleCompletionGateTests
{
    private static CompletedModule Module(ModuleProcessingStatus status) => new()
    {
        Module = new ModuleSkeleton { Title = "Test module" },
        Script = new ModuleScript(),
        Metrics = new ModuleMetricAssignment(),
        Status = status
    };

    private static ModuleRouterOutput CompletedWith(params ModuleProcessingStatus[] statuses) => new()
    {
        Completed = new AllModulesCompleted
        {
            BlueprintId = Guid.NewGuid(),
            Modules = statuses.Select(Module).ToList()
        }
    };

    [Fact]
    public void MeetsPublishThreshold_TrueWhenEveryModuleIsVerified()
    {
        var message = CompletedWith(ModuleProcessingStatus.Verified, ModuleProcessingStatus.Verified);

        ModuleCompletionGate.MeetsPublishThreshold(message).Should().BeTrue();
        ModuleCompletionGate.RequiresRevision(message).Should().BeFalse();
    }

    [Fact]
    public void MeetsPublishThreshold_TrueForZeroModules()
    {
        var message = CompletedWith();

        ModuleCompletionGate.MeetsPublishThreshold(message).Should().BeTrue();
    }

    [Fact]
    public void RequiresRevision_TrueWhenAnyModuleRequiresRevision()
    {
        var message = CompletedWith(ModuleProcessingStatus.Verified, ModuleProcessingStatus.RequiresRevision);

        ModuleCompletionGate.MeetsPublishThreshold(message).Should().BeFalse();
        ModuleCompletionGate.RequiresRevision(message).Should().BeTrue();
    }

    [Fact]
    public void RequiresRevision_TrueWhenAnyModuleFailed()
    {
        var message = CompletedWith(ModuleProcessingStatus.Verified, ModuleProcessingStatus.Failed);

        ModuleCompletionGate.MeetsPublishThreshold(message).Should().BeFalse();
        ModuleCompletionGate.RequiresRevision(message).Should().BeTrue();
    }

    [Fact]
    public void NeitherPredicateFiresBeforeGenerationIsDone()
    {
        var message = new ModuleRouterOutput
        {
            NextModule = new ModuleWorkItem
            {
                BlueprintId = Guid.NewGuid(),
                Layer = HumanEvolutionLayer.Foundation,
                Module = new ModuleSkeleton { Title = "Still pending" }
            }
        };

        ModuleCompletionGate.MeetsPublishThreshold(message).Should().BeFalse();
        ModuleCompletionGate.RequiresRevision(message).Should().BeFalse();
    }

    [Fact]
    public void ExperienciaDoesNotRunWhileVerifiedRatioIsBelowThreshold()
    {
        // Mirrors the workflow's real conditional edges: Experiencia is
        // only reachable via MeetsPublishThreshold — this asserts the gate
        // blocks it for a 50% mix, well below the 85% threshold.
        var mixedOutcome = CompletedWith(
            ModuleProcessingStatus.Verified, ModuleProcessingStatus.ScriptGenerated);

        ModuleCompletionGate.MeetsPublishThreshold(mixedOutcome).Should().BeFalse();
    }

    [Fact]
    public void ExperienciaRunsWhenAllModulesAreVerified()
    {
        var allVerified = CompletedWith(
            ModuleProcessingStatus.Verified, ModuleProcessingStatus.Verified, ModuleProcessingStatus.Verified);

        ModuleCompletionGate.MeetsPublishThreshold(allVerified).Should().BeTrue();
    }

    [Fact]
    public void MeetsPublishThreshold_TrueAtExactly85Percent()
    {
        // 17/20 = 85% exactly — the Paso 7 capability-level threshold, NOT
        // 100%: this is the key new behavior that lets a run publish with
        // some modules still RequiresRevision, without touching what
        // "Verified" means for any individual module.
        var statuses = Enumerable.Repeat(ModuleProcessingStatus.Verified, 17)
            .Concat(Enumerable.Repeat(ModuleProcessingStatus.RequiresRevision, 3))
            .ToArray();
        var message = CompletedWith(statuses);

        ModuleCompletionGate.MeetsPublishThreshold(message).Should().BeTrue();
        ModuleCompletionGate.RequiresRevision(message).Should().BeFalse();
    }

    [Fact]
    public void RequiresRevision_TrueJustBelow85Percent()
    {
        // 16/20 = 80% — below the 85% threshold.
        var statuses = Enumerable.Repeat(ModuleProcessingStatus.Verified, 16)
            .Concat(Enumerable.Repeat(ModuleProcessingStatus.RequiresRevision, 4))
            .ToArray();
        var message = CompletedWith(statuses);

        ModuleCompletionGate.MeetsPublishThreshold(message).Should().BeFalse();
        ModuleCompletionGate.RequiresRevision(message).Should().BeTrue();
    }
}
