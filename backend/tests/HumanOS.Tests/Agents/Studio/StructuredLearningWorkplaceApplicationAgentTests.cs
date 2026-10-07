using System.Text.Json;
using FluentAssertions;
using HumanOS.Agents.Studio;
using HumanOS.Contracts.Studio;
using HumanOS.Services;
using Xunit;

namespace HumanOS.Tests.Agents.Studio;

public sealed class StructuredLearningWorkplaceApplicationAgentTests
{
    [Theory]
    [InlineData(false, "en")]
    [InlineData(true, "es")]
    public void Prompt_ContainsLessonRoleAndUserRequestWithoutAssessment(bool isStudent, string language)
    {
        var context = new StructuredLearningWorkplaceApplicationContext
        {
            TeachingContent = "Design, test and refine prompts.",
            RoleTitle = isStudent ? "Finance student" : "CTO AI",
            StudentName = "Alex Smith",
            IsStudent = isStudent,
            StudentAcademicProfileSummary = isStudent ? "Accounting coursework" : null,
            UserInstructions = "Apply this to invoice reconciliation with formulas.",
            TargetLanguage = language
        };

        var prompt = StructuredLearningWorkplaceApplicationAgent.BuildPrompt(context);
        prompt.Should().Contain(context.TeachingContent).And.Contain(context.RoleTitle)
            .And.Contain(context.UserInstructions).And.Contain("First Name: Alex");
        prompt.Should().NotContain("CQI").And.NotContain("HermanScience")
            .And.NotContain("ReflectionQuestions").And.NotContain("GrowthOpportunity");
        if (isStudent)
            prompt.Should().Contain("Accounting coursework").And.NotContain("Tecnológico de Monterrey");
        prompt.Should().Contain(language == "es" ? "Spanish (es)" : "English (en)");
    }

    [Fact]
    public void EmptyRequest_UsesRoleApplicationWithoutFabricatedRequest()
    {
        var prompt = StructuredLearningWorkplaceApplicationAgent.BuildPrompt(new()
        {
            TeachingContent = "Prompt testing", RoleTitle = "Accountant"
        });
        prompt.Should().Contain("No specific request").And.Contain("Accountant");
    }

    [Fact]
    public void ValidWorkedApplication_IsAccepted()
    {
        var scenario = ValidScenario();
        var validate = () => StructuredLearningWorkplaceApplicationService.ValidateScenario(scenario);
        validate.Should().NotThrow();
    }

    [Theory]
    [InlineData("title")]
    [InlineData("situation")]
    [InlineData("inputs")]
    [InlineData("steps")]
    [InlineData("artifact")]
    [InlineData("result")]
    [InlineData("validation")]
    public void IncompleteWorkedExample_IsRejected(string missing)
    {
        var scenario = ValidScenario();
        var example = scenario.WorkedExamples[0];
        switch (missing)
        {
            case "title": example.Title = ""; break;
            case "situation": example.Situation = ""; break;
            case "inputs": example.InputsAndAssumptions = ""; break;
            case "steps": example.Steps = []; break;
            case "artifact": example.Artifact = ""; break;
            case "result": example.ExpectedResult = ""; break;
            case "validation": example.Validation = ""; break;
        }
        var validate = () => StructuredLearningWorkplaceApplicationService.ValidateScenario(scenario);
        validate.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void MissingExamplesOrMismatchedAudio_IsRejected()
    {
        var scenario = ValidScenario();
        scenario.SpokenAudioScript = "Different narration";
        var validate = () => StructuredLearningWorkplaceApplicationService.ValidateScenario(scenario);
        validate.Should().Throw<InvalidOperationException>();
        scenario = ValidScenario();
        scenario.WorkedExamples.Clear();
        validate.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void StoredResponse_RoundTripsRequestAndArtifactWithoutRemovedSections()
    {
        var response = new StructuredLearningWorkplaceApplicationResponse
        {
            PersonId = Guid.NewGuid(),
            SectionId = Guid.NewGuid(),
            UserInstructions = "Show a finance calculation",
            WorkedExamples = ValidScenario().WorkedExamples,
            SchemaVersion = 7
        };
        var json = JsonSerializer.Serialize(response);
        var restored = JsonSerializer.Deserialize<StructuredLearningWorkplaceApplicationResponse>(json)!;
        restored.PersonId.Should().Be(response.PersonId);
        restored.SectionId.Should().Be(response.SectionId);
        restored.UserInstructions.Should().Be(response.UserInstructions);
        restored.WorkedExamples[0].Artifact.Should().Be("100 - 20 = 80 USD");
        json.Should().NotContain("Cqi").And.NotContain("ReflectionQuestions").And.NotContain("GrowthOpportunity");
        JsonSerializer.Serialize(ValidScenario()).Should().NotContain("ReflectionQuestions").And.NotContain("GrowthOpportunity");
    }

    private static StructuredLearningWorkplaceApplicationResult ValidScenario() => new()
    {
        ScenarioTitle = "Check invoice totals",
        ScenarioDescription = "Hello Alex, apply prompt testing to invoice reconciliation.",
        SpokenAudioScript = "Hello Alex, apply prompt testing to invoice reconciliation.",
        WhyThisMatters = "- Reconcile invoices reliably.\n- Test calculations.",
        WorkedExamples =
        [
            new()
            {
                Title = "Invoice balance",
                Situation = "Check an illustrative invoice.",
                InputsAndAssumptions = "Sample invoice 100 USD; payment 20 USD.",
                Steps = ["Subtract the payment from the invoice total."],
                Artifact = "100 - 20 = 80 USD",
                ArtifactLanguage = "text",
                ExpectedResult = "Outstanding balance: 80 USD.",
                Validation = "Check that balance plus payment equals 100 USD."
            }
        ]
    };
}
