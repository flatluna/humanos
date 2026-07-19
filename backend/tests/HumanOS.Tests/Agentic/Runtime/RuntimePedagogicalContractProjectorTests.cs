using FluentAssertions;
using HumanOS.Agentic.Runtime;
using HumanOS.Agents.Studio;
using HumanOS.Models.Capabilities;
using Xunit;

namespace HumanOS.Tests.Agentic.Runtime;

/// <summary>
/// Paso 4 pre-work (2026-07-14) — verifies <see cref="RuntimePedagogicalContractProjector"/>
/// projects a published <see cref="CapabilityModule"/> into a
/// <see cref="HumanOS.Agents.Runtime.RuntimePedagogicalContract"/> correctly,
/// per /memories/repo/human-os-runtime-design.md.
/// </summary>
public class RuntimePedagogicalContractProjectorTests
{
    private static CapabilityModule ModuleWithVerifications(params CapabilityModuleVerification[] verifications)
    {
        var module = new CapabilityModule
        {
            CapabilityModuleId = Guid.NewGuid(),
            CapabilityLevelId = Guid.NewGuid(),
            Title = "Crear una agenda accionable",
            Description = "El alumno transforma el objetivo de una reunión real en una agenda ejecutable.",
            Type = ModuleType.Practica,
            Script = "...",
            MetricRationale = "...",
            RecallRequirement = "El alumno recupera de memoria los componentes de una agenda accionable.",
            LearnerProduction = "Una agenda para una reunión real con propósito, resultado, tiempos y responsables.",
            CapabilityLevel = new CapabilityLevel
            {
                CapabilityLevelId = Guid.NewGuid(),
                CapabilityId = Guid.NewGuid(),
                Capability = new Capability
                {
                    CapabilityId = Guid.NewGuid(),
                    Code = "crear-agendas-accionables",
                    Name = "Crear agendas accionables"
                }
            }
        };

        foreach (var verification in verifications)
        {
            module.Verifications.Add(verification);
        }

        return module;
    }

    private static CapabilityModuleVerification Verification(
        CapabilityMetric targetMetric,
        DateTime createdDate,
        params (string Criterion, int SortOrder)[] criteria)
    {
        var verification = new CapabilityModuleVerification
        {
            CapabilityModuleVerificationId = Guid.NewGuid(),
            TargetMetric = targetMetric,
            Status = MetricVerificationStatus.Verified,
            Evidence = "...",
            EvidenceLocation = "...",
            Explanation = "...",
            CreatedDate = createdDate
        };

        foreach (var (criterion, sortOrder) in criteria)
        {
            verification.SuccessCriteriaResults.Add(new CapabilityModuleSuccessCriterionResult
            {
                CapabilityModuleSuccessCriterionResultId = Guid.NewGuid(),
                SortOrder = sortOrder,
                Criterion = criterion,
                IsSatisfied = true,
                Evidence = "..."
            });
        }

        return verification;
    }

    [Fact]
    public void Project_MapsAllFourContractFieldsFromModuleAndLatestVerification()
    {
        var verification = Verification(
            CapabilityMetric.Application,
            DateTime.UtcNow,
            ("El propósito define una decisión o entrega concreta.", 0),
            ("Cada elemento tiene objetivo, tiempo y responsable.", 1));

        var module = ModuleWithVerifications(verification);

        var contract = RuntimePedagogicalContractProjector.Project(module);

        contract.CapabilityModuleId.Should().Be(module.CapabilityModuleId);
        contract.TargetMetric.Should().Be(CapabilityMetric.Application);
        contract.RecallRequirement.Should().Be(module.RecallRequirement);
        contract.LearnerProduction.Should().Be(module.LearnerProduction);
        contract.SuccessCriteria.Should().Equal(
            "El propósito define una decisión o entrega concreta.",
            "Cada elemento tiene objetivo, tiempo y responsable.");
    }

    [Fact]
    public void Project_UsesTheMostRecentVerification_NotTheFirstOne()
    {
        var older = Verification(
            CapabilityMetric.Recall,
            DateTime.UtcNow.AddMinutes(-30),
            ("Old criterion", 0));

        var newer = Verification(
            CapabilityMetric.Application,
            DateTime.UtcNow,
            ("New criterion", 0));

        var module = ModuleWithVerifications(older, newer);

        var contract = RuntimePedagogicalContractProjector.Project(module);

        contract.TargetMetric.Should().Be(CapabilityMetric.Application);
        contract.SuccessCriteria.Should().Equal("New criterion");
    }

    [Fact]
    public void Project_OrdersSuccessCriteriaBySortOrder_NotInsertionOrder()
    {
        var verification = Verification(
            CapabilityMetric.Independence,
            DateTime.UtcNow,
            ("Second", 1),
            ("First", 0));

        var module = ModuleWithVerifications(verification);

        var contract = RuntimePedagogicalContractProjector.Project(module);

        contract.SuccessCriteria.Should().Equal("First", "Second");
    }

    [Fact]
    public void Project_Throws_WhenModuleHasNoVerifications()
    {
        var module = ModuleWithVerifications();

        var act = () => RuntimePedagogicalContractProjector.Project(module);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Project_MapsChaptersInSortOrder_AndReflectionPrompt()
    {
        var verification = Verification(
            CapabilityMetric.Application,
            DateTime.UtcNow,
            ("Criterion", 0));

        var module = ModuleWithVerifications(verification);
        module.ReflectionPrompt = "Compara lo que recordaste con lo que produjiste.";
        module.Chapters.Add(new CapabilityModuleChapter
        {
            CapabilityModuleChapterId = Guid.NewGuid(),
            SortOrder = 1,
            Title = "🟢 Segundo capítulo",
            TeachingContent = "Contenido del segundo capítulo.",
            RecallPrompt = "Recuerda el segundo capítulo.",
            IsPrimaryWeight = false,
            IsCumulativeRecall = false
        });
        module.Chapters.Add(new CapabilityModuleChapter
        {
            CapabilityModuleChapterId = Guid.NewGuid(),
            SortOrder = 0,
            Title = "📘 Primer capítulo",
            TeachingContent = "Contenido del primer capítulo.",
            RecallPrompt = "Recuerda el primer capítulo.",
            IsPrimaryWeight = true,
            IsCumulativeRecall = true,
            PredictionPrompt = "¿Qué predices?",
            MiniPracticePrompt = "Practica esto."
        });

        var contract = RuntimePedagogicalContractProjector.Project(module);

        contract.ReflectionPrompt.Should().Be("Compara lo que recordaste con lo que produjiste.");
        contract.Chapters.Should().HaveCount(2);
        contract.Chapters[0].Title.Should().Be("📘 Primer capítulo");
        contract.Chapters[0].IsPrimaryWeight.Should().BeTrue();
        contract.Chapters[0].PredictionPrompt.Should().Be("¿Qué predices?");
        contract.Chapters[0].MiniPracticePrompt.Should().Be("Practica esto.");
        contract.Chapters[1].Title.Should().Be("🟢 Segundo capítulo");
        contract.Chapters[1].IsPrimaryWeight.Should().BeFalse();
        contract.Chapters[1].PredictionPrompt.Should().BeNull();
    }

    [Fact]
    public void Project_LeavesChaptersEmpty_WhenModuleHasNone()
    {
        var verification = Verification(
            CapabilityMetric.Application,
            DateTime.UtcNow,
            ("Criterion", 0));

        var module = ModuleWithVerifications(verification);

        var contract = RuntimePedagogicalContractProjector.Project(module);

        contract.Chapters.Should().BeEmpty();
    }
}
