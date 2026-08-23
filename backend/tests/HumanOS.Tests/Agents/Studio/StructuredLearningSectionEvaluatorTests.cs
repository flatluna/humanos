using System.Reflection;
using System.Text.Json;
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

    // ==================== Atomicity checks (2026-08-17, Paso 4) ====================

    [Fact]
    public void CheckCapabilityAtomicity_SingleIdea_Passes()
    {
        var result = StructuredLearningSectionEvaluator.CheckCapabilityAtomicity(
            "The volunteer can encourage a participant to stay engaged during a group activity.");

        result.Passed.Should().BeTrue();
        result.Reason.Should().BeNull();
    }

    [Theory]
    [InlineData("The volunteer can encourage participants y además gestionar conflictos entre ellos.")]
    [InlineData("The volunteer can support participants así como coordinar con otros voluntarios.")]
    [InlineData("The employee can file the report as well as approve budget requests independently.")]
    public void CheckCapabilityAtomicity_CompoundScopeConnector_Fails(string capability)
    {
        var result = StructuredLearningSectionEvaluator.CheckCapabilityAtomicity(capability);

        result.Passed.Should().BeFalse();
        result.Reason.Should().Contain("compound-scope connector");
    }

    [Fact]
    public void CheckCapabilityAtomicity_SemicolonJoinedClauses_Fails()
    {
        var result = StructuredLearningSectionEvaluator.CheckCapabilityAtomicity(
            "The volunteer can greet new participants; the volunteer can also close out the activity log.");

        result.Passed.Should().BeFalse();
        result.Reason.Should().Contain("semicolon-joined");
    }

    [Fact]
    public void CheckCapabilityAtomicity_ExcessiveLength_Fails()
    {
        var longCapability = "The volunteer can " + string.Concat(Enumerable.Repeat("support participants during activities and manage supplies ", 5));

        var result = StructuredLearningSectionEvaluator.CheckCapabilityAtomicity(longCapability);

        result.Passed.Should().BeFalse();
        result.Reason.Should().Contain("characters");
    }

    [Fact]
    public void CheckCapabilityAtomicity_EmptyStatement_Fails()
    {
        var result = StructuredLearningSectionEvaluator.CheckCapabilityAtomicity("   ");

        result.Passed.Should().BeFalse();
        result.Reason.Should().Contain("empty");
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
    public void CapabilityMetadata_IsOneSentenceAndActionOriented()
    {
        var capability = "The volunteer can encourage a participant, help them remain engaged, and support their inclusion without taking over.";

        capability.Should().NotBeNullOrWhiteSpace();
        capability.Should().Contain("can");
        capability.Should().Contain("volunteer");
        capability.Should().NotContain("\n");
        capability.Should().EndWith(".");
    }

    [Fact]
    public void CheckHypothesisContractVersion_UsesLatestVersionMarker()
    {
        const string hypothesis = "ContractVersion: 2026-08-11-v1-source-faithful-professor - How should a volunteer support a participant during a group activity?";

        StructuredLearningSectionEvaluator.CheckHypothesisContractVersion(hypothesis).Should().BeTrue();
    }

    [Fact]
    public void BuildStepDataJson_HypothesisIncludesTwoQuestionSet()
    {
        var step = new StructuredLearningSectionStepDto
        {
            StepType = StructuredLearningStepType.Hypothesis,
            Content = "Volunteers walk beside participants and help them stay engaged.",
            HypothesisQuestions =
            [
                new StructuredLearningSectionStepDto.HypothesisQuestionDto
                {
                    Question = "What is the main role of a volunteer in the group?",
                    Options = ["They guide the activity and keep the participant engaged.", "They replace the teacher completely.", "They avoid participating in the group.", "They only watch from the side."],
                    CorrectIndex = 0
                },
                new StructuredLearningSectionStepDto.HypothesisQuestionDto
                {
                    Question = "Which action best supports a participant?",
                    Options = ["Cheering them on and helping them stay engaged.", "Ignoring them.", "Taking over their task.", "Leaving the group."],
                    CorrectIndex = 0
                }
            ]
        };

        var json = StructuredLearningPedagogyService.BuildStepDataJson(step);

        json.Should().NotBeNullOrEmpty();
        json.Should().Contain("question");
        json.Should().Contain("correctIndex");
        json.Should().NotContain("Volunteers walk beside participants");
        step.Content.Should().Be("Volunteers walk beside participants and help them stay engaged.");
    }

    [Fact]
    public void StructuredLearningAssessmentEngine_BuildQuestionGenerationPrompt_UsesTwoQuestionDefaultWhenConfigMissing()
    {
        var method = typeof(StructuredLearningAssessmentEngine)
            .GetMethod("BuildQuestionGenerationPrompt", BindingFlags.NonPublic | BindingFlags.Static);

        method.Should().NotBeNull();

        var sectionContextType = typeof(StructuredLearningAssessmentEngine)
            .GetNestedType("SectionContext", BindingFlags.NonPublic);
        sectionContextType.Should().NotBeNull();

        var sectionContext = Activator.CreateInstance(sectionContextType!);
        sectionContextType!.GetProperty("ChapterTitle")!.SetValue(sectionContext, "Companionship and Support");
        sectionContextType.GetProperty("SubchapterTitle")!.SetValue(sectionContext, "How volunteers help in group activities");
        sectionContextType.GetProperty("TeachingContent")!.SetValue(sectionContext,
            "Volunteers walk beside participants during group activities and help them stay engaged while they participate.");
        sectionContextType.GetProperty("QuestionType")!.SetValue(sectionContext, StructuredLearningAssessmentQuestionType.MultipleChoice);

        var prompt = (string?)method!.Invoke(null, [sectionContext, 1, Array.Empty<string>(), Array.Empty<string>()]);

        prompt.Should().NotBeNullOrWhiteSpace();
        prompt.Should().Contain("Question number: 1 of 2");
    }

    [Fact]
    public void StructuredLearningSectionDesignResponse_Statements_DefaultToEmptyCollection()
    {
        var response = new StructuredLearningSectionDesignResponse();

        response.Statements.Should().NotBeNull();
        response.Statements.Should().BeEmpty();
    }

    [Fact]
    public void StructuredLearningDocumentSection_Statements_DefaultToEmptyCollection()
    {
        var section = new StructuredLearningDocumentSection();

        section.Statements.Should().NotBeNull();
        section.Statements.Should().BeEmpty();
    }

    [Fact]
    public void StructuredLearningDocumentSection_ConditionalStatement_StoresRuleMetadata()
    {
        var sectionId = Guid.NewGuid();
        var statement = new StructuredLearningStatement
        {
            StatementId = Guid.NewGuid(),
            SectionId = sectionId,
            StatementType = "ConditionalApprovalRule",
            Statement = "Manager approval is required if the expense exceeds $100.",
            SourceText = "Expenses exceeding $100 require manager approval.",
            ConditionEntity = "Expense",
            ConditionAttribute = "Amount",
            ConditionOperator = "GreaterThan",
            ConditionValue = "100",
            ConditionUnit = "USD",
            ActorEntity = "Manager",
            ActorType = "Human",
            Action = "Approve",
            TargetEntity = "Expense",
            Modality = "Required",
            PossibleOutcomes = ["Approved", "Rejected"]
        };

        statement.StatementType.Should().Be("ConditionalApprovalRule");
        statement.ConditionEntity.Should().Be("Expense");
        statement.ConditionOperator.Should().Be("GreaterThan");
        statement.PossibleOutcomes.Should().Contain("Approved").And.Contain("Rejected");
        statement.SectionId.Should().Be(sectionId);
    }

    [Fact]
    public void StructuredLearningDocumentSection_Statements_SerializeAndDeserializeRoundTrip()
    {
        var sectionId = Guid.NewGuid();
        var section = new StructuredLearningDocumentSection
        {
            StructuredLearningDocumentSectionId = Guid.NewGuid(),
            StructuredLearningDocumentId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            ChapterTitle = "Expenses",
            SubchapterTitle = "Approval rules",
            Text = "Expenses exceeding $100 require manager approval.",
            Capability = "The manager can approve expenses responsibly.",
            Statements =
            [
                new StructuredLearningStatement
                {
                    StatementId = Guid.NewGuid(),
                    SectionId = sectionId,
                    StatementType = "ConditionalApprovalRule",
                    Statement = "Manager approval is required if the expense exceeds $100.",
                    SourceText = "Expenses exceeding $100 require manager approval.",
                    ConditionEntity = "Expense",
                    ConditionAttribute = "Amount",
                    ConditionOperator = "GreaterThan",
                    ConditionValue = "100",
                    ConditionUnit = "USD",
                    ActorEntity = "Manager",
                    ActorType = "Human",
                    Action = "Approve",
                    TargetEntity = "Expense",
                    Modality = "Required",
                    PossibleOutcomes = ["Approved", "Rejected"]
                }
            ]
        };

        var json = JsonSerializer.Serialize(section, new JsonSerializerOptions { WriteIndented = true });
        var roundTripped = JsonSerializer.Deserialize<StructuredLearningDocumentSection>(json);

        roundTripped.Should().NotBeNull();
        roundTripped!.Statements.Should().HaveCount(1);
        roundTripped.Statements[0].StatementType.Should().Be("ConditionalApprovalRule");
        roundTripped.Statements[0].PossibleOutcomes.Should().Contain("Approved");
        roundTripped.Capability.Should().Be("The manager can approve expenses responsibly.");
    }

    [Fact]
    public void StructuredLearningDocumentSection_Statements_BackwardCompatibility_UsesEmptyCollectionWhenMissing()
    {
        const string json = """
        {
          "StructuredLearningDocumentSectionId": "b8af2d78-8f5e-4e6a-b311-0e1b18f0ca5b",
          "StructuredLearningDocumentId": "f19b0fc6-c4c9-4f88-83a7-4cbdbd46d8a8",
          "TenantId": "5dda1155-16a8-4256-9c36-4c0df158784f",
          "ChapterTitle": "Expenses",
          "SubchapterTitle": "Approval rules",
          "Text": "Expenses exceeding $100 require manager approval.",
          "Capability": "The manager can approve expenses responsibly."
        }
        """;

        var section = JsonSerializer.Deserialize<StructuredLearningDocumentSection>(json);

        section.Should().NotBeNull();
        section!.Statements.Should().NotBeNull();
        section.Statements.Should().BeEmpty();
    }

    [Fact]
    public void StructuredLearningAssessmentEngine_BuildQuestionGenerationPrompt_RequiresPostTeachingMultipleChoice()
    {
        var method = typeof(StructuredLearningAssessmentEngine)
            .GetMethod("BuildQuestionGenerationPrompt", BindingFlags.NonPublic | BindingFlags.Static);

        method.Should().NotBeNull();

        var sectionContextType = typeof(StructuredLearningAssessmentEngine)
            .GetNestedType("SectionContext", BindingFlags.NonPublic);
        sectionContextType.Should().NotBeNull();

        var sectionContext = Activator.CreateInstance(sectionContextType!);
        sectionContextType!.GetProperty("ChapterTitle")!.SetValue(sectionContext, "Companionship and Support");
        sectionContextType.GetProperty("SubchapterTitle")!.SetValue(sectionContext, "How volunteers help in group activities");
        sectionContextType.GetProperty("TeachingContent")!.SetValue(sectionContext,
            "Volunteers walk beside participants during group activities and help them stay engaged while they participate.");
        sectionContextType.GetProperty("QuestionType")!.SetValue(sectionContext, StructuredLearningAssessmentQuestionType.MultipleChoice);
        sectionContextType.GetProperty("QuestionsPerRound")!.SetValue(sectionContext, 2);

        var prompt = (string?)method!.Invoke(null, [sectionContext, 1, Array.Empty<string>(), Array.Empty<string>()]);

        prompt.Should().NotBeNullOrWhiteSpace();
        prompt.Should().Contain("post-teaching");
        prompt.Should().Contain("student has already seen the teaching");
        prompt.Should().Contain("do not ask literal recall");
        prompt.Should().NotContain("According to the text");
    }

    [Fact]
    public async Task CheckGroundedInSourceAsync_GroundedTeachingWithNaturalParaphrase_Passes()
    {
        const string teaching =
            "Alright everyone, let's look at this: the volunteer walks beside you during group activities, " +
            "helping you stay engaged and supporting you as you take part alongside everyone else.";

        var passed = await StructuredLearningSectionEvaluator.CheckGroundedInSourceAsync(teaching, SourceText);

        passed.Should().BeTrue();
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
