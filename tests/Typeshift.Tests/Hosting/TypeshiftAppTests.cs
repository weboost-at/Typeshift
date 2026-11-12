using Typeshift.Tests.Agents.Support;
using FluentAssertions;

namespace Typeshift.Tests.Hosting;

// D75: Typeshift without a container of the app's own
public class TypeshiftAppTests : AgentTestBase
{
    [Fact]
    public async Task Given_An_App_Over_A_Client_An_Agent_Runs_On_It()
    {
        // Arrange
        ChatClient.Reply(Text(Data.Tool.Answer));
        await using var app = TypeshiftApp.Create(ChatClient);

        // Act
        var response = await app.Agent<ProbeAgent>().RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        response.Text.Should().Be(Data.Tool.Answer);
    }

    [Fact]
    public async Task Given_An_App_With_A_Store_An_Agents_Configured_Settings_Apply_To_Its_Runs()
    {
        // Arrange
        await using var app = TypeshiftApp.Create(
            ChatClient,
            typeshift => typeshift.WithStore<InMemoryTypeshiftStore>());

        await app.Agent<ProbeAgent>().ConfigureAsync(
            Data.Persistence.SettingsId,
            settings => settings with { Temperature = Data.Agent.Temperature },
            Ct);

        // Act
        var options = await app.Agent<ProbeAgent>().ResolveOptionsAsync(Data.Persistence.SettingsId, ct: Ct);

        // Assert
        options.Temperature.Should().Be(Data.Agent.Temperature);
    }

    [Fact]
    public async Task Given_An_App_Disposed_The_Client_It_Was_Given_Stays_Undisposed()
    {
        // Arrange
        var app = TypeshiftApp.Create(ChatClient);

        // Act
        await app.DisposeAsync();

        // Assert
        ChatClient.Disposed.Should().BeFalse();
    }

    [Fact]
    public void Given_No_Client_Creating_An_App_Throws()
    {
        // Act
        var create = () => TypeshiftApp.Create(null!);

        // Assert
        create.Should().Throw<ArgumentNullException>();
    }
}
