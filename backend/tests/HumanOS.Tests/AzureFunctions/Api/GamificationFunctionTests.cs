using FluentAssertions;
using HumanOS.AzureFunctions.Api;
using Xunit;

namespace HumanOS.Tests.AzureFunctions.Api;

public sealed class GamificationFunctionTests
{
    [Theory]
    [InlineData(0, 500, "Sinapsis")]
    [InlineData(250, 250, "Sinapsis")]
    [InlineData(499, 1, "Sinapsis")]
    [InlineData(500, 1000, "Circuito")]
    [InlineData(1499, 1, "Circuito")]
    [InlineData(1500, 2000, "Red Neuronal")]
    [InlineData(3499, 1, "Red Neuronal")]
    [InlineData(3500, 3500, "Mente Maestra")]
    [InlineData(6999, 1, "Mente Maestra")]
    [InlineData(7000, 0, null)]
    [InlineData(15000, 0, null)]
    public void CalculateNextLevelProgress_CalculatesRemainingPointsAndTargetLevelAccurately(
        int totalPoints, int expectedPointsToNext, string? expectedNextLevelName)
    {
        var (pointsToNext, nextName) = GamificationFunction.CalculateNextLevelProgress(totalPoints);
        pointsToNext.Should().Be(expectedPointsToNext);
        nextName.Should().Be(expectedNextLevelName);
    }

    [Fact]
    public void PlayerStatusDto_ContainsAllAssignedCareerMetrics()
    {
        var dto = new HumanOS.Contracts.Gamification.PlayerStatusDto
        {
            TotalPoints = 105,
            CurrentLevel = 1,
            CurrentLevelName = "Neuron",
            CurrentStreakDays = 1,
            TotalEngramsMastered = 2,
            TotalAssignedEngrams = 41,
            TotalAssignedCourses = 20,
            CompletedCourses = 0,
            CoursesReady = 1,
            CoursesPendingGeneration = 19
        };

        dto.TotalAssignedEngrams.Should().Be(41);
        dto.TotalAssignedCourses.Should().Be(20);
        dto.CompletedCourses.Should().Be(0);
        dto.CoursesReady.Should().Be(1);
        dto.CoursesPendingGeneration.Should().Be(19);
    }
}
