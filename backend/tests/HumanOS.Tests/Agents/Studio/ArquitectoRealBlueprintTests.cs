using System.Text.Json;
using FluentAssertions;
using HumanOS.Agents.Studio;
using Xunit;

namespace HumanOS.Tests.Agents.Studio;

/// <summary>
/// Paso 2 (2026-07-14) — validates the Paso 2 contract (TargetMetric,
/// RecallRequirement, LearnerProduction, SuccessCriteria, active-levels-only)
/// against a REAL blueprint captured from a live run of the actual backend
/// (Azure Functions host + real Azure OpenAI ArquitectoAgent call), not
/// hand-typed synthetic data. Captured via
/// <c>backend/HumanOS/test-paso2-blueprint.ps1</c> against
/// <c>POST /api/studio/capability-creation/start</c> for the goal "Aprender
/// a facilitar reuniones de trabajo efectivas..." — the fixture is the
/// Arquitecto's actual structured-output response, unedited except for
/// being lifted out of the run-status envelope. Re-captured 2026-07-14
/// (same day, later run) after correcting the "Memory Paradox" model: ALL
/// 4 active metrics are mandatory at EVERY active level (not a
/// level-specific subset) — this fixture's 3 levels each contain a
/// Recall, Application, Confidence, and Independence module.
/// </summary>
public class ArquitectoRealBlueprintTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    private static CapabilityBlueprint LoadRealBlueprint()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Agents", "Studio", "Fixtures", "real-paso2-blueprint.json");
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<CapabilityBlueprint>(json, JsonOptions)!;
    }

    [Fact]
    public void RealBlueprint_PassesValidationAsIs()
    {
        var blueprint = LoadRealBlueprint();

        var act = () => BlueprintValidator.Validate(blueprint);

        act.Should().NotThrow();
    }

    [Fact]
    public void RealBlueprint_OnlyUsesActiveLevels()
    {
        var blueprint = LoadRealBlueprint();

        blueprint.Levels.Select(l => l.Layer).Should().OnlyContain(
            layer => BlueprintValidator.ActiveLevels.Contains(layer));
        blueprint.Levels.Should().Contain(l => l.Layer == HumanEvolutionLayer.Foundation);
    }

    [Fact]
    public void RealBlueprint_EveryModuleDeclaresExactlyOneTargetMetricAndAFullContract()
    {
        var blueprint = LoadRealBlueprint();
        var modules = blueprint.Levels.SelectMany(l => l.Modules).ToList();

        modules.Should().NotBeEmpty();

        foreach (var module in modules)
        {
            // TargetMetric is structurally a single enum value, never a list —
            // this loop asserts every real module actually has one assigned.
            module.TargetMetric.Should().BeDefined();
            module.RecallRequirement.Should().NotBeNullOrWhiteSpace();
            module.LearnerProduction.Should().NotBeNullOrWhiteSpace();
            module.SuccessCriteria.Should().HaveCountGreaterThanOrEqualTo(2)
                .And.HaveCountLessThanOrEqualTo(5);
            module.SuccessCriteria.Should().OnlyContain(c => !string.IsNullOrWhiteSpace(c));
        }
    }

    [Fact]
    public void RealBlueprint_RecallRequirementIsPresentEvenWhenTargetMetricIsNotRecall()
    {
        var blueprint = LoadRealBlueprint();
        var modules = blueprint.Levels.SelectMany(l => l.Modules).ToList();

        // Real proof that RecallRequirement (transversal contract) is independent
        // from TargetMetric == Recall: modules targeting Application/Confidence/
        // Independence in the real run still declare a RecallRequirement.
        var nonRecallModules = modules.Where(m => m.TargetMetric != CapabilityMetric.Recall).ToList();

        nonRecallModules.Should().NotBeEmpty();
        nonRecallModules.Should().OnlyContain(m => !string.IsNullOrWhiteSpace(m.RecallRequirement));
    }

    [Fact]
    public void RealBlueprint_HasExactlyOneRecallCapstoneInTheFinalLevel()
    {
        var blueprint = LoadRealBlueprint();

        // Recall as TargetMetric is reserved for a single capstone module,
        // placed in the final level — confirms it wasn't assigned to every
        // level just because RecallRequirement (transversal) exists there too.
        var recallModules = blueprint.Levels.SelectMany(l => l.Modules)
            .Count(m => m.TargetMetric == CapabilityMetric.Recall);

        recallModules.Should().Be(1);
        blueprint.Levels[^1].Modules.Should().Contain(m => m.TargetMetric == CapabilityMetric.Recall);
    }

    [Fact]
    public void RealBlueprint_OnlyUsesActiveMetrics()
    {
        var blueprint = LoadRealBlueprint();

        blueprint.Levels.SelectMany(l => l.Modules).Select(m => m.TargetMetric)
            .Should().OnlyContain(metric => BlueprintValidator.ActiveMetrics.Contains(metric));
    }

}
