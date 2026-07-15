using FluentAssertions;
using HumanOS.Agents.Studio;
using Xunit;

namespace HumanOS.Tests.Agents.Studio;

/// <summary>
/// Paso 5 (2026-07-14) minimum test set for <see cref="CompletedModuleValidator"/>
/// — see HUMAN-OS-STUDIO.md §14.
/// </summary>
public class CompletedModuleValidatorTests
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
        CapabilityMetric targetMetric,
        RecallSupportLevel supportLevel = RecallSupportLevel.WithCues,
        bool occursBeforeInstruction = true) => new()
    {
        Script = "Antes de leer, recuerda de memoria los componentes de una agenda accionable...",
        TargetMetric = targetMetric,
        RecallActivity = new RecallActivity
        {
            Instructions = "Antes de consultar la guía, escribe de memoria los componentes de una agenda.",
            OccursBeforeInstruction = occursBeforeInstruction,
            SupportLevel = supportLevel
        },
        LearnerTask = "Crea una agenda para una reunión real con propósito, resultado, tiempos y responsables.",
        SuccessCriteria =
        [
            "El propósito define una decisión o entrega concreta.",
            "Cada elemento tiene objetivo, tiempo y responsable."
        ]
    };

    private static MetricVerification Verification(
        ModuleSkeleton module,
        MetricVerificationStatus status,
        bool allCriteriaSatisfied = true) => new()
    {
        ModuleId = module.ModuleId.ToString(),
        TargetMetric = module.TargetMetric,
        Status = status,
        Evidence = "El alumno produce una agenda real con propósito, tiempos y responsables.",
        EvidenceLocation = "Sección \"Tarea obligatoria\".",
        SuccessCriteriaResults = module.SuccessCriteria
            .Select(c => new SuccessCriterionResult
            {
                Criterion = c,
                IsSatisfied = allCriteriaSatisfied,
                Evidence = "El LearnerTask exige explícitamente este componente en la agenda."
            })
            .ToList(),
        Recall = new RecallVerification
        {
            Status = RecallVerificationStatus.WithCues,
            Evidence = "El alumno recupera de memoria antes de recibir la guía.",
            EvidenceLocation = "Sección \"Recuperación\".",
            OccursBeforeInstruction = true
        },
        Explanation = "La producción exige aplicar los criterios a material real del alumno."
    };

    [Fact]
    public void Validate_VerifiedResultAllowsAdvancing()
    {
        var module = ApprovedModule();
        var script = ValidScript(module.TargetMetric);
        var verification = Verification(module, MetricVerificationStatus.Verified);

        var status = CompletedModuleValidator.Validate(module, script, verification, HumanEvolutionLayer.Foundation);

        status.Should().Be(ModuleProcessingStatus.Verified);
    }

    [Fact]
    public void Validate_NotVerifiedNeverBecomesVerified()
    {
        var module = ApprovedModule();
        var script = ValidScript(module.TargetMetric);
        var verification = Verification(module, MetricVerificationStatus.NotVerified);

        var status = CompletedModuleValidator.Validate(module, script, verification, HumanEvolutionLayer.Foundation);

        status.Should().NotBe(ModuleProcessingStatus.Verified);
        status.Should().Be(ModuleProcessingStatus.RequiresRevision);
    }

    [Fact]
    public void Validate_NotVerifiedProducesRequiresRevision_NotFailed()
    {
        var module = ApprovedModule(CapabilityMetric.Independence);
        var script = ValidScript(module.TargetMetric);
        var verification = Verification(module, MetricVerificationStatus.NotVerified);

        var status = CompletedModuleValidator.Validate(module, script, verification, HumanEvolutionLayer.Foundation);

        status.Should().Be(ModuleProcessingStatus.RequiresRevision);
    }

    [Fact]
    public void Validate_FailedRequiredCriterionBlocksTheModule()
    {
        var module = ApprovedModule();
        var script = ValidScript(module.TargetMetric);
        // A Verified status with a failed criterion should never legitimately
        // occur (Paso 4's MetricVerificationValidator already rejects it at
        // the source) but is still guarded here as a hard invariant.
        var verification = Verification(module, MetricVerificationStatus.Verified, allCriteriaSatisfied: false);

        var status = CompletedModuleValidator.Validate(module, script, verification, HumanEvolutionLayer.Foundation);

        status.Should().Be(ModuleProcessingStatus.RequiresRevision);
    }

    [Fact]
    public void Validate_DifferentTargetMetricBetweenAgentsBlocksTheModule()
    {
        var module = ApprovedModule(CapabilityMetric.Application);
        var script = ValidScript(CapabilityMetric.Application);
        var verification = Verification(module, MetricVerificationStatus.Verified);
        verification.TargetMetric = CapabilityMetric.Confidence;

        var act = () => CompletedModuleValidator.Validate(module, script, verification, HumanEvolutionLayer.Foundation);

        act.Should().Throw<InvalidOperationException>().WithMessage("*Métrico changed the approved TargetMetric*");
    }

    [Fact]
    public void Validate_InstructorChangedTargetMetricBlocksTheModule()
    {
        var module = ApprovedModule(CapabilityMetric.Application);
        var script = ValidScript(CapabilityMetric.Confidence);
        var verification = Verification(module, MetricVerificationStatus.Verified);

        var act = () => CompletedModuleValidator.Validate(module, script, verification, HumanEvolutionLayer.Foundation);

        act.Should().Throw<InvalidOperationException>().WithMessage("*Instructor changed the approved TargetMetric*");
    }

    [Fact]
    public void Validate_RecallAfterInstructionBlocksTheModule()
    {
        var module = ApprovedModule();
        var script = ValidScript(module.TargetMetric, occursBeforeInstruction: false);
        var verification = Verification(module, MetricVerificationStatus.Verified);

        var act = () => CompletedModuleValidator.Validate(module, script, verification, HumanEvolutionLayer.Foundation);

        act.Should().Throw<InvalidOperationException>().WithMessage("*Recall does not occur before instruction*");
    }

    [Fact]
    public void Validate_MasteryWithCuedRecallBlocksTheModule()
    {
        var module = ApprovedModule();
        var script = ValidScript(module.TargetMetric, supportLevel: RecallSupportLevel.WithCues);
        var verification = Verification(module, MetricVerificationStatus.Verified);

        var act = () => CompletedModuleValidator.Validate(module, script, verification, HumanEvolutionLayer.Mastery);

        act.Should().Throw<InvalidOperationException>().WithMessage("*Mastery requires Recall without cues*");
    }

    [Fact]
    public void Validate_MasteryWithoutCuesIsAcceptedWhenVerified()
    {
        var module = ApprovedModule();
        var script = ValidScript(module.TargetMetric, supportLevel: RecallSupportLevel.WithoutCues);
        var verification = Verification(module, MetricVerificationStatus.Verified);

        var status = CompletedModuleValidator.Validate(module, script, verification, HumanEvolutionLayer.Mastery);

        status.Should().Be(ModuleProcessingStatus.Verified);
    }

    [Fact]
    public void Validate_TechnicalErrorIsDistinctFromRequiresRevision()
    {
        // A structural contract violation (caught by the caller,
        // MetricoExecutor.cs, and mapped to Failed) is a THROW here — not a
        // returned status — precisely so it can be distinguished from the
        // RequiresRevision business outcome.
        var module = ApprovedModule();
        var script = ValidScript(CapabilityMetric.Confidence); // mismatched TargetMetric
        var verification = Verification(module, MetricVerificationStatus.Verified);

        var act = () => CompletedModuleValidator.Validate(module, script, verification, HumanEvolutionLayer.Foundation);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void PipelineTokenUsage_RetriesAppendRatherThanReplaceHistory()
    {
        var history = new List<AgentTokenUsage>
        {
            new() { AgentName = "Instructor", ModuleId = "m1", InputTokens = 100, OutputTokens = 50 }
        };

        // Simulates a retry re-running the Métrico call for the same module —
        // tokens accumulate, the earlier attempt's entry is never removed.
        history.Add(new AgentTokenUsage { AgentName = "Metrico", ModuleId = "m1", InputTokens = 80, OutputTokens = 40 });
        history.Add(new AgentTokenUsage { AgentName = "Metrico", ModuleId = "m1", InputTokens = 85, OutputTokens = 42 });

        history.Should().HaveCount(3);
        history.Sum(u => u.TotalTokens).Should().Be(150 + 120 + 127);
    }
}
