using FluentAssertions;
using HumanOS.Agents.Studio;
using Xunit;

namespace HumanOS.Tests.Agents.Studio;

/// <summary>
/// Paso 3 (2026-07-14) minimum test set for <see cref="ModuleScriptValidator"/>
/// — see HUMAN-OS-STUDIO.md §12.
/// </summary>
public class ModuleScriptValidatorTests
{
    private static ModuleSkeleton ApprovedModule(CapabilityMetric targetMetric = CapabilityMetric.Application) => new()
    {
        Title = "Crear una agenda accionable",
        Description = "El alumno transforma el objetivo de una reunión real en una agenda ejecutable.",
        Type = ModuleType.Practica,
        TargetMetric = targetMetric,
        RecallRequirement = "El alumno recupera de memoria los componentes de una agenda accionable.",
        LearnerProduction = "Una agenda para una reunión real con propósito, resultado, tiempos y responsables.",
        SuccessCriteria =
        [
            "El propósito define una decisión o entrega concreta.",
            "Cada elemento tiene objetivo, tiempo y responsable."
        ]
    };

    private static ModuleScript ValidScript(
        CapabilityMetric targetMetric = CapabilityMetric.Application,
        RecallSupportLevel supportLevel = RecallSupportLevel.WithCues) => new()
    {
        Script = "Antes de leer, recuerda de memoria los componentes de una agenda accionable...",
        TargetMetric = targetMetric,
        RecallActivity = new RecallActivity
        {
            Instructions = "Antes de consultar la guía, escribe de memoria los componentes de una agenda.",
            OccursBeforeInstruction = true,
            SupportLevel = supportLevel
        },
        LearnerTask = "Crea una agenda para una reunión real con propósito, resultado, tiempos y responsables.",
        SuccessCriteria =
        [
            "El propósito define una decisión o entrega concreta.",
            "Cada elemento tiene objetivo, tiempo y responsable."
        ]
    };

    [Fact]
    public void Validate_AcceptsScriptThatPreservesTargetMetric()
    {
        var module = ApprovedModule(CapabilityMetric.Application);
        var script = ValidScript(CapabilityMetric.Application);

        var act = () => ModuleScriptValidator.Validate(HumanEvolutionLayer.Foundation, module, script);

        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_RejectsScriptThatChangesTargetMetric()
    {
        var module = ApprovedModule(CapabilityMetric.Application);
        var script = ValidScript(CapabilityMetric.Confidence);

        var act = () => ModuleScriptValidator.Validate(HumanEvolutionLayer.Foundation, module, script);

        act.Should().Throw<InvalidOperationException>().WithMessage("*changed the approved TargetMetric*");
    }

    [Fact]
    public void Validate_RejectsScriptWithoutRecallInstructions()
    {
        var module = ApprovedModule();
        var script = ValidScript();
        script.RecallActivity.Instructions = "  ";

        var act = () => ModuleScriptValidator.Validate(HumanEvolutionLayer.Foundation, module, script);

        act.Should().Throw<InvalidOperationException>().WithMessage("*explicit Recall activity*");
    }

    [Fact]
    public void Validate_RejectsRecallThatDoesNotOccurBeforeInstruction()
    {
        var module = ApprovedModule();
        var script = ValidScript();
        script.RecallActivity.OccursBeforeInstruction = false;

        var act = () => ModuleScriptValidator.Validate(HumanEvolutionLayer.Foundation, module, script);

        act.Should().Throw<InvalidOperationException>().WithMessage("*must occur before instruction*");
    }

    [Fact]
    public void Validate_RejectsScriptWithoutLearnerTask()
    {
        var module = ApprovedModule();
        var script = ValidScript();
        script.LearnerTask = "";

        var act = () => ModuleScriptValidator.Validate(HumanEvolutionLayer.Foundation, module, script);

        act.Should().Throw<InvalidOperationException>().WithMessage("*observable learner production*");
    }

    [Fact]
    public void Validate_RejectsFewerThanTwoSuccessCriteria()
    {
        var module = ApprovedModule();
        var script = ValidScript();
        script.SuccessCriteria = ["Only one criterion"];

        var act = () => ModuleScriptValidator.Validate(HumanEvolutionLayer.Foundation, module, script);

        act.Should().Throw<InvalidOperationException>().WithMessage("*at least two SuccessCriteria*");
    }

    [Fact]
    public void Validate_Foundation_AllowsCuesAfterFirstAttempt()
    {
        var module = ApprovedModule();
        var script = ValidScript(supportLevel: RecallSupportLevel.WithCues);

        var act = () => ModuleScriptValidator.Validate(HumanEvolutionLayer.Foundation, module, script);

        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_Exploration_RetrievesCriteriaBeforeApplying_AllowsEitherSupportLevel()
    {
        var module = ApprovedModule();

        ModuleScriptValidator.Validate(
            HumanEvolutionLayer.Exploration, module, ValidScript(supportLevel: RecallSupportLevel.WithCues));
        ModuleScriptValidator.Validate(
            HumanEvolutionLayer.Exploration, module, ValidScript(supportLevel: RecallSupportLevel.WithoutCues));
    }

    [Fact]
    public void Validate_Mastery_RequiresRecallWithoutCues()
    {
        var module = ApprovedModule();
        var script = ValidScript(supportLevel: RecallSupportLevel.WithCues);

        var act = () => ModuleScriptValidator.Validate(HumanEvolutionLayer.Mastery, module, script);

        act.Should().Throw<InvalidOperationException>().WithMessage("*Mastery requires Recall without cues*");
    }

    [Fact]
    public void Validate_Mastery_AcceptsRecallWithoutCues()
    {
        var module = ApprovedModule();
        var script = ValidScript(supportLevel: RecallSupportLevel.WithoutCues);

        var act = () => ModuleScriptValidator.Validate(HumanEvolutionLayer.Mastery, module, script);

        act.Should().NotThrow();
    }

    [Fact]
    public void AgentTokenUsage_TotalTokens_SumsInputAndOutput()
    {
        var usage = new AgentTokenUsage
        {
            AgentName = "Instructor",
            ModuleId = "module-1",
            InputTokens = 120,
            OutputTokens = 340
        };

        usage.TotalTokens.Should().Be(460);
    }
}
