using System.Reflection;
using Agentic.Tests.Agents.Support;
using FluentAssertions;

namespace Agentic.Tests.Agents;

public class AgentConfigurationViewTests : AgentTestBase
{
    [Fact]
    public void Exposes_Only_The_Allowlisted_Chat_Options_And_The_Configurable_Tools()
    {
        // Act
        var members = typeof(ProbeAgent.Configuration)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(property => property.Name);

        // Assert
        members.Should().BeEquivalentTo("Temperature", "MaxOutputTokens", "Instructions", "FindCompanies", "Probe");
    }

    [Fact]
    public async Task Given_Values_Written_Through_The_View_The_Run_Resolves_With_Them()
    {
        // Arrange
        await Runtime.ConfigureAsync(configuration =>
        {
            configuration.Temperature = Agent.Temperature;
            configuration.Instructions = Agent.Instructions;
            configuration.Probe.Limit = Data.Tool.Limit;
        }, Ct);

        // Act
        var options = await Runtime.ResolveChatOptionsAsync(ct: Ct);
        var probe = await ProbeAsync(options);

        // Assert
        options.Temperature.Should().Be(Agent.Temperature.Default);
        options.Instructions.Should().Be(Agent.Instructions);
        probe.Limit.Should().Be(Data.Tool.Limit.Default);
    }
}
