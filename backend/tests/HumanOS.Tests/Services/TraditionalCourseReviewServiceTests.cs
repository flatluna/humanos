using FluentAssertions;
using HumanOS.AzureFunctions.Api;
using HumanOS.Models.Capabilities.Traditional;
using HumanOS.Services;
using Xunit;

namespace HumanOS.Tests.Services;

public class TraditionalCourseReviewServiceTests
{
    [Theory]
    [InlineData("change the image to see more people", true)]
    [InlineData("make the lesson clearer", false)]
    [InlineData("rewrite this card text and add a more modern illustration", true)]
    [InlineData("update the example to include animals", false)]
    public void ShouldRequestIllustrationUpdate_DetectsImageDrivenInstructions(string instruction, bool expected)
    {
        TraditionalCourseReviewService.ShouldRequestIllustrationUpdate(instruction).Should().Be(expected);
    }

    [Theory]
    [InlineData("Your role is a truly lovely one to encourage, guide, gently rephrase, or softly clarify the instructions given by the teacher or leader of whatever activity is happening in that moment. There's no need to take over or feel like you have to do it all on your own—just lean on program leadership, and together you'll help our friends feel successful, included, safe, and full of joy as they participate.", true)]
    [InlineData("Make this text more welcoming and supportive for volunteers.", false)]
    public void ShouldTreatAsDirectTextReplacement_DetectsFullReplacementParagraphs(string instruction, bool expected)
    {
        TraditionalCourseReviewService.ShouldTreatAsDirectTextReplacement(instruction).Should().Be(expected);
    }

    [Fact]
    public void CreateDefaultLessonCard_LeavesNewLessonEmptyForUserContent()
    {
        var card = AddTraditionalCourseLessonFunction.CreateDefaultLessonCard("Introduction to the lesson");

        card.Title.Should().Be("Introduction to the lesson");
        card.Text.Should().BeEmpty();
        card.CapabilityTraditionalIllustrationId.Should().BeNull();
        card.SortOrder.Should().Be(1);
    }

    [Fact]
    public void CreateStarterCard_ForEmptyLesson_StartsEmptyForUserInput()
    {
        var card = AddTraditionalCourseLessonCardFunction.CreateStarterCard("Volunteer requirements", "");

        card.Title.Should().Be("Volunteer requirements");
        card.Text.Should().BeEmpty();
        card.SortOrder.Should().Be(1);
    }

    [Fact]
    public void CreateDefaultLessonCard_AssignsParentLessonId_WhenProvided()
    {
        var lessonId = Guid.NewGuid();
        var card = AddTraditionalCourseLessonFunction.CreateDefaultLessonCard("Introduction to the lesson", lessonId);

        card.CapabilityLessonId.Should().Be(lessonId);
        card.Title.Should().Be("Introduction to the lesson");
    }

    [Fact]
    public void CreateStarterCard_AssignsParentLessonId_WhenProvided()
    {
        var lessonId = Guid.NewGuid();
        var card = AddTraditionalCourseLessonCardFunction.CreateStarterCard("Volunteer requirements", "", lessonId);

        card.CapabilityLessonId.Should().Be(lessonId);
        card.Title.Should().Be("Volunteer requirements");
    }

    [Fact]
    public void BuildLessonCardIllustrationPrompt_UsesTheLessonTopic_AndImageDirection()
    {
        var prompt = AddTraditionalCourseLessonFunction.BuildLessonCardIllustrationPrompt("Volunteer onboarding", "This lesson explains how to complete the volunteer intake process.");

        prompt.Should().Contain("Volunteer onboarding");
        prompt.Should().Contain("volunteer intake process");
        prompt.Should().Contain("illustration");
    }
}
