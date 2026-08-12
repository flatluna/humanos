using FluentAssertions;
using HumanOS.Agents.Studio;
using HumanOS.Models.Capabilities.Studio;
using HumanOS.Services;
using Xunit;

namespace HumanOS.Tests.Agents.Studio;

/// <summary>
/// Executable specification for Step 3's future "Diseñador de Sección"
/// agent, written BEFORE the agent itself (2026-08-10 design decision) —
/// these checks encode the exact failure modes already hit once in this
/// codebase (LessonCardAgent invention, Recall duplicating Teaching's own
/// wording) so the agent's prompt gets built/iterated against a concrete,
/// automatable spec instead of manual review alone.
/// </summary>
public class StructuredLearningSectionEvaluatorTests
{
    private const string SourceText =
        "3.1 Companionship and Support During Group Activities. Volunteers are seen as kind, " +
        "friendly companions who walk beside program participants during group activities. Their " +
        "main function is to provide one-on-one help within the group, which includes cheering on " +
        "a participant, helping them stay engaged, and gently supporting them as they take part " +
        "alongside everyone else.";

    // ==================== Structural checks ====================

    [Fact]
    public void ValidateStructure_CorrectFullOrder_Passes()
    {
        var steps = new List<(StructuredLearningStepType, string)>
        {
            (StructuredLearningStepType.Hypothesis, "¿Cómo crees que un voluntario debería apoyar durante una actividad grupal?"),
            (StructuredLearningStepType.Teaching, "Los voluntarios son compañeros amables que caminan junto a los participantes."),
            (StructuredLearningStepType.DeepDive, "Ejemplo: animar a un participante durante un juego grupal."),
            (StructuredLearningStepType.Recall, "¿Cuál es la función principal del voluntario dentro del grupo?"),
            (StructuredLearningStepType.Production, "Describe cómo apoyarías a un participante en una actividad real."),
            (StructuredLearningStepType.Assessment, "Selecciona la opción que describe mejor el rol del voluntario."),
        };

        var result = StructuredLearningSectionEvaluator.ValidateStructure(
            steps, includeDeepDiveStep: true, includeRecallStep: true, includeProductionStep: true);

        result.Passed.Should().BeTrue();
        result.Failures.Should().BeEmpty();
    }

    [Fact]
    public void ValidateStructure_RecallDisabled_SkipsRecallStep()
    {
        var steps = new List<(StructuredLearningStepType, string)>
        {
            (StructuredLearningStepType.Hypothesis, "¿Cómo crees que un voluntario debería apoyar durante una actividad grupal?"),
            (StructuredLearningStepType.Teaching, "Los voluntarios son compañeros amables que caminan junto a los participantes."),
            (StructuredLearningStepType.Production, "Describe cómo apoyarías a un participante en una actividad real."),
            (StructuredLearningStepType.Assessment, "Selecciona la opción que describe mejor el rol del voluntario."),
        };

        var result = StructuredLearningSectionEvaluator.ValidateStructure(
            steps, includeDeepDiveStep: false, includeRecallStep: false, includeProductionStep: true);

        result.Passed.Should().BeTrue();
    }

    [Fact]
    public void ValidateStructure_WrongOrder_Fails()
    {
        var steps = new List<(StructuredLearningStepType, string)>
        {
            (StructuredLearningStepType.Teaching, "Los voluntarios son compañeros amables que caminan junto a los participantes."),
            (StructuredLearningStepType.Hypothesis, "¿Cómo crees que un voluntario debería apoyar durante una actividad grupal?"),
            (StructuredLearningStepType.Recall, "¿Cuál es la función principal del voluntario dentro del grupo?"),
            (StructuredLearningStepType.Assessment, "Selecciona la opción que describe mejor el rol del voluntario."),
        };

        var result = StructuredLearningSectionEvaluator.ValidateStructure(
            steps, includeDeepDiveStep: false, includeRecallStep: true, includeProductionStep: false);

        result.Passed.Should().BeFalse();
        result.Failures.Should().Contain(f => f.Contains("Expected step order"));
    }

    [Fact]
    public void ValidateStructure_EmptyStepContent_Fails()
    {
        var steps = new List<(StructuredLearningStepType, string)>
        {
            (StructuredLearningStepType.Hypothesis, "¿Cómo crees que un voluntario debería apoyar durante una actividad grupal?"),
            (StructuredLearningStepType.Teaching, "   "),
            (StructuredLearningStepType.Assessment, "Selecciona la opción que describe mejor el rol del voluntario."),
        };

        var result = StructuredLearningSectionEvaluator.ValidateStructure(
            steps, includeDeepDiveStep: false, includeRecallStep: false, includeProductionStep: false);

        result.Passed.Should().BeFalse();
        result.Failures.Should().Contain(f => f.Contains("Teaching") && f.Contains("empty"));
    }

    // ==================== Content-quality checks (real Eval framework) ====================

    [Fact]
    public async Task CheckGroundedInSourceAsync_ContentReusesSourceWords_Passes()
    {
        var teaching = "Los voluntarios son companions amables que caminan beside los participantes " +
                        "durante group activities, ofreciendo one-on-one help y ayudándolos a stay engaged.";

        var passed = await StructuredLearningSectionEvaluator.CheckGroundedInSourceAsync(teaching, SourceText);

        passed.Should().BeTrue();
    }

    [Fact]
    public async Task CheckGroundedInSourceAsync_InventedContentUnrelatedToSource_Fails()
    {
        const string invented =
            "The Haven Program requires every volunteer to complete a background check within " +
            "thirty days and submit quarterly performance reviews to the regional coordinator.";

        var passed = await StructuredLearningSectionEvaluator.CheckGroundedInSourceAsync(invented, SourceText);

        passed.Should().BeFalse();
    }

    [Fact]
    public async Task CheckNotDuplicateOfAsync_DistinctRecallQuestion_Passes()
    {
        const string teaching =
            "Los voluntarios son compañeros amables que caminan junto a los participantes durante " +
            "actividades grupales, ofreciendo apoyo uno a uno.";
        const string recall = "¿Qué palabra describe el tipo de apoyo que da un voluntario dentro del grupo?";

        var passed = await StructuredLearningSectionEvaluator.CheckNotDuplicateOfAsync(recall, teaching);

        passed.Should().BeTrue();
    }

    [Fact]
    public async Task CheckNotDuplicateOfAsync_RecallRepeatsTeachingWording_Fails()
    {
        const string teaching =
            "Los voluntarios son compañeros amables que caminan junto a los participantes durante " +
            "actividades grupales, ofreciendo apoyo uno a uno dentro del grupo.";
        // Same bug class already hit once in production: Recall just restates Teaching almost verbatim.
        const string recall =
            "Recuerda: los voluntarios son compañeros amables que caminan junto a los participantes " +
            "durante actividades grupales, ofreciendo apoyo uno a uno dentro del grupo.";

        var passed = await StructuredLearningSectionEvaluator.CheckNotDuplicateOfAsync(recall, teaching);

        passed.Should().BeFalse();
    }

    [Fact]
    public void CheckHypothesisContractVersion_UsesLatestVersionMarker()
    {
        const string hypothesis = "ContractVersion: 2026-08-11-v1-source-faithful-professor - How should a volunteer support a participant during a group activity?";

        StructuredLearningSectionEvaluator.CheckHypothesisContractVersion(hypothesis).Should().BeTrue();
    }

    [Fact]
    public async Task CheckTeachingStyleAsync_ProfessorDirectAddress_Passes()
    {
        const string teaching =
            "Alright everyone, let's look at this: volunteers are kind companions who walk beside participants " +
            "during group activities. You can see how they help by cheering on a participant and helping them stay engaged.";

        var passed = await StructuredLearningSectionEvaluator.CheckTeachingStyleAsync(teaching, SourceText);

        passed.Should().BeTrue();
    }

    [Fact]
    public async Task CheckTeachingStyleAsync_MetaNarrationOrNoStudentAddress_Fails()
    {
        const string teaching =
            "The section explains that volunteers are kind companions who walk beside participants during group activities. " +
            "This is one of those important ideas that helps everyone understand the program.";

        var passed = await StructuredLearningSectionEvaluator.CheckTeachingStyleAsync(teaching, SourceText);

        passed.Should().BeFalse();
    }

    [Fact]
    public void CreateInstructionAlignedCard_UsesExactReviewerSubject_WithoutHardcodedVolunteerContext()
    {
        var card = StructuredLearningReviewService.CreateInstructionAlignedCard("A teacher is teaching Sign Language");

        card.Title.Should().Contain("Sign Language");
        card.Text.Should().Contain("teacher");
        card.Text.Should().Contain("Sign Language");
        card.IllustrationPrompt.Should().NotBeNull();
        card.IllustrationPrompt.Should().Contain("teacher");
        card.IllustrationPrompt.Should().Contain("Sign Language");
        card.Title.Should().NotContain("volunteer");
        card.Text.Should().NotContain("volunteer");
    }

    [Fact]
    public void LessonCardAgent_EnsureImageDescriptionSeparator_AppendsRequiredFormat()
    {
        const string lessonText = "Welcome wonderful volunteer — we are so happy you're here! The Haven is a place built on warmth, dignity, respect, and safety.";
        const string prompt = "A warm welcoming volunteer at a community center with a supportive group setting.";

        var formatted = LessonCardAgent.EnsureImageDescriptionSeparator(lessonText, prompt);

        formatted.Should().Contain("---");
        formatted.Should().Contain("Image description:");
        formatted.Should().Contain(prompt);
        formatted.Should().MatchRegex("\n\n---\n\nImage description:");
    }

    [Fact]
    public void LessonCardAgent_NormalizeIllustrationPrompt_ReplacesLogoDriftWithOnboardingProcess()
    {
        const string lessonText = "Before you begin this journey with us, we just have a few little steps to walk through together. We ask each volunteer to fill out a volunteer application, share information for a background check, attend orientation, read the policy, and sign the agreement at the end.";
        const string logoPrompt = "A square logo on a dark teal/blue background. Central large white serif capital H, script word Haven, decorative brand mark with no instructional content.";

        var normalized = LessonCardAgent.NormalizeIllustrationPrompt(logoPrompt, lessonText);

        normalized.Should().Contain("application");
        normalized.Should().Contain("background check");
        normalized.Should().Contain("orientation");
        normalized.Should().Contain("policy");
        normalized.Should().Contain("agreement");
        normalized.Should().NotContain("logo");
        normalized.Should().NotContain("decorative brand");
    }
}
