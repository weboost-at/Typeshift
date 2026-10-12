using Typeshift.Agents;
using Typeshift.Tests.Agents.Support;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Typeshift.Tests.Persistence;

public class NoStoreTests : HostTestBase
{
    private AgentRuntime<ProbeAgent> Runtime => Services.GetRequiredService<AgentRuntime<ProbeAgent>>();

    [Fact]
    public void Given_No_Store_Resolving_A_Runs_Options_Works_From_The_Code_Alone()
    {
        // Act
        var options = Runtime.ResolveChatOptions();

        // Assert
        options.Temperature.Should().Be(0.2f);
    }

    [Fact]
    public async Task Given_No_Store_Saving_Settings_Throws_What_To_Register()
    {
        // Act
        var save = () => Runtime.SaveSettingsAsync(Data.Agent.Id.Default, new ProbeAgent.Settings(), Ct);

        // Assert
        await save.Should().ThrowAsync<InvalidOperationException>().WithMessage("*AddTypeshiftStore<TStore, Guid>()*");
    }

    [Fact]
    public async Task Given_No_Store_Resolving_With_An_Id_Throws_What_To_Register()
    {
        // Act
        var resolve = () => Runtime.ResolveChatOptionsAsync(Data.Agent.Id.Default, ct: Ct);

        // Assert
        await resolve.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*AddTypeshiftStore<TStore, Guid>()*");
    }
}
