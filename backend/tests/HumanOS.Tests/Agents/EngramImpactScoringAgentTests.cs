using FluentAssertions;
using HumanOS.Agents;
using HumanOS.Contracts.EmployeeScoring;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace HumanOS.Tests.Agents;

public sealed class EngramImpactScoringAgentTests
{
    [Fact]
    public void IsConfigured_ReturnsFalse_WhenNoConfiguration()
    {
        var config = new ConfigurationBuilder().Build();
        var agent = new EngramImpactScoringAgent(config);

        agent.IsConfigured.Should().BeFalse();
    }

    [Fact]
    public async Task ScoreEngramsAsync_Throws_WhenAgentNotConfigured()
    {
        var config = new ConfigurationBuilder().Build();
        var agent = new EngramImpactScoringAgent(config);

        var profile = new EmployeeScoringProfile
        {
            EmployeeId = Guid.NewGuid(),
            RoleTitle = "Senior Software Engineer"
        };

        var engrams = new List<EngramInput>
        {
            new() { EngramId = "e1", Title = "Test Engram" }
        };

        var act = () => agent.ScoreEngramsAsync(profile, engrams);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not configured*");
    }

    [Fact]
    public async Task ScoreEngramsAsync_ReturnsEmpty_WhenEngramsListIsEmpty()
    {
        var config = new ConfigurationBuilder().Build();
        var agent = new EngramImpactScoringAgent(config);

        var profile = new EmployeeScoringProfile
        {
            EmployeeId = Guid.NewGuid(),
            RoleTitle = "Senior Software Engineer"
        };

        var result = await agent.ScoreEngramsAsync(profile, []);

        result.Should().BeEmpty();
    }
}
