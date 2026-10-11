using Agentic.Agents;
using Agentic.Tests.Agents.Support;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Agentic.Tests.Persistence;

public class NoStoreTests : HostTestBase
{
    private AgentRuntime<ProbeAgent> Runtime => Services.GetRequiredService<AgentRuntime<ProbeAgent>>();

    [Fact]
    public async Task Given_No_Store_Resolving_A_Runs_Options_Works_From_The_Code_Alone()
    {
        // Act
        var options = await Runtime.ResolveChatOptionsAsync(ct: Ct);

        // Assert
        options.Temperature.Should().Be(0.2f);
    }

    [Fact]
    public async Task Given_No_Store_Updating_A_Configuration_Throws_What_To_Register()
    {
        // Act
        var update = () => Runtime.UpdateConfigurationAsync(configuration => configuration, Ct);

        // Assert
        await update.Should().ThrowAsync<InvalidOperationException>().WithMessage("*IAgentConfigurationStore*");
    }
}
