using FluentAssertions;
using HumanOS.Agents.Studio;
using Xunit;

namespace HumanOS.Tests.Agents.Studio;

/// <summary>
/// Paso 2 (2026-07-14) minimum test set for <see cref="BlueprintValidator"/>,
/// updated for the "Recall's two roles" model (corrected 2026-07-14, see
/// HUMAN-OS-STUDIO.md §16/§17): Recall as a transversal LEARNING MECHANISM
/// (every module's RecallRequirement, regardless of TargetMetric) versus
/// Recall as a VERIFIABLE METRIC (TargetMetric=Recall), reserved for
/// exactly one capstone module in the capability's final level — see
/// HUMAN-OS-STUDIO.md §11.
/// </summary>
public class BlueprintValidatorTests
{
    private static ModuleSkeleton Module(CapabilityMetric metric, string suffix = "") => new()
    {
        Title = $"Crear una agenda accionable{suffix}",
        Description = "El alumno transforma el objetivo de una reunión real en una agenda ejecutable.",
        Type = ModuleType.Practica,
        TargetMetric = metric,
        RecallRequirement =
            "Antes de consultar cualquier guía, el alumno recupera de memoria los componentes " +
            "que considera necesarios para convertir un objetivo en una agenda.",
        LearnerProduction =
            "Una agenda para una reunión real con propósito, resultado esperado, tiempos, " +
            "responsables y respuesta ante posibles fricciones.",
        SuccessCriteria =
        [
            "El propósito define una decisión o entrega concreta.",
            "Cada elemento de la agenda tiene objetivo, tiempo y responsable.",
            "El orden de los elementos conduce al resultado esperado.",
            "La agenda incluye una acción ante una fricción probable."
        ]
    };

    /// <summary>A single valid module — Application by default (never
    /// Recall, so tests using this helper don't accidentally satisfy/
    /// interfere with the single-capstone-at-the-end rule).</summary>
    private static ModuleSkeleton ValidModule() => Module(CapabilityMetric.Application);

    /// <summary>The capstone module — the only one allowed to have
    /// TargetMetric=Recall, meant to be placed in the FINAL level.</summary>
    private static ModuleSkeleton RecallCapstoneModule(string suffix = "-capstone") =>
        Module(CapabilityMetric.Recall, suffix);

    private static CapabilityBlueprint BlueprintWith(HumanEvolutionLayer layer, params ModuleSkeleton[] modules) => new()
    {
        CapabilityName = "Test Capability",
        Goal = "Test goal",
        ScopeDeclaration = "Scope: test.",
        Levels =
        [
            new CapabilityLevelBlueprint
            {
                Layer = layer,
                Title = "Level",
                HumanTransformation = "Transformation",
                Modules = [.. modules]
            }
        ]
    };

    /// <summary>A 2-level blueprint (Foundation, then Exploration as the
    /// FINAL level) with the capstone correctly placed last.</summary>
    private static CapabilityBlueprint TwoLevelBlueprintWithCapstoneAtTheEnd() => new()
    {
        CapabilityName = "Test Capability",
        Goal = "Test goal",
        ScopeDeclaration = "Scope: test.",
        Levels =
        [
            new CapabilityLevelBlueprint
            {
                Layer = HumanEvolutionLayer.Foundation,
                Title = "Foundation",
                HumanTransformation = "Transformation",
                Modules = [Module(CapabilityMetric.Application, "-1")]
            },
            new CapabilityLevelBlueprint
            {
                Layer = HumanEvolutionLayer.Exploration,
                Title = "Exploration",
                HumanTransformation = "Transformation",
                Modules = [Module(CapabilityMetric.Confidence, "-2"), RecallCapstoneModule()]
            }
        ]
    };

    [Fact]
    public void Validate_AcceptsModuleWithAllFourFieldsDeclared()
    {
        var blueprint = TwoLevelBlueprintWithCapstoneAtTheEnd();

        var act = () => BlueprintValidator.Validate(blueprint);

        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_RejectsModuleWithoutRecallRequirement()
    {
        var module = ValidModule();
        module.RecallRequirement = "";
        var blueprint = BlueprintWith(HumanEvolutionLayer.Foundation, module, RecallCapstoneModule());

        var act = () => BlueprintValidator.Validate(blueprint);

        act.Should().Throw<InvalidOperationException>().WithMessage("*RecallRequirement*");
    }

    [Fact]
    public void Validate_RejectsModuleWithoutLearnerProduction()
    {
        var module = ValidModule();
        module.LearnerProduction = "   ";
        var blueprint = BlueprintWith(HumanEvolutionLayer.Foundation, module, RecallCapstoneModule());

        var act = () => BlueprintValidator.Validate(blueprint);

        act.Should().Throw<InvalidOperationException>().WithMessage("*LearnerProduction*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(6)]
    public void Validate_RejectsModuleWithSuccessCriteriaCountOutOfRange(int count)
    {
        var module = ValidModule();
        module.SuccessCriteria = Enumerable.Range(1, count).Select(i => $"Criterion {i}").ToList();
        var blueprint = BlueprintWith(HumanEvolutionLayer.Foundation, module, RecallCapstoneModule());

        var act = () => BlueprintValidator.Validate(blueprint);

        act.Should().Throw<InvalidOperationException>().WithMessage("*SuccessCriteria*");
    }

    [Fact]
    public void Validate_RejectsModuleWithAnEmptySuccessCriterion()
    {
        var module = ValidModule();
        module.SuccessCriteria = ["A real criterion", "   "];
        var blueprint = BlueprintWith(HumanEvolutionLayer.Foundation, module, RecallCapstoneModule());

        var act = () => BlueprintValidator.Validate(blueprint);

        act.Should().Throw<InvalidOperationException>().WithMessage("*empty SuccessCriterion*");
    }

    [Fact]
    public void TargetMetric_IsAlwaysASingleValue_NeverAListOfMetrics()
    {
        // The data model itself makes "more than one TargetMetric" impossible —
        // it's a single CapabilityMetric, never a collection. Asserted here so a
        // future refactor can't silently turn it into a list without a test
        // failing (see SINGLE TARGET METRIC RULE in ArquitectoAgent's prompt).
        var property = typeof(ModuleSkeleton).GetProperty(nameof(ModuleSkeleton.TargetMetric));

        property!.PropertyType.Should().Be(typeof(CapabilityMetric));
    }

    [Theory]
    [InlineData(HumanEvolutionLayer.Professional)]
    [InlineData(HumanEvolutionLayer.Frontier)]
    [InlineData(HumanEvolutionLayer.Creator)]
    public void Validate_RejectsInactiveLevels(HumanEvolutionLayer inactiveLevel)
    {
        var blueprint = BlueprintWith(inactiveLevel, RecallCapstoneModule());

        var act = () => BlueprintValidator.Validate(blueprint);

        act.Should().Throw<InvalidOperationException>().WithMessage("*not active*");
    }

    [Theory]
    [InlineData(HumanEvolutionLayer.Foundation)]
    [InlineData(HumanEvolutionLayer.Exploration)]
    [InlineData(HumanEvolutionLayer.Mastery)]
    public void Validate_AcceptsActiveLevels(HumanEvolutionLayer activeLevel)
    {
        // A single-level blueprint is valid as long as that (only, hence
        // final) level contains the one required Recall capstone.
        var blueprint = BlueprintWith(activeLevel, ValidModule(), RecallCapstoneModule());

        var act = () => BlueprintValidator.Validate(blueprint);

        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_DoesNotTreatRecallRequirementAsVerifiedRecall()
    {
        // A module can freely declare a RecallRequirement while its
        // TargetMetric is something other than Recall — the validator must
        // never infer/force TargetMetric from the presence of a
        // RecallRequirement (they are independent concepts).
        var applicationModule = Module(CapabilityMetric.Application, "-1");
        applicationModule.RecallRequirement =
            "El alumno recupera de memoria los criterios antes de aplicarlos.";
        var blueprint = BlueprintWith(HumanEvolutionLayer.Foundation, applicationModule, RecallCapstoneModule());

        BlueprintValidator.Validate(blueprint);

        applicationModule.TargetMetric.Should().Be(CapabilityMetric.Application);
    }

    [Theory]
    [InlineData(CapabilityMetric.Knowledge)]
    [InlineData(CapabilityMetric.Retention)]
    [InlineData(CapabilityMetric.Fluency)]
    public void Validate_RejectsInactiveMetrics(CapabilityMetric inactiveMetric)
    {
        var blueprint = BlueprintWith(HumanEvolutionLayer.Foundation, Module(inactiveMetric), RecallCapstoneModule());

        var act = () => BlueprintValidator.Validate(blueprint);

        act.Should().Throw<InvalidOperationException>().WithMessage("*not active*");
    }

    [Theory]
    [InlineData(CapabilityMetric.Application)]
    [InlineData(CapabilityMetric.Confidence)]
    [InlineData(CapabilityMetric.Independence)]
    public void Validate_AcceptsActiveNonRecallMetricsFreelyDistributed(CapabilityMetric activeMetric)
    {
        // Corrected 2026-07-14: Application/Confidence/Independence have
        // no per-level mandatory-coverage rule — the Architect distributes
        // them freely. Only the Recall capstone placement is enforced.
        var blueprint = BlueprintWith(HumanEvolutionLayer.Foundation, Module(activeMetric), RecallCapstoneModule());

        var act = () => BlueprintValidator.Validate(blueprint);

        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_RejectsBlueprintWithNoRecallCapstoneAtAll()
    {
        var blueprint = BlueprintWith(
            HumanEvolutionLayer.Foundation,
            Module(CapabilityMetric.Application, "-1"),
            Module(CapabilityMetric.Confidence, "-2"));

        var act = () => BlueprintValidator.Validate(blueprint);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*exactly one capstone module with TargetMetric=Recall*");
    }

    [Fact]
    public void Validate_RejectsBlueprintWithMoreThanOneRecallModule()
    {
        var blueprint = BlueprintWith(
            HumanEvolutionLayer.Foundation,
            RecallCapstoneModule("-1"),
            Module(CapabilityMetric.Application, "-2"),
            RecallCapstoneModule("-3"));

        var act = () => BlueprintValidator.Validate(blueprint);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*exactly one module with TargetMetric=Recall*found 2*");
    }

    [Fact]
    public void Validate_RejectsRecallModuleThatIsNotInTheFinalLevel()
    {
        var blueprint = new CapabilityBlueprint
        {
            CapabilityName = "Test Capability",
            Goal = "Test goal",
            ScopeDeclaration = "Scope: test.",
            Levels =
            [
                new CapabilityLevelBlueprint
                {
                    Layer = HumanEvolutionLayer.Foundation,
                    Title = "Foundation",
                    HumanTransformation = "Transformation",
                    Modules = [RecallCapstoneModule()]
                },
                new CapabilityLevelBlueprint
                {
                    Layer = HumanEvolutionLayer.Exploration,
                    Title = "Exploration",
                    HumanTransformation = "Transformation",
                    Modules = [Module(CapabilityMetric.Application, "-2")]
                }
            ]
        };

        var act = () => BlueprintValidator.Validate(blueprint);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*must be in the final level*");
    }

    [Fact]
    public void Validate_AcceptsRecallCapstoneCorrectlyPlacedInTheFinalLevel()
    {
        var blueprint = TwoLevelBlueprintWithCapstoneAtTheEnd();

        var act = () => BlueprintValidator.Validate(blueprint);

        act.Should().NotThrow();
    }
}



