using FluentAssertions;
using HumanOS.Agents.Studio;
using Xunit;

namespace HumanOS.Tests.Agents.Studio;

/// <summary>
/// Paso 4 (2026-07-14) minimum test set for <see cref="MetricVerificationValidator"/>
/// — see HUMAN-OS-STUDIO.md §13.
/// </summary>
public class MetricVerificationValidatorTests
{
    private static ModuleSkeleton ApprovedModule(
        CapabilityMetric targetMetric = CapabilityMetric.Application,
        List<string>? successCriteria = null) => new()
    {
        Title = "Crear una agenda accionable",
        Description = "El alumno transforma el objetivo de una reunión real en una agenda ejecutable.",
        Type = ModuleType.Practica,
        TargetMetric = targetMetric,
        RecallRequirement = "El alumno recupera de memoria los componentes de una agenda accionable.",
        LearnerProduction = "Una agenda para una reunión real con propósito, resultado, tiempos y responsables.",
        SuccessCriteria = successCriteria ??
        [
            "El propósito define una decisión o entrega concreta.",
            "Cada elemento tiene objetivo, tiempo y responsable."
        ]
    };

    private static MetricVerification ValidVerification(
        ModuleSkeleton module,
        MetricVerificationStatus status = MetricVerificationStatus.Verified,
        RecallVerificationStatus recallStatus = RecallVerificationStatus.WithCues,
        bool recallOccursBeforeInstruction = true) => new()
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
                IsSatisfied = status == MetricVerificationStatus.Verified,
                Evidence = "El LearnerTask exige explícitamente este componente en la agenda."
            })
            .ToList(),
        Recall = new RecallVerification
        {
            Status = recallStatus,
            Evidence = "El alumno recupera de memoria antes de recibir la guía.",
            EvidenceLocation = "Sección \"Recuperación\".",
            OccursBeforeInstruction = recallOccursBeforeInstruction
        },
        Explanation = "La producción exige aplicar los criterios a material real del alumno."
    };

    [Fact]
    public void Validate_AcceptsVerifiedWithFullEvidence()
    {
        var module = ApprovedModule();
        var verification = ValidVerification(module);

        var act = () => MetricVerificationValidator.Validate(module, verification);

        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_RejectsMismatchedModuleId()
    {
        var module = ApprovedModule();
        var verification = ValidVerification(module);
        verification.ModuleId = Guid.NewGuid().ToString();

        var act = () => MetricVerificationValidator.Validate(module, verification);

        act.Should().Throw<InvalidOperationException>().WithMessage("*unexpected ModuleId*");
    }

    [Fact]
    public void Validate_RejectsChangedTargetMetric()
    {
        var module = ApprovedModule(CapabilityMetric.Application);
        var verification = ValidVerification(module);
        verification.TargetMetric = CapabilityMetric.Confidence;

        var act = () => MetricVerificationValidator.Validate(module, verification);

        act.Should().Throw<InvalidOperationException>().WithMessage("*changed the approved TargetMetric*");
    }

    [Fact]
    public void Validate_RequiresConcreteEvidenceWhenVerified()
    {
        var module = ApprovedModule();
        var verification = ValidVerification(module);
        verification.Evidence = "  ";

        var act = () => MetricVerificationValidator.Validate(module, verification);

        act.Should().Throw<InvalidOperationException>().WithMessage("*requires both Evidence and*");
    }

    [Fact]
    public void Validate_RequiresExactEvidenceLocation()
    {
        var module = ApprovedModule();
        var verification = ValidVerification(module);
        verification.EvidenceLocation = "";

        var act = () => MetricVerificationValidator.Validate(module, verification);

        act.Should().Throw<InvalidOperationException>().WithMessage("*evidence location*");
    }

    [Fact]
    public void Validate_RequiresEverySuccessCriterionToBeEvaluated()
    {
        var module = ApprovedModule();
        var verification = ValidVerification(module);
        verification.SuccessCriteriaResults.RemoveAt(0);

        var act = () => MetricVerificationValidator.Validate(module, verification);

        act.Should().Throw<InvalidOperationException>().WithMessage("*every SuccessCriterion must be evaluated*");
    }

    [Fact]
    public void Validate_RejectsSuccessCriterionWithoutItsOwnEvidence()
    {
        var module = ApprovedModule();
        var verification = ValidVerification(module);
        verification.SuccessCriteriaResults[0].Evidence = "";

        var act = () => MetricVerificationValidator.Validate(module, verification);

        act.Should().Throw<InvalidOperationException>().WithMessage("*requires its own Evidence*");
    }

    [Fact]
    public void Validate_CannotBeVerifiedWhenARequiredCriterionFailed()
    {
        var module = ApprovedModule();
        var verification = ValidVerification(module, MetricVerificationStatus.Verified);
        verification.SuccessCriteriaResults[0].IsSatisfied = false;

        var act = () => MetricVerificationValidator.Validate(module, verification);

        act.Should().Throw<InvalidOperationException>().WithMessage("*cannot be Verified when a required criterion failed*");
    }

    [Fact]
    public void Validate_AllowsFailedStatusEvenWhenCriteriaAreUnsatisfied()
    {
        var module = ApprovedModule();
        var verification = ValidVerification(module, MetricVerificationStatus.Failed);
        verification.SuccessCriteriaResults[0].IsSatisfied = false;

        var act = () => MetricVerificationValidator.Validate(module, verification);

        act.Should().NotThrow();
    }

    [Theory]
    [InlineData(RecallVerificationStatus.Missing)]
    [InlineData(RecallVerificationStatus.WithCues)]
    [InlineData(RecallVerificationStatus.WithoutCues)]
    public void Validate_IdentifiesEachRecallStatus(RecallVerificationStatus recallStatus)
    {
        var module = ApprovedModule();
        var verification = ValidVerification(
            module, MetricVerificationStatus.NotVerified, recallStatus, recallOccursBeforeInstruction: true);

        var act = () => MetricVerificationValidator.Validate(module, verification);

        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_DetectsRecallAfterInstructionAsInvalid()
    {
        var module = ApprovedModule();
        var verification = ValidVerification(
            module, MetricVerificationStatus.NotVerified, RecallVerificationStatus.WithCues,
            recallOccursBeforeInstruction: false);

        var act = () => MetricVerificationValidator.Validate(module, verification);

        act.Should().Throw<InvalidOperationException>().WithMessage("*Recall must occur before instruction*");
    }

    [Fact]
    public void Validate_MissingRecallNeverNeedsToOccurBeforeInstruction()
    {
        var module = ApprovedModule();
        var verification = ValidVerification(
            module, MetricVerificationStatus.NotVerified, RecallVerificationStatus.Missing,
            recallOccursBeforeInstruction: false);

        var act = () => MetricVerificationValidator.Validate(module, verification);

        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_Confidence_NotVerifiedWithoutCalibrationCriterionSatisfied()
    {
        // Confidence requires a declared confidence + real performance +
        // comparison — enforced generically here via the "all approved
        // SuccessCriteria must be satisfied to be Verified" rule: the
        // Arquitecto is responsible for writing a calibration-comparison
        // criterion for any Confidence-targeted module (see HUMAN-OS-
        // STUDIO.md §10.4), and this single mechanism enforces it without
        // needing metric-specific string matching in the validator.
        var module = ApprovedModule(
            CapabilityMetric.Confidence,
            ["El alumno declara su confianza antes de responder y la compara con su desempeño real."]);
        var verification = ValidVerification(module, MetricVerificationStatus.Verified);
        verification.SuccessCriteriaResults[0].IsSatisfied = false;
        verification.SuccessCriteriaResults[0].Evidence = "El alumno practica pero nunca declara ni compara confianza.";

        var act = () => MetricVerificationValidator.Validate(module, verification);

        act.Should().Throw<InvalidOperationException>().WithMessage("*cannot be Verified when a required criterion failed*");
    }

    [Fact]
    public void Validate_Independence_NotVerifiedWithHighScaffoldingCriterionFailed()
    {
        var module = ApprovedModule(
            CapabilityMetric.Independence,
            ["El alumno ejecuta sin pasos, ejemplos, pistas, checklist ni respuestas de IA."]);
        var verification = ValidVerification(module, MetricVerificationStatus.Verified);
        verification.SuccessCriteriaResults[0].IsSatisfied = false;
        verification.SuccessCriteriaResults[0].Evidence = "El alumno recibió un checklist paso a paso.";

        var act = () => MetricVerificationValidator.Validate(module, verification);

        act.Should().Throw<InvalidOperationException>().WithMessage("*cannot be Verified when a required criterion failed*");
    }

    [Fact]
    public void Validate_Retention_ScheduledActivityAloneIsNotVerified()
    {
        var module = ApprovedModule(
            CapabilityMetric.Retention,
            ["Existe evidencia obtenida después de un intervalo real, no solo una actividad programada."]);
        var verification = ValidVerification(module, MetricVerificationStatus.NotVerified);
        verification.SuccessCriteriaResults[0].IsSatisfied = false;
        verification.SuccessCriteriaResults[0].Evidence = "Solo hay un repaso programado; el intervalo aún no ocurrió.";

        var act = () => MetricVerificationValidator.Validate(module, verification);

        act.Should().NotThrow();
        verification.Status.Should().Be(MetricVerificationStatus.NotVerified);
    }

    [Fact]
    public void MetricVerification_OnlyEverReportsASingleTargetMetric_NeverSecondaryMetrics()
    {
        // Structural guarantee (Paso 4's SINGLE TARGET METRIC rule): the
        // type itself has no field capable of representing a second,
        // independently "verified" metric — there is no secondary-metrics
        // list at all in this design.
        var properties = typeof(MetricVerification).GetProperties().Select(p => p.Name);

        properties.Should().NotContain(name => name.Contains("Secondary", StringComparison.OrdinalIgnoreCase));
        typeof(MetricVerification).GetProperty(nameof(MetricVerification.TargetMetric))!
            .PropertyType.Should().Be(typeof(CapabilityMetric));
    }

    [Fact]
    public void AgentTokenUsage_RecordsMetricoUsage()
    {
        var usage = new AgentTokenUsage
        {
            AgentName = "Metrico",
            ModuleId = Guid.NewGuid().ToString(),
            InputTokens = 200,
            OutputTokens = 150
        };

        usage.AgentName.Should().Be("Metrico");
        usage.TotalTokens.Should().Be(350);
    }
}
